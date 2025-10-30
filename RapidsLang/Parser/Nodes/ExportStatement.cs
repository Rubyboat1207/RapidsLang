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

public record ChannelExportable(
    Token BaseToken,
    DefineTargetOrSourceNode TargetOrSourceNode
) : Exportable(BaseToken);
public record ExpressionExportable(
    Token BaseToken,
    ExpressionNode Expression
) : Exportable(BaseToken);