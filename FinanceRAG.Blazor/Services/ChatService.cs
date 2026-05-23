namespace FinanceRAG.Blazor.Services;

public interface IChatService
{
    Task<string> GetAnswerAsync(string question);
}

public class ChatService : IChatService
{
    private readonly RagService _ragService;

    public ChatService(RagService ragService)
    {
        _ragService = ragService;
    }

    public async Task<string> GetAnswerAsync(string question)
    {
        return await _ragService.GetAnswerAsync(question);
    }
}
