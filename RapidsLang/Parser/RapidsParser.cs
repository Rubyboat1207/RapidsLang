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
                if (stepper.Next?.TokenType == TokenType.OpenParen)
                {
                    if (IsNamedFunctionDefinition(stepper))
                    {
            
                        var nameToken = stepper.Step();
                        stepper.Increment(); // eat '('
                        
                        var funcNode = ParseFunctionDefinition(stepper, builder, nameToken, null);

                        if (funcNode != null)
                        {
                            builder.AddStatement(new FunctionDeclarationNode(
                                nameToken,
                                funcNode,
                                0
                            ));
                        }
                        continue;
                    }
                }
                
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
                    if (stepper.Cur.Value == "extern")
                    {
                        var externToken = stepper.Step();

                        if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.Identifier)
                        {
                            builder.AddDiagnostic(new Diagnostic(externToken, "Expected name of exported type.", true));
                            TrashUntilEndOfLine(stepper);
                            continue;
                        }

                        var exportedTypeName = stepper.Step();

                        if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.Colon)
                        {
                            builder.AddDiagnostic(new Diagnostic(exportedTypeName, "Expected colon", true));
                            TrashUntilEndOfLine(stepper);
                            continue;
                        }
                        
                        stepper.Increment(); // trash :

                        var type = ParseTypeNode(stepper, builder);

                        if (type is null)
                        {
                            TrashUntilEndOfLine(stepper);
                            continue;
                        }
                        
                        builder.AddStatement(
                            new ExportStatement(
                                export,
                                new ExternalExportable(
                                    externToken,
                                    new IdentifierNode(exportedTypeName),
                                    type
                                ),
                                GetLogLevel(stepper, builder)
                            )
                        );
                        continue;
                    }
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
                        new FunctionExportable(new IdentifierNode(name), funcNode),
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

                var curlyOrTiming = stepper.Step();
                Token curly;
                TimingNode? timing = null;
                if(curlyOrTiming.TokenType is TokenType.Identifier && curlyOrTiming.Value is "throttle" or "queue" or "latest")
                {
                    var measurement = ParseExpression(stepper, builder);
                    if (measurement is not null)
                    {
                        timing = new TimingNode(curlyOrTiming, measurement);
                    }
                    else
                    {
                        builder.AddDiagnostic(new(on, "expected a measurement after every or after in on source statement", true));
                        TrashUntilEndOfLine(stepper);
                        continue; 
                    }

                    curly = stepper.Step();
                }
                else
                {
                    curly = curlyOrTiming;
                }
                
                
                if(curly.TokenType is not TokenType.OpenCurly)
                {
                    builder.AddDiagnostic(new(on, "expected start of block", true));
                    TrashUntilEndOfLine(stepper);
                    continue; 
                }
                
                var block = Parse(stepper, new StatementsNode(curly));
                
                builder.AddStatement(new OnSourceStatement(
                    on,
                    expr!,
                    timing,
                    block.RootNode,
                    GetLogLevel(stepper, builder)
                ));
                
                builder.AddDiagnostics(block.Diagnostics);
                continue;
            }

            if (stepper.Cur.TokenType is TokenType.For)
            {
                ParseForLoop(stepper, builder);
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
                var cont = stepper.Step();
                Token? timing = null;
                if (stepper is { AtEnd: false, Cur.TokenType: TokenType.Identifier })
                {
                    if (stepper.Cur.Value != "now")
                    {
                        builder.AddIssue("Expected \"now\" or nothing after continue.");
                        TrashUntilEndOfLine(stepper);
                        continue;
                    }
                    timing = stepper.Step();
                }
                builder.AddStatement(new ContinueNode(
                    cont,
                    timing,
                    GetLogLevel(stepper, builder)
                ));
                continue;
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

    private static void ParseForLoop(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        var @for = stepper.Step();

        if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.OpenParen)
        {
            builder.AddDiagnostic(new(@for, "Expected '(' after for"));
            TrashUntilEndOfLine(stepper);
            return;
        }
        
        var openParen = stepper.Step();

        if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.Identifier)
        {
            builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "expected index or item name identifier"));
            TrashUntilEndOfLine(stepper);
            return;
        }

        var item = new IdentifierNode(stepper.Step());

        if (stepper.AtEnd)
        {
            return;
        }

        if (stepper.Cur is { TokenType: TokenType.Identifier, Value: "in" or "at" })
        {
            Token @in;
            Token? at = null;
            IdentifierNode? index = null;
            // ExpressionNode? index;

            if (stepper.Cur.Value == "at")
            {
                at = stepper.Step();

                if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.Identifier)
                {
                    builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "expected an index identifier"));
                    return;
                }

                index = new(stepper.Step());
                
                if (stepper.AtEnd || stepper.Cur is not {TokenType: TokenType.Identifier, Value: "in"})
                {
                    builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "expected in after the index identifier"));
                    return;
                }

                @in = stepper.Step();
            }
            else
            {
                @in = stepper.Step();
            }

            if (stepper.AtEnd)
            {
                TrashUntilEndOfLine(stepper);
                return;
            }

            var iterable = ParseExpression(stepper, builder);

            if (iterable is null)
            {
                builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "Expected iterable after index"));
                return;
            }
            
            if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedParen)
            {
                builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "Expected ')' after iterable"));
                TrashUntilEndOfLine(stepper);
                return;
            }

            stepper.Increment();

            if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.OpenCurly)
            {
                builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "expected '{' after for loop"));
                TrashUntilEndOfLine(stepper);
                return;
            }
            
            stepper.Increment();
            
            var iterativeBody = Parse(stepper);
            builder.AddDiagnostics(iterativeBody.Diagnostics);
            
            builder.AddStatement(new IterativeForLoop(
                @for,
                item,
                at,
                index,
                @in,
                iterable,
                iterativeBody.RootNode
            ));

            return;
        }
        
        if (stepper.Cur.TokenType is not TokenType.Assignment)
        {
            builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "Expected '=' after index"));
        }

        var equal = stepper.Step();

        var start = ParseExpression(stepper, builder);

        if (start is null)
        {
            builder.AddDiagnostic(new(openParen, "expected starting index expression"));
            TrashUntilEndOfLine(stepper);
            return;
        }

        if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.Identifier)
        {
            builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "expected 'to' or 'until' after starting index."));
            if (stepper.AtEnd) return;
        }

        var to = stepper.Step();

        var end = ParseExpression(stepper, builder);
        
        if (end is null)
        {
            builder.AddDiagnostic(new(openParen, "expected ending index expression"));
            TrashUntilEndOfLine(stepper);
            return;
        }

        if (stepper.AtEnd)
        {
            return;
        }

        Token? step = null;
        ExpressionNode? stepExpr = null;
        
        if (stepper.Cur is { TokenType: TokenType.Identifier, Value: "step" })
        {
            // this is a lot of stepping
            step = stepper.Step();

            stepExpr = ParseExpression(stepper, builder);

            if (stepExpr is null)
            {
                builder.AddDiagnostic(new(step, "Expected step size expression after step token"));
            }
        }
        
        if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.ClosedParen)
        {
            builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "Expected ')' ending index"));
            TrashUntilEndOfLine(stepper);
            return;
        }
        
        stepper.Increment(); // trash closed paren
        
        if (stepper.AtEnd || stepper.Cur.TokenType is not TokenType.OpenCurly)
        {
            builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "expected '{' after for loop"));
            TrashUntilEndOfLine(stepper);
            return;
        }

        stepper.Increment();
        
        var numericBody = Parse(stepper);
        builder.AddDiagnostics(numericBody.Diagnostics);
        
        builder.AddStatement(new NumericForLoop(
            @for,
            item,
            equal,
            start,
            to,
            end,
            numericBody.RootNode,
            step,
            stepExpr
        ));
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
        var left = ParseSimpleTypeNode(stepper, builder);

        if (left is null)
        {
            return null;
        }
        
        while (!stepper.AtEnd && stepper.Cur.TokenType is TokenType.Ampersand)
        {
            var ampersand = stepper.Step();
            var right = ParseSimpleTypeNode(stepper, builder);

            if (right is null)
            {
                builder.AddDiagnostic(new Diagnostic(ampersand, "Expected type after '&'"));
                return left;
            }
            
            bool optional = CheckOptional(stepper);

            left = new UnionTypeNode(left, ampersand, right, optional);
        }

        return left;
    }

    private static TypeNode? ParseSimpleTypeNode(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        if (stepper.AtEnd)
        {
            return null;
        }

        var token = stepper.Cur;
        TypeNode type;
        switch (token.TokenType)
        {
            case TokenType.Identifier:
            case TokenType.True:
            case TokenType.False:
                type = new IdentifierTypeNode(stepper.Step(), CheckOptional(stepper));
                break;

            case TokenType.OpenCurly:
                type = ParseObjectType(stepper, builder);
                break;

            case TokenType.Caret:
                type =  ParseSourceChannelType(stepper, builder);
                break;

            case TokenType.OpenParen:
                type =  ParseParenStartType(stepper, builder);
                break;

            default:
                builder.AddDiagnostic(new Diagnostic(token, "Unexpected token in type definition."));
                return null;
        }

        if (stepper is { AtEnd: false, Cur.TokenType: TokenType.OpenSquare })
        {
            var openSquare = stepper.Step();

            if (stepper is { AtEnd: false, Cur.TokenType: not TokenType.ClosedSquare })
            {
                builder.AddDiagnostic(new Diagnostic(openSquare, "Expected a closed square bracket."));
                return null;
            }

            var closedSquare = stepper.Step();

            return new ArrayTypeNode(openSquare, closedSquare, type, CheckOptional(stepper));
        }

        return type;
    }

    private static bool CheckOptional(ListStepper<Token> stepper)
    {
        if (!stepper.AtEnd && stepper.Cur.TokenType == TokenType.QuestionMark)
        {
            stepper.Increment();
            return true;
        }
        return false;
    }

    private static TypeNode ParseObjectType(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        var openCurly = stepper.Step();
        List<ObjectPropertyTypeNode> properties = [];

        while (!stepper.AtEnd && stepper.Cur.TokenType != TokenType.ClosedCurly)
        {
            if (stepper.Cur.TokenType is not (TokenType.Identifier or TokenType.StartString))
            {
                builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected identifier or string key for object type property."));
                break;
            }

            var nameToken = stepper.Step();

            // Handle literal string keys
            if (nameToken.TokenType == TokenType.StartString)
            {
                while (!stepper.AtEnd && stepper.Cur.TokenType != TokenType.EndString)
                {
                    stepper.Increment();
                }
                if (!stepper.AtEnd) stepper.Increment(); 
            }

            if (stepper.AtEnd || stepper.Cur.TokenType != TokenType.Colon)
            {
                builder.AddDiagnostic(new Diagnostic(stepper.Prev!, "Expected ':' after property name."));
                break;
            }
            stepper.Increment(); // Consume Colon

            var type = ParseTypeNode(stepper, builder);
            
            properties.Add(new ObjectPropertyTypeNode(nameToken, type));

            if (!stepper.AtEnd && stepper.Cur.TokenType == TokenType.Comma)
            {
                stepper.Increment();
            }
            else if (!stepper.AtEnd && stepper.Cur.TokenType != TokenType.ClosedCurly)
            {
                builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected ',' or '}'"));
                break;
            }
        }

        if (stepper.AtEnd || stepper.Cur.TokenType != TokenType.ClosedCurly)
        {
            builder.AddDiagnostic(new Diagnostic(stepper.AtEnd ? stepper.Prev! : stepper.Cur, "Expected '}'"));
            return new ObjectTypeNode(openCurly, properties, openCurly, CheckOptional(stepper));
        }

        var closeCurly = stepper.Step();
        return new ObjectTypeNode(openCurly, properties, closeCurly, CheckOptional(stepper));
    }

    private static TypeNode ParseSourceChannelType(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        var caret = stepper.Step(); // ^

        if (stepper.AtEnd || stepper.Cur.TokenType != TokenType.Minus)
        {
            builder.AddDiagnostic(new Diagnostic(caret, "Expected '-' after '^' for Source Channel definition."));
            return new ChannelSourceTypeNode(caret, caret, new IdentifierTypeNode(caret, false), null, null, null);
        }
        var minus = stepper.Step(); // -

        TypeNode? innerType;
        if (stepper.Cur.TokenType == TokenType.OpenParen)
        {
             stepper.Increment(); 
             innerType = ParseTypeNode(stepper, builder);
             if(stepper.Cur.TokenType == TokenType.ClosedParen) stepper.Increment(); 
             else builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected closing parenthesis for channel type"));
        }
        else
        {
             innerType = ParseTypeNode(stepper, builder);
        }

        if (innerType == null)
        {
             innerType = new IdentifierTypeNode(minus, false); // Junk
        }

        Token? openTri = null;
        IdentifierNode? dataName = null;
        Token? closeTri = null;

        if (!stepper.AtEnd && stepper.Cur.TokenType == TokenType.OpenTriangle)
        {
            openTri = stepper.Step();
            
            if (stepper.Cur.TokenType == TokenType.Identifier)
            {
                dataName = new IdentifierNode(stepper.Step());
            }
            else
            {
                builder.AddDiagnostic(new Diagnostic(openTri, "Expected identifier inside <...> for channel data name"));
            }

            if (stepper.Cur.TokenType == TokenType.ClosedTriangle)
            {
                closeTri = stepper.Step();
            }
            else
            {
                builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected '>'"));
            }
        }

        return new ChannelSourceTypeNode(minus, caret, innerType, openTri, dataName, closeTri);
    }

    private static TypeNode ParseParenStartType(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        var openParen = stepper.Step();

        if (stepper.Cur.TokenType == TokenType.ClosedParen)
        {
            var closeParenEmpty = stepper.Step();
            return FinishParsingFunctionType(stepper, builder, openParen, [], closeParenEmpty);
        }

        bool isFunction = stepper.Cur.TokenType == TokenType.Identifier && stepper.Next?.TokenType == TokenType.Colon;

        if (isFunction)
        {
            List<FunctionParamTypeNode> paramsList = [];
            while (!stepper.AtEnd && stepper.Cur.TokenType != TokenType.ClosedParen)
            {
                var name = stepper.Step();
                stepper.Increment();

                var type = ParseTypeNode(stepper, builder);
                if (type != null)
                {
                    paramsList.Add(new FunctionParamTypeNode(new IdentifierNode(name), type));
                }

                if (stepper.Cur.TokenType == TokenType.Comma) stepper.Increment();
                else if (stepper.Cur.TokenType != TokenType.ClosedParen) break;
            }

            Token closeParenFunc;
            if (stepper.Cur.TokenType == TokenType.ClosedParen) closeParenFunc = stepper.Step();
            else 
            {
                closeParenFunc = stepper.Prev!; 
                builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected ')'"));
            }

            return FinishParsingFunctionType(stepper, builder, openParen, paramsList, closeParenFunc);
        }
        
        var innerType = ParseTypeNode(stepper, builder);

        if (stepper.Cur.TokenType != TokenType.ClosedParen)
        {
            builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected ')'"));
            return new ParenthesizedTypeNode(openParen, innerType ?? new IdentifierTypeNode(openParen, false), openParen, false);
        }
        
        var closeParen = stepper.Step();

        if (stepper.Cur.TokenType == TokenType.Minus && stepper.Next?.TokenType == TokenType.Caret)
        {
            var minus = stepper.Step();
            var caret = stepper.Step();
            return new ChannelTargetTypeNode(innerType!, minus, caret);
        }
        
        if (innerType is UnionTypeNode union)
        {
            return new BiDirectionalChannelTypeNode(
                openParen, 
                union.A, 
                union.Ampersand, 
                union.B, 
                closeParen
            );
        }

        bool isOptional = CheckOptional(stepper);

        return new ParenthesizedTypeNode(openParen, innerType!, closeParen, isOptional);
    }

    private static FunctionTypeNode FinishParsingFunctionType(
        ListStepper<Token> stepper, 
        RapidsParseResult.Builder builder, 
        Token openParen, 
        List<FunctionParamTypeNode> parameters, 
        Token closeParen)
    {
        Token openTri;
        if (stepper.Cur.TokenType == TokenType.ClosedTriangle || (stepper.Cur.TokenType == TokenType.OpenTriangle))
        {
             openTri = stepper.Step();
        }
        else 
        {
            builder.AddDiagnostic(new Diagnostic(closeParen, "Expected '>' before return type"));
            openTri = closeParen;
        }

        var returnType = ParseTypeNode(stepper, builder);
        
        if (returnType == null)
        {
             builder.AddDiagnostic(new Diagnostic(openTri, "Expected return type"));
             returnType = new IdentifierTypeNode(openTri, false);
        }

        return new FunctionTypeNode(openParen, parameters, closeParen, openTri, returnType, CheckOptional(stepper));
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

        if (stepper.Cur.TokenType is not (TokenType.Identifier) && stepper.Cur.Value is "target" or "source")
        {
            builder.AddDiagnostic(new(stepper.Cur, "Expected 'target' or 'source' after 'define'."));
            return null;
        }

        var typeToken = stepper.Step();
        var isTarget = typeToken.Value == "target";

        if (stepper.Cur.TokenType is not TokenType.Identifier)
        {
            builder.AddDiagnostic(new(stepper.Cur, $"Expected name (identifier) after 'define {typeToken.Value} {typeToken.Value}'."));
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

    public static (ExpressionNode?, RapidsPreprocMetaData metaData) ParseExpression(string code)
    {
        var metadata = RapidsPreproc.Preprocess(code);
        var tokens = RapidsLexer.Lex(metadata.Output);

        var stepper = new ListStepper<Token>(tokens);
        var builder = new RapidsParseResult.Builder(stepper, null);

        return (ParseExpression(stepper, builder), metadata.Metadata);
    }

    private static ExpressionNode? ParseExpression(ListStepper<Token> stepper, RapidsParseResult.Builder builder, int minPrecedence = 0)
    {
        ExpressionNode? left = null;

        if (stepper.AtEnd)
        {
            builder.AddDiagnostic(new Diagnostic(stepper.Prev ?? stepper.ActiveList.Last(), "Unexpected end of file."));
            return null;
        }

        switch (stepper.Cur.TokenType)
        {
            case TokenType.OpenCurly:
                left = new ObjectNode(stepper.Step(), ParseObjectKeyValues(stepper, builder));
                break;
            case TokenType.OpenSquare:
                left = new ListNode(stepper.Step(), ParseList(stepper, builder));
                break;
            case TokenType.StartString: 
                left = ParseString(stepper.Step(), stepper, builder); 
                break;
                
            case TokenType.LiteralNumber:
            {
                if (!double.TryParse(stepper.Cur.Value, out var num))
                {
                    builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Unable to parse number"));
                    return null;
                }

                var numberNode = new LiteralNumberNode(stepper.Step(), num);

                if (stepper is { AtEnd: false, Cur.TokenType: TokenType.Identifier })
                {
                    left = new LiteralMeasurementNode(numberNode, new IdentifierNode(stepper.Step()));
                }
                else
                {
                    left = numberNode;
                }
                break;
            }

            case TokenType.Identifier:
                left = new IdentifierNode(stepper.Step());
                break;

            case TokenType.OpenParen:
                // This calls the complex method we wrote previously
                var openParen = stepper.Step(); 
                left = ParseGroupOrFunction(stepper, builder, openParen);
                break;

            case TokenType.Minus:
            case TokenType.Not:
            {
                var opToken = stepper.Step();
                var right = ParseExpression(stepper, builder, 8); 
                if (right != null)
                {
                    left = new UnaryOperationNode(opToken, right);
                }
                break;
            }
            
            case TokenType.True:
            case TokenType.False:
                left = new BooleanNode(stepper.Step());
                break;
            case TokenType.Null:
                left = new NullExpression(stepper.Step());
                break;

            default:
                builder.AddDiagnostic(new Diagnostic(stepper.Cur, $"Unexpected token {stepper.Cur.TokenType} at start of expression."));
                return null;
        }

        
        
        while (!stepper.AtEnd && Token.GetPrecedence(stepper.Cur.TokenType) > minPrecedence)
        {
            if (stepper.Next?.TokenType is TokenType.Assignment)
            {
                break;
            }
            
            if (left is null) return null;
            
            var opToken = stepper.Cur;

            switch (opToken.TokenType)
            {
                case TokenType.OpenParen:
                    left = ParseFunctionCall(stepper, left, builder);
                    break;

                case TokenType.OpenSquare:
                    left = ParseIndexing(stepper, builder, left);
                    break;

                case TokenType.Dot:
                    left = ParseMemberAccess(stepper, builder, left);
                    break;

                default:
                    stepper.Increment();

                    var right = ParseExpression(stepper, builder, Token.GetPrecedence(opToken.TokenType));
                    
                    if (right != null)
                    {
                        left = new OperationNode(left, opToken, right);
                    }
                    break;
            }
        }

        return left;
    }

    private static List<ExpressionNode> ParseList(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        List<ExpressionNode> items = [];
        while (!stepper.AtEnd && stepper.Cur.TokenType is not TokenType.ClosedSquare)
        {
            var item = ParseExpression(stepper, builder);

            if (item is null)
            {
                return items;
            }
            
            items.Add(item);

            if (!stepper.AtEnd)
            {
                if (stepper.Cur.TokenType is TokenType.Comma)
                {
                    stepper.Increment();
                    continue;
                }
                if (stepper.Cur.TokenType is TokenType.ClosedSquare)
                {
                    break;
                }
                
            }
            builder.AddDiagnostic(new(stepper.AtEnd ? stepper.ActiveList.Last() : stepper.Cur, "Expected Comma"));
        }
        stepper.Increment(); // trash ']'

        return items;
    }
    
    private static ExpressionNode? ParseGroupOrFunction(ListStepper<Token> stepper, RapidsParseResult.Builder builder, Token openParen)
    {
        if (stepper.Cur.TokenType == TokenType.ClosedParen)
        {
            stepper.Increment(); // eat '('
            return ParseFunctionDefinition(stepper, builder, openParen, new List<ArgumentNode>());
        }

        if (stepper.Cur.TokenType != TokenType.Identifier)
        {
            return ParseParenthesizedExpression(stepper, builder);
        }

        var nextType = stepper.Next?.TokenType;

        if (nextType is TokenType.Colon or TokenType.Comma)
        {
            return ParseFunctionDefinition(stepper, builder, openParen, null);
        }
        
        if (nextType != TokenType.ClosedParen)
        {
            return ParseParenthesizedExpression(stepper, builder);
        }

        var potentialArgName = stepper.Cur;
        stepper.Increment(); 
        stepper.Increment(); 

        var hasArrow = stepper is { AtEnd: false, Cur.TokenType: TokenType.ClosedTriangle };

        if (!hasArrow || stepper.Next is { TokenType: TokenType.OpenCurly } && IsObjectLiteral(stepper, 2)) return new IdentifierNode(potentialArgName);


        var args = new List<ArgumentNode> { new(potentialArgName, null) };
        return ParseFunctionBody(stepper, builder, openParen, args, null);

    }
    
    private static bool IsObjectLiteral(ListStepper<Token> stepper, int offset)
    {
        var i = offset + 1;
        var depth = 0; // Track bracket depth

        while (stepper.Index + i < stepper.ActiveList.Count)
        {
            var t = stepper.ActiveList[stepper.Index + i];
        
            switch (t.TokenType)
            {
                case TokenType.OpenCurly:
                case TokenType.OpenParen:
                case TokenType.OpenSquare:
                    depth++;
                    break;

                case TokenType.ClosedCurly:
                case TokenType.ClosedParen:
                case TokenType.ClosedSquare:
                    if (depth == 0) 
                    {
                        return false; 
                    }
                    depth--;
                    break;

                case TokenType.SemiColon:
                    if (depth == 0) return false; 
                    break;

                case TokenType.Colon:
                    // If we find a colon at depth 0, it is definitely an object key:pair
                    if (depth == 0) return true;
                    break;
            }
            i++;
        }
        return false;
    }
    
    private static bool IsNamedFunctionDefinition(ListStepper<Token> stepper)
    {
        if (stepper.Index + 1 >= stepper.ActiveList.Count) return false;
        if (stepper.ActiveList[stepper.Index + 1].TokenType != TokenType.OpenParen) return false;
        
        int i = 2; 
        int depth = 0;

        while (stepper.Index + i < stepper.ActiveList.Count)
        {
            var t = stepper.ActiveList[stepper.Index + i];

            if (t.TokenType == TokenType.OpenParen || t.TokenType == TokenType.OpenCurly || t.TokenType == TokenType.OpenSquare)
            {
                depth++;
            }
            else if (t.TokenType == TokenType.ClosedParen || t.TokenType == TokenType.ClosedCurly || t.TokenType == TokenType.OpenSquare)
            {
                if (depth == 0 && t.TokenType == TokenType.ClosedParen)
                {
                    break; 
                }
                if (depth > 0) depth--;
            }

            if (depth == 0)
            {
                if (t.TokenType == TokenType.Colon) return true;
            
                if (Token.GetPrecedence(t.TokenType) > 0) return false;
            }

            i++;
        }
        
        int afterParenOffset = i + 1; 
        
        if (stepper.Index + afterParenOffset >= stepper.ActiveList.Count) return false;
        
        var tokenAfterParen = stepper.ActiveList[stepper.Index + afterParenOffset];

        if (tokenAfterParen.TokenType == TokenType.Colon) return true;

        if (tokenAfterParen.TokenType == TokenType.ClosedTriangle)
        {
            int afterArrowOffset = afterParenOffset + 1;
            if (stepper.Index + afterArrowOffset < stepper.ActiveList.Count)
            {
                var tokenAfterArrow = stepper.ActiveList[stepper.Index + afterArrowOffset];
                if (tokenAfterArrow.TokenType == TokenType.OpenCurly)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ExpressionNode? ParseParenthesizedExpression(ListStepper<Token> stepper, RapidsParseResult.Builder builder)
    {
        var expr = ParseExpression(stepper, builder);

        if (expr is null)
        {
            return null;
        }
        
        if (stepper.Cur.TokenType != TokenType.ClosedParen)
        {
            builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected ')' after expression."));
        }
        else
        {
            stepper.Increment(); // Eat ')'
        }
        
        return expr; 
    }

    private static FunctionNode? ParseFunctionDefinition(
        ListStepper<Token> stepper, 
        RapidsParseResult.Builder builder, 
        Token openParen,
        List<ArgumentNode>? knownArgs) // Pass list if we already parsed some, null if we need to parse them
    {
        var arguments = knownArgs ?? new List<ArgumentNode>();

        // If we haven't parsed args yet, do it now
        if (knownArgs == null)
        {
            while (stepper is { AtEnd: false, Cur.TokenType: not TokenType.ClosedParen })
            {
                if (stepper.Cur.TokenType != TokenType.Identifier)
                {
                     builder.AddDiagnostic(new(stepper.Cur, "Expected argument name."));
                     break; 
                }
                
                var name = stepper.Step();
                TypeNode? type = null;
                
                if (stepper.Cur.TokenType == TokenType.Colon)
                {
                    stepper.Increment(); // Eat :
                    type = ParseTypeNode(stepper, builder);
                }
                
                arguments.Add(new ArgumentNode(name, type));
                
                if (stepper.Cur.TokenType == TokenType.Comma)
                {
                    stepper.Increment();
                }
            }
            stepper.Increment(); // Eat ')'
        }

        // Check optional return type: (args): Int >
        TypeNode? returnType = null;
        if (stepper.Cur.TokenType == TokenType.Colon)
        {
            stepper.Increment();
            returnType = ParseTypeNode(stepper, builder);
        }

        if (stepper.Cur.TokenType != TokenType.ClosedTriangle)
        {
            builder.AddDiagnostic(new(stepper.Cur, "Expected '>' after function parameters."));
            return null;
        }

        return ParseFunctionBody(stepper, builder, openParen, arguments, returnType);
    }
    
    private static FunctionNode ParseFunctionBody(
        ListStepper<Token> stepper,
        RapidsParseResult.Builder builder,
        Token openParen,
        List<ArgumentNode> args,
        TypeNode? returnType)
    {
        stepper.Increment(); // Eat '>'
    
        if (stepper.Cur.TokenType != TokenType.OpenCurly)
        {
            builder.AddDiagnostic(new(stepper.Cur, "Expected '{' to start function body."));
        }
        stepper.Increment(); // Eat '{'

        var body = Parse(stepper); // Your existing block parser
        builder.AddDiagnostics(body.Diagnostics);
    
        return new FunctionNode(openParen, args, body.RootNode, null, returnType);
    }
    
    private static ExpressionNode ParseMemberAccess(ListStepper<Token> stepper, RapidsParseResult.Builder builder, ExpressionNode target)
    {
        stepper.Increment(); // Consume '.'

        if (stepper.Cur.TokenType != TokenType.Identifier)
        {
            builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected identifier after '.'."));
            return target; 
        }

        var memberName = stepper.Step();

        return new MemberAccessNode(target, memberName);
    }
    
    private static ExpressionNode? ParseIndexing(ListStepper<Token> stepper, RapidsParseResult.Builder builder, ExpressionNode target)
    {
        var openBracket = stepper.Step(); // Consume '['
    
        var indexExpr = ParseExpression(stepper, builder);

        if (stepper.Cur.TokenType != TokenType.ClosedSquare)
        {
            builder.AddDiagnostic(new Diagnostic(stepper.Cur, "Expected ']' after index."));
        }
        else
        {
            stepper.Increment(); // Consume ']'
        }

        return indexExpr != null ? new OperationNode(target, openBracket, indexExpr) : null;
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
        
        // Parens
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

        if (explorer.Cur.TokenType is TokenType.Colon)
        {
            
        }
        
        // so in theory this is ambiguous in the exact situation where you are trying to compare the result of a function with a property of an object declared inline
        // like so: function() > {test: 5}.test
        // but I don't want to deal with this situation, so developers have to do this:
        // function() > ({test: 5}).test
        return explorer.Cur.TokenType is TokenType.ClosedTriangle && explorer.Next is { TokenType: TokenType.OpenCurly };
    }
}
