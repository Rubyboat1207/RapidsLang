using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record FunctionDeclarationNode(
   IdentifierNode Name, 
   FunctionNode Function,
   int DebugLevel
) : StatementNode(Name.Token, DebugLevel)
{
   public override int EndIndex => Function.EndIndex;
   public override IEnumerable<Node> GetChildren() => [Function];
}