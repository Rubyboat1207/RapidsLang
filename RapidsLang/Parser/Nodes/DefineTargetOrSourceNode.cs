using RapidsLang.Lexer;

namespace RapidsLang.Parser.Nodes;

public record DefineTargetOrSourceNode(
    Token BaseToken,
    Token Name,
    bool IsTarget,
    Token? DataName,
    TypeNode? Type
) : Node(BaseToken)
{
    public override int EndIndex
    {
        get
        {
            if (Type is not null)
            {
                return Type.EndIndex;
            }

            return DataName?.EndIndex ?? Name.EndIndex;
        }
    }

    public override IEnumerable<Node> GetChildren()
    {
        if (Type is null)
        {
            return [];
        }

        return [Type];
    }
}