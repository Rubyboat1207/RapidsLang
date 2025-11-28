using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter.Lib.Modules;

public class MathModule : Module
{
    private static void Round(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();
        
        var a = utils.LatestNumber();

        if (a == null)
        {
            return;
        }

        utils.Return(Math.Round(a.Value));
    }

    private static readonly RapidsType RoundType = new RapidsFunctionType(
        [new("a", RapidsPrimitiveType.Number)],
        RapidsPrimitiveType.Number
    );

    private static void Floor(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();
        
        var a = utils.LatestNumber();

        if (a == null)
        {
            return;
        }

        utils.Return(Math.Floor(a.Value));
    }

    private static readonly RapidsType FloorType = new RapidsFunctionType(
        [new("a", RapidsPrimitiveType.Number)],
        RapidsPrimitiveType.Number
    );

    private static void Ceil(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();
        
        var a = utils.LatestNumber();

        if (a == null)
        {
            return;
        }

        utils.Return(Math.Ceiling(a.Value));
    }

    private static readonly RapidsType CeilType = new RapidsFunctionType(
        [new("a", RapidsPrimitiveType.Number)],
        RapidsPrimitiveType.Number
    );

    private static void Map(RapidsInterpreter interpreter)
    {
        using var utils = interpreter.GetNativeUtil().GuaranteeReturn();

        var outEnd = utils.LatestNumber();
        var outStart = utils.LatestNumber();

        var inEnd = utils.LatestNumber();
        var inStart = utils.LatestNumber();

        var input = utils.LatestNumber();

        if (outEnd == null || outStart == null || inEnd == null || inStart == null || input == null)
        {
            return;
        }
        
        utils.Return(outStart.Value + ((outEnd.Value - outStart.Value) / (inEnd.Value - inStart.Value)) * (input.Value - inStart.Value));
    }
    
    private static readonly RapidsType MapType = new RapidsFunctionType(
        [
            new("input", RapidsPrimitiveType.Number),
            new("inStart", RapidsPrimitiveType.Number),
            new("inEnd", RapidsPrimitiveType.Number),
            new("outStart", RapidsPrimitiveType.Number),
            new("outEnd", RapidsPrimitiveType.Number),
        ],
        RapidsPrimitiveType.Number
    );

    public override ModuleExports Exports { get; } = new(new()
    {
        {"round", new(RapidsFunctionReferenceVariable.OfNative(Round, RoundType), RoundType)},
        {"map", new(RapidsFunctionReferenceVariable.OfNative(Map, MapType), MapType)},
        {"floor", new(RapidsFunctionReferenceVariable.OfNative(Floor, FloorType), FloorType)},
        {"ceil", new(RapidsFunctionReferenceVariable.OfNative(Ceil, CeilType), CeilType)},
    });
}