namespace FinanceRAG.Blazor.Services;

public class RagService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RagService> _logger;

    public RagService(HttpClient httpClient, ILogger<RagService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _logger.LogInformation("✅ RagService initialized with HttpClient");
    }

    public async Task<string> GetAnswerAsync(string question)
    {
        try
        {
            _logger.LogInformation($"🔍 Processing question: {question}");

            // RAG API çağrısı yapılacak
            // Şimdilik örnek yanıt döndürülüyor
            await Task.Delay(1000);

            var response = $"Sorunuz: \"{question}\"\n\nBu soru hakkında detaylı bilgi sağlanacaktır.";
            _logger.LogInformation($"✅ Response prepared: {response.Substring(0, 50)}...");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in GetAnswerAsync");
            throw;
        }
    }
}
