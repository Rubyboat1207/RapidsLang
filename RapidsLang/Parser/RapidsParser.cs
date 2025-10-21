using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;
using RapidsLang.Utils;

namespace RapidsLang.Parser;

public static class RapidsParser
{
    public static StatementsNode Parse(string code, out RapidsPreprocMetaData metaData)
    {
        var preprocRes = RapidsPreproc.Preprocess(code);

        metaData = preprocRes.Metadata;

        var lexResult = RapidsLexer.Lex(preprocRes.Output);
        
        var parseResult = Parse(lexResult);

        return parseResult;
    }
    
    public static StatementsNode Parse(List<Token> tokens)
    {
        ListStepper<Token> stepper = new(tokens);

        return Parse(stepper);
    }

    private static StatementsNode Parse(ListStepper<Token> stepper, StatementsNode? activeBlock=null)
    {
        var root = activeBlock ?? new StatementsNode(stepper.Cur);
        
        while (!stepper.AtEnd)
        {
            if (stepper.Cur.TokenType is TokenType.Identifier)
            {
                var expression = ParseExpression(stepper);

                // check for function call
                if (stepper.Cur.TokenType is TokenType.OpenParen)
                {
                    if (CheckIfIsFunctionDeclaration(stepper))
                    {
                        if (expression is not IdentifierNode identNode)
                        {
                            throw new Exception("Invalid function definition");
                        }

                        var functionExpr = ParseExpression(stepper);

                        if (functionExpr is not FunctionNode function)
                        {
                            throw new Exception("I dun messed up.");
                        }

                        root.Statements.Add(new FunctionDeclarationNode(
                            identNode.Token,
                            function,
                            0
                        ));
                    }
                    else
                    {
                        throw new Exception("I dont know.");
                    }

                    continue;
                }

                if (expression is FunctionCallExpressionNode call)
                {
                    root.Statements.Add(new FunctionCallStatementNode(call,
                        GetLogLevel(stepper)));
                    continue;
                }

                if (expression is MemberAccessNode access && IsCurValidAssignPrefix(stepper))
                {
                    Token op = stepper.Step();
                    if (op.TokenType != TokenType.Assignment)
                    {
                        stepper.Increment();
                    }

                    root.Statements.Add(
                        new AssignmentNode(
                            access,
                            op,
                            ParseExpression(stepper),
                            GetLogLevel(stepper)
                        )
                    );
                    
                    continue;
                }

                if (expression is IdentifierNode ident && IsCurValidAssignPrefix(stepper))
                {
                    Token op = stepper.Step();
                    if (op.TokenType != TokenType.Assignment)
                    {
                        stepper.Increment();
                    }

                    root.Statements.Add(
                        new AssignmentNode(
                            new MemberAccessNode(null, ident.Token),
                            op,
                            ParseExpression(stepper),
                            GetLogLevel(stepper)
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

                    root.Statements.Add(
                        new ListItemAssignmentNode(
                            indexingOperation.Left,
                            indexingOperation.Right,
                            op,
                            ParseExpression(stepper),
                            GetLogLevel(stepper)
                        )
                    );
                    continue;
                }
                
            }

            if (stepper.Cur.TokenType is TokenType.Use)
            {
                var use = stepper.Step();
                
                var moduleName = "";

                if (stepper.Cur.TokenType is TokenType.StartString)
                {
                    var str = ParseString(stepper.Step(), stepper);

                    if (str.Parts.Count != 1 || str.Parts[0] is not LiteralStringPart litStr)
                    {
                        throw new Exception("Formatted strings are not allowed in use statement module path");
                    }

                    moduleName = litStr.Value.Value;
                }
                else
                {
                    while (stepper.Cur is { TokenType: TokenType.Identifier or TokenType.Dot })
                    {
                        moduleName += stepper.Step().Value;
                    }
                }

                root.Statements.Add(new UseStatementNode(
                    use,
                    moduleName,
                    ParseImportNodes(stepper),
                    GetLogLevel(stepper)
                ));

                continue;
            }
            
            if(stepper.Cur is {TokenType: TokenType.Const or TokenType.Let})
            {
                var declaration = stepper.Step();

                var name = stepper.Step();

                stepper.Increment(); // =

                var expression = ParseExpression(stepper);

                root.Statements.Add(new DeclarationNode(
                    declaration,
                    declaration.TokenType == TokenType.Const,
                    name,
                    null,
                    expression,
                    GetLogLevel(stepper)
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
                        
                        root.Statements.Add(new ExportStatement(
                            export,
                            new ExpressionExportable(name, ParseExpression(stepper)),
                            GetLogLevel(stepper)
                        ));
                        continue;
                    }
                    if (!CheckIfIsFunctionDeclaration(stepper))
                    {
                        throw new Exception("Expected a function declaration or assignment after export");
                    }

                    var func = ParseExpression(stepper);

                    if (func is not FunctionNode funcNode)
                    {
                        throw new Exception("I dun messed up again.");
                    }
                    
                    root.Statements.Add(new ExportStatement(
                        export,
                        new FunctionExportable(name, funcNode),
                        GetLogLevel(stepper)
                    ));
                    continue;
                }
                
                // todo: sources & targets
            }

            if (stepper.Cur.TokenType is TokenType.While )
            {
                var whileToken = stepper.Step();
                var paren = stepper.Step();
                if (paren is not { TokenType: TokenType.OpenParen })
                {
                    throw new Exception("Expected open paren");
                }

                var expression = ParseExpression(stepper);

                var closeParen = stepper.Step();

                if (closeParen is not { TokenType: TokenType.ClosedParen })
                {
                    throw new Exception("Expected closed paren");
                }

                var openCurly = stepper.Step();

                if (openCurly is not { TokenType: TokenType.OpenCurly })
                {
                    throw new Exception("Expected open curly");
                }

                var block = Parse(stepper, new StatementsNode(whileToken));

                root.Statements.Add(new WhileLoopNode(
                    whileToken,
                    expression,
                    block,
                    0
                ));

                continue;
            }
            
            if (stepper.Cur.TokenType is TokenType.If )
            {
                var ifToken = stepper.Step();
                var paren = stepper.Step();
                if (paren is not { TokenType: TokenType.OpenParen })
                {
                    throw new Exception("Expected open paren");
                }

                var expression = ParseExpression(stepper);

                var closeParen = stepper.Step();

                if (closeParen is not { TokenType: TokenType.ClosedParen })
                {
                    throw new Exception("Expected closed paren");
                }

                var openCurly = stepper.Step();

                if (openCurly is not { TokenType: TokenType.OpenCurly })
                {
                    throw new Exception("Expected open curly");
                }

                var block = Parse(stepper, new StatementsNode(ifToken));
                List<ElseNode> elseNodes = [];

                while (stepper is { AtEnd: false, Cur.TokenType: TokenType.Else })
                {
                    var el = stepper.Step();
                    var final = true;
                    ExpressionNode? expressionNode = null;
                    if (stepper.Cur.TokenType == TokenType.If)
                    {
                        stepper.Increment();
                        
                        stepper.Increment(); // open paren
                        expressionNode = ParseExpression(stepper);
                        stepper.Increment(); // close paren
                        
                        final = false;
                    }
                    
                    stepper.Increment(); // open curly
                    
                    elseNodes.Add(new ElseNode(
                        el,
                        expressionNode,
                        Parse(stepper, new StatementsNode(el))
                    ));

                    if (final)
                        break;
                }

                root.Statements.Add(new IfNode(
                    ifToken,
                    expression,
                    block,
                    elseNodes,
                    0
                ));

                continue;
            }
            
            if (stepper.Cur.TokenType is TokenType.Return)
            {
                var ret = stepper.Step();
                root.Statements.Add(new ReturnNode(
                    ret,
                    ParseExpression(stepper),
                    GetLogLevel(stepper)
                ));
                continue;
            }

            if (stepper.Cur.TokenType is TokenType.Break)
            {
                root.Statements.Add(new BreakNode(
                    stepper.Step(),
                    GetLogLevel(stepper)
                ));
            }
            
            if (stepper.Cur.TokenType is TokenType.Continue)
            {
                root.Statements.Add(new ContinueNode(
                    stepper.Step(),
                    GetLogLevel(stepper)
                ));
            }

            if (stepper.Cur.TokenType is TokenType.ClosedCurly)
            {
                // hopefully at the end of the block ??? please work <3
                stepper.Increment();
                return root;
            }
        }

        return root;
    }

    private static Tuple<StringNode, ExpressionNode> GetObjectPair(ListStepper<Token> stepper)
    {
        var startString = stepper.Step();

        StringNode str;
        
        if (startString.TokenType is TokenType.StartString)
        {
            str = ParseString(startString, stepper);
        }else if (startString.TokenType is TokenType.Identifier)
        {
            // this is a bit of a hack, but ultimately it works.
            str = new StringNode(startString, [new LiteralStringPart(startString)]);
        }
        else
        {
            throw new Exception("Object key must be string, if you tried to pass an expression, try `{expression}`.");
        }
        
        

        if (stepper.Step().TokenType != TokenType.Colon)
        {
            throw new Exception("Object key value pair is defined as `string`: value. Expected Colon.");
        }

        var expr = ParseExpression(stepper);

        return new Tuple<StringNode, ExpressionNode>(str, expr);
    }

    private static List<ImportNode>? ParseImportNodes(ListStepper<Token> stepper)
    {
        if (stepper.Cur.TokenType is not TokenType.Colon)
        {
            return null;
        }

        stepper.Increment();
        List<ImportNode> imports = [];
        while (stepper.Cur.TokenType is not (TokenType.SemiColon or TokenType.QuestionMark) && !stepper.AtEnd)
        {
            var name = stepper.Step();
            if (name.TokenType is not TokenType.Identifier)
            {
                throw new Exception("Imported item should be an identifier");
            }

            if (stepper.Cur.Value == "as")
            {
                stepper.Increment();

                if (stepper.Cur.TokenType is not TokenType.Identifier)
                {
                    throw new Exception("Import as name should be an identifier");
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

    private static List<Tuple<StringNode, ExpressionNode>> ParseObjectKeyValues(ListStepper<Token> stepper)
    {
        List<Tuple<StringNode, ExpressionNode>> kvp = [];
        while (stepper.Cur.TokenType != TokenType.ClosedCurly)
        {
            kvp.Add(GetObjectPair(stepper));

            if (stepper.Cur.TokenType != TokenType.Comma)
            {
                break;
            }
        }

        if (stepper.Cur.TokenType != TokenType.ClosedCurly)
        {
            throw new Exception("Expected Closed curly");
        }
        
        stepper.Increment();

        return kvp;
    }

    private static bool IsCurValidAssignPrefix(ListStepper<Token> stepper)
    {
        return stepper.Cur is { TokenType: TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Star or TokenType.Modulo or TokenType.Assignment };
    }

    private static FunctionCallExpressionNode ParseFunctionCall(ListStepper<Token> stepper, ExpressionNode initialExpression)
    {
        if (initialExpression is not (MemberAccessNode or IdentifierNode or FunctionNode))
        {
            throw new Exception("Unexpected Function Call");
        }
        
        stepper.Increment();
        var parameters = ParseParams(stepper);

        if (stepper.Cur.TokenType is not TokenType.ClosedParen)
            throw new Exception("Expected end of function call.");

        stepper.Increment(); // closed paren
        return new FunctionCallExpressionNode(
            initialExpression,
            parameters
        );
    }

    private static List<ExpressionNode> ParseParams(ListStepper<Token> stepper)
    {
        List<ExpressionNode> parameters = [];
        while (stepper is { AtEnd: false, Cur.TokenType: not TokenType.ClosedParen })
        {
            parameters.Add(ParseExpression(stepper));

            if (stepper.Cur.TokenType is not TokenType.Comma)
            {
                break;
            }
            stepper.Increment();
        }

        return parameters;
    }

    private static int GetLogLevel(ListStepper<Token> stepper)
    {
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
                throw new Exception("Expected End of line (? or ;)");
        }
    }

    private static ExpressionNode ParseExpression(ListStepper<Token> stepper, int minPrecedence = 0)
    {
        var left = ParseSimpleExpression(stepper);

        switch (stepper.Cur.TokenType)
        {
            case TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Star or TokenType.Modulo when stepper.Next?.TokenType == TokenType.Assignment:
            {
                return left is not (MemberAccessNode or IdentifierNode) ? throw new Exception("Cannot assign to literals") : left;
            }
            case TokenType.OpenParen:
            {
                if (!CheckIfIsFunctionDeclaration(stepper))
                {
                    left = ParseFunctionCall(stepper, left);
                }

                break;
            }
        }

        while (!stepper.AtEnd && Token.GetPrecedence(stepper.Cur.TokenType) >= minPrecedence && stepper.Next?.TokenType != TokenType.Assignment)
        {
            if (stepper.Cur.TokenType is TokenType.Dot)
            {
                stepper.Step();
                var member = stepper.Step();
                if (member.TokenType is not TokenType.Identifier)
                    throw new Exception("Expected identifier");

                left = new MemberAccessNode(left, member);
                continue;
            }

            var op = stepper.Step();
            var precedence = Token.GetPrecedence(op.TokenType);
            var right = ParseExpression(stepper, precedence + 1);
            if (stepper.Cur.TokenType == TokenType.OpenParen)
            {
                if (!CheckIfIsFunctionDeclaration(stepper))
                {
                    right = ParseFunctionCall(stepper, left);
                }
            }
            if (op.TokenType is TokenType.OpenSquare)
                stepper.Increment();

            left = new OperationNode(left, op, right);
        }
        
        // ReSharper disable once InvertIf
        if (stepper.Cur.TokenType == TokenType.OpenParen)
        {
            if (!CheckIfIsFunctionDeclaration(stepper))
            {
                left = ParseFunctionCall(stepper, left);
            }
        }

        return left;
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
        // so in theory this is ambiguous in the exact situation where you are trying to compare the result of a function with a property of an object declared inline
        // like so: function() > {test: 5}.test
        // but I don't want to deal with this situation, so developers have to do this:
        // function() > ({test: 5}).test
        return explorer.Cur.TokenType is TokenType.ClosedTriangle && explorer.Next is { TokenType: TokenType.OpenCurly };
    }

    private static ExpressionNode ParseSimpleExpression(ListStepper<Token> stepper)
    {
        var start = stepper.Step();

        switch (start.TokenType)
        {
            case TokenType.Identifier:
                return new IdentifierNode(start);
            case TokenType.LiteralNumber:
                return new LiteralNumberNode(start, double.Parse(start.Value));
            case TokenType.StartString:
                return ParseString(start, stepper);
            case TokenType.True or TokenType.False:
                return new BooleanNode(start);
            case TokenType.OpenCurly:
                return new ObjectNode(start, ParseObjectKeyValues(stepper));
            case TokenType.OpenParen:
            {
                if (CheckIfIsFunctionDeclaration(stepper))
                {
                    // function
                    var arguments = new List<ArgumentNode>();
                    while (stepper.Cur.TokenType is not TokenType.ClosedParen)
                    {
                        var name = stepper.Step();
                        if (stepper.Cur.TokenType is TokenType.Colon)
                        {
                            // types
                            throw new NotImplementedException("Types not yet implemented.");
                        }

                        if (stepper.Cur.TokenType is not (TokenType.Comma or TokenType.ClosedParen))
                        {
                            throw new Exception("Expected comma or end of function header");
                        }

                        if (stepper.Cur.TokenType is TokenType.Comma)
                        {
                            stepper.Increment();
                        }
                        
                        arguments.Add(new ArgumentNode(name, null));
                    }
                    stepper.Increment(); // closed paren
                    var openTriangle = stepper.Step(); // open triangle
                    stepper.Increment(); // open curly

                    var functionBody = Parse(stepper, new StatementsNode(openTriangle));
                    StatementsNode? debugBody = null;

                    if (stepper.Cur.TokenType is TokenType.QuestionMark)
                    {
                        stepper.Increment();
                        debugBody = Parse(stepper, new StatementsNode(openTriangle));
                    }

                    return new FunctionNode(openTriangle, arguments, functionBody, debugBody);
                }
                
                // not function just normal expression.
                
                var expr = ParseExpression(stepper);

                if (stepper.Cur.TokenType is not TokenType.ClosedParen)
                    throw new Exception("Expected ')' after expression");
                
                stepper.Increment();
                
                return expr;
            }
            case TokenType.OpenSquare:
                List<ExpressionNode> list = [];
                while (stepper.Cur.TokenType != TokenType.ClosedSquare)
                {
                    if (list.Count > 0 && stepper.Step().TokenType != TokenType.Comma)
                    {
                        throw new Exception("Expected Comma");
                    }
                    list.Add(ParseExpression(stepper));
                }
                stepper.Increment();
                return new ListNode(start, list);
            default:
                throw new Exception("Unexpected end of expression");
        }
    }

    private static StringNode ParseString(Token startString, ListStepper<Token> stepper)
    {
        StringNode stringNode = new(startString);

        while (stepper is { Cur.TokenType: not TokenType.EndString, AtEnd: false })
        {
            var token = stepper.Step();

            switch (token.TokenType)
            {
                case TokenType.StringContent:
                    stringNode.Parts.Add(new LiteralStringPart(token));
                    break;
                case TokenType.OpenCurly:
                    stringNode.Parts.Add(new TemplateStringPart(ParseExpression(stepper)));
                    stepper.Increment(); // }
                    break;
                default:
                    throw new Exception("Unexpected token type");
            }

        }
        
        stepper.Increment();

        return stringNode;
    }
}