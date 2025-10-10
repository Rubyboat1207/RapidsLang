using RapidsLang.Interpreter;
using RapidsLang.Utils;

namespace RapidsLang.Lexer;

public enum TokenType
{
    // -- Keywords
    On,
    Pipe,
    Define,
    Target,
    Source,
    If,
    For,
    While,
    Let,
    Const,
    Null,
    Use,
    True,
    False,

    // -- Symbols
    Dot,
    Comma,
    Colon,
    SemiColon,
    QuestionMark,

    // -- Operators
    Plus,
    Minus,
    Slash,
    Star,
    Modulo,
    Assignment,
    Not,

    // -- Comparison Operators
    Equality,
    LessThanOrEqualTo,
    GreaterThanOrEqualTo,
    NotEqual,
    And,
    Or,

    // -- Blocks
    OpenCurly,
    ClosedCurly,
    OpenTriangle, // also gt
    ClosedTriangle, // also lt
    OpenParen,
    ClosedParen,
    OpenSquare,
    ClosedSquare,

    // -- Variables
    Identifier,
    LiteralNumber,

    // -- Strings
    StartString,
    StringContent,
    EndString,
}

public class Token(TokenType type, int index, string? value = null)
{
    public TokenType TokenType { get; private init; } = type;
    public string Value { get; private init; } = value ?? GetDefaultValueForTokenType(type)!;
    public int Index { get; private init; } = index;

    public static string? GetDefaultValueForTokenType(TokenType type)
    {
        return type switch
        {
            TokenType.On => "on",
            TokenType.Pipe => "pipe",
            TokenType.Define => "define",
            TokenType.Target => "target",
            TokenType.Source => "source",
            TokenType.If => "if",
            TokenType.For => "for",
            TokenType.While => "while",
            TokenType.Let => "let",
            TokenType.Const => "const",
            TokenType.Null => "null",
            TokenType.Use => "use",
            TokenType.True => "true",
            TokenType.False => "false",
            TokenType.Dot => ".",
            TokenType.Comma => ",",
            TokenType.Colon => ":",
            TokenType.SemiColon => ";",
            TokenType.OpenCurly => "{",
            TokenType.ClosedCurly => "}",
            TokenType.OpenTriangle => "<",
            TokenType.ClosedTriangle => ">",
            TokenType.OpenParen => "(",
            TokenType.ClosedParen => ")",
            TokenType.OpenSquare => "[",
            TokenType.ClosedSquare => "]",
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Slash => "/",
            TokenType.Star => "*",
            TokenType.Modulo => "%",
            TokenType.Assignment => "=",
            TokenType.Not => "!",
            TokenType.Equality => "==",
            TokenType.LessThanOrEqualTo => "<=",
            TokenType.GreaterThanOrEqualTo => ">=",
            TokenType.NotEqual => "!=",
            TokenType.And => "&&",
            TokenType.Or => "||",
            TokenType.StartString or TokenType.EndString => "`",
            TokenType.QuestionMark => "?",
            _ => null
        };
    }

    public static int GetPrecedence(TokenType type)
    {
        return type switch
        {
            TokenType.Dot                     => 7,  // Member access (highest precedence)
            TokenType.OpenSquare              => 7,  // Indexing []
            TokenType.Star                    => 6,  // Multiplication / division / modulo
            TokenType.Slash                   => 6,
            TokenType.Modulo                  => 6,
            TokenType.Plus                    => 5,  // Addition / subtraction
            TokenType.Minus                   => 5,
            TokenType.Equality                => 4,  // Equality / inequality
            TokenType.NotEqual                => 4,
            TokenType.OpenTriangle            => 3,  // Comparison
            TokenType.GreaterThanOrEqualTo    => 3,
            TokenType.ClosedTriangle          => 3,
            TokenType.LessThanOrEqualTo       => 3,
            TokenType.And                     => 2,  // Logical AND
            TokenType.Or                      => 1,  // Logical OR
            _                                 => -1,  // Everything else
        };
    }

    public RapidsOperator GetOperator()
    {
        return TokenType switch
        {
            TokenType.Plus => RapidsOperator.Add,
            TokenType.Minus => RapidsOperator.Subtract,
            TokenType.Slash => RapidsOperator.Divide,
            TokenType.Star => RapidsOperator.Multiply,
            TokenType.Modulo => RapidsOperator.Modulo,
            TokenType.ClosedTriangle => RapidsOperator.GreaterThan,
            TokenType.OpenTriangle => RapidsOperator.LessThan,
            TokenType.GreaterThanOrEqualTo => RapidsOperator.GreaterThanEqualTo,
            TokenType.LessThanOrEqualTo => RapidsOperator.LessThanEqualTo,
            TokenType.Equality => RapidsOperator.Equality,
            TokenType.NotEqual => RapidsOperator.Inequal,
            TokenType.OpenSquare => RapidsOperator.Index,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public static class RapidsLexer
{
    private static readonly TokenType[] Keywords =
    [
        TokenType.On,
        TokenType.Pipe,
        TokenType.Define,
        TokenType.Target,
        TokenType.Source,
        TokenType.If,
        TokenType.For,
        TokenType.While,
        TokenType.Let,
        TokenType.Const,
        TokenType.Null,
        TokenType.Use,
        TokenType.True,
        TokenType.False
    ];

    private static readonly TokenType[] Symbols =
    [
        TokenType.GreaterThanOrEqualTo,
        TokenType.LessThanOrEqualTo,
        TokenType.NotEqual,
        TokenType.Equality,
        
        TokenType.Dot,
        TokenType.Comma,
        TokenType.SemiColon,
        TokenType.Colon,
        TokenType.OpenCurly,
        TokenType.ClosedCurly,
        TokenType.OpenParen,
        TokenType.ClosedParen,
        TokenType.OpenTriangle,
        TokenType.ClosedTriangle,
        TokenType.OpenSquare,
        TokenType.ClosedSquare,
        TokenType.Assignment,
        TokenType.Not,
        TokenType.Star,
        TokenType.Slash,
        TokenType.Modulo,
        TokenType.Plus,
        TokenType.Minus,
        TokenType.QuestionMark
    ];
    
    public static List<Token> Lex(string code)
    {
        StringTokenStepper stepper = new(code);

        return Lex(stepper);
    }

    private static List<Token> Lex(StringTokenStepper stepper)
    {
        while (!stepper.AtEnd)
        {
            if (char.IsWhiteSpace(stepper.Cur))
            {
                stepper.Trash();
                continue;
            }
            
            if (Keywords.Any(kw => stepper.CaptureIfNextHas(Token.GetDefaultValueForTokenType(kw), kw)))
            {
                continue;
            }
            
            if (char.IsNumber(stepper.Cur) || (stepper.Cur == '.' && char.IsNumber(stepper.Next)))
            {
                var hasHadDecimal = stepper.Cur == '.';
                while (!stepper.AtEnd)
                {
                    stepper.Append();
                    if(stepper.AtEnd)
                    {
                        break;
                    }
                    if (char.IsNumber(stepper.Cur))
                    {
                        continue;
                    }

                    if (stepper.Cur == '.' && !hasHadDecimal)
                    {
                        continue;
                    }

                    break;
                }
                stepper.FlushBufferToToken(TokenType.LiteralNumber);
                continue;
            }
            
            if (Symbols.Any(sy => stepper.CaptureIfNextHas(Token.GetDefaultValueForTokenType(sy), sy)))
            {
                continue;
            }

            if (stepper.Cur == '`')
            {
                stepper.Append();
                stepper.FlushBufferToToken(TokenType.StartString);

                while (!stepper.AtEnd)
                {
                    if (stepper is { Prev: not '\\', Cur: not '\\', Next: '`' })
                    {
                        stepper.Append();
                        stepper.FlushBufferToToken(TokenType.StringContent);
                        stepper.Append();
                        stepper.FlushBufferToToken(TokenType.EndString);
                        break;
                    }

                    if (stepper is { Prev: not '\\', Cur: not '\\', Next: '{' })
                    {
                        stepper.Append();
                        stepper.FlushBufferToToken(TokenType.StringContent);

                        stepper.Append();
                        stepper.FlushBufferToToken(TokenType.OpenCurly);

                        int depth = 1;
                        bool inString = false;
                        stepper.ClearBuffer();

                        while (true)
                        {
                            if (stepper.AtEnd)
                                throw new Exception("Unterminated { in template string");

                            char c = stepper.Cur;

                            if (c == '`' && stepper.Prev != '\\')
                            {
                                inString = !inString;
                                stepper.Append();
                                continue;
                            }

                            if (!inString)
                            {
                                if (c == '{')
                                {
                                    depth++;
                                    stepper.Append();
                                    continue;
                                }

                                if (c == '}')
                                {
                                    depth--;
                                    if (depth == 0)
                                    {
                                        break;
                                    }
                                    stepper.Append();
                                    continue;
                                }
                            }

                            stepper.Append();
                        }

                        var innerCode = stepper.Buffer;
                        var innerTokens = Lex(innerCode);
                        stepper.Tokens.AddRange(innerTokens);
                        stepper.ClearBuffer();

                        stepper.Append();
                        stepper.FlushBufferToToken(TokenType.ClosedCurly);

                        if (stepper is { AtEnd: false, Cur: '`' })
                        {
                            stepper.Append();
                            stepper.FlushBufferToToken(TokenType.EndString);
                            break;
                        }

                        continue;
                    }


                    
                    stepper.Append();
                }
                
                continue;
            }

            if (!char.IsLetter(stepper.Cur)) continue;
            stepper.Append();

            while (!stepper.AtEnd)
            {
                if (char.IsNumber(stepper.Cur) || char.IsLetter(stepper.Cur))
                {
                    stepper.Append();
                    continue;
                }

                break;
            }

            stepper.FlushBufferToToken(TokenType.Identifier);
        }

        return stepper.Tokens;
    }
}