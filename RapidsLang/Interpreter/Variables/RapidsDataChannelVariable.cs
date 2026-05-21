using System.Collections.Concurrent;
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
        get => DataChannel.DataVariableName;
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
                interpreter.EnqueueAction(() => 
                {
                    if (rv is not RapidsNullVariable)
                    {
                        interpreter.Context.FunctionCallStack.Push(rv);
                    }
                    
                    func.Function.EnqueueExecution(interpreter, parentCodeBlock);
                });
            };
        }
    }

    private Dictionary<Guid, DataChannelSubscription> OnStatementSubscriptions = [];

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

        if (node.Every is not null)
        {
            interpreter.EvaluateExpression(node.Every.Time, measurement =>
            {
                if (measurement is not RapidsNumberVariable number)
                {
                    throw new Exception("Expected number for pipe subscription timing");
                }
                DataChannelSubscription subscription = node.Every?.BaseToken.Value switch
                {
                    "queue" => new DataChannelQueued(
                        subscriptionId,
                        node.Body,
                        interpreter,
                        parent,
                        closure,
                        this,
                        number.Value
                    ),
                    "latest" => new DataChannelLatest(
                        subscriptionId,
                        node.Body,
                        interpreter,
                        parent,
                        closure,
                        this,
                        number.Value
                    ),
                    "throttle" => new DataChannelThrottled(
                        subscriptionId,
                        node.Body,
                        interpreter,
                        parent,
                        closure,
                        this,
                        number.Value
                    ),
                    _ => throw new Exception("Expected, latest, throttle or queue as timing method for pipe")
                };
                
                DataChannel.OnData += subscription.OnTargetStatementCallback;

                OnStatementSubscriptions[subscriptionId] = subscription;
            }, parent);
            
        }
        else
        {
            var subscription = new DataChannelSubscription(
                subscriptionId,
                node.Body,
                interpreter,
                parent,
                closure,
                this
            );
            DataChannel.OnData += subscription.OnTargetStatementCallback;

            OnStatementSubscriptions[subscriptionId] = subscription;
        }
    }

    public void UnsubscribeOnStatement(Guid id)
    {
        var subscription = OnStatementSubscriptions[id];
        DataChannel.OnData -= subscription.OnTargetStatementCallback;
        // todo: call when everyone has unsubscribed
        // subscription.OnUnsubscribed();
    }
    
    public override List<(RapidsVariable, RapidsVariable)>? GetIterable() => null;
}

public class DataChannelSubscription(
    Guid id,
    StatementsNode body,
    RapidsInterpreter interpreter,
    CodeBlockRunWork? parent,
    InterpreterContext closure,
    RapidsDataChannelVariable dataChannel
)
{
    protected readonly RapidsInterpreter Interpreter = interpreter;
    public virtual void OnTargetStatementCallback(RapidsVariable rv)
    {
        Interpreter.EnqueueAction(() => 
        {
            var closureInstance = new InterpreterContext(closure);
            if (dataChannel.DataVariableName is not null)
            {
                closureInstance.AddVariable(dataChannel.DataVariableName, new VariableHolder(rv, true));
            }

            var block = Interpreter.StartNewBlock(body, BlockType.SourceCallback, parent, closureInstance);

            block.Scope.Source = dataChannel;
            block.Scope.SourceSubscriptionId = id;
        });
    }

    public virtual void OnUnsubscribed() { }
}

public class DataChannelThrottled(
    Guid id,
    StatementsNode body,
    RapidsInterpreter interpreter,
    CodeBlockRunWork? parent,
    InterpreterContext closure,
    RapidsDataChannelVariable dataChannel,
    double seconds
) : DataChannelSubscription(id, body, interpreter, parent, closure, dataChannel)
{
    private DateTime? _lastFired = null;
    public override void OnTargetStatementCallback(RapidsVariable rv)
    {
        if (_lastFired is null)
        {
            _lastFired = DateTime.Now;
            base.OnTargetStatementCallback(rv);
            return;
        }
            
        if (DateTime.Now < _lastFired.Value.AddSeconds(seconds)) return;
            
        _lastFired = DateTime.Now;
        base.OnTargetStatementCallback(rv);
    }
}

public class DataChannelQueued(
    Guid id,
    StatementsNode body,
    RapidsInterpreter interpreter,
    CodeBlockRunWork? parent,
    InterpreterContext closure,
    RapidsDataChannelVariable dataChannel,
    double seconds
) : DataChannelSubscription(id, body, interpreter, parent, closure, dataChannel)
{
    private DateTime? _lastFired;
    private readonly ConcurrentQueue<RapidsVariable> _queuedData = [];
    private bool _tickRunning;
    private readonly Lock _lock = new();
    
    public override void OnTargetStatementCallback(RapidsVariable rv)
    {
        lock (_lock)
        {
            if (_lastFired is null)
            {
                _lastFired = DateTime.Now;
                base.OnTargetStatementCallback(rv);
                return;
            }
            
            _queuedData.Enqueue(rv);
            
            if (_tickRunning) return;
            
            Task.Run(Tick);
            _tickRunning = true;
        }
    }

    private async Task Tick()
    {
        while (true)
        {
            TimeSpan delay;
            if (_lastFired is null)
            {
                delay = TimeSpan.FromSeconds(seconds);
            }
            else
            {
                delay = (_lastFired.Value + TimeSpan.FromSeconds(seconds)) - DateTime.Now;
            }
            await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay);

            lock (_lock)
            {
                if (!_tickRunning)
                {
                    return;
                }
                
                if (_queuedData.IsEmpty)
                {
                    _lastFired = null;
                    _tickRunning = false;
                    return;
                }
                _lastFired = DateTime.Now;
                
                if (_queuedData.TryDequeue(out var rv))
                {
                    base.OnTargetStatementCallback(rv);
                }
            }
        }
    }
    
    public override void OnUnsubscribed()
    {
        lock (_lock)
        {
            _tickRunning = false;
        }
    }
}

public class DataChannelLatest(
    Guid id,
    StatementsNode body,
    RapidsInterpreter interpreter,
    CodeBlockRunWork? parent,
    InterpreterContext closure,
    RapidsDataChannelVariable dataChannel,
    double seconds
) : DataChannelSubscription(id, body, interpreter, parent, closure, dataChannel)
{
    private DateTime? _lastFired;
    private RapidsVariable? _latest;
    private bool _enqueueRunning;
    private readonly Lock _lock = new();
    
    public override void OnTargetStatementCallback(RapidsVariable rv)
    {
        lock (_lock)
        {
            if (_lastFired is null)
            {
                _lastFired = DateTime.Now;
                base.OnTargetStatementCallback(rv);
                return;
            }
            
            _latest = rv;

            if (_enqueueRunning) return;

            _enqueueRunning = true;
            Task.Run(Enqueue);
        }
    }

    private async Task Enqueue()
    {
        TimeSpan delay;
        if (_lastFired is null)
        {
            delay = TimeSpan.FromSeconds(seconds);
        }
        else
        {
            delay = _lastFired.Value + TimeSpan.FromSeconds(seconds) - DateTime.Now;
        }
        await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay);
        lock (_lock)
        {
            if (!_enqueueRunning)
            {
                return;
            }

            _lastFired = DateTime.Now;
            base.OnTargetStatementCallback(_latest!);
            _enqueueRunning = false;
        }
    }

    public override void OnUnsubscribed()
    {
        lock (_lock)
        {
            _enqueueRunning = false;
        }
    }
}