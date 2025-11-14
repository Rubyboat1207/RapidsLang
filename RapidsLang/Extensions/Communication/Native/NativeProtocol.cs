using RapidsLang.Extensions.Channel;
using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Communication.Native;

public class NativeProtocol : CommunicationProtocol
{
    private readonly Dictionary<Identifier, Action<RapidsVariable?>> _inputDictionary = [];
    private readonly Stack<(Identifier, RapidsVariable?)> _dataOutputs = [];
    public event Action<Identifier>? OutputEnabled = null;
    public event Action<Identifier>? OutputDisabled = null;

    private readonly Lock _lock = new();
    
    public override ChannelWriteResult WriteToInput(Identifier identifier, RapidsVariable? value)
    {
        if (_inputDictionary.TryGetValue(identifier, out var action))
        {
            action.Invoke(value);

            return new GoodChannelWriteResult();
        }

        return new BadChannelWriteResult("not_exist", "The channel you tried to write to does not exist.");
    }

    public override void Tick(InterpreterContext ctx)
    {
        lock (_lock)
        {
            foreach (var dataOutput in _dataOutputs)
            {
                var listenerGroup = EventListeners[dataOutput.Item1];

                foreach (var channelSubscriber in listenerGroup)
                {
                    channelSubscriber.Value.Event.Invoke(dataOutput.Item2);
                }
            }
        
            _dataOutputs.Clear(); 
        }
    }

    protected override void OutputAdded(Identifier identifier)
    {
        OutputEnabled?.Invoke(identifier);
    }

    protected override void OutputRemoved(Identifier identifier)
    {
        OutputDisabled?.Invoke(identifier);
    }

    public void RegisterInput(Identifier identifier, Action<RapidsVariable?> input)
    {
        _inputDictionary[identifier] = input;
    }

    public void WriteToOutput(Identifier identifier, RapidsVariable? output=null)
    {
        lock (_lock)
        {
            _dataOutputs.Push((identifier, output));
        }
        ResponsibleInterpreter?.WakeUp();
    }
}
