using RapidsLang.Analyzer.Types;
using RapidsLang.Extension.Channel;
using RapidsLang.Extension.Communication;
using RapidsLang.Extension.Communication.Native;
using RapidsLang.Extensions.Channel;
using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Lib.Modules;

public class QueueModule : Module
{
    private NativeProtocol? _protocol;
    public override CommunicationProtocol? Protocol => _protocol;

    public override ModuleExports Exports => new(new()
    {
        { "initQueue", new(RapidsFunctionReferenceVariable.OfNative(InitQueue), InitQueueType)}
    });
    
    private static readonly RapidsType EnqueueType = new RapidsFunctionType(
        [new RapidsFunctionParamType("data", RapidsAnyType.Instance)],
        null
    );

    private static readonly RapidsType DequeueType = new RapidsFunctionType(
        [],
        RapidsAnyType.Instance
    );

    private static readonly RapidsType DequeueAsSourceType = new RapidsFunctionType(
        [],
        new RapidsChannelSourceType(RapidsAnyType.Instance, "value")
    );
    
    private static readonly RapidsType InitQueueType = new RapidsFunctionType(
        [],
        new RapidsShapeType(new()
        {
            {"enqueue", EnqueueType},
            {"dequeue", DequeueType},
            {"dequeueAsSource", DequeueAsSourceType}
        })
    );
    
    private void InitQueue(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil().GuaranteeReturn();

        var queue = new DataQueue(this);
        
        util.Return(new RapidsObjectVariable(new()
        {
            {"enqueue", RapidsFunctionReferenceVariable.OfNative(queue.Enqueue, EnqueueType)},
            {"dequeue", RapidsFunctionReferenceVariable.OfNative(queue.Dequeue, DequeueType)},
            {"dequeueAsSource", RapidsFunctionReferenceVariable.OfNative(queue.DequeueAsSource, DequeueAsSourceType)},
        }));
    }

    public override void Import(RapidsInterpreter interpreter, List<ImportNode>? importNodes)
    {
        _protocol = interpreter.NativeProtocol;
        base.Import(interpreter, importNodes);
    }
    
    private class DataQueue(QueueModule module)
    {
        private readonly Queue<RapidsVariable> _items = [];
        private DataChannel? _channel;
        public void Enqueue(RapidsInterpreter interpreter)
        {
            if (_channel is not null)
            {
                module._protocol?.WriteToOutput(_channel.SourceIdentifier, interpreter.Context.FunctionCallStack.Pop());
                return;
            }
            _items.Enqueue(interpreter.Context.FunctionCallStack.Pop());
        }

        public void Dequeue(RapidsInterpreter interpreter)
        {
            using var util = interpreter.GetNativeUtil().GuaranteeReturn();
        
            util.Return(interpreter.Context.FunctionCallStack.Pop());
        }

        public void DequeueAsSource(RapidsInterpreter interpreter)
        {
            using var util = interpreter.GetNativeUtil().GuaranteeReturn();

            _channel ??= new DataChannel(
                interpreter.NativeProtocol,
                new Identifier("queue", Guid.CreateVersion7().ToString()),
                true,
                false,
                "value"
            );
            // interpreter.NativeProtocol.RegisterInput(_channel.SourceIdentifier);

            interpreter.NativeProtocol.OutputEnabled += (ident) =>
            {
                if (ident != _channel.SourceIdentifier) return;
            
                foreach (var item in _items)
                {
                    _channel.SendData(item);
                }
                _items.Clear();
            };
        
            interpreter.NativeProtocol.OutputDisabled += (ident) =>
            {
                if (ident == _channel.SourceIdentifier)
                {
                    _items.Clear();
                    _channel = null;
                }
            };
        
            util.Return(new RapidsDataChannelVariable(_channel, module));
        }
    }
}

