namespace RapidsLang.Utils;

public class ListStepper<T>(List<T> list) where T : class
{
    public List<T> ActiveList { get; private init; } = list;
    public int Index { get; private set; }
    
    public T Cur => ActiveList[Index];
    public T? Next => Index + 1 >= ActiveList.Count ? null : ActiveList[Index + 1];
    public T? Prev => Index == 0 || ActiveList.Count == 0 ? null : ActiveList[Math.Min(Index, ActiveList.Count) - 1];
    
    public bool HasNext => ActiveList.Count > Index + 1;
    public bool AtEnd => Index >= ActiveList.Count;
    
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

    public List<T> FromIndex()
    {
        return ActiveList.GetRange(Index, ActiveList.Count - Index);
    }
}