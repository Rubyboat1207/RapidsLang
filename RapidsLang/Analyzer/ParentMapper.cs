using RapidsLang.Parser.Nodes;

namespace RapidsLang.Analyzer;

public static class ParentMapper
{
    public static Dictionary<Node, Node> BuildParentMap(Node rootNode)
    {
        var parentMap = new Dictionary<Node, Node>();
        PopulateMapRecursive(rootNode, parentMap);
        return parentMap;
    }

    private static void PopulateMapRecursive(Node node, Dictionary<Node, Node> parentMap)
    {
        foreach (var child in node.GetChildren())
        {
            parentMap[child] = node;
            PopulateMapRecursive(child, parentMap);
        }
    }
}