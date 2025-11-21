using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;
using RapidsLang.Utils;

namespace RapidsLang.Parser;

public class Diagnostic(Token token, string issue, bool atEndOfLine = false, bool ignoreInLanguageServer=false)
{
    public Token Token { get; } = token;
    public string Issue { get; } = issue;
    public bool AtEndOfLine { get; } = atEndOfLine;

    public bool IgnoreInLanguageServer { get; } = ignoreInLanguageServer;
}

public static class RapidsParser
{
    public static RapidsParseResult Parse(string code, out RapidsPreprocMetaData metaData)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);

        metaData = preprocRes.Metadata;

        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        
        var parseResult = Parse(lexResult);

        return parseResult;
    }
    
    public static RapidsParseResult Parse(List<Token> tokens)
    {
        ListStepper<Token> stepper = new(tokens);

        return Parse(stepper);
    }

    private static RapidsParseResult Parse(ListStepper<Token> stepper, StatementsNode? activeBlock=null)
    {
        var builder = new RapidsParseResult.Builder(stepper, activeBlock ?? new StatementsNode(stepper.Cur));
        
        while (!stepper.AtEnd)
        {
            if (stepper.Cur.TokenType is TokenType.Identifier)
            {
                var expression = ParseExpression(stepper, builder);
                if (expression is null)
                {
                    TrashUntilEndOfLine(stepper);
                    continue;
                }

                if (stepper.AtEnd)
                {
                    TrashUntilEndOfLine(stepper);
                    continue;
                }

                // check for function call
                if (stepper.Cur.TokenType is TokenType.OpenParen)
                {
                    if (CheckIfIsFunctionDeclaration(stepper))
                    {
                        
                        if (expression is not IdentifierNode identNode)
                        {
                            builder.AddDiagnostic(new(expression.BaseToken, "Expected identifier as name of function."));
                            
                            // junk function body, don't bother parsing really.
                            _ = ParseExpression(stepper, builder);
                            
                            continue;
                        }

                        var functionExpr = ParseExpression(stepper, builder);

                        if (functionExpr is not FunctionNode function)
                        {
                            builder.AddDiagnostic(
                                new(functionExpr?.BaseToken ?? expression.BaseToken, 
                                    "Expected a valid function definition '()> { ... }' after the function name.")
                            );
                            continue;
                        }

                        builder.AddStatement(new FunctionDeclarationNode(
                            identNode.Token,
                            function,
                            0
                        ));
                        
                        continue;
                    }
                    
                    builder.AddIssue("Function call statements are not currently supported. For now, store a null return value to a junk variable.");

                    continue;
                }

                if (expression is FunctionCallExpressionNode call)
                {
                    builder.AddStatement(new FunctionCallStatementNode(call,
                        GetLogLevel(stepper, builder)));
                    continue;
                }

                if (expression is MemberAccessNode access && IsCurValidAssignPrefix(stepper))
                {
                    Token op = stepper.Step();
                    Token? assignment = null;
                    if (op.TokenType != TokenType.Assignment)
                    {
                        assignment = stepper.Step();
                    }

                    builder.AddStatement(
                        new AssignmentNode(
                            access,
                            op, // can sometimes be assignment
                            assignment, // if this one is empty
                            ParseExpression(stepper, builder)!,
                            GetLogLevel(stepper, builder)
                        )
                    );
                    
                    continue;
                }
                
                // lamo just copy & paste I guess
                if (expression is IdentifierNode ident && IsCurValidAssignPrefix(stepper))
                {
                    Token op = stepper.Step();
                    Token? assignment = null;
                    if (op.TokenType != TokenType.Assignment)
                    {
                        assignment = stepper.Step();
                    }

                    builder.AddStatement(
                        new AssignmentNode(
                            new MemberAccessNode(null, ident.Token),
                            op,
                            assignment,
                            ParseExpression(stepper, builder)!,
                            GetLogLevel(stepper, builder)
                        )
                    );
                    continue;
                }
                
                if (expression is OperationNode { Operator.TokenType: TokenType.OpenSquare } indexingOperation && IsCurValidAssignPrefix(stepper))
                {
                    Token op = stepper.Step();
                    if (op.TokenType != TokenType.Assignment)
                    {
                        stepper.Increment();
                    }

                    builder.AddStatement(
                        new ListItemAssignmentNode(
                            indexingOperation.Left,
                            indexingOperation.Right,
                            op,
                            ParseExpression(stepper, builder)!, // this cleans itself
                            GetLogLevel(stepper, builder)
                        )
                    );
                    continue;
                }
                
                // we have an invalid or WIP line, so lets try to make something out of it.

                if (expression is MemberAccessNode memberAccessNode)
                {
                    if (memberAccessNode.MemberName.TokenType is TokenType.Dot)
                    {
                        // unfinished line like: test.\n
                        
                        builder.AddStatement(
                            new UnfinishedMemberAccessNode(
                                memberAccessNode.Left ?? memberAccessNode,
                                memberAccessNode.MemberName
                            )
                        );
                        continue;
                    }
                }
                
            }

            if (stepper.Cur.TokenType is TokenType.Use)
            {
                var use = stepper.Step();
                
                ModuleIdent moduleName;
                
                if (stepper.AtEnd)
                {
                    break;
                }

                if (stepper.Cur.TokenType is TokenType.StartString)
                {
                    var str = ParseString(stepper.Step(), stepper, builder);

                    if (str is null)
                    {
                        continue;
                    }

                    if (str.Parts.Count != 1 || str.Parts[0] is not LiteralStringPart)
                    {
                        builder.AddDiagnostic(new (str.BaseToken, "Formatted strings are not allowed in a use statement module path"));
                        continue;
                    }

                    moduleName = new StringModuleIdent(str);
                }
                else
                {
                    var literalModuleIdentifier = new LiteralModuleIdentifier(stepper.Cur, []);
                    while (stepper is { AtEnd: false, Cur.TokenType: TokenType.Identifier or TokenType.Dot })
                    {
                        literalModuleIdentifier.Tokens.Add(stepper.Step());
                    }

                    if (stepper.AtEnd)
                    {
                        TrashUntilEndOfLine(stepper);
                        continue;
                    }

                    moduleName = literalModuleIdentifier;
                }

                builder.AddStatement(new UseStatementNode(
                    use,
                    moduleName,
                    ParseImportNodes(stepper, builder),
                    GetLogLevel(stepper, builder)
                ));

                continue;
            }
            
            if(stepper.Cur is {TokenType: TokenType.Const or TokenType.Let})
            {
                if (stepper.AtEnd)
                {
                    TrashUntilEndOfLine(stepper);
                    continue;
                }
                
                var declaration = stepper.Step();

                if (stepper.AtEnd)
                {
                    TrashUntilEndOfLine(stepper);
                    continue;
                }

                var name = stepper.Step();
                if (name.TokenType is not TokenType.Identifier)
                {
                    builder.AddDiagnostic(new(name, "Expected variable name (identifier)."));
                    TrashUntilEndOfLine(stepper);
                    continue;
                }

                TypeNode? type = null;
                if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Colon)
                {
                    stepper.Increment(); // Consume ':'
                    type = ParseTypeNode(stepper, builder);
                }

                if (stepper is { AtEnd: false, Cur.TokenType: TokenType.Assignment })
                {
                    stepper.Increment(); // Consume =
                }
                else
                {
                    builder.AddDiagnostic(new(stepper.AtEnd ? name : stepper.Cur, "expected equal sign", stepper.AtEnd));
                }

                var expression = ParseExpression(stepper, builder)!; // this should clean itself up?
                

                builder.AddStatement(new DeclarationNode(
                    declaration,
                    declaration.TokenType == TokenType.Const,
                    name,
                    type,
                    expression,
                    GetLogLevel(stepper, builder)
                ));

                continue;
            }

            if (stepper.Cur.TokenType is TokenType.Export)
            {
                var export = stepper.Step();

                if (stepper.Cur.TokenType is TokenType.Identifier)
                {
                    var name = stepper.Step();

                    if (stepper.Cur.TokenType is TokenType.Assignment)
                    {
                        stepper.Increment();
                        
                        builder.AddStatement(new ExportStatement(
                            export,
                            new ExpressionExportable(name, ParseExpression(stepper, builder)!), // this cleans itself up
                            GetLogLevel(stepper, builder)
                        ));
                        continue;
                    }
                    if (!CheckIfIsFunctionDeclaration(stepper))
                    {
                        builder.AddIssue("Expected a function declaration or assignment after export");

                        TrashUntilEndOfLine(stepper);
                        continue;
                    }

                    var func = ParseExpression(stepper, builder);

                    if (func is not FunctionNode funcNode)
                    {
                        builder.AddIssue("Expected a function declaration or assignment after export");
                        TrashUntilEndOfLine(stepper);
                        continue;
                    }
                    
                    builder.AddStatement(new ExportStatement(
                        export,
                        new FunctionExportable(name, funcNode),
                        GetLogLevel(stepper, builder)
                    ));
                    continue;
                }

                if (stepper.Cur.TokenType is TokenType.Define)
                {
                    var define = stepper.Cur;
                    // MODIFIED: Pass builder to ParseTargetOrSourceDefinition
                    var node = ParseTargetOrSourceDefinition(stepper, builder);
                    if (node is not null)
                    {
                        builder.AddStatement(new ExportStatement(
                            export,
                            new ChannelExportable(
                                define,
                                node
                            ),
                            GetLogLevel(stepper, builder)
                        ));
                    }
                    else
                    {
                        TrashUntilEndOfLine(stepper);
                    }
                    continue;
                }
            }

            if (stepper.Cur.TokenType is TokenType.Define)
            {
                var node = ParseTargetOrSourceDefinition(stepper, builder);
                if (node is not null)
                {
                    builder.AddStatement(new DefineTargetOrSourceStatement(
                        node.BaseToken,
                        node,
                        GetLogLevel(stepper, builder)
                    ));
                }
                else
                {
                    TrashUntilEndOfLine(stepper);
                }
                continue;
            }

            if (stepper.Cur.TokenType is TokenType.On)
            {
                var on = stepper.Step();

                var expr = ParseExpression(stepper, builder);

                var openCurly = stepper.Step();
                if (openCurly.TokenType is not TokenType.OpenCurly)
                {
                    builder.AddDiagnostic(new(on, "expected start of block", true));
                    TrashUntilEndOfLine(stepper);
                    continue;
                }

                var block = Parse(stepper, new StatementsNode(openCurly));
                
                builder.AddStatement(new OnSourceStatement(
                    on,
                    expr!,
                    block.RootNode,
                    GetLogLevel(stepper, builder)
                ));
                
                builder.AddDiagnostics(block.Diagnostics);
                continue;
            }

            if (stepper.Cur.TokenType is TokenType.While )
            {
                var whileToken = stepper.Step();
                if (stepper.AtEnd)
                {
                    continue;
                }
                var paren = stepper.Step();
                if (paren is not { TokenType: TokenType.OpenParen })
                {
                    builder.AddDiagnostic(new(paren, "Expected Open Parenthesis"));
                    TrashUntilEndOfLine(stepper);
                    continue;
                }
                
                // we're going to assume this was valid, it would put out some diagnostics and I don't think we need
                // any additional cleanup in the null case.
                ExpressionNode expression = ParseExpression(stepper, builder)!;

                if (stepper.AtEnd)
                {
                    continue;
                }
                
                var closeParen = stepper.Step();

                if (closeParen is not { TokenType: TokenType.ClosedParen })
                {
                    builder.AddDiagnostic(new(paren, "Expected Closed Parenthesis"));
                    TrashUntilEndOfLine(stepper);
                    continue;
                }
                
                if (stepper.AtEnd)
                {
                    continue;
                }

                var openCurly = stepper.Step();

                if (openCurly is not { TokenType: TokenType.OpenCurly })
                {
                    builder.AddDiagnostic(new(paren, "Expected Open Curly Brace"));
                    TrashUntilEndOfLine(stepper);
                    continue;
                }
                
                if (stepper.AtEnd)
                {
                    continue;
                }

                var block = Parse(stepper, new StatementsNode(openCurly));

                builder.AddStatement(new WhileLoopNode(
                    whileToken,
                    expression,
                    block.RootNode,
                    0
                ));
                
                builder.AddDiagnostics(block.Diagnostics);

                continue;
            }
            
            if (stepper.Cur.TokenType is TokenType.If )
            {
                var ifToken = stepper.Step();
                if (stepper.AtEnd)
                {
                    builder.AddDiagnostic(new(ifToken, "Expected open paren"));
                    continue;
                }
                var paren = stepper.Step();
                if (paren is not { TokenType: TokenType.OpenParen })
                {
                    builder.AddDiagnostic(new(paren, "Expected open paren"));
                    // go on assuming it actually was an open paren
                }

                var expression = ParseExpression(stepper, builder);

                if (expression is null)
                {
                    // for now just assume it's valid and replace it with some garbage.
                    expression = new NullExpression(paren);
                }


                if (stepper.AtEnd || stepper.Cur is not { TokenType: TokenType.ClosedParen })
                {
                    builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected closed paren"));
                    // go on assuming it actually was a closed paren
                }

                stepper.Increment();

                
                if (stepper.AtEnd || stepper.Cur is not { TokenType: TokenType.OpenCurly })
                {
                    builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected start of body"));
                    // ok this is irrecoverable. pack it up.
                    TrashUntilEndOfLine(stepper);
                    continue; // hopefully this should place us somewhere nice.
                }

                var openCurly = stepper.Step();

                var block = Parse(stepper, new StatementsNode(openCurly));
                List<ElseNode> elseNodes = [];

                while (stepper is { AtEnd: false, Cur.TokenType: TokenType.Else })
                {
                    var el = stepper.Step();
                    var final = true;
                    ExpressionNode? expressionNode = null;
                    Token? elif = null;
                    if (stepper.AtEnd)
                    {
                        break;
                    }
                    if (stepper.Cur.TokenType == TokenType.If)
                    {
                        elif = stepper.Step();
                        
                        if (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.OpenParen)
                        {
                            builder.AddIssue("Expected else if condition to start with open parenthesis");
                        }
                        stepper.Increment();
                        expressionNode = ParseExpression(stepper, builder);
                        if (expressionNode is null)
                        {
                            break;
                        }
                        if (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.ClosedParen)
                        {
                            builder.AddIssue("Expected else if codition to end with closed parenthesis");
                        }
                        stepper.Increment();
                        
                        final = false;
                    }

                    if (stepper.AtEnd)
                    {
                        continue;
                    }

                    var openCurlyEl = stepper.Cur;

                    if (openCurlyEl.TokenType is not TokenType.OpenCurly)
                    {
                        builder.AddIssue("Expected start of else block");
                        continue;
                    }
                    
                    stepper.Increment();

                    var elseBlock = Parse(stepper, new StatementsNode(openCurlyEl));
                    
                    elseNodes.Add(new ElseNode(
                        el,
                        elif,
                        expressionNode,
                        elseBlock.RootNode
                    ));
                    
                    builder.AddDiagnostics(elseBlock.Diagnostics);

                    if (final)
                        break;
                }

                builder.AddStatement(new IfNode(
                    ifToken,
                    expression,
                    block.RootNode,
                    elseNodes,
                    0
                ));

                continue;
            }
            
            if (stepper.Cur.TokenType is TokenType.Return)
            {
                var ret = stepper.Step();
                builder.AddStatement(new ReturnNode(
                    ret,
                    ParseExpression(stepper, builder),
                    GetLogLevel(stepper, builder)
                ));
                continue;
            }

            if (stepper.Cur.TokenType is TokenType.Break)
            {
                builder.AddStatement(new BreakNode(
                    stepper.Step(),
                    GetLogLevel(stepper, builder)
                ));
            }
            
            if (stepper.Cur.TokenType is TokenType.Continue)
            {
                builder.AddStatement(new ContinueNode(
                    stepper.Step(),
                    GetLogLevel(stepper, builder)
                ));
            }

            if (stepper.Cur.TokenType is TokenType.ClosedCurly)
            {
                // hopefully at the end of the block ??? please work <3
                stepper.Increment();
                return builder.Build();
            }
            
            builder.AddIssue("Unexpected token");
            break;
        }

        return builder.Build();
    }

    public static TypeNode? ParseTypeNode(string typeStr)
    {
        var tokens = RapidsLexer.Lex(typeStr);
        if (tokens.Count == 0)
        {
            return null;
        }
        var stepper = new ListStepper<Token>(tokens);
        var builder = new RapidsParseResult.Builder(stepper, new StatementsNode(tokens[0]));

        var typeNode = ParseTypeNode(stepper, builder);

        var parseResult = builder.Build();

        if (parseResult.Diagnostics.Count <= 0) return typeNode;
        
        Console.WriteLine("ERRORS IN RUNTIME PARSED TYPE NODE! THIS IS LIKELY A EXTENSION DEVELOPER BUG.");
        parseResult.PrintDiagnostics("<native module>", typeStr, new([]));
        return null;
    }
    
    public static TypeNode? ParseTypeNode(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        TypeNode? baseType;

        if (stepper.AtEnd)
        {
            builder.AddDiagnostic(new(stepper.Prev!, "Expected a type.", true));
            return null;
        }

        // 1. Handle Prefix: Source Channel (-^)
        if (stepper.Cur.TokenType is TokenType.Minus && stepper.Next?.TokenType is TokenType.Caret)
        {
            var minus = stepper.Step();
            var caret = stepper.Step();
            
            // Recursively parse the inner type (allows for -^-^string, though semantic analysis might reject it)
            var innerType = ParseTypeNode(stepper, builder);
            
            if (innerType == null) return null;

            baseType = new ChannelSourceTypeNode(minus, caret, innerType);
        }
        // 2. Handle Bi-Directional Channel: (Source&Target)
        else if (stepper.Cur.TokenType is TokenType.OpenParen)
        {
            var openParen = stepper.Step();
            var left = ParseTypeNode(stepper, builder);
            
            if (left == null) return null;

            if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.Ampersand)
            {
                builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected '&' in bidirectional channel type."));
                return null;
            }

            var ampersand = stepper.Step();
            var right = ParseTypeNode(stepper, builder);

            if (right == null) return null;

            if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedParen)
            {
                builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected ')' to close bidirectional channel type."));
                return null;
            }

            var closeParen = stepper.Step();
            baseType = new BiDirectionalChannelTypeNode(openParen, left, ampersand, right, closeParen);
        }

        else if (stepper.Cur.TokenType is TokenType.OpenCurly)
        {
            var openCurly = stepper.Step();
            var properties = new List<ObjectPropertyTypeNode>();

            while (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.ClosedCurly)
            {
                if (stepper.Cur.TokenType is not TokenType.Identifier)
                {
                    builder.AddDiagnostic(new(stepper.Cur, "Expected property name (identifier) in object type."));
                    // Recovery
                    while (!stepper.AtEnd && stepper.Cur.TokenType is not (TokenType.Comma or TokenType.ClosedCurly))
                    {
                        stepper.Increment();
                    }
                }
                else
                {
                    var name = stepper.Step();
                    TypeNode? propType = null;

                    if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Colon)
                    {
                        stepper.Increment(); // Consume ':'
                        propType = ParseTypeNode(stepper, builder);
                        if (propType is null)
                        {
                            // Recovery
                            while (!stepper.AtEnd && stepper.Cur.TokenType is not (TokenType.Comma or TokenType.ClosedCurly))
                            {
                                stepper.Increment();
                            }
                        }
                    }

                    properties.Add(new ObjectPropertyTypeNode(name, propType));
                }

                if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Comma)
                {
                    stepper.Increment();
                }
                else if (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.ClosedCurly)
                {
                    builder.AddDiagnostic(new(stepper.Cur, "Expected ',' or '}' in object type definition."));
                    while (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.ClosedCurly)
                    {
                        stepper.Increment();
                    }
                }
            }

            if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedCurly)
            {
                builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected '}' to close object type.", true));
                return null; 
            }

            var closeCurly = stepper.Step(); 
            baseType = new ObjectTypeNode(openCurly, properties, closeCurly, false);
        }

        else if (stepper.Cur.TokenType is TokenType.Identifier)
        {
            var identifier = stepper.Step();
            baseType = new IdentifierTypeNode(identifier, false);
        }
        else
        {
            builder.AddDiagnostic(new(stepper.Cur, "Expected a type (e.g., 'string', '-^source', or '{...}')."));
            return null;
        }


        
        while (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Minus && stepper.Next?.TokenType is TokenType.Caret)
        {
            var minus = stepper.Step();
            var caret = stepper.Step();
            baseType = new ChannelTargetTypeNode(baseType, minus, caret);
        }


        if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.QuestionMark)
        {
            stepper.Increment();

            baseType = baseType switch 
            {
                ObjectTypeNode obj => obj with { Optional = true },
                IdentifierTypeNode ident => ident with { Optional = true },
                ChannelSourceTypeNode src => src with { Optional = true },
                ChannelTargetTypeNode tgt => tgt with { Optional = true },
                BiDirectionalChannelTypeNode bidi => bidi with { Optional = true },
                _ => baseType
            };
        }

        return baseType;
    }

    private static void TrashUntilEndOfLine(ListStepper<Token> stepper)
    {
        while (!stepper.AtEnd)
        {
            var cur = stepper.Step();
            if (cur.TokenType is (TokenType.SemiColon or TokenType.QuestionMark))
            {
                while(!stepper.AtEnd && stepper.Cur.TokenType is TokenType.QuestionMark)
                {
                    stepper.Increment();
                }
                return;
            }
        }
    }

    private static Tuple<StringNode, ExpressionNode>? GetObjectPair(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        var startString = stepper.Step();

        StringNode? str;
        
        if (startString.TokenType is TokenType.StartString)
        {
            str = ParseString(startString, stepper, builder);
        }else if (startString.TokenType is TokenType.Identifier)
        {
            // this is a bit of a hack, but ultimately it works.
            str = new StringNode(startString, null, [new LiteralStringPart(startString)]);
        }
        else
        {
            builder.AddDiagnostic(new(startString, "Object key must be string, if you tried to pass an expression, try `{expression}`."));
            return null;
        }

        if (str is null && stepper.Next?.TokenType != TokenType.Colon)
        {
            return null;
        }

        if (stepper.Step().TokenType != TokenType.Colon)
        {
            builder.AddDiagnostic(new(stepper.Prev!, "Object key value pair is defined as `string`: value. Expected Colon."));
            return null;
        }

        var expr = ParseExpression(stepper, builder);

        if (str is null || expr is null)
        {
            return null;
        }

        return new Tuple<StringNode, ExpressionNode>(str, expr);
    }
    
    private static DefineTargetOrSourceNode? ParseTargetOrSourceDefinition(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        // This function assumes the stepper.Cur is TokenType.Define
        var defineToken = stepper.Step();

        if (stepper.Cur.TokenType is not (TokenType.Target or TokenType.Source))
        {
            builder.AddDiagnostic(new(stepper.Cur, "Expected 'target' or 'source' after 'define'."));
            return null;
        }

        var typeToken = stepper.Step();
        bool isTarget = typeToken.TokenType == TokenType.Target;

        if (stepper.Cur.TokenType is not TokenType.Identifier)
        {
            builder.AddDiagnostic(new(stepper.Cur, $"Expected name (identifier) after 'define {typeToken.Value}'."));
            return null;
        }
        
        var nameToken = stepper.Step();

        Token? dataIdentifier = null;

        if (!isTarget)
        {
            // can optionally specify the name for the data object, otherwise it is assumed it has no data object.
            if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.OpenParen)
            {
                stepper.Increment();
                if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.Identifier)
                {
                    builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected identifier for source data name."));
                }
                else
                {
                    dataIdentifier = stepper.Step();
                }
                
                if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedParen)
                {
                     builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected ')' after source data name.", true));
                }
                else
                {
                    stepper.Increment(); // closed paren
                }
            }
        }
        
        TypeNode? type = null;
        if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.OpenTriangle)
        {
            stepper.Increment(); // Consume '<'
            type = ParseTypeNode(stepper, builder);
            
            if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedTriangle)
            {
                builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected '>' to close type definition.", true));
                if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.ClosedTriangle)
                {
                    stepper.Increment();
                }
            }
            else
            {
                stepper.Increment(); 
            }
        }
        
        return new DefineTargetOrSourceNode(
            defineToken,
            nameToken,
            isTarget,
            dataIdentifier,
            type
        );
    }

    private static List<ImportNode>? ParseImportNodes(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        if (stepper.AtEnd)
        {
            return [];
        }
        
        if (stepper.Cur.TokenType is not TokenType.Colon)
        {
            return null;
        }

        stepper.Increment();
        List<ImportNode> imports = [];
        while (stepper is { AtEnd: false, Cur.TokenType: not (TokenType.SemiColon or TokenType.QuestionMark) } && !stepper.AtEnd)
        {
            var name = stepper.Step();
            if (name.TokenType is not TokenType.Identifier)
            {
                builder.AddDiagnostic(new(name, "Imported item should be an identifier"));
                return imports;
            }

            if (stepper.AtEnd)
            {
                break;
            }

            if (stepper.Cur.Value == "as")
            {
                stepper.Increment();
                
                if (stepper.AtEnd)
                {
                    break;
                }

                if (stepper.Cur.TokenType is not TokenType.Identifier)
                {
                    builder.AddDiagnostic(new Diagnostic(stepper.Cur, "mapped import name must be an identifier"));
                }
                
                imports.Add(new ImportNode(name, stepper.Step()));
            }
            else
            {
                imports.Add(new ImportNode(name, null));
            }

            if (stepper.Cur.TokenType is not TokenType.Comma)
            {
                break;
            }
            stepper.Increment();
        }
        
        return imports;
    }

    private static List<Tuple<StringNode, ExpressionNode>> ParseObjectKeyValues(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        List<Tuple<StringNode, ExpressionNode>> objectKeyValues = [];
        while (!stepper.AtEnd && stepper.Cur.TokenType != TokenType.ClosedCurly)
        {
            var pair = GetObjectPair(stepper, builder);
            if (pair == null)
            {
                return objectKeyValues;
            }
            objectKeyValues.Add(pair);

            if (stepper.Cur.TokenType != TokenType.Comma)
            {
                break;
            }
            stepper.Increment();
        }

        if (stepper.Cur.TokenType != TokenType.ClosedCurly)
        {
            builder.AddIssue("Expected Closed curly");
            return objectKeyValues;
        }
        
        stepper.Increment();

        return objectKeyValues;
    }

    private static bool IsCurValidAssignPrefix(ListStepper<Token> stepper)
    {
        return stepper.Cur is { TokenType: TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Star or TokenType.Modulo or TokenType.Assignment };
    }

    private static FunctionCallExpressionNode? ParseFunctionCall(ListStepper<Token> stepper, ExpressionNode initialExpression, RapidsParseResult.Builder builder)
    {
        if (initialExpression is not (MemberAccessNode or IdentifierNode or FunctionNode))
        {
            builder.AddIssue("Unexpected Function call");
            return null;
        }
        
        stepper.Increment();
        var parameters = ParseParams(stepper, builder);

        if (stepper.AtEnd)
        {
            return null;
        }

        if (stepper.Cur.TokenType is not TokenType.ClosedParen)
        {
            builder.AddIssue("Unexpected end of function call");
            return null;
        }

        stepper.Increment(); // closed paren
        return new FunctionCallExpressionNode(
            initialExpression,
            parameters
        );
    }

    private static List<ExpressionNode> ParseParams(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        List<ExpressionNode> parameters = [];
        while (stepper is { AtEnd: false, Cur.TokenType: not TokenType.ClosedParen })
        {
            var param = ParseExpression(stepper, builder);
            if (param is null)
            {
                return parameters;
            }
            parameters.Add(param);

            if (stepper.AtEnd)
            {
                return parameters;
            }

            if (stepper.Cur.TokenType is not TokenType.Comma)
            {
                break;
            }
            stepper.Increment();
        }

        return parameters;
    }

    private static int GetLogLevel(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        if (stepper.AtEnd)
        {
            builder.AddDiagnostic(new Diagnostic(stepper.Prev ?? stepper.ActiveList.Last(), "Expected End of line (? or ;)", true, ignoreInLanguageServer:true));
            return 0;
        }
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (stepper.Cur.TokenType)
        {
            case TokenType.SemiColon:
                stepper.Increment();
                return 0;
            case TokenType.QuestionMark:
            {
                var logLevel = 1;
                stepper.Increment();
                while (stepper is { AtEnd: false, Cur.TokenType: TokenType.QuestionMark })
                {
                    ++logLevel;
                    stepper.Increment();
                }

                return logLevel;
            }
            default:
                builder.AddDiagnostic(new Diagnostic(stepper.Prev ?? stepper.Cur, "Expected End of line (? or ;)", true, ignoreInLanguageServer:true));
                return 0;
        }
    }

    private static ExpressionNode? ParseExpression(ListStepper<Token> stepper, RapidsParseResult.Builder builder, int minPrecedence = 0)
    {
        var left = ParseSimpleExpression(stepper, builder);

        if (left is null)
        {
            return null;
        }

        if (!stepper.AtEnd)
        {
            switch (stepper.Cur.TokenType)
            {
                case TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Star or TokenType.Modulo when stepper.Next?.TokenType == TokenType.Assignment:
                {
                    if (left is not (MemberAccessNode or IdentifierNode))
                    {
                        builder.AddDiagnostic(new(left.BaseToken, "cannot assign to non member access or identifier nodes"));
                        return null;
                    }

                    return left;
                }
                case TokenType.OpenParen:
                {
                    if (!CheckIfIsFunctionDeclaration(stepper))
                    {
                        left = ParseFunctionCall(stepper, left, builder);
                        if (left is null)
                        {
                            return null;
                        }
                    }

                    break;
                }
            }
        }
        
        while (!stepper.AtEnd && Token.GetPrecedence(stepper.Cur.TokenType) >= minPrecedence && stepper.Next?.TokenType != TokenType.Assignment)
        {
            if (stepper.Cur.TokenType is TokenType.Dot)
            {
                var dot = stepper.Step();
                if (stepper is { HasNext: true, Cur: not { TokenType: TokenType.Identifier } })
                {
                    builder.AddDiagnostic(new(stepper.Cur, "Expected an identifier", ignoreInLanguageServer:true));
                    return new MemberAccessNode(left, dot); // sort of garbage data here
                }
                var member = stepper.Step();

                left = new MemberAccessNode(left, member);
                continue;
            }

            var op = stepper.Step();
            var precedence = Token.GetPrecedence(op.TokenType);
            var right = ParseExpression(stepper, builder, precedence + 1);

            if (stepper.AtEnd)
            {
                return null;
            }
            
            if (stepper.Cur.TokenType == TokenType.OpenParen)
            {
                if (!CheckIfIsFunctionDeclaration(stepper))
                {
                    right = ParseFunctionCall(stepper, left, builder);
                }
            }
            if (right is null)
            {
                return null;
            }
            if (op.TokenType is TokenType.OpenSquare)
                stepper.Increment();

            left = new OperationNode(left, op, right);
        }

        if (stepper.AtEnd)
        {
            return left;
        }
        
        // ReSharper disable once InvertIf
        if (stepper.Cur.TokenType == TokenType.OpenParen)
        {
            if (!CheckIfIsFunctionDeclaration(stepper))
            {
                left = ParseFunctionCall(stepper, left, builder);
            }
        }

        return left;
    }

    private static ExpressionNode? ParseSimpleExpression(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        if (!stepper.HasNext)
        {
            return null;
        }
        var start = stepper.Step();

        switch (start.TokenType)
        {
            case TokenType.Identifier:
                return new IdentifierNode(start);
            case TokenType.LiteralNumber:
                return new LiteralNumberNode(start, double.Parse(start.Value));
            case TokenType.StartString:
                return ParseString(start, stepper, builder);
            case TokenType.True or TokenType.False:
                return new BooleanNode(start);
            case TokenType.OpenCurly:
                return new ObjectNode(start, ParseObjectKeyValues(stepper, builder));
            case TokenType.Null:
                return new NullExpression(start);
            case TokenType.OpenParen:
            {
                if (CheckIfIsFunctionDeclaration(stepper))
                {
                    var arguments = new List<ArgumentNode>();
                    while (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.ClosedParen)
                    {
                        if (stepper.Cur.TokenType is not TokenType.Identifier)
                        {
                            builder.AddDiagnostic(new(stepper.Cur, "Expected argument name (identifier)."));
                            while (!stepper.AtEnd && stepper.Cur.TokenType is not (TokenType.Comma or TokenType.ClosedParen))
                            {
                                stepper.Increment();
                            }
                        }
                        else
                        {
                            var name = stepper.Step();
                            TypeNode? argType = null;
                            if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Colon)
                            {
                                stepper.Increment(); // Consume ':'
                                argType = ParseTypeNode(stepper, builder);
                            }
                            arguments.Add(new ArgumentNode(name, argType));
                        }
                        
                        if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Comma)
                        {
                            stepper.Increment();
                        }
                        else if (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.ClosedParen)
                        {
                            builder.AddDiagnostic(new(stepper.Cur, "Expected ',' or ')' after argument."));
                            while (!stepper.AtEnd && stepper.Cur.TokenType is not (TokenType.Comma or TokenType.ClosedParen))
                            {
                                stepper.Increment();
                            }
                        }
                    }
                    
                    if (stepper.AtEnd)
                    {
                        builder.AddDiagnostic(new(stepper.Prev!, "Unexpected end of function definition, expected ')'.", true));
                        return null;
                    }
                    
                    stepper.Increment(); // closed paren

                    TypeNode? returnType = null;
                    if (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Colon)
                    {
                        stepper.Increment(); // Consume ':'
                        returnType = ParseTypeNode(stepper, builder);
                    }

                    if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedTriangle)
                    {
                        builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected '>' before function body.", true));
                        TrashUntilEndOfLine(stepper);
                        return null;
                    }

                    var openTriangle = stepper.Step(); // consume '>'

                    if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.OpenCurly)
                    {
                        builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected '{' to start function body.", true));
                        TrashUntilEndOfLine(stepper);
                        return null;
                    }
                    
                    var openCurly = stepper.Step(); // open curly

                    var functionBody = Parse(stepper, new StatementsNode(openCurly));
                    RapidsParseResult? debugBody = null;

                    if (stepper is { HasNext: true, Cur.TokenType: TokenType.QuestionMark })
                    {
                        var openCurlyDebug = stepper.Step();
                        debugBody = Parse(stepper, new StatementsNode(openCurlyDebug));
                        builder.AddDiagnostics(debugBody.Diagnostics);
                    }
                    
                    builder.AddDiagnostics(functionBody.Diagnostics);

                    return new FunctionNode(openTriangle, arguments, functionBody.RootNode, debugBody?.RootNode, returnType);
                }
                
                var expr = ParseExpression(stepper, builder);

                if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedParen)
                {
                    builder.AddDiagnostic(new(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected closed parenthesis"));
                    return null;
                }
                
                stepper.Increment();
                
                return expr;
            }
            case TokenType.OpenSquare:
                List<ExpressionNode> list = [];
                while (stepper.Cur.TokenType != TokenType.ClosedSquare)
                {
                    if (list.Count > 0 && stepper.Step().TokenType != TokenType.Comma)
                    {
                        builder.AddIssue("Expected a comma");
                        continue;
                    }

                    var item = ParseExpression(stepper, builder);
                    if (item is null)
                    {
                        while (stepper.Cur.TokenType != TokenType.ClosedSquare)
                        {
                            stepper.Increment();
                        }

                        break;
                    }
                    list.Add(item);
                }
                stepper.Increment();
                return new ListNode(start, list);
            default:
                builder.AddDiagnostic(new(start, "Unexpected end of expression"));
                return null;
        }
    }

    private static StringNode? ParseString(Token startString, ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        StringNode stringNode = new(startString, null);

        while (stepper is { Cur.TokenType: not TokenType.EndString, AtEnd: false })
        {
            var token = stepper.Step();

            switch (token.TokenType)
            {
                case TokenType.StringContent:
                    stringNode.Parts.Add(new LiteralStringPart(token));
                    break;
                case TokenType.OpenCurly:
                    var template = ParseExpression(stepper, builder);
                    if (template == null)
                    {
                        continue;
                    }

                    var closed = stepper.Step();
                    stringNode.Parts.Add(new TemplateStringPart(template, token, closed));
                    break;
                default:
                    builder.AddDiagnostic(new(token, "There was an issue parsing this string"));
                    return null;
            }

        }
        
        stringNode.EndString = stepper.Step();

        return stringNode;
    }
    
    private static bool CheckIfIsFunctionDeclaration(ListStepper<Token> stepper)
    {
        var openParens = 1;
        var explorer = new ListStepper<Token>(stepper.FromIndex());
        if(explorer.Cur.TokenType == TokenType.OpenParen)
        {
            explorer.Increment();
        }

        while (openParens > 0)
        {
            if (!explorer.HasNext)
            {
                break;
            }
            if (explorer.Cur.TokenType is TokenType.ClosedParen)
            {
                openParens--;
            }
            if (explorer.Cur.TokenType is TokenType.OpenParen)
            {
                openParens++;
            }
            explorer.Increment();
        }
        
        if (!explorer.HasNext)
        {
            return false;
        }
        
        // so in theory this is ambiguous in the exact situation where you are trying to compare the result of a function with a property of an object declared inline
        // like so: function() > {test: 5}.test
        // but I don't want to deal with this situation, so developers have to do this:
        // function() > ({test: 5}).test
        return explorer.Cur.TokenType is TokenType.ClosedTriangle && explorer.Next is { TokenType: TokenType.OpenCurly };
    }
}