namespace RapidsLang.Utils;

public class ListStepper<T>(List<T> list) where T : class
{
    public List<T> ActiveList { get; private init; } = list;
    public int Index { get; private set; }
    
    public T Cur => ActiveList[Index];
    public T? Next => Index >= ActiveList.Count ? null : ActiveList[Index + 1];
    public T? Prev => Index == 0 ? null : ActiveList[Index - 1];
    
    public bool HasNext => ActiveList.Count > Index + 1;
    public bool AtEnd => ActiveList.Count == Index;
    
    public void Increment(int count=1)
    {
        Index += count;
    }

    public T Step()
    {
        var cur = Cur;
        Increment();
        return cur;
    }
}