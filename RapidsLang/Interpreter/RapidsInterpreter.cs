using RapidsLang.Lexer;
using RapidsLang.PreProcessor;

namespace RapidsLang.Interpreter;

using RapidsLang.Parser.Nodes;

public class BlockProgress(StatementsNode block, int programCounter=0)
{
    public StatementsNode Block { get; init; } = block;
    public int ProgramCounter { get; set; } = programCounter;
    public List<string> ScopedVariables = [];
}

public class RapidsInterpreter(string sourceCode, RapidsPreprocMetaData preprocessorMetadata, StatementsNode Root)
{
    private InterpreterContext Ctx { get; } = new();
    private readonly Stack<BlockProgress> _statementsStack = [];

    private int ProgramCounter
    {
        get => _statementsStack.Peek().ProgramCounter;
        set => _statementsStack.Peek().ProgramCounter = value;
    }

    private string GetLineCol(Token token)
    {
        return RapidsPreproc.GetRowCol(sourceCode, token.Index, preprocessorMetadata);
    }

    public void StartNewBlock(StatementsNode block)
    {
        _statementsStack.Push(new BlockProgress(block));
    }

    public void Interpret()
    {
        StartNewBlock(Root);
        while (true)
        {
            while (ProgramCounter >= _statementsStack.Peek().Block.Statements.Count)
            {
                if (_statementsStack.Count == 1)
                {
                    break;
                }

                var lastBlock = _statementsStack.Pop();

                foreach (var variable in lastBlock.ScopedVariables)
                {
                    Ctx.variables.Remove(variable);
                }
            }
            StatementNode statement = _statementsStack.Peek().Block.Statements[ProgramCounter];

            if (statement is UseStatementNode useNode)
            {
                Modules.RegisteredModules.TryGetValue(useNode.ModuleIdentifier, out var module);

                if (module is null)
                {
                    Console.WriteLine($"Module {useNode.ModuleIdentifier} at {GetLineCol(useNode.Use)} was not found.");
                }
                else
                {
                    module.Import(Ctx);
                }
                ProgramCounter++;
                continue;
            }

            if (statement is FunctionCallStatementNode functionCallStatementNode)
            {
                // check if in global namespace
                if (functionCallStatementNode.Function.Function is IdentifierNode identifierNode)
                {
                    Ctx.variables.TryGetValue(identifierNode.Token.Value, out var maybeFunction);

                    foreach (var param in functionCallStatementNode.Function.Arguments)
                    {
                        Ctx.FunctionCallStack.Push(EvaluateExpression(param));
                    }

                    if (maybeFunction is { Variable: RapidsFunctionReferenceVariable func })
                    {
                        func.Function!.Function.Invoke(Ctx);
                    }
                }

                ProgramCounter++;
                continue;
            }

            if (statement is DeclarationNode declaration)
            {
                Ctx.variables.Add(
                    declaration.Name.Value,
                    new(EvaluateExpression(declaration.Expression), declaration.Constant, declaration.Type)
                );

                _statementsStack.Peek().ScopedVariables.Add(declaration.Name.Value);

                ProgramCounter++;
                continue;
            }

            if (statement is AssignmentNode assignment)
            {
                if (assignment.Variable.Left == null)
                {
                    if (!TryGetValue(assignment.Variable, out var variable))
                        throw new Exception($"Variable named {assignment.Variable} was not found at {GetLineCol(assignment.Operator)}");

                    if (variable.Constant)
                        throw new Exception($"attempted to assign to a constant variable at {GetLineCol(assignment.Operator)}.");

                    var evaluatedExpression = EvaluateExpression(assignment.Expression);

                    var result = evaluatedExpression;
                    
                    if(assignment.Operator.TokenType != TokenType.Assignment)
                    {
                        result = variable.Variable.GetResult(assignment.Operator.GetOperator(), evaluatedExpression);
                    }

                    if(result is null)
                    {
                        throw new Exception(
                        $"Operation {assignment.Operator.GetOperator()} is not compatible with types {variable.Variable.VariableTypeName} and {evaluatedExpression.VariableTypeName}");
                    }

                    variable.Variable = result;
                }
                ProgramCounter++;
                continue;
            }

            if (statement is WhileLoopNode whileLoopNode)
            {
                var exprValue = EvaluateExpression(whileLoopNode.Condition);

                if (exprValue.Truthy)
                {
                    StartNewBlock(whileLoopNode.Block);
                }
                else
                {
                    ProgramCounter++;
                }

                continue;
            }
            
            if (statement is IfNode ifNode)
            {
                var exprValue = EvaluateExpression(ifNode.Condition);
                ProgramCounter++;

                if (exprValue.Truthy)
                {
                    StartNewBlock(ifNode.Block);
                }

                continue;
            }
        }
    }

    public bool TryGetValue(MemberAccessNode accessNode, out VariableHolder? rapidsVariable)
    {
        if (accessNode.Left is null)
        {
            return Ctx.variables.TryGetValue(accessNode.MemberName.Value, out rapidsVariable);
        }

        throw new NotImplementedException(); // evaluate expression and then call some function ill make called RapidsVariable::GetMember(string)
    }

    public RapidsVariable EvaluateExpression(ExpressionNode expressionNode)
    {
        switch (expressionNode)
        {
            case StringNode strNode:
            {
                var str = "";

                foreach (var part in strNode.Parts)
                {
                    switch (part)
                    {
                        case LiteralStringPart lit:
                            str += lit.Value.Value;
                            break;
                        case TemplateStringPart template:
                            str += Utils.StringifyVariable(EvaluateExpression(template.Value));
                            break;
                    }
                }

                return new RapidsStringVariable(str);
            }
            case LiteralNumberNode numNode:
                return new RapidsNumberVariable(numNode.Number);
            case OperationNode operationNode:
                var left = EvaluateExpression(operationNode.Left);
                var right = EvaluateExpression(operationNode.Right);
                var res = left.GetResult(operationNode.Operator.GetOperator(), right);

                if (res == null)
                {
                    throw new Exception(
                        $"Operation {operationNode.Operator.GetOperator()} is not compatible with types {left.VariableTypeName} and {right.VariableTypeName}");
                }

                return res;
            case IdentifierNode identifierNode:
                if (!Ctx.variables.TryGetValue(identifierNode.Token.Value, out var variable))
                {
                    throw new Exception($"Attempted to access variable \"{identifierNode.Token.Value}\" which is not defined at {GetLineCol(identifierNode.Token)}.");
                }

                return variable.Variable;
            case BooleanNode booleanNode:
                return new RapidsBooleanVariable(booleanNode.value.TokenType == TokenType.True);
            default:
                throw new NotImplementedException("Expression not yet implemented. Sorry!");
        }
    }
    
}