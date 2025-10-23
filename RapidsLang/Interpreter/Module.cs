using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public abstract class Module
{
    protected abstract ModuleExports Exports { get; }

    public virtual void Import(InterpreterContext context, List<ImportNode>? importNodes)
    {
        if (importNodes is null)
        {
            foreach (var exportedVariable in Exports.Exports)
            {
                context.AddVariable(exportedVariable.Key, new VariableHolder(
                    exportedVariable.Value,
                    true
                ));
            }

            return;
        }
        foreach (var import in importNodes)
        {
            if (Exports.Exports.TryGetValue(import.BaseToken.Value, out var value))
            {
                var contextName = import.BaseToken.Value;
                
                if (import.AsName is not null)
                {
                    contextName = import.AsName.Value;
                }
                
                context.AddVariable(contextName, new VariableHolder(value, true));
            }
            else
            {
                throw new Exception(
                    $"Attempted to import {import.BaseToken.Value} which is not a valid export in this module.");
            }
        }
    }
}