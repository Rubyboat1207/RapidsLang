using System.Diagnostics;
using RapidsLang.Extension.Channel;
using RapidsLang.Extension.Communication;
using RapidsLang.Extension.Communication.Native;
using RapidsLang.Extensions.Channel;
using RapidsLang.Extensions.Communication;
using RapidsLang.Interpreter.Variables;
using RapidsLang.Interpreter.Work;
using RapidsLang.Parser.Nodes;
using RapidsLang.Parser.Types;

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

    private static readonly RapidsType SleepType = new RapidsFunctionType(
        [new("milliseconds", RapidsPrimitiveType.Number)],
        null
    );

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

    private static readonly RapidsType ClockType = new RapidsFunctionType(
        [new("seconds", RapidsPrimitiveType.Number)],
        new RapidsChannelSourceType(RapidsPrimitiveType.Number, "delta")
    );

    public override ModuleExports Exports { get; } = new ModuleExports(new()
    {
        {"sleep", new(RapidsFunctionReferenceVariable.OfNative(Sleep, SleepType), SleepType)},
        {"clock", new(RapidsFunctionReferenceVariable.OfNative(Clock, ClockType), ClockType)},
        {"slide", new(RapidsFunctionReferenceVariable.OfNative(Slide, SlideType), SlideType)}
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

    private static void Slide(RapidsInterpreter interpreter)
    {
        using var util = interpreter.GetNativeUtil();
        
        var callbackRef = util.LatestFunction();
        var styleString = util.LatestString();
        var durationSeconds = util.LatestNumber();
        var endVal = util.LatestNumber();
        var startVal = util.LatestNumber();

        if (callbackRef is null || durationSeconds is null || endVal is null || startVal is null)
        {
             return;
        }

        var easing = ParseEasing(styleString?.Value);
        
        var start = startVal.Value;
        var end = endVal.Value;
        var duration = durationSeconds.Value;

        Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            while (stopwatch.Elapsed.TotalSeconds < duration)
            {
                double t = stopwatch.Elapsed.TotalSeconds / duration;
                
                double easedT = ApplyEasing(t, easing);
                
                double currentValue = start + (end - start) * easedT;
                
                interpreter.EnqueueAction(() => 
                {
                    interpreter.Context.FunctionCallStack.Push(new RapidsNumberVariable(currentValue));
                    
                    callbackRef.Function.EnqueueExecution(interpreter, null); 
                });

                await Task.Delay(16); 
            }

            interpreter.EnqueueAction(() =>
            {
                interpreter.Context.FunctionCallStack.Push(new RapidsNumberVariable(end));
                callbackRef.Function.EnqueueExecution(interpreter, null);
            });
        });
        
    }
    
    private static readonly RapidsType SlideType = new RapidsFunctionType(
        [
            new("startVal", RapidsPrimitiveType.Number),
            new("endVal", RapidsPrimitiveType.Number),
            new("duration", RapidsPrimitiveType.Number),
            new("interpolationFunction", RapidsPrimitiveType.String),
            new("fn", new RapidsFunctionType([new("delta", RapidsPrimitiveType.Number)], null))
        ],
        null 
    );
    
    public enum EasingStyle
    {
        Linear,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad
    }
    
    private static EasingStyle ParseEasing(string? input)
    {
        return input?.ToLower() switch
        {
            "easein" => EasingStyle.EaseInQuad,
            "easeout" => EasingStyle.EaseOutQuad,
            "easeinout" => EasingStyle.EaseInOutQuad,
            _ => EasingStyle.Linear
        };
    }

    private static double ApplyEasing(double t, EasingStyle style)
    {
        // Clamp t between 0 and 1 just in case
        if (t < 0) return 0;
        if (t > 1) return 1;

        return style switch
        {
            EasingStyle.EaseInQuad => t * t,
            EasingStyle.EaseOutQuad => t * (2 - t),
            EasingStyle.EaseInOutQuad => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t,
            _ => t // Linear
        };
    }
}