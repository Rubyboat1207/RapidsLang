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

        var baseEquals = ActiveString.AsSpan(index, str.Length).SequenceEqual(str);

        return baseEquals;
    }

    public bool CaptureIfNextHas(string? str, TokenType tokenType, bool isKeywords=false)
    {
        if (isKeywords)
        {
            if (str is null || !NextHasKeyword(str)) return false;
        }
        else
        {
            if (str is null || !NextHas(str)) return false;
        }
        
        Tokens.Add(new Token(tokenType, index));
        Increment(count:str.Length);
        return true;
    }
    
    public bool NextHasKeyword(string str)
    {
        if (!NextHas(str))
        {
            return false;
        }

        int charAfterIndex = index + str.Length;
        
        if (charAfterIndex >= ActiveString.Length)
        {
            return true;
        }

        char charAfter = ActiveString[charAfterIndex];
        
        if (char.IsLetterOrDigit(charAfter) || charAfter == '_')
        {
            return false;
        }
        
        return true;
    }
    
    public bool IsEscaped(int index)
    {
        var count = 0;
        var i = index - 1;
        while (i >= 0 && ActiveString[i] == '\\')
        {
            count++;
            i--;
        }
        // odd = escaped, even = not escaped
        return (count % 2) != 0;
    }
    
    public bool CurIsEscaped() => IsEscaped(index);
}