using RapidsLang.Utils;

namespace RapidsLang.Interpreter.Variables;

public class RapidsAudioVariable(byte[] wavData) : RapidsVariable
{
    public override string VariableTypeName => "audio";
    
    public override bool Truthy => wavData.Length > 44; 
    
    public byte[] Data { get; } = wavData;

    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (op is RapidsOperator.Add && other is RapidsAudioVariable otherAudio)
        {
            // We treat 'this' variable as the Master Format.
            // If the other file differs, we convert IT to match US.
            var cleanOtherData = WavUtils.MatchFormat(Data, otherAudio.Data);

            // Now combine using the safe Logic from the previous step
            var combined = WavUtils.CombineWavs(Data, cleanOtherData);
        
            return new RapidsAudioVariable(combined);
        }

        if (op is RapidsOperator.Equality or RapidsOperator.Inequal)
        {
            bool isEqual = other is RapidsAudioVariable o && Data.SequenceEqual(o.Data);
            
            if (op is RapidsOperator.Inequal) isEqual = !isEqual;
            return new RapidsBooleanVariable(isEqual);
        }

        return null;
    }

    public override RapidsVariable? GetMember(string memberName)
    {
        if (memberName is "duration")
        {
            return new RapidsNumberVariable(WavUtils.GetDurationSeconds(Data));
        }
        
        if (memberName == "size")
        {
             return new RapidsNumberVariable(Data.Length);
        }
        
        if (memberName == "bytes")
        {
            return new RapidsListVariable([..Data.Select((b) => new RapidsNumberVariable(b))]);
        }

        return null;
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsAudioVariable((byte[])Data.Clone());
    }
    
    // this list will not be small.
    public override List<(RapidsVariable, RapidsVariable)>? GetIterable() => Data
        .Select(((RapidsVariable, RapidsVariable) (b, i) => (new RapidsNumberVariable(i), new RapidsNumberVariable(b))))
        .ToList();
}