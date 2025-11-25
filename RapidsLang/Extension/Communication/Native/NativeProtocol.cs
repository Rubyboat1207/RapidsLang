using System.Runtime.Loader;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using RapidsLang.Analyzer;
using RapidsLang.Extensions;
using RapidsLang.Extensions.Channel;
using RapidsLang.Extensions.Communication;
using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;
using RapidsLang.NativeExtension;
using RapidsLang.NativeExtension.Variables;
using RapidsLang.Parser;
using Identifier = RapidsLang.Extension.Channel.Identifier;
using Module = RapidsLang.Interpreter.Module;
using NativeIdentifier = RapidsLang.NativeExtension.Identifier;


namespace RapidsLang.Extension.Communication.Native;

public class NativeProtocol : CommunicationProtocol, INativeProtocol
{
    [JsonPropertyName("dll_path")] [UsedImplicitly] public string? DllPath { get; init; } = null;
    private IExtensionEntrypoint? NativeEntrypoint = null;
    
    private readonly Dictionary<Identifier, Action<RapidsVariable?>> _inputDictionary = [];
    private readonly Stack<(Identifier, RapidsVariable?)> _dataOutputs = [];
    public event Action<Identifier>? OutputEnabled = null;

    private readonly Dictionary<Action<NativeIdentifier>, Action<Identifier>> _externalDisabled = [];
    private readonly Dictionary<Action<NativeIdentifier>, Action<Identifier>> _externalEnabled = [];
    event Action<NativeIdentifier> INativeProtocol.OutputDisabled
    {
        add
        {
            void Mapped(Identifier ident) => value(ident.ToExternalIdentifier());
            _externalDisabled[value] = Mapped;
            OutputDisabled += Mapped;
        }
        remove
        {
            OutputDisabled += _externalDisabled[value];
            _externalDisabled.Remove(value);
        }
    }

    public void WriteToOutput(NativeIdentifier identifier, ExtensionVariable variable)
    {
        WriteToOutput(identifier.ToRapidsIdentifier(), variable.ToRapidsVariable());
    }

    public void RegisterInput(NativeIdentifier identifier, Action<ExtensionVariable?> variable)
    {
        RegisterInput(identifier.ToRapidsIdentifier(), (v) => variable.Invoke(v?.ToExtensionVariable()));
    }

    event Action<NativeIdentifier> INativeProtocol.OutputEnabled
    {
        add
        {
            void Mapped(Identifier ident) => value(ident.ToExternalIdentifier());
            _externalEnabled[value] = Mapped;
            OutputEnabled += Mapped;
        }
        remove
        {
            OutputEnabled += _externalEnabled[value];
            _externalEnabled.Remove(value);
        }
    }
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
                if (!EventListeners.TryGetValue(dataOutput.Item1, out var listenerGroup))
                {
                    continue;
                }

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

    public override void Init(RapidsInterpreter interpreter, ExtensionData? extension)
    {
        base.Init(interpreter, extension);

        if (NativeEntrypoint is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(DllPath))
        {
            return;
        }

        if (!File.Exists(DllPath))
        {
            return;
        }

        var loadContext = new ExtensionLoadContext(DllPath, extension?.DirectoryPath);

        var assembly = loadContext.LoadFromAssemblyPath(DllPath);
        
        var entryPointType = assembly.GetTypes().FirstOrDefault(t => 
            typeof(IExtensionEntrypoint).IsAssignableFrom(t) 
            && t is { IsClass: true, IsAbstract: false }
        );

        if (entryPointType is null || Activator.CreateInstance(entryPointType) is not IExtensionEntrypoint instance)
        {
            loadContext.Unload();
            return;
        }

        NativeEntrypoint = instance;

        NativeEntrypoint.Init(this);
    }

    public Dictionary<string, ModuleExport> GetExports(Module module)
    {
        if (NativeEntrypoint is not null)
        {
            return NativeEntrypoint.Exports?.Select(e => 
                (
                    e.Key,
                    new ModuleExport(
                        e.Value.ExtensionVariable.ToRapidsVariableWithModule(module, this),
                        RapidsStaticAnalysis.ComputeFromTypeNode(RapidsParser.ParseTypeNode(e.Value.TypeString))
                    )
                )
            ).ToDictionary() ?? [];
        }

        return [];
    }
}