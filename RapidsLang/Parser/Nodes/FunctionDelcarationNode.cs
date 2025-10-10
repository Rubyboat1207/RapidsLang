using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionDelcarationNode(
   Token Name, 
   FunctionNode Function,
   int DebugLevel
) : StatementNode(DebugLevel);