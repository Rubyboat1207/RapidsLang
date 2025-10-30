using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter;

public class InterpreterNativeFunctionUtil(RapidsInterpreter interpreter) : IDisposable
{
    private RapidsVariable? _returnValue;
    private bool _guaranteeReturn;

    public InterpreterNativeFunctionUtil GuaranteeReturn()
    {
        _guaranteeReturn = true;
        return this;
    }
    
    public RapidsNumberVariable? LatestNumber()
    {
        if (interpreter.Context.FunctionCallStack.TryPop(out var result) && result is RapidsNumberVariable numberVariable)
        {
            return numberVariable;
        }

        return null;
    }

    public RapidsVariable? LatestVariable()
    {
        if (interpreter.Context.FunctionCallStack.TryPop(out var result))
        {
            return result;
        }

        return null;
    }

    public void Return(RapidsVariable? rapidsVariable = null)
    {
        _returnValue = rapidsVariable;
    }
    
    public void Return(double numberRet)
    {
        _returnValue = new RapidsNumberVariable(numberRet);
    }

    public void Return(string strRet)
    {
        _returnValue = new RapidsStringVariable(strRet);
    }

    public void Dispose()
    {
        if (_returnValue is not null)
        {
            interpreter.Context.FunctionCallStack.Push(_returnValue);
        }else if (_guaranteeReturn)
        {
            interpreter.Context.FunctionCallStack.Push(new RapidsNullVariable());
        }
    }
}