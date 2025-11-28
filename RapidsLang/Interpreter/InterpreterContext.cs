using RapidsLang.Analyzer.Types;
using RapidsLang.Interpreter.Variables;
using RapidsLang.Parser.Nodes;
using RapidsLang.PreProcessor;

namespace RapidsLang.Interpreter;

public class InterpreterContext
{
    public bool Active = true;
    public InterpreterContext? Parent { get; }
    public Stack<RapidsVariable> FunctionCallStack = [];
    private Dictionary<string, VariableHolder> Variables { get; init; } = [];

    public static readonly Dictionary<string, (VariableHolder, RapidsType)> GlobalSymbols = new()
    {
        { 
            "exit", (
                new VariableHolder(RapidsFunctionReferenceVariable.OfNative(RapidsInterpreter.Exit), true),
                new RapidsFunctionType([], null)
            )
        },
        {
            "inPrimaryModule", (
                new VariableHolder(RapidsFunctionReferenceVariable.OfNative(RapidsInterpreter.InPrimaryModule), true),
                new RapidsFunctionType([], RapidsPrimitiveType.Bool)
            )
        }
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
                Variables = new Dictionary<string, VariableHolder>(Variables),
                // CurrentModule = CurrentModule
            };
        
        throw new Exception("but why though?");
    }
    
    public bool TryFindVariable(string name, out VariableHolder? variable)
    {
        variable = null;

        if (Variables.TryGetValue(name, out variable))
        {
            return true;
        }
        
        if (Parent != null && Parent.TryFindVariable(name, out variable))
        {
            return true;
        }
        
        // I prefer this, the control flow is more clear this way
        // ReSharper disable once InvertIf
        if (GlobalSymbols.TryGetValue(name, out var global))
        {
            variable = global.Item1;
            return true;
        }

        return false;
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

public class VariableHolder(RapidsVariable variable, bool constant)
{
    public virtual RapidsVariable Variable { get; set; } = variable;
    public bool Constant { get; } = constant;
}