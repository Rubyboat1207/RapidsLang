using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.InterpreterVM;

public class VirtualMachine
{
    private readonly Stack<Frame> _frames = [];
    private Frame Frame => _frames.Peek();
    private RapidsVariable[] _globals = null!;
    private ModuleRegistry _moduleRegistry = new();
    
    public void Run(RapidProgram program)
    {
        _globals = new RapidsVariable[program.Header.GlobalsCount];
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
            var opCode = program.Code[Frame.Pc++];
            
            switch (opCode)
            {
                case LoadLocal op:
                {
                    Frame.Stack.Push(Frame.Locals[op.Index]);
                    break;
                }
                case StoreLocal op:
                {
                    Frame.Locals[op.Index] = Frame.Stack.Pop();
                    break;
                }
                case LoadGlobal op:
                {
                    Frame.Stack.Push(_globals[op.Index]);
                    break;
                }
                case LoadString op:
                {
                    Frame.Stack.Push(new RapidsStringVariable(program.Header.Strings[op.Index]));
                    break;
                }
                case Call:
                {
                    var popped = Frame.Stack.Pop();
                    if (popped is RapidsFunctionReferenceVariable func)
                    {
                        switch (func.Function)
                        {
                            case RapidsNativeFunction nativeFunction:
                            {
                                var frame = new Frame(0);
                                for (var i = 0; i < nativeFunction.ParameterCount; i++)
                                {
                                    frame.Stack.Push(Frame.Stack.Pop());
                                }
                                nativeFunction.Execute(frame);
                                var didReturnValue = (RapidsBooleanVariable) frame.Stack.Pop();
                                if (didReturnValue.Value)
                                {
                                    Frame.Stack.Push(frame.Stack.Pop());
                                }
                                break;
                            }
                            case RapidsUserFunction userFunction:
                            {
                                var frame = new Frame(0);
                                for (var i = 0; i < userFunction.ParameterCount; i++)
                                {
                                    frame.Stack.Push(Frame.Stack.Pop());
                                }

                                frame.Pc = userFunction.Index;
                                _frames.Push(frame);
                                
                                break;
                            }
                        }
                    }
                    break;
                }
            }
        }
    }
}