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

        return new(str[index..(index + Length)]) {
            ParentIndex = index + ParentIndex,
            ParentBufferSize = Buffer.Length + ParentBufferSize
        };
    }

    public void Join(StringStepper child)
    {
        index += child.index;
        Buffer += child.Buffer;
    }
}