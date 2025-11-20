using RapidsLang.Extension.Communication;
using RapidsLang.Extensions.Communication;
using RapidsLang.Parser.Nodes;

namespace RapidsLang.Interpreter;

public abstract class Module
{
    public abstract ModuleExports Exports { get; }
    public virtual CommunicationProtocol? Protocol { get; } = null;

    public virtual void Import(RapidsInterpreter interpreter, List<ImportNode>? importNodes)
    {
        var context = interpreter.Context;
        if (importNodes is null)
        {
            foreach (var exportedVariable in Exports.Exports)
            {
                context.AddVariable(exportedVariable.Key, new VariableHolder(
                    exportedVariable.Value.Variable,
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
                
                context.AddVariable(contextName, new VariableHolder(value.Variable, true));
            }
            else
            {
                throw new Exception(
                    $"Attempted to import {import.BaseToken.Value} which is not a valid export in this module.");
            }
        }
    }
}