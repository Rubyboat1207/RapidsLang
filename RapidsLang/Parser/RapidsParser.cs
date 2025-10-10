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
                    stepper.Increment();
                    List<ExpressionNode> parameters = [];
                    while (stepper is { AtEnd: false, Cur.TokenType: not TokenType.ClosedParen })
                    {
                        parameters.Add(ParseExpression(stepper));

                        if (stepper.Cur.TokenType is not TokenType.Comma)
                        {
                            break;
                        }
                    }

                    if (stepper.Cur.TokenType is not TokenType.ClosedParen)
                        throw new Exception("Expected end of function call.");

                    stepper.Increment(); // closed paren
                    root.Statements.Add(new FunctionCallStatementNode(
                        new FunctionCallExpressionNode(
                            expression,
                            parameters
                        ),
                        GetLogLevel(stepper)
                    ));
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

            if (stepper.Cur.TokenType is TokenType.ClosedCurly)
            {
                // hopefully at the end of the block ??? please work <3
                stepper.Increment();
                return root;
            }
        }

        return root;
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
        if (stepper.Cur.TokenType == TokenType.OpenSquare)
        {
            List<ExpressionNode> list = [];
            stepper.Increment();
            while (stepper.Cur.TokenType != TokenType.ClosedSquare)
            {
                if (list.Count > 0 && stepper.Step().TokenType != TokenType.Comma)
                {
                    throw new Exception("Expected Comma");
                }
                list.Add(ParseExpression(stepper));
            }
            stepper.Increment();

            left = new ListNode(list);
        }
        else
        {
            left = ParseSimpleExpression(stepper);
        }
        

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
                if(member.TokenType is not TokenType.Identifier) 
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

        return left;
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
                var expr = ParseExpression(stepper);

                if (stepper.Cur.TokenType is not TokenType.ClosedParen)
                    throw new Exception("Expected ')' after expression");
                
                stepper.Increment();
                
                return expr;
            }
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