using RapidsLang.Extensions;
using RapidsLang.Extensions.Channel;
using RapidsLang.Interpreter.Work;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Variables;

public class RapidsDataChannelVariable : RapidsVariable
{
    private DataChannel DataChannel { get; }
    private readonly RapidsFunction _sendDataFunction;
    private readonly Module _module;
    public bool Readable => DataChannel.Readable;
    public bool Writable => DataChannel.Writable;

    public string? DataVariableName
    {
        set => DataChannel.DataVariableName = value;
    }

    public RapidsDataChannelVariable(DataChannel dataChannel, Module module)
    {
        DataChannel = dataChannel;
        _sendDataFunction = new RapidsNativeFunction(SendData);
        _module = module;
    }

    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (op == RapidsOperator.Equality && other is RapidsDataChannelVariable pipe)
        {
            return new RapidsBooleanVariable(DataChannel.Equals(pipe.DataChannel));
        }
        return null;
    }

    public override string VariableTypeName => "[object pipe]";
    public override bool Truthy => true;
    public override RapidsVariable? GetMember(string memberName)
    {
        if (memberName == "writable")
        {
            return new RapidsBooleanVariable(DataChannel.Writable);
        }

        if (memberName == "readable")
        {
            return new RapidsBooleanVariable(DataChannel.Readable);
        }
        
        if (memberName == "send")
        {
            return new RapidsFunctionReferenceVariable(_sendDataFunction);
        }

        if (memberName == "on_data")
        {
            return RapidsFunctionReferenceVariable.OfNative(OnData);
        }
        return null;
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsDataChannelVariable(DataChannel, _module);
    }

    private void SendData(RapidsInterpreter interpreter)
    {
        var data = interpreter.Context.FunctionCallStack.Pop();
        DataChannel?.SendData(data);
    }

    public void SetReadable()
    {
        DataChannel.Readable = true;
    }

    public void SetWritable()
    {
        DataChannel.Writable = true;
    }

    private void OnData(RapidsInterpreter interpreter, CodeBlockRunWork? parentCodeBlock)
    {
        interpreter.Context.GetRoot().ModuleRegistry.MarkModuleAsTicking(_module, interpreter);
        if (interpreter.Context.FunctionCallStack.TryPop(out var funcVar) && funcVar is RapidsFunctionReferenceVariable func)
        {
            DataChannel.OnData += rv =>
            {
                if (rv is not RapidsNullVariable)
                {
                    interpreter.Context.FunctionCallStack.Push(rv);
                }
                
                func.Function.EnqueueExecution(interpreter, parentCodeBlock);
            };
        }
    }

    private Dictionary<Guid, Action<RapidsVariable>> OnStatementSubscriptions = [];

    public void SubscribeUsingOnStatement(OnSourceStatement node, RapidsInterpreter interpreter, CodeBlockRunWork? parent)
    {
        if (!interpreter.SupportsOnStatements)
        {
            // eventually create parent interpreters, so that this can find the root interpreter
            // then register the on in that environment and not here, but for now we're going to ignore it.
            return;
        }
        
        interpreter.Context.GetRoot().ModuleRegistry.MarkModuleAsTicking(_module, interpreter);

        var closure = new InterpreterContext(interpreter.Context);
        var subscriptionId = Guid.CreateVersion7();

        void OnTargetStatementCallback(RapidsVariable rv)
        {
            var closureInstance = new InterpreterContext(closure);
            if (DataChannel.DataVariableName is not null)
            {
                closureInstance.AddVariable(DataChannel.DataVariableName, new VariableHolder(rv, true));
            }

            var block = interpreter.StartNewBlock(node.Body, BlockType.SourceCallback, parent, closureInstance);

            block.Scope.Source = this;
            block.Scope.SourceSubscriptionId = subscriptionId;
        }

        DataChannel.OnData += OnTargetStatementCallback;

        OnStatementSubscriptions[subscriptionId] = OnTargetStatementCallback;
    }

    public void UnsubscribeOnStatement(Guid id)
    {
        DataChannel.OnData -= OnStatementSubscriptions[id];
    }
}