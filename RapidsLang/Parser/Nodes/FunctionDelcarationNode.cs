using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionDelcarationNode(
   Token Name, 
   FunctionNode Function,
   FunctionNode? DebugFunction,
   int DebugLevel
) : StatementNode(DebugLevel);