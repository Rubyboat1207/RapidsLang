using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Interpreter;

public static class Utils
{
    public static string StringifyVariable(RapidsVariable v)
    {
        if (v is RapidsStringVariable rString)
        {
            return rString.Value;
        }

        if (v is RapidsBooleanVariable rBool)
        {
            return rBool.Value.ToString();
        }
        if (v is RapidsNumberVariable rNum)
        {
            return rNum.Value.ToString();
        }
        if (v is RapidsFunctionReferenceVariable)
        {
            return "[Function]";
        }
        if (v is RapidsObjectVariable)
        {
            return "[Object]";
        }
        if (v is RapidsListVariable list)
        {
            List<string> buf = [];
            foreach (var item in list.List)
            {
                buf.Add(StringifyVariable(item));
            }

            return "[" + string.Join(", ", buf) + "]";
        }
        if(v is RapidsNullVariable)
        {
            return "null";
        }

        return v.VariableTypeName;
    } 
}