using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record NumericForLoop(
    Token For,
    IdentifierNode Index,
    Token Equal,
    ExpressionNode Start,
    Token To,
    ExpressionNode End,
    StatementsNode Body,
    Token? StepKeyword,
    ExpressionNode? StepExpr
) : StatementNode(For, 0)
{
    public override int EndIndex => Body.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Index, Start, End, Body];

    public bool IncludesEnd => To.Value != "to";
}