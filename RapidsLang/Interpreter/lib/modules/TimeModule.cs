using System.Diagnostics;
using RapidsLang.Extensions.Channel;
using RapidsLang.Extensions.Communication;
using RapidsLang.Extensions.Communication.Native;
using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Lib.Modules;

public class TimeModule : Module
{
    private NativeProtocol? _protocol;
    public override CommunicationProtocol? Protocol => _protocol;
    private static readonly Dictionary<Identifier, ClockStorage> Clocks = new();

    private class ClockStorage(double interval)
    {
        public double Interval { get; } = interval;
        public bool Enabled { get; set; }
        public Stopwatch Stopwatch = new();
    }

    private static void Sleep(RapidsInterpreter interpreter)
    {
        if (interpreter.Context.FunctionCallStack.TryPop(out var msVariable) && msVariable is RapidsNumberVariable number)
        {
            Thread.Sleep((int) number.Value);
        }
    }

    private static void Clock(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil().GuaranteeReturn();

        var seconds = util.LatestNumber();

        if (seconds is null)
        {
            throw new Exception("Called clock function without a seconds value");
        }

        var ident = new Identifier("time", "clock:" + seconds.Value);
        Clocks[ident] = new ClockStorage(seconds.Value);
        
        util.Return(new RapidsDataChannelVariable(
            new(
                interpreter.NativeProtocol,
                ident,
                true,
                false,
                "delta"
            ),
            interpreter.Context.ModuleRegistry.GetModule("time")
        ));
    }
    
    protected override ModuleExports Exports { get; } = new ModuleExports(new()
    {
        {"sleep", RapidsFunctionReferenceVariable.ofNative(Sleep)},
        {"clock", RapidsFunctionReferenceVariable.ofNative(Clock)}
    });


    public override void Import(RapidsInterpreter interpreter, List<ImportNode>? importNodes)
    {
        _protocol = interpreter.NativeProtocol;
        _protocol.OutputEnabled += (ident) =>
        {
            if (Clocks.ContainsKey(ident))
            {
                Task.Run(() => StartClock(ident));
            }
        };
        _protocol.OutputDisabled += (ident) =>
        {
            if (Clocks.TryGetValue(ident, out var clock))
            {
                clock.Enabled = false;
            }
        };
        
        base.Import(interpreter, importNodes);

    }

    private async Task StartClock(Identifier ident)
    {
        var clock = Clocks[ident];
        clock.Enabled = true;
        while (true)
        {
            clock.Stopwatch.Restart();
            await Task.Delay(TimeSpan.FromSeconds(clock.Interval));

            if (clock.Enabled)
            {
                _protocol?.WriteToOutput(ident, new RapidsNumberVariable(clock.Stopwatch.ElapsedMilliseconds / 1000d));
            }
            else
            {
                return;
            }
        }
    }
}