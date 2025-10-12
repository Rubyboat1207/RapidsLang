namespace RapidsLang.Utils;

public class StringStepper(string str)
{
    public string ActiveString { get; private init; } = str;
    private int ParentIndex;
    private int ParentBufferSize;
    public int index { get; private set; }
    public string Buffer { get; private set; } = "";
    
    public char Cur => ActiveString[index];
    public char Next => index >= ActiveString.Length ? ' ' : ActiveString[index + 1];
    public char Prev => index == 0 ? ' ' : ActiveString[index - 1];
    public bool HasNext => ActiveString.Length > index + 1;
    public bool AtEnd => ActiveString.Length == index;
    public int SourceIndex => index + ParentIndex;
    public int SourceBufferSize => Buffer.Length + ParentBufferSize;
    public string DebugStr => ActiveString[index..Math.Min(ActiveString.Length, index + 70)];

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

    public StringStepper CreateChild(int Length)
    {

        return new(ActiveString[index..(index + Length)]) {
            ParentIndex = index + ParentIndex,
            ParentBufferSize = Buffer.Length + ParentBufferSize
        };
    }

    public void Join(StringStepper child)
    {
        index += child.index;
        Buffer += child.Buffer;
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