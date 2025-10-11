using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.Utils;

namespace RapidsLang.Parser;

public class RapidsParser
{
    public static StatementsNode Parse(List<Token> tokens)
    {
        ListStepper<Token> stepper = new(tokens);

        return Parse(stepper);
    }

    public static StatementsNode Parse(ListStepper<Token> stepper, StatementsNode? activeBlock=null)
    {
        var root = activeBlock ?? new StatementsNode();
        
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
                        throw new Exception("todo");
                    }

                    continue;
                }

                if (expression is FunctionCallExpressionNode call)
                {
                    root.Statements.Add(new FunctionCallStatementNode(call,
                        GetLogLevel(stepper)));
                    continue;
                }

                if (expression is MemberAccessNode access && stepper.Cur is { TokenType: TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Star or TokenType.Modulo or TokenType.Assignment })
                {
                    Token op = stepper.Step();
                    if (stepper.Cur.TokenType != TokenType.Equality)
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

                if (expression is IdentifierNode ident && stepper.Cur is { TokenType: TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Star or TokenType.Modulo or TokenType.Assignment })
                {
                    Token op = stepper.Step();
                    if (stepper.Cur.TokenType != TokenType.Equality)
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
                
            }

            if (stepper.Cur.TokenType is TokenType.Use)
            {
                var use = stepper.Step();

                var moduleName = "";

                while (stepper.Cur is { TokenType: TokenType.Identifier or TokenType.Dot })
                {
                    moduleName += stepper.Step().Value;
                }

                if (stepper.Cur is not { TokenType: TokenType.SemiColon or TokenType.QuestionMark })
                {
                    throw new Exception("Expected end of statement.");
                }

                root.Statements.Add(new UseStatementNode(
                    use, moduleName, GetLogLevel(stepper)
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
                    declaration.TokenType == TokenType.Const,
                    name,
                    null,
                    expression,
                    GetLogLevel(stepper)
                ));

                continue;
            }

            if (stepper.Cur.TokenType is TokenType.While )
            {
                stepper.Increment();
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

                var block = Parse(stepper);

                root.Statements.Add(new WhileLoopNode(
                    expression,
                    block,
                    0
                ));

                continue;
            }
            
            
            if (stepper.Cur.TokenType is TokenType.If )
            {
                stepper.Increment();
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

                var block = Parse(stepper);

                root.Statements.Add(new IfNode(
                    expression,
                    block,
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

            if (stepper.Cur.TokenType is TokenType.ClosedCurly)
            {
                // hopefully at the end of the block ??? please work <3
                stepper.Increment();
                return root;
            }
        }

        return root;
    }

    public static FunctionCallExpressionNode ParseFunctionCall(ListStepper<Token> stepper, ExpressionNode initialExpression)
    {
        if (initialExpression is not (MemberAccessNode or IdentifierNode))
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

    public static List<ExpressionNode> ParseParams(ListStepper<Token> stepper)
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

    public static int GetLogLevel(ListStepper<Token> stepper)
    {
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

    public static ExpressionNode ParseExpression(ListStepper<Token> stepper, int minPrecedence = 0)
    {
        // Array
        ExpressionNode left;
        
        left = ParseSimpleExpression(stepper);
        

        if(stepper.Cur.TokenType is TokenType.Plus or TokenType.Minus or TokenType.Slash or TokenType.Star or TokenType.Modulo && stepper.Next?.TokenType == TokenType.Assignment)
        {
            if (left is not (MemberAccessNode or IdentifierNode))
            {
                throw new Exception("Cannot assign to literals");
            }
            return left;
        }



        while (!stepper.AtEnd && Token.GetPrecedence(stepper.Cur.TokenType) >= minPrecedence)
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
            if (op.TokenType is TokenType.OpenSquare)
                stepper.Increment();

            left = new OperationNode(left, op, right);
        }
        
        if (stepper.Cur.TokenType == TokenType.OpenParen)
        {
            if (!CheckIfIsFunctionDeclaration(stepper))
            {
                left = ParseFunctionCall(stepper, left);
            }
        }

        return left;
    }

    public static bool CheckIfIsFunctionDeclaration(ListStepper<Token> stepper)
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
        // but I dont want to deal with this situation, so developers are gonna have to do this:
        // function() > ({test: 5}).test
        return explorer.Cur.TokenType is TokenType.ClosedTriangle && explorer.Next.TokenType is TokenType.OpenCurly;
    }

    public static ExpressionNode ParseSimpleExpression(ListStepper<Token> stepper)
    {
        Token start = stepper.Step();

        switch (start.TokenType)
        {
            case TokenType.Identifier:
                return new IdentifierNode(start);
            case TokenType.LiteralNumber:
                return new LiteralNumberNode(start, double.Parse(start.Value));
            case TokenType.StartString:
                return ParseString(stepper);
            case TokenType.True or TokenType.False:
                return new BooleanNode(start);
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
                    stepper.Increment(); // open triangle
                    stepper.Increment(); // open curly

                    var functionBody = Parse(stepper);
                    StatementsNode? debugBody = null;

                    if (stepper.Cur.TokenType is TokenType.QuestionMark)
                    {
                        stepper.Increment();
                        debugBody = Parse(stepper);
                    }

                    return new FunctionNode(arguments, functionBody, debugBody);
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
                return new ListNode(list);
            default:
                throw new Exception("Unexpected end of expression");
        }
    }

    public static StringNode ParseString(ListStepper<Token> stepper)
    {
        StringNode stringNode = new();

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