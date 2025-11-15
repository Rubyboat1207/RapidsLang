using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionDeclarationNode(
   Token Name, 
   FunctionNode Function,
   int DebugLevel
) : StatementNode(Name, DebugLevel)
{
   public override int EndIndex => Function.EndIndex;
   public override IEnumerable<Node> GetChildren() => [Function];
}