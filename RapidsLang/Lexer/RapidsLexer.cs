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
    
    // -- Symbols
    Dot,
    Comma,
    Colon,
    SemiColon,
    
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
    
    // -- Blocks
    OpenCurly,
    ClosedCurly,
    OpenTriangle, // also gt
    ClosedTriangle, // also lt
    OpenParen,
    ClosedParen,
    
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
            TokenType.StartString or TokenType.EndString => "`",
            _ => null
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
        TokenType.Assignment,
        TokenType.Not,
        TokenType.Star,
        TokenType.Slash,
        TokenType.Modulo,
        TokenType.Plus,
        TokenType.Minus
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