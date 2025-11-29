using RapidsLang.Interpreter;
using RapidsLang.Parser;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Testing;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [TestCase("1 + 1", "2")]
    [TestCase("5 * 4", "20")]
    [TestCase("(5 + 5) * 2", "20")]
    [TestCase("5 + 5 * 2", "15")]
    [TestCase("10 / 5", "2")]
    
    [TestCase("-5 + 5", "0")] 
    [TestCase("-(5 + 5)", "-10")]
    [TestCase("!true", "False")]
    [TestCase("!!true", "True")] 

    [TestCase("2 + 3 * 4", "14")] 
    [TestCase("2 * 3 + 4", "10")]
    [TestCase("10 - 5 - 2", "3")]
    [TestCase("10 / 2 * 5", "25")]
    
    [TestCase("5 > 3", "True")]
    [TestCase("5 == 5", "True")]
    [TestCase("5 != 5", "False")]
    [TestCase("5 < 10 && 10 > 5", "True")]
    [TestCase("true || false && false", "True")]
    [TestCase("(true || false) && false", "False")]
    [TestCase("5 == 2 + 3", "True")]
    
    [TestCase("`Hello {5 + 5}`", "Hello 10")]
    [TestCase("`Hello ` + 5", "Hello 5")]
    [TestCase("`` + ``", "")]
    
    [TestCase("(()> {return 5;})()", "5")]
    [TestCase("((x)> { return x * 2; })(10)", "20")]
    [TestCase("((a, b)> { return a + b; })(5, 10)", "15")]
    [TestCase("((x)> { return x + 1; })( ((y)> { return y * 2; })(5) )", "11")]
    [TestCase("((x)> { return ((y)> { return x + y; })(5); })(10)", "15")]
    
    [TestCase("1 + (1 + (1 + (1 + (1 + 1))))", "6")]
    [TestCase("((((((5))))))", "5")]
    
    [TestCase("{test: 5}.test", "5")]
    public async Task ExpressionsEvaluateCorrectly(string source, string expected)
    {
        var (expr, metaData) = RapidsParser.ParseExpression(source);

        var interpreter = new RapidsInterpreter(source, metaData);

        Assert.That(expr, Is.Not.EqualTo(null), "Expression is not null");
        
        var res = await interpreter.InterpretExpressionAndThenDie(expr!);

        Assert.That(res, Is.Not.Null, $"Interpreter returned null result for input: '{source}'");

        var actual = Interpreter.Utils.StringifyVariable(res!);

        Assert.That(actual, Is.EqualTo(expected), $"Expected \"{expected}\" found \"{actual}\". (source: \"{source}\")");
    }
}