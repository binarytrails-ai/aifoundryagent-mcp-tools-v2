namespace ContosoBikestore.Agent.Host.Models;

public class OpenAIChatCompletionsRequest
{
    public string? Model { get; set; }
    public OpenAIMessage[]? Messages { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public bool? Stream { get; set; }
}

public class OpenAIMessage
{
    public string? Role { get; set; }
    public string? Content { get; set; }
}

public class OpenAIChatCompletionsResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "chat.completion";
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public OpenAIChoice[] Choices { get; set; } = Array.Empty<OpenAIChoice>();
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIChoice
{
    public int Index { get; set; }
    public OpenAIMessage Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

public class OpenAIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
