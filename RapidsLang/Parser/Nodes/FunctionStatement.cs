using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionStatement(
   Token Name, 
   FunctionDeclarationNode Function,
   FunctionDeclarationNode? DebugFunction,
   int DebugLevel
) : StatementNode(DebugLevel);