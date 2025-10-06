namespace RapidsLang.Utils;

public class EditableStack<T> : Stack<T>
{
    public void ReplaceTop(T newValue)
    {
        if (Count == 0) throw new InvalidOperationException("Stack is empty");

        Pop();           // remove old top
        Push(newValue);  // push replacement
    }

    public void UpdateTop(Func<T, T> updater)
    {
        if (Count == 0) throw new InvalidOperationException("Stack is empty");

        var oldTop = Pop();
        Push(updater(oldTop));
    }
}