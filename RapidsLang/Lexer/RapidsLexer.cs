using System.Security.Cryptography;
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
    public static readonly TokenType[] keywords =
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

    public static readonly TokenType[] symbols =
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

    public static List<Token> Lex(StringTokenStepper stepper)
    {
        while (stepper.HasNext)
        {
            if (char.IsWhiteSpace(stepper.Cur))
            {
                stepper.Trash();
                continue;
            }
            
            if (keywords.Any(kw => stepper.CaptureIfNextHas(Token.GetDefaultValueForTokenType(kw), kw)))
            {
                continue;
            }
            
            if (char.IsNumber(stepper.Cur) || stepper.Cur == '.')
            {
                var hasHadDecimal = stepper.Cur == '.';
                while (stepper.HasNext)
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
            
            if (symbols.Any(sy => stepper.CaptureIfNextHas(Token.GetDefaultValueForTokenType(sy), sy)))
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

                        // var templateStepper = new StringStepper(stepper.ActiveString[stepper.index..]);

                        int curlyCount = 1;
                        bool inString = false;
                        Stack<bool> wasInString = new();
                        while (stepper.HasNext)
                        {
                            if (stepper is { Prev: not '\\', Cur: not '\\', Next: '{' })
                            {
                                curlyCount += 1;
                                wasInString.Push(inString);
                                inString = false;
                            }
                            if (stepper is { Prev: not '\\', Cur: not '\\', Next: '}' } && !inString)
                            {
                                curlyCount -= 1;
                                wasInString.TryPop(out inString);
                            }
                            if (stepper is { Prev: not '\\', Cur: not '\\', Next: '`' })
                            {
                                inString = !inString;
                            }

                            // templateStepper.Append();
                            stepper.Append();
                            
                            if (curlyCount == 0)
                            {
                                break;
                            }
                            
                        }
                        
                        // Should at this point be at the final curly brace
                        if (stepper.Cur != '}')
                        {
                            throw new Exception("woah");
                        }

                        var lex_result = Lex(stepper.Buffer);
                        
                        stepper.Tokens.AddRange(lex_result);
                        
                        stepper.ClearBuffer();

                        stepper.Append();
                        stepper.FlushBufferToToken(TokenType.ClosedCurly);
                        
                        if (stepper is { Cur: '`' })
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

            if (char.IsLetter(stepper.Cur))
            {
                stepper.Append();

                while (stepper.HasNext)
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
        }

        return stepper.Tokens;
    }
}