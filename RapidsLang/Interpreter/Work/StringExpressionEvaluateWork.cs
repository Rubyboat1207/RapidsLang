using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record StringExpressionEvaluateWork(StringNode Expression, Action<RapidsVariable> Callback, RapidsInterpreter Interpreter)
    : ExpressionEvaluateWork<StringNode>(Expression, Callback, Interpreter)
{
    private string _str = "";
    private int partIndex;
    private bool _done = false;
    public override void Execute()
    {
        for (; partIndex < Expression.Parts.Count; partIndex++)
        {
            var part = Expression.Parts[partIndex];
            switch (part)
            {
                case LiteralStringPart lit:
                    _str += lit.Value.Value;
                    break;
                case TemplateStringPart template:
                    EvaluateExpression(template.Value, val =>
                    {
                        _str += Utils.StringifyVariable(val);
                    });
                    partIndex++;
                    return;
            }
        }
        _done = true;
        Callback.Invoke(new RapidsStringVariable(_str));
    }

    public override bool IsDone()
    {
        return _done;
    }
}