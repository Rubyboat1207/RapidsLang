using RapidsLang.Extension.Channel;

namespace RapidsLang.Extension.Communication.Native;

using ExternalIdentifier = RapidsLang.NativeExtension.Identifier;

public static class NativeIdentifierExtensions
{
    public static Identifier ToRapidsIdentifier(this ExternalIdentifier instance)
    {
        return new Identifier(instance.NameSpace, instance.Path);
    }
}