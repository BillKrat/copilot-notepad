namespace Adventures.Shared.AI;

public sealed class AiSettings
{
    public string? Provider { get; set; } // OpenAI | Azure
    public string? OpenAIApiKey { get; set; }
    public string? ChatModelId { get; set; }
    public string? EmbeddingModelId { get; set; }
    // Azure specifics
    public string? AzureEndpoint { get; set; }
    public string? AzureApiKey { get; set; }
    public string? AzureChatDeployment { get; set; }
    public string? AzureEmbeddingDeployment { get; set; }
}
