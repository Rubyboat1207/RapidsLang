using RapidsLang.Lexer;

namespace RapidsLang.Utils;

public class StringTokenStepper(string str)
{
    public string ActiveString { get; private init; } = str;
    public int Index { get; private set; }
    public int ParentIndex { get; set; }
    public List<Token> Tokens { get; private init; } = [];
    public string Buffer { get; private set; }  = "";

    public char Cur => ActiveString[Index];
    public char Next => Index + 1 >= ActiveString.Length ? ' ' : ActiveString[Index + 1];
    public char Prev => Index == 0 ? ' ' : ActiveString[Index - 1];
    public bool HasNext => ActiveString.Length > Index + 1;
    public bool AtEnd => ActiveString.Length == Index;

    public void Increment(bool intoBuff=false, int count=1)
    {
        if (intoBuff)
            if (count == 1)
            {
                Buffer += ActiveString[Index];
            }
            else
            {
                Buffer += ActiveString[Index..(Index + count)];
            }
        Index += count;
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
        Tokens.Add(new Token(tokenType, Index + ParentIndex, Buffer));
        Buffer = "";
    }

    public void ClearBuffer()
    {
        Buffer = "";
    }

    public bool NextHas(string str)
    {
        if (Index + str.Length > ActiveString.Length)
        {
            return false;
        }

        var baseEquals = ActiveString.AsSpan(Index, str.Length).SequenceEqual(str);

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
        
        Tokens.Add(new Token(tokenType, Index + ParentIndex));
        Increment(count:str.Length);
        return true;
    }
    
    public bool NextHasKeyword(string str)
    {
        if (!NextHas(str))
        {
            return false;
        }

        int charAfterIndex = Index + str.Length;
        
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
    
    public bool CurIsEscaped() => IsEscaped(Index);
}