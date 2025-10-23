using RapidsLang.Extensions;
using RapidsLang.Extensions.Pipes;
using RapidsLang.Interpreter.Work;

namespace RapidsLang.Interpreter.Variables;

public class RapidsDataInputOutputVariable : RapidsVariable
{
    private DataInputOutput DataInputOutput { get; }
    private readonly RapidsFunction _sendDataFunction;
    private readonly ExtensionModule _module;

    public RapidsDataInputOutputVariable(DataInputOutput dataInputOutput, ExtensionModule module)
    {
        DataInputOutput = dataInputOutput;
        _sendDataFunction = new RapidsNativeFunction(SendData);
        _module = module;
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

        if (memberName == "on_data")
        {
            return RapidsFunctionReferenceVariable.ofNative(OnData);
        }
        return null;
    }

    public override RapidsVariable ShallowCopy()
    {
        return new RapidsDataInputOutputVariable(DataInputOutput, _module);
    }

    private void SendData(RapidsInterpreter interpreter)
    {
        var data = interpreter.Context.FunctionCallStack.Pop();
        DataInputOutput?.SendData(data);
    }

    public void SetReadable()
    {
        DataInputOutput.Readable = true;
    }

    public void SetWritable()
    {
        DataInputOutput.Writable = true;
    }

    private void OnData(RapidsInterpreter interpreter, CodeBlockRunWork? parentCodeBlock)
    {
        interpreter.Context.GetRoot().ModuleRegistry.MarkModuleAsTicking(_module, interpreter);
        if (interpreter.Context.FunctionCallStack.TryPop(out var funcVar) && funcVar is RapidsFunctionReferenceVariable func)
        {
            DataInputOutput.OnData += rv =>
            {
                interpreter.Context.FunctionCallStack.Push(rv);
                func.Function.EnqueueExecution(interpreter, parentCodeBlock);
            };
        }
    }
}