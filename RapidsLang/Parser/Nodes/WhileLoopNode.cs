using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record WhileLoopNode(
    Token While,
    ExpressionNode Condition,
    StatementsNode Block,
    int DebugLevel
) : StatementNode(While, DebugLevel)
{
    public override int EndIndex => Block.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Condition, Block];
}