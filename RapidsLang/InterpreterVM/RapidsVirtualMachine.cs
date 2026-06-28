using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;
using RapidsLang.InterpreterVM.ExtendedTypes;

namespace RapidsLang.InterpreterVM;

public class RapidsVirtualMachine
{
    private readonly Stack<Frame> _frames = [];
    private Frame Frame => _frames.Peek();
    private RapidsVariable[] _globals = null!;
    private ModuleRegistry _moduleRegistry = new();
    
    public void Run(RapidProgram program)
    {
        _globals = new RapidsVariable[program.Header.GlobalsCount];
        VariableHolder[][] functionClosures = new VariableHolder[program.FunctionBlock.Functions.Length][];
        var globalImportIndex = 0;
        foreach (var importedModule in program.Header.Modules)
        {
            if (!_moduleRegistry.TryGetModule(importedModule.ModuleName, out var module)) continue;
            
            foreach (var import in importedModule.Imports)
            {
                _globals[globalImportIndex++] = module.Exports.Exports[import].Variable;
            }
        }
        _frames.Push(new Frame(program.Header.OutermostLocalsCount));

        while(Frame.Pc < program.Code.Length)
        {
            var opCode = Frame.FunctionIndex.HasValue ? program.FunctionBlock.Functions[Frame.FunctionIndex.Value].Code[Frame.Pc++] : program.Code[Frame.Pc++];
            try
            {
                switch (opCode)
                {
                    case LoadLocal op:
                    {
                        // may cause undefined reference if it has not been stored yet.
                        Frame.Stack.Push(Frame.Locals[op.Value]!.Variable);
                        break;
                    }
                    case LoadNumber op:
                    {
                        Frame.Stack.Push(new RapidsNumberVariable(op.Value));
                        break;
                    }
                    case StoreLocal op:
                    {
                        var holder = Frame.Locals[op.Value];
                        if (holder is null)
                        {
                            Frame.Locals[op.Value] = new VariableHolder(Frame.Stack.Pop(), false);
                        }
                        else
                        {
                            holder.Variable = Frame.Stack.Pop();
                        }

                        break;
                    }
                    case LoadGlobal op:
                    {
                        Frame.Stack.Push(_globals[op.Value]);
                        break;
                    }
                    case LoadFunction op:
                    {
                        Frame.Stack.Push(new RapidsBytecodeFunctionReference(op.Value));
                        break;
                    }
                    case LoadString op:
                    {
                        Frame.Stack.Push(new RapidsStringVariable(program.Header.Strings[op.Value]));
                        break;
                    }
                    case Exit:
                    {
                        return;
                    }
                    case CaptureFunctionClosure op:
                    {
                        functionClosures[op.Value] = Frame.Locals;
                        break;
                    }
                    case Concat op:
                    {
                        var str = "";
                        var emptyString = new RapidsStringVariable("");
                        for (var i = 0; i < op.Value; i++)
                        {
                            var result = Frame.Stack.Pop().GetResult(RapidsOperator.Add, emptyString);
                            if (result is RapidsStringVariable resStr)
                            {
                                str = resStr.Value + str;
                            }
                            else
                            {
                                str += "undefined";
                            }
                        }

                        Frame.Stack.Push(new RapidsStringVariable(str));
                        break;
                    }
                    case Jump op:
                    {
                        Frame.Pc = op.Value;
                        break;
                    }
                    case JumpRel op:
                    {
                        Frame.Pc += op.Value;
                        break;
                    }
                    case JumpIfTrue op:
                    {
                        var value = Frame.Stack.Pop();
                        if (value.Truthy)
                        {
                            Frame.Pc = op.Value;
                        }

                        break;
                    }
                    case JumpIfTrueRel op:
                    {
                        var value = Frame.Stack.Pop();
                        if (value.Truthy)
                        {
                            Frame.Pc += op.Value;
                        }

                        break;
                    }
                    case JumpIfFalse op:
                    {
                        var value = Frame.Stack.Pop();
                        if (!value.Truthy)
                        {
                            Frame.Pc = op.Value;
                        }

                        break;
                    }
                    case JumpIfFalseRel op:
                    {
                        var value = Frame.Stack.Pop();
                        if (!value.Truthy)
                        {
                            Frame.Pc += op.Value;
                        }

                        break;
                    }
                    case LoadBool op:
                    {
                        Frame.Stack.Push(new RapidsBooleanVariable(op.Bool));
                        break;
                    }
                    case Add:
                    {
                        Calculate(RapidsOperator.Add);
                        break;
                    }
                    case Subtract:
                    {
                        Calculate(RapidsOperator.Subtract);
                        break;
                    }
                    case Multiply:
                    {
                        Calculate(RapidsOperator.Multiply);
                        break;
                    }
                    case Divide:
                    {
                        Calculate(RapidsOperator.Divide);
                        break;
                    }
                    case Modulo:
                    {
                        Calculate(RapidsOperator.Modulo);
                        break;
                    }
                    case Index:
                    {
                        Calculate(RapidsOperator.Index);
                        break;
                    }
                    case Equal:
                    {
                        Calculate(RapidsOperator.Equality);
                        break;
                    }
                    case GreaterThan:
                    {
                        Calculate(RapidsOperator.GreaterThan);
                        break;
                    }
                    case GreaterThanEqualto:
                    {
                        Calculate(RapidsOperator.GreaterThanEqualTo);
                        break;
                    }
                    case LessThanEqualto:
                    {
                        Calculate(RapidsOperator.LessThanEqualTo);
                        break;
                    }
                    case LessThan:
                    {
                        Calculate(RapidsOperator.LessThan);
                        break;
                    }
                    case And:
                    {
                        Calculate(RapidsOperator.AndAnd);
                        break;
                    }
                    case Or:
                    {
                        Calculate(RapidsOperator.OrOr);
                        break;
                    }
                    case Call:
                    {
                        var popped = Frame.Stack.Pop();
                        if (popped is RapidsFunctionReferenceVariable functionRef &&
                            functionRef.Function is RapidsNativeFunction nativeFunction)
                        {
                            var frame = new Frame(0)
                            {
                                Locals = new VariableHolder[nativeFunction.ParameterCount]
                            };
                            for (var i = 0; i < nativeFunction.ParameterCount; i++)
                            {
                                frame.Locals[nativeFunction.ParameterCount - 1 - i] =
                                    new VariableHolder(Frame.Stack.Pop(), false);
                            }

                            nativeFunction.Execute(frame);
                            var didReturnValue = (RapidsBooleanVariable)frame.Stack.Pop();
                            if (didReturnValue.Value)
                            {
                                Frame.Stack.Push(frame.Stack.Pop());
                            }
                        }

                        if (popped is RapidsBytecodeFunctionReference fn)
                        {
                            var function = program.FunctionBlock.Functions[fn.Index];
                            var closure = functionClosures[fn.Index];

                            var frame = new Frame(0)
                            {
                                Locals = new VariableHolder[function.ParameterCount + closure.Length +
                                                            function.LocalCount],
                                FunctionIndex = fn.Index
                            };
                            for (var i = 0; i < function.ParameterCount; i++)
                            {
                                frame.Locals[function.ParameterCount + closure.Length - 1 - i] =
                                    new VariableHolder(Frame.Stack.Pop(), false);
                            }

                            closure.CopyTo(frame.Locals, function.ParameterCount);

                            _frames.Push(frame);
                        }

                        break;
                    }
                    case Return:
                    {
                        var didReturnValue = (RapidsBooleanVariable)Frame.Stack.Pop();
                        if (didReturnValue.Value)
                        {
                            var retVal = Frame.Stack.Pop();
                            _frames.Pop();
                            Frame.Stack.Push(retVal);
                        }
                        else
                        {
                            _frames.Pop();
                        }

                        break;
                    }
                    case AssembleList op:
                    {
                        var list = new RapidsListVariable();
                        for (var i = 0; i < op.Value; i++)
                        {
                            list.List.Add(Frame.Stack.Pop());
                        }
                        Frame.Stack.Push(list);
                        break;
                    }
                    case GetIterator:
                    {
                        Frame.Stack.Push(new RapidsIteratorVariable(Frame.Stack.Pop().GetIterable() ?? []));
                        break;
                    }
                    case IteratorNext:
                    {
                        var variable = Frame.Stack.Peek();
                        if (variable is RapidsIteratorVariable iterator)
                        {
                            iterator.Next();
                        }

                        break;
                    }
                    case PushIteratorKey:
                    {
                        var variable = Frame.Stack.Peek();
                        if (variable is RapidsIteratorVariable iterator)
                        {
                            Frame.Stack.Push(iterator.GetKey());
                        }

                        break;
                    }
                    case PushIteratorValue:
                    {
                        var variable = Frame.Stack.Peek();
                        if (variable is RapidsIteratorVariable iterator)
                        {
                            Frame.Stack.Push(iterator.GetValue());
                        }

                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                foreach (var frame in _frames)
                {
                    Console.WriteLine($"At instruction {frame.Pc - 1}");
                }

                return;
            }
            
        }
    }

    public void Calculate(RapidsOperator op)
    {
        var b = Frame.Stack.Pop();
        var a = Frame.Stack.Pop();

        var res = a.GetResult(op, b);
        if (res is null)
        {
            // do something
            return;
        }
        
        Frame.Stack.Push(res);
    }
}