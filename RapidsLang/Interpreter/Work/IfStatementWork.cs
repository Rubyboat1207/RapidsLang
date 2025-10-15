using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter.Work;

public record IfStatementWork(RapidsInterpreter Interpreter, CodeBlockRunWork Parent, IfNode IfNode) : InterpreterWork(Interpreter, Parent)
{
    private int ElseProgress { get; set; }
    private bool Done { get; set; }

    public override void Execute()
    {
        if (ElseProgress == 0)
        {
            EvaluateExpression(IfNode.Condition, OnIfStatementExpressionEvaluated, Parent!);
        }
        else
        {
            var elseNode = IfNode.ElseNodes[ElseProgress - 1];

            if (elseNode.Condition is not null)
            {
                EvaluateExpression(elseNode.Condition, OnIfStatementExpressionEvaluated, Parent!);
            }
            else
            {
                Interpreter.StartNewBlock(elseNode.Block, BlockType.Statement, Parent);
            }
        }
    }

    private void OnIfStatementExpressionEvaluated(RapidsVariable res)
    {
        if (res.Truthy)
        {
            if (ElseProgress == 0)
            {
                Interpreter.StartNewBlock(IfNode.Block, BlockType.Statement, Parent);
            }
            else
            {
                Interpreter.StartNewBlock(IfNode.ElseNodes[ElseProgress - 1].Block, BlockType.Statement, Parent);
            }
            Done = true;
        }
        else if (ElseProgress < IfNode.ElseNodes.Count)
        {
            ElseProgress += 1;
        }
        else
        {
            Done = true;
        }
    }

    public override bool IsDone() => Done;

    public IfNode IfNode { get; } = IfNode;
    public override Node ActiveNode => IfNode;
}