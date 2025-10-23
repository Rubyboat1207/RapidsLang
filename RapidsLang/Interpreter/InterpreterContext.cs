using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;

namespace RapidsLang.Interpreter;

public class InterpreterContext
{
    public bool Active = true;
    public InterpreterContext? Parent { get; }
    public Stack<RapidsVariable> FunctionCallStack = [];
    private Dictionary<string, VariableHolder> Variables { get; init; } = new()
    {
        {"exit", new VariableHolder(RapidsFunctionReferenceVariable.ofNative(RapidsInterpreter.Exit), true)} 
    };
    public ModuleRegistry ModuleRegistry = new();
    public ModuleExports Exports = new();
    public Module? CurrentModule;
    
    // todo: this sucks
    public string SourceCode { get; }
    public RapidsPreprocMetaData PreprocMetaData { get; }
    public string? SourcePath { get; }

    public InterpreterContext(string sourceCode, RapidsPreprocMetaData preprocMetaData, string? sourcePath)
    {
        SourceCode = sourceCode;
        PreprocMetaData = preprocMetaData;
        SourcePath = sourcePath;
    }
    
    public InterpreterContext(InterpreterContext parent, Module? currentModule=null)
    {
        Parent = parent;
        
        FunctionCallStack = parent.FunctionCallStack;
        ModuleRegistry = parent.ModuleRegistry;
        Exports = parent.Exports;
        Variables = new Dictionary<string, VariableHolder>(parent.Variables);
        SourceCode = parent.SourceCode;
        PreprocMetaData = parent.PreprocMetaData;
        SourcePath = parent.SourcePath;
        CurrentModule = currentModule ?? parent.CurrentModule;
    }

    public InterpreterContext Clone()
    {
        if (Parent != null)
            return new InterpreterContext(Parent)
            {
                Variables = new Dictionary<string, VariableHolder>(Variables)
            };
        
        throw new Exception("but why though?");
    }
    
    public bool TryFindVariable(string name, out VariableHolder? variable)
    {
        if (Variables.TryGetValue(name, out variable))
        {
            return true;
        }

        variable = null;
        
        return Parent != null && Parent.TryFindVariable(name, out variable);
    }

    public void AddVariable(string name, VariableHolder variableHolder)
    {
        Variables[name] = variableHolder;
    }

    public InterpreterContext GetRoot()
    {
        var root = this;

        while (root.Parent is not null)
        {
            root = root.Parent!;
        }

        return root;
    }
}

public class VariableHolder(RapidsVariable Variable, bool Constant, TypeNode? TypeNode = null)
{
    public virtual RapidsVariable Variable { get; set; } = Variable;
    public bool Constant { get; } = Constant;
    public TypeNode? TypeNode { get; } = TypeNode;
}