using RapidsLang.Utils;

namespace RapidsLang.PreProcessor;

public class RapidsPreprocResult(string output, RapidsPreprocMetaData metadata)
{
    public RapidsPreprocMetaData Metadata { get; private init; } = metadata;
    public string Output { get; private init; } = output;
}
public class RapidsPreprocMetaData(List<CommentedIndices> commentedIndices)
{
    public List<CommentedIndices> CommentedIndices { get; private init; }  = commentedIndices;
}

public class CommentedIndices(int sourceIndex, int processedIndex, int length)
{
    public int SourceIndex { get; private init; } = sourceIndex;
    public int ProcessedIndex { get; private init; } = processedIndex;
    public int Length { get; private init; } = length;
}

public static class RapidsPreproc
{
    public static RapidsPreprocResult Preprocess(string code)
    {
        return Preprocess(new StringStepper(code));
    }

    private static RapidsPreprocResult Preprocess(StringStepper stepper)
    {
        List<CommentedIndices> indices = [];
        while (stepper.HasNext)
        {
            var start = stepper.SourceIndex;
            switch (stepper.Cur)
            {
                case '/' when stepper.Next == '/':
                    ProcessComment(stepper);
                    indices.Add(new CommentedIndices(start, stepper.SourceBufferSize - 1, stepper.SourceIndex - start));
                    break;
                case '/' when stepper.Next == '*':
                    ProcessMultiline(stepper);
                    indices.Add(new CommentedIndices(start, stepper.SourceBufferSize - 1, stepper.SourceIndex - start));
                    break;
                case '`':
                    indices.AddRange(ProcessString(stepper).CommentedIndices);
                    break;
                default:
                    stepper.Append();
                    break;
            }
        }
        
        while(!stepper.AtEnd)
            stepper.Append();
        

        return new(stepper.Buffer, new(indices));
    }

    private static void ProcessComment(StringStepper stepper)
    {
        while (!stepper.AtEnd)
        {
            if (stepper.Cur != '\n')
            {
                stepper.Trash();
                        
                continue;
            }
            
            stepper.Trash();
                    
            break;
        }
    }

    private static void ProcessMultiline(StringStepper stepper)
    {
        stepper.Trash();
        while (stepper.HasNext)
        {
            if (stepper is { Cur: '*', Next: '/' })
            {
                stepper.Trash(2);
                break;
            }
                    
            stepper.Trash(); 
        }
    }

    private static RapidsPreprocMetaData ProcessString(StringStepper stepper)
{
    stepper.Append();
    List<CommentedIndices> indices = [];

    while (stepper.HasNext)
    {
        if (stepper.Cur == '`' && !stepper.CurIsEscaped())
        {
            stepper.Append();
            break;
        }

        stepper.Append();

        if (stepper.Cur == '{' && !stepper.CurIsEscaped())
        {
            stepper.Append();

            var stringLiteralStepper = new StringStepper(stepper.ActiveString[stepper.index..]);
            int curlyCount = 1;

            while (stringLiteralStepper.HasNext)
            {
                char c = stringLiteralStepper.Cur;

                if (c == '{' && !stringLiteralStepper.CurIsEscaped())
                {
                    curlyCount++;
                }
                else if (c == '}' && !stringLiteralStepper.CurIsEscaped())
                {
                    curlyCount--;
                    if (curlyCount == 0)
                    {
                        stringLiteralStepper.Append();
                        break;
                    }
                }

                stringLiteralStepper.Append();
            }

            if (curlyCount != 0)
                throw new Exception("Unterminated { in template string");

            var baby = stepper.CreateChild(stringLiteralStepper.Buffer.Length - 1); 
            var process = Preprocess(baby);
            indices.AddRange(process.Metadata.CommentedIndices);
            stepper.Join(baby);
        }
    }

    return new RapidsPreprocMetaData(indices);
}


    public static int GetSourceIdx(int processedIndex, RapidsPreprocMetaData metaData)
    {
        return processedIndex 
               + metaData.CommentedIndices
                   .Where(commentIndex => commentIndex.ProcessedIndex < processedIndex)
                   .Sum(commentIndex => commentIndex.Length);
    }

    public static Tuple<int, int> GetRowColFromIndex(int index, string str)
    {
        var lines = 1;
        var cols = 0;
        for (var i = 0; i < index; i++)
        {
            if (str[i] == '\n')
            {
                lines += 1;
                cols = 0;
            }

            cols += 1;
        }

        return new Tuple<int, int>(lines, cols);
    }

    public static string GetRowCol(string str, int processedIndex, RapidsPreprocMetaData metaData)
    {
        var rowcol = GetRowColFromIndex(GetSourceIdx(processedIndex, metaData), str);

        return $"{rowcol.Item1}:{rowcol.Item2}";
    }
}