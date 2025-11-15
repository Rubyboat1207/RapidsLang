using System.Diagnostics;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;
using RapidsLang.Utils;

namespace RapidsLang.Parser;

public class RapidsParseResult(StatementsNode rootNode, List<Diagnostic>? diagnostics)
{
    public StatementsNode RootNode { get; } = rootNode;
    public List<Diagnostic> Diagnostics { get; } = diagnostics ?? [];

    public void PrintDiagnostics(string sourcePath, string code, RapidsPreprocMetaData metaData)
    {
        if (Diagnostics.Count == 0)
        {
            Console.WriteLine("No diagnostics found.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Found {Diagnostics.Count} error(s) in {sourcePath}:");
        Console.ResetColor();

        foreach (var diagnostic in Diagnostics)
        {
            var sourceIndex = RapidsPreproc.GetSourceIdx(diagnostic.Token.Index, metaData);

            var (lineNum, colNum) = RapidsPreproc.GetRowColFromIndex(sourceIndex, code);

            Console.WriteLine($"\n--- Error (line {lineNum}, col {colNum}) ---");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(diagnostic.Issue);
            Console.ResetColor();

            var lineStart = sourceIndex;
            while (lineStart > 0 && code[lineStart - 1] != '\n')
            {
                lineStart--;
            }

            var lineEnd = sourceIndex;
            // Go to the end of the line
            while (lineEnd < code.Length && code[lineEnd] != '\n' && code[lineEnd] != '\r')
            {
                lineEnd++;
            }

            var errorLine = code.Substring(lineStart, lineEnd - lineStart);
            Console.WriteLine(errorLine);

            // 5. Print the pointer (e.g., "   ^")
            int pointerColumn;

            // Check the new property
            if (diagnostic.AtEndOfLine)
            {
                // For "missing" tokens, point just *after* the
                // last non-whitespace character on the line.
                pointerColumn = errorLine.TrimEnd().Length;
            }
            else
            {
                // For errors at a specific token, use the column your function provided
                pointerColumn = colNum - 1; // 0-based index for the pointer
            }

            var pointer = new string(' ', pointerColumn) + "^";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(pointer);
            Console.ResetColor();
        }
    }

    public class Builder(ListStepper<Token> stepper, StatementsNode activeBlock)
    {
        private StatementsNode RootNode { get; } = activeBlock;
        private List<Diagnostic> Diagnostics { get; } = [];
        
        public void AddIssue(string issue)
        {
            if (stepper.AtEnd)
            {
                Diagnostics.Add(new Diagnostic(stepper.ActiveList.Last(), issue));
                return;
            }
            Diagnostics.Add(new Diagnostic(stepper.Cur, issue));
        }

        public void AddDiagnostics(List<Diagnostic> diagnostics)
        {
            Diagnostics.AddRange(diagnostics);
        }
        
        public void AddDiagnostic(Diagnostic diagnostic)
        {
            Diagnostics.Add(diagnostic);
        }

        public void AddStatement(StatementNode node)
        {
            RootNode.Statements.Add(node);
        }

        public RapidsParseResult Build()
        {
            return new RapidsParseResult(RootNode, Diagnostics);
        }
    }
    
    public Node? FindNodeAt(int processedIndex)
    {
        Node? bestMatch = null;
        Debug.WriteLine("Finding node at " + processedIndex);
        FindNodeRecursive(RootNode, processedIndex, ref bestMatch);
        return bestMatch;
    }
    
    private static void FindNodeRecursive(Node node, int processedIndex, ref Node? bestMatch)
    {
        if (processedIndex < node.StartIndex || processedIndex >= node.EndIndex)
        {
            return;
        }
        
        bestMatch = node;
        Debug.WriteLine("new best match is: " + bestMatch);

        foreach (var child in node.GetChildren())
        {
            FindNodeRecursive(child, processedIndex, ref bestMatch);
        }
    }
}