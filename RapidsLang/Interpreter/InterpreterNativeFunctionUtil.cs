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
    
    public RapidsFunctionReferenceVariable? LatestFunction()
    {
        if (interpreter.Context.FunctionCallStack.TryPop(out var result) && result is RapidsFunctionReferenceVariable numberVariable)
        {
            return numberVariable;
        }

        return null;
    }

    public RapidsStringVariable? LatestString()
    {
        if (interpreter.Context.FunctionCallStack.TryPop(out var result) && result is RapidsStringVariable stringVariable)
        {
            return stringVariable;
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
    
    public void Return(double? numberRet)
    {
        if (numberRet is null)
        {
            _returnValue = new RapidsNullVariable();
            return;
        }
        _returnValue = new RapidsNumberVariable(numberRet.Value);
    }

    public void Return(string? strRet)
    {
        if (strRet is null)
        {
            _returnValue = new RapidsNullVariable();
            return;
        }
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