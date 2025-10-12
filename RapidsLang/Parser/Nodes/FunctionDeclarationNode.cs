using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionDeclarationNode(
   Token Name, 
   FunctionNode Function,
   int DebugLevel
) : StatementNode(Name, DebugLevel);