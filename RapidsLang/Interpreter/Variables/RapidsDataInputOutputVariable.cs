using RapidsLang.Extensions.Pipes;

namespace RapidsLang.Interpreter.Variables;

public class RapidsDataInputOutputVariable : RapidsVariable
{
    private DataInputOutput DataInputOutput { get; }
    private readonly RapidsFunction _sendDataFunction;

    public RapidsDataInputOutputVariable(DataInputOutput dataInputOutput)
    {
        DataInputOutput = dataInputOutput;
        _sendDataFunction = new RapidsNativeFunction(SendData);
    }

    public override RapidsVariable? GetResult(RapidsOperator op, RapidsVariable other)
    {
        if (op == RapidsOperator.Equality && other is RapidsDataInputOutputVariable pipe)
        {
            return new RapidsBooleanVariable(DataInputOutput.Equals(pipe.DataInputOutput));
        }
        return null;
    }

    public override string VariableTypeName => "[object pipe]";
    public override bool Truthy => true;
    public override RapidsVariable? GetMember(string memberName)
    {
        if (memberName == "writable")
        {
            return new RapidsBooleanVariable(DataInputOutput.Writable);
        }

        if (memberName == "readable")
        {
            return new RapidsBooleanVariable(DataInputOutput.Readable);
        }
        
        if (memberName == "send")
        {
            return new RapidsFunctionReferenceVariable(_sendDataFunction);
        }
        return null;
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsDataInputOutputVariable(DataInputOutput);
    }
    
    public void SendData(RapidsInterpreter interpreter)
    {
        var data = interpreter.Context.FunctionCallStack.Pop();
        DataInputOutput?.SendData(data);
    }
}