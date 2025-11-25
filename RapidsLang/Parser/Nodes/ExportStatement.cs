using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ExportStatement(
    Token BaseToken,
    Exportable ExportNode,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel)
{
    public override int EndIndex => ExportNode.EndIndex;
    public override IEnumerable<Node> GetChildren() => [ExportNode];
}

public abstract record Exportable(Token BaseToken) : Node(BaseToken);

public record FunctionExportable(
    IdentifierNode Name,
    FunctionNode FunctionNode
) : Exportable(Name.BaseToken)
{
    public override int EndIndex => FunctionNode.EndIndex;
    public override IEnumerable<Node> GetChildren() => [FunctionNode];
}

public record ChannelExportable(
    Token BaseToken,
    DefineTargetOrSourceNode TargetOrSourceNode
) : Exportable(BaseToken)
{
    public override int EndIndex => TargetOrSourceNode.EndIndex;
    public override IEnumerable<Node> GetChildren() => [TargetOrSourceNode];
}

public record ExpressionExportable(
    Token BaseToken,
    ExpressionNode Expression
) : Exportable(BaseToken)
{
    public override int EndIndex => Expression.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Expression];
}

public record ExternalExportable(
    Token BaseToken, // extern token
    IdentifierNode Name,
    TypeNode Type
) : Exportable(BaseToken)
{
    public override int EndIndex => Type.EndIndex;
    public override IEnumerable<Node> GetChildren() => [Name, Type];
}