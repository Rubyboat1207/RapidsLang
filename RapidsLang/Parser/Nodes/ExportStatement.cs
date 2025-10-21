using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record ExportStatement(
    Token BaseToken,
    Exportable ExportNode,
    int DebugLevel
) : StatementNode(BaseToken, DebugLevel);

public abstract record Exportable(Token BaseToken) : Node(BaseToken);

public record FunctionExportable(
    Token BaseToken,
    FunctionNode FunctionNode
) : Exportable(BaseToken);

public record TargetOrSourceExportable(
    Token BaseToken,
    bool IsTarget,
    Token Name,
    TypeNode? Type=null
) : Exportable(BaseToken)
{
    public bool IsSource => !IsTarget;
}
public record ExpressionExportable(
    Token BaseToken,
    ExpressionNode Expression
) : Exportable(BaseToken);