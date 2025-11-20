using RapidsLang.Extension.Channel;

namespace RapidsLang.Extension.Communication.Native;
using ExternalIdentifier = RapidsLang.NativeExtension.Identifier;


public static class IdentifierExtensions
{
    public static ExternalIdentifier ToExternalIdentifier(this Identifier identifier)
    {
        return new(identifier.NameSpace, identifier.Path);
    } 
}