using System.Diagnostics.CodeAnalysis;
using RapidsLang.Analyzer;
using RapidsLang.Interpreter;
using RapidsLang.Lexer;
using RapidsLang.Parser.Nodes;
using RapidsLang.Utils;

namespace RapidsLang.InterpreterVM;

public class RapidsCompiler
{
    private readonly List<string> _strings = [];
    private readonly List<ModuleImport> _modules = [];
    private readonly List<Symbol> _definedGlobalSymbols = [];
    private readonly List<RapidsBytecodeFunction> _functions = [];
    private RapidsStaticAnalysisResult _staticAnalysisResult = null!;

    public static RapidProgram Compile(StatementsNode root, RapidsStaticAnalysisResult staticAnalysisResult)
    {
        return new RapidsCompiler().GenerateProgram(root, staticAnalysisResult);
    }
    
    private RapidProgram GenerateProgram(StatementsNode root, RapidsStaticAnalysisResult staticAnalysisResult)
    {
        _staticAnalysisResult = staticAnalysisResult;
        var res = CompileStatements(root, [], new VariableSlotHolder());

        return new RapidProgram
        {
            Header = new BytecodeHeader
            {
                Version = 0,
                Strings = _strings.ToArray(),
                Modules = _modules.ToArray(),
                GlobalsCount = (uint) _definedGlobalSymbols.Count,
                OutermostLocalsCount = res.LocalsUsed
            },
            Code = res.Operations.ToArray(),
            FunctionBlock = new RapidsProgramFunctionBlock
            {
                Functions = _functions.ToArray()
            }
        };
    }

    private class CompileStatementsResult
    {
        public uint LocalsUsed;
        public List<OpCode> Operations = [];

        public List<OpCode> BreakPlaceholders = [];

        public List<OpCode> ContinuePlaceholders = [];
    }

    enum StatementCompilationContext
    {
        OuterScope,
        Function,
        Loop,
        DataSource
    }
    
    private CompileStatementsResult CompileStatements(
        StatementsNode root,
        List<Symbol> definedSymbols,
        VariableSlotHolder variableSlotHolder,
        int startIndex=0
    )
    {
        var sResult = new CompileStatementsResult();
        foreach (var statement in root.Statements)
        {
            switch (statement)
            {
                case FunctionCallStatementNode functionCall:
                {
                    var res = CompileFunctionCall(functionCall.Function, definedSymbols, variableSlotHolder);
                    sResult.Operations.AddRange(res.OpCodes);
                    break;
                }
                case BreakNode:
                {
                    var br = new NoOp();
                    sResult.Operations.AddRange(br);
                    sResult.BreakPlaceholders.Add(br);
                    break;
                }
                case ContinueNode:
                {
                    var cont = new NoOp();
                    sResult.Operations.AddRange(cont);
                    sResult.ContinuePlaceholders.Add(cont);
                    break;
                }
                case WhileLoopNode whileLoopNode:
                {
                    var condRes = CompileExpression(whileLoopNode.Condition, definedSymbols, variableSlotHolder);

                    var res = GenerateLoop([], condRes.OpCodes, [], whileLoopNode.Block, sResult.Operations.Count + startIndex, definedSymbols, variableSlotHolder);
                    
                    sResult.Operations.AddRange(res.Operations);
                    sResult.LocalsUsed += res.LocalsUsed;

                    break;
                }
                case NumericForLoop numericForLoop:
                {
                    definedSymbols.Add(_staticAnalysisResult.SymbolReferences[numericForLoop.Index]);
                    var variableIdx = variableSlotHolder.AddOrGetSymbolSlot(_staticAnalysisResult.SymbolReferences[numericForLoop.Index]);
                    var startIdx = variableSlotHolder.ClaimNextOpenSlotId();
                    var endIdx = variableSlotHolder.ClaimNextOpenSlotId();
                    int? stepIdx = numericForLoop.StepExpr is null ? null : variableSlotHolder.ClaimNextOpenSlotId();
                    
                    var res = GenerateLoop(
                        [
                            ..CompileExpression(numericForLoop.Start, definedSymbols, variableSlotHolder).OpCodes,
                            new StoreLocal(variableIdx),
                            new LoadLocal(variableIdx),
                            new StoreLocal(startIdx),
                            ..CompileExpression(numericForLoop.End, definedSymbols, variableSlotHolder).OpCodes,
                            new StoreLocal(endIdx),
                            ..(stepIdx is null ? Array.Empty<OpCode>() : [
                                ..CompileExpression(numericForLoop.StepExpr!, definedSymbols, variableSlotHolder).OpCodes,
                                new StoreLocal(stepIdx.Value)
                            ])
                        ], 
                        [
                            new LoadLocal(variableIdx),
                            new LoadLocal(endIdx),
                            new LoadLocal(startIdx),
                            new LoadLocal(endIdx),
                            ..CompileBranch(
                                [new LessThan()], 
                                [numericForLoop.IncludesEnd ? new LessThan() : new LessThanEqualto()], 
                                [numericForLoop.IncludesEnd ? new GreaterThan() : new GreaterThanEqualto()]
                            )
                        ], 
                        [
                            new LoadLocal(variableIdx),
                            ..(stepIdx is null ? [new LoadNumber(1)] : CompileExpression(numericForLoop.StepExpr!, definedSymbols, variableSlotHolder).OpCodes),
                            new LoadLocal(startIdx),
                            new LoadLocal(endIdx),
                            ..CompileBranch(
                                [new LessThan()], 
                                [new Add()], 
                                [new Subtract()]
                            ),
                            new StoreLocal(variableIdx)
                        ], 
                        numericForLoop.Body, 
                        sResult.Operations.Count + startIndex,
                        definedSymbols,
                        variableSlotHolder
                    );
                    
                    sResult.Operations.AddRange(res.Operations);
                    sResult.LocalsUsed += res.LocalsUsed;
                    
                    break;
                }
                case DeclarationNode declarationNode:
                {
                    var res = CompileExpression(declarationNode.Expression, definedSymbols, variableSlotHolder);
                    sResult.Operations.AddRange(res.OpCodes);
                    var symbol = _staticAnalysisResult.SymbolReferences[declarationNode.Name];
                    sResult.Operations.Add(new StoreLocal(variableSlotHolder.AddOrGetSymbolSlot(symbol)));
                    definedSymbols.Add(symbol);
                    break;
                }
                case AssignmentNode assignmentNode:
                {
                    var exprRes = CompileExpression(assignmentNode.Expression, definedSymbols, variableSlotHolder).OpCodes;
                    sResult.Operations.AddRange(exprRes);
                    if (assignmentNode.Variable.Left is null)
                    {
                        // assigning a local
                        if (!_staticAnalysisResult.SymbolReferences.TryGetValue(assignmentNode.Variable.MemberName,
                                out var symbol))
                        {
                            // bad
                            throw new Exception($"Attempted to assign to unknown symbol, '{assignmentNode.Variable.MemberName.Value}'.");
                        }
                        
                        var localIndex = variableSlotHolder.AddOrGetSymbolSlot(symbol);
                        if (assignmentNode.Operator.TokenType != TokenType.Assignment)
                        {
                            sResult.Operations.AddRange([new LoadLocal(localIndex), ..GetOpcodesForOperation(assignmentNode.Operator)]);
                        }
                        
                        sResult.Operations.Add(new StoreLocal(localIndex));
                    }
                    break;
                }
                case UseStatementNode useStatementNode:
                {
                    _modules.Add(new ModuleImport(
                        useStatementNode.ModuleName.GetName(),
                        useStatementNode.ImportNodes.Select(i => i.BaseToken.Value).ToArray())
                    );
                    if (useStatementNode.ImportNodes is null)
                    {
                        _definedGlobalSymbols.AddRange(_staticAnalysisResult.ImplicitlyImportedSymbols[useStatementNode]);
                    }
                    else
                    {
                        foreach (var importNode in useStatementNode.ImportNodes)
                        {
                            _definedGlobalSymbols.Add(_staticAnalysisResult.ExplicitlyImportedSymbols[importNode]);
                        }
                    }
                    
                    break;
                }
                case FunctionDeclarationNode functionDeclarationNode:
                {
                    var res = CompileExpression(functionDeclarationNode.Function, definedSymbols, variableSlotHolder);
                    sResult.Operations.AddRange(res.OpCodes);
                    sResult.Operations.Add(new StoreLocal((int) sResult.LocalsUsed++));
                    definedSymbols.Add(_staticAnalysisResult.SymbolReferences[functionDeclarationNode.Name]);
                    break;
                }
                case ReturnNode returnNode:
                {
                    if (returnNode.Value is null)
                    {
                        sResult.Operations.AddRange([new LoadBool(false), new Return()]);
                    }
                    else
                    {
                        var res = CompileExpression(returnNode.Value, definedSymbols, variableSlotHolder);
                        sResult.Operations.AddRange(res.OpCodes);
                        sResult.Operations.AddRange([new LoadBool(true), new Return()]);
                    }
                    break;
                }
                case IfNode ifNode:
                {
                    var res = CompileExpression(ifNode.Condition, definedSymbols, variableSlotHolder);
                    sResult.Operations.AddRange(res.OpCodes);
                    {
                        var block = CompileStatements(ifNode.Block, definedSymbols, variableSlotHolder, startIndex + sResult.Operations.Count);
                        sResult.BreakPlaceholders.AddRange(block.BreakPlaceholders);
                        sResult.ContinuePlaceholders.AddRange(block.ContinuePlaceholders);
                        
                        sResult.Operations.Add(new JumpIfFalseRel(block.Operations.Count + (ifNode.ElseNodes.Count > 0 ? 1 : 0))); 
                        sResult.LocalsUsed += block.LocalsUsed;
                        sResult.Operations.AddRange(block.Operations);
                    }

                    
                    var opsToReplace = new List<OpCode>();
                    if (ifNode.ElseNodes.Count > 0)
                    {
                        var endOfInitialBlock = new NoOp();
                        sResult.Operations.Add(endOfInitialBlock);
                        opsToReplace.Add(endOfInitialBlock);
                    }

                    for (var index = 0; index < ifNode.ElseNodes.Count; index++)
                    {
                        var isLast = index == ifNode.ElseNodes.Count - 1;
                        var eNode = ifNode.ElseNodes[index];
                        var block = CompileStatements(eNode.Block, definedSymbols, variableSlotHolder,
                            startIndex + sResult.Operations.Count);
                        sResult.BreakPlaceholders.AddRange(block.BreakPlaceholders);
                        sResult.ContinuePlaceholders.AddRange(block.ContinuePlaceholders);

                        if (eNode.Condition is not null)
                        {
                            var condRes = CompileExpression(eNode.Condition, definedSymbols, variableSlotHolder);
                            sResult.Operations.AddRange(condRes.OpCodes);
                            sResult.Operations.Add(new JumpIfFalseRel(block.Operations.Count + (isLast ? 0 : 1)));
                        }

                        sResult.LocalsUsed += block.LocalsUsed;
                        sResult.Operations.AddRange(block.Operations);
                        
                        if (isLast) continue;
                        
                        var endOf = new NoOp();
                        sResult.Operations.Add(endOf);
                        opsToReplace.Add(endOf);
                    }

                    foreach (var index in opsToReplace.Select(opCode => sResult.Operations.IndexOf(opCode)))
                    {
                        sResult.Operations.RemoveAt(index);
                        
                        sResult.Operations.Insert(index, new Jump(startIndex + sResult.Operations.Count + 1));
                    }
                    break;
                }
            }
        }

        sResult.LocalsUsed += variableSlotHolder.LocalsUsed;
        return sResult;
    }

    private OpCode[] CompileBranch(IEnumerable<OpCode> condition, IList<OpCode> positive,
        IList<OpCode> negative) =>
    [
        ..condition,
        new JumpIfTrueRel(negative.Count + 1),
        ..negative,
        new JumpRel(positive.Count),
        ..positive
    ];

    private CompileExpressionResult CompileFunctionCall(FunctionCallExpressionNode callExpressionNode, List<Symbol> definedSymbols, VariableSlotHolder variableSlotHolder)
    {
        List<OpCode> operations = [];
        foreach (var argRes in callExpressionNode.Arguments.Select(arg => CompileExpression(arg, definedSymbols, variableSlotHolder)))
        {
            operations.AddRange(argRes.OpCodes);
        }
        
        var res = CompileExpression(callExpressionNode.Function, definedSymbols, variableSlotHolder);
        operations.AddRange(res.OpCodes);
        
        operations.Add(new Call());

        return new CompileExpressionResult(operations);
    }

    private CompileExpressionResult CompileExpression(ExpressionNode expressionNode, List<Symbol> definedSymbols, VariableSlotHolder variableSlotHolder)
    {
        List<OpCode> operations = [];
        switch (expressionNode)
        {
            case IdentifierNode identifierNode:
            {
                if (_staticAnalysisResult.SymbolReferences.TryGetValue(identifierNode, out var symbol))
                {
                    if (definedSymbols.Contains(symbol))
                    {
                        operations =  [new LoadLocal(variableSlotHolder.AddOrGetSymbolSlot(symbol))];
                    }

                    if (_definedGlobalSymbols.Contains(symbol))
                    {
                        operations =  [new LoadGlobal(_definedGlobalSymbols.IndexOf(symbol))];
                        break;
                    }

                    if (symbol.Name == "exit")
                    {
                        operations = [new Exit()];
                        break;
                    }
                    
                    // undefined reference
                }
                break;
            }
            case OperationNode operationNode:
            {
                var leftRes = CompileExpression(operationNode.Left, definedSymbols, variableSlotHolder);
                operations.AddRange(leftRes.OpCodes);
                var rightRes = CompileExpression(operationNode.Right, definedSymbols, variableSlotHolder);
                operations.AddRange(rightRes.OpCodes);
                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
                operations.AddRange(GetOpcodesForOperation(operationNode.Operator));

                break;
            }
            case FunctionNode functionNode:
            {
                List<Symbol> innerDefinedSymbols = [];
                foreach (var arg in functionNode.Arguments ?? [])
                {
                    innerDefinedSymbols.Add(_staticAnalysisResult.SymbolReferences[arg.Name]);
                }
                var innerSlotHolder = variableSlotHolder.CloneForFunction(innerDefinedSymbols);

                var res = CompileStatements(functionNode.Body, innerDefinedSymbols, innerSlotHolder);

                var func = new RapidsBytecodeFunction([..res.Operations, new LoadBool(false), new Return()], (uint) (functionNode.Arguments?.Count ?? 0), res.LocalsUsed);
                _functions.Add(func);
                var idx = _functions.IndexOf(func);
                operations = [new LoadFunction(idx), new CaptureFunctionClosure(idx)];
                
                break;
            }
            case LiteralNumberNode literalNumberNode:
            {
                operations = [new LoadNumber(literalNumberNode.Number)];
                break;
            }
            case BooleanNode booleanNode:
            {
                operations = [new LoadBool(booleanNode.Value.TokenType is TokenType.True)];
                break;
            }
            case StringNode stringNode:
            {
                int pushedParts = 0;
                foreach (var part in stringNode.Parts)
                {
                    switch (part)
                    {
                        case LiteralStringPart lit when lit.Value.Value == "":
                            break;
                        case LiteralStringPart lit when !_strings.Contains(lit.Value.Value):
                            _strings.Add(lit.Value.Value.Unescape());
                            operations.Add(new LoadString(_strings.Count - 1));
                            pushedParts++;
                            break;
                        case LiteralStringPart lit:
                            operations.Add(new LoadString(_strings.IndexOf(lit.Value.Value)));
                            pushedParts++;
                            break;
                        case TemplateStringPart template:
                            var res = CompileExpression(template.Value, definedSymbols, variableSlotHolder);
                            operations.AddRange(res.OpCodes);
                            pushedParts++;
                            break;
                    }
                }

                if (stringNode.Parts.Count > 1)
                {
                    operations.Add(new Concat(pushedParts));
                }
                break;
            }
            case FunctionCallExpressionNode functionCallExpressionNode:
            {
                return CompileFunctionCall(functionCallExpressionNode, definedSymbols, variableSlotHolder);
            }
        }

        return new CompileExpressionResult(operations);
    }

    private class CompileExpressionResult(List<OpCode> opCodes)
    {
        public List<OpCode> OpCodes { get; } = opCodes;
    }

    public OpCode[] GetOpcodesForOperation(Token op)
    {
        return (op.TokenType switch
        {
            TokenType.Plus => [new Add()],
            TokenType.Minus => [new Subtract()],
            TokenType.Slash => [new Divide()],
            TokenType.Star => [new Multiply()],
            TokenType.Modulo => [new Modulo()],
            TokenType.Not => [new Not()],
            TokenType.Equality => [new Equal()],
            TokenType.LessThanOrEqualTo => [new LessThanEqualto()],
            TokenType.GreaterThanOrEqualTo => [new GreaterThanEqualto()],
            TokenType.OpenTriangle => [new LessThan()],
            TokenType.ClosedTriangle => [new GreaterThan()],
            TokenType.NotEqual => [new Equal(), new Not()],
            TokenType.And => [new And()],
            TokenType.Or => [new Or()],
            TokenType.OpenSquare => [new Index()],
            _ => throw new ArgumentOutOfRangeException($"{op.Value} is not a known operator.")
        })!;
    }

    private CompileStatementsResult GenerateLoop(IEnumerable<OpCode> pre, IEnumerable<OpCode> condition, IList<OpCode> post, StatementsNode block, int index, List<Symbol> definedSymbols, VariableSlotHolder variableSlotHolder)
    {
        CompileStatementsResult sResult = new();
        
        sResult.Operations.AddRange(pre);
        sResult.Operations.Add(new JumpRel(post.Count));
        
        var continuePosition = sResult.Operations.Count + index;
        sResult.Operations.AddRange(post);
        sResult.Operations.AddRange(condition);
        
                    
        var placeholder = new NoOp();
        var placeholderIndex = sResult.Operations.Count;
        sResult.Operations.Add(placeholder);
                    
        var blockStart = sResult.Operations.Count + index;
        var contId = Guid.CreateVersion7();
        var brId = Guid.CreateVersion7();
        var blockRes = CompileStatements(block, definedSymbols, variableSlotHolder, blockStart);
                    
        sResult.Operations.AddRange(blockRes.Operations);
        // back to the jump if false
        sResult.Operations.Add(new Jump(continuePosition));
        sResult.Operations.RemoveAt(placeholderIndex);
        // Add one to account for jump back to start
        var breakPosition = blockStart + blockRes.Operations.Count + 1;
        sResult.Operations.Insert(placeholderIndex, new JumpIfFalse(breakPosition));

        foreach (var idx in blockRes.ContinuePlaceholders.Select(cont => sResult.Operations.IndexOf(cont)))
        {
            sResult.Operations.RemoveAt(idx);
            sResult.Operations.Insert(idx, new Jump(continuePosition));
        }
                    
        foreach (var idx in blockRes.BreakPlaceholders.Select(cont => sResult.Operations.IndexOf(cont)))
        {
            sResult.Operations.RemoveAt(idx);
            sResult.Operations.Insert(idx, new Jump(breakPosition));
        }

        return sResult;
    }
}