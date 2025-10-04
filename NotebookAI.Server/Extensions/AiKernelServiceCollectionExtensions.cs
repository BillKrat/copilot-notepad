using Adventures.Shared.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace NotebookAI.Server.Extensions;

public static class AiKernelServiceCollectionExtensions
{
    public static IServiceCollection AddAiKernel(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(sp =>
        {
            var ai = sp.GetRequiredService<IOptions<AiSettings>>().Value;
            var kb = Kernel.CreateBuilder();
            kb.Services.AddLogging();

            var provider = ai.Provider?.Trim() ?? "OpenAI";
            var chatModel = ai.ChatModelId ?? "gpt-4o-mini";
            var embeddingModel = ai.EmbeddingModelId ?? "text-embedding-3-small";

            if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                var endpoint = ai.AzureEndpoint ?? config["AZURE_OPENAI_ENDPOINT"];
                var apiKey = ai.AzureApiKey ?? config["AZURE_OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                var chatDeployment = ai.AzureChatDeployment ?? chatModel;
                var embeddingDeployment = ai.AzureEmbeddingDeployment ?? embeddingModel;
                if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("Azure OpenAI configuration missing Endpoint or ApiKey");
                }
                kb.AddAzureOpenAIChatCompletion(chatDeployment, endpoint, apiKey);
                kb.AddAzureOpenAITextEmbeddingGeneration(embeddingDeployment, endpoint, apiKey);
            }
            else
            {
                var apiKey = ai.OpenAIApiKey ?? config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("OpenAI ApiKey not configured");
                }
                kb.AddOpenAIChatCompletion(chatModel, apiKey);
                kb.AddOpenAITextEmbeddingGeneration(embeddingModel, apiKey);
            }
            return kb.Build();
        });
        return services;
    }
}
