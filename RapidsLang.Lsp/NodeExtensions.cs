using RapidsLang.Parser.Nodes;

namespace RapidsLang.LanguageServer;

public static class NodeExtensions
{
    /// <summary>
    /// Traverses up the AST from the current node to find the first
    /// ancestor of the specified type T.
    /// </summary>
    /// <typeparam name="T">The type of ancestor node to find (e.g., StatementsNode)</typeparam>
    /// <param name="startingNode">The node to start searching from</param>
    /// <param name="parentMap">The pre-computed parent map</param>
    /// <returns>The first ancestor of type T, or null if none is found</returns>
    public static T? GetAncestor<T>(this Node startingNode, Dictionary<Node, Node> parentMap)
        where T : Node
    {
        Node? currentNode = startingNode;

        // Loop while we can find a parent for the current node
        while (parentMap.TryGetValue(currentNode, out var parent))
        {
            if (parent is T target)
            {
                return target;
            }
            currentNode = parent;
        }

        return null;
    }
}