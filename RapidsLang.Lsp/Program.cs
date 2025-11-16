using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;

namespace RapidsLang.LanguageServer;

internal abstract class Program
{
    private static async Task Main(string[] args)
    {
        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options => 
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithLoggerFactory(new LoggerFactory())
                .AddDefaultLoggingProvider()
                .WithServices(services =>
                {
                    services.AddSingleton<DocumentManager>();
                })
                .WithHandler<RapidsTextDocumentHandler>()
                .WithHandler<RapidsHoverHandler>()
                .WithHandler<RapidsSemanticTokensHandler>()
                .WithHandler<RapidsCompletionHandler>()
        );
        
        await server.WaitForExit;
    }
}