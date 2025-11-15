using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetOrSourceStatement(
    Token BaseToken,
    DefineTargetOrSourceNode TargetOrSourceNode,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => TargetOrSourceNode.EndIndex;
    public override IEnumerable<Node> GetChildren() => [TargetOrSourceNode];
}