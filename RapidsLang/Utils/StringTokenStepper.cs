using RapidsLang.Lexer;

namespace RapidsLang.Utils;

public class StringTokenStepper(string str)
{
    public string ActiveString { get; private init; } = str;
    public int index { get; private set; }
    public List<Token> Tokens { get; private init; } = [];
    public string Buffer { get; private set; }  = "";

    public char Cur => ActiveString[index];
    public char Next => index + 1 >= ActiveString.Length ? ' ' : ActiveString[index + 1];
    public char Prev => index == 0 ? ' ' : ActiveString[index - 1];
    public bool HasNext => ActiveString.Length > index + 1;
    public bool AtEnd => ActiveString.Length == index;

    public void Increment(bool intoBuff=false, int count=1)
    {
        if (intoBuff)
            if (count == 1)
            {
                Buffer += ActiveString[index];
            }
            else
            {
                Buffer += ActiveString[index..(index + count)];
            }
        index += count;
    }

    public void Trash(int count=1)
    {
        Increment(count:count);
    }

    public void Append(int count=1)
    {
        Increment(true, count);
    }

    public void FlushBufferToToken(TokenType tokenType)
    {
        Tokens.Add(new Token(tokenType, index, Buffer));
        Buffer = "";
    }

    public void ClearBuffer()
    {
        Buffer = "";
    }

    public bool NextHas(string str)
    {
        if (index + str.Length > ActiveString.Length)
        {
            return false;
        }

        return ActiveString[index..(index + str.Length)] == str;
    }

    public bool CaptureIfNextHas(string? str, TokenType tokenType)
    {
        if (str is null || !NextHas(str)) return false;
        
        Tokens.Add(new Token(tokenType, index));
        Increment(count:str.Length);
        return true;
    }
}