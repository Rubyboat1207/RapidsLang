namespace RapidsLang.Parser.Nodes;

public record LiteralMeasurementNode(LiteralNumberNode Quantity, IdentifierNode Unit) : ExpressionNode(Quantity.BaseToken)
{
    public override int EndIndex => Unit.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Quantity, Unit];
}