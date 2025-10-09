using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.Utils;

namespace RapidsLang.Parser;

public class RapidsParser
{
    public static StatementsNode Parse(List<Token> tokens)
    {
        StatementsNode root = new();
        
        ListStepper<Token> stepper = new(tokens);

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
                }
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
        var left = ParseSimpleExpression(stepper);

        while (!stepper.AtEnd && Token.GetPrecedence(stepper.Cur.TokenType) > minPrecedence)
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
            
            var op = stepper.Cur;
            var right = ParseExpression(stepper, Token.GetPrecedence(stepper.Cur.TokenType));
            if(op.TokenType is TokenType.OpenSquare) 
                stepper.Increment();
            return new OperationNode(left, op, right);
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
                return new LiteralNumberNode(start, float.Parse(start.Value));
            case TokenType.StartString:
                return ParseString(stepper);
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
                    break;
                default:
                    throw new Exception("Unexpected token type");
            }

        }
        
        stepper.Increment();

        return stringNode;
    }
}