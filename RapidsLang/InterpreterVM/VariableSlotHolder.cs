using RapidsLang.Analyzer;

namespace RapidsLang.InterpreterVM;

public class VariableSlotHolder
{
    private Dictionary<Symbol, int> Slots { get; } = [];
    private int _largestSlotId;
    public uint LocalsUsed { get; private set; }
    
    public int AddOrGetSymbolSlot(Symbol symbol)
    {
        if (Slots.TryGetValue(symbol, out var slotId))
        {
            return slotId;
        }
        
        Slots[symbol] = _largestSlotId;
        LocalsUsed += 1;

        return _largestSlotId++;
    }

    public int ClaimNextOpenSlotId() {
        LocalsUsed += 1;

        return _largestSlotId++;
    }

    private void RecalculateLargestSlotId() => _largestSlotId = Slots.Count > 0 ? Slots.Max(s => s.Value) : 0;

    public VariableSlotHolder CloneForFunction(List<Symbol> parameters)
    {
        var cloned = new VariableSlotHolder();
        foreach (var symbol in parameters)
        {
            _ = cloned.AddOrGetSymbolSlot(symbol);
        }
        
        foreach (var (key, value) in Slots)
        {
            cloned.Slots[key] = value + parameters.Count;
        }
        
        cloned.RecalculateLargestSlotId();

        return cloned;
    }
}