namespace FinanceRAG.Blazor.Services;

public class ChatMessage
{
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
}
