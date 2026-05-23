    using Microsoft.Data.SqlClient;
    using OllamaSharp;
    using OllamaSharp.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;

    namespace FinanceRAG.ConsoleApp
    {
        internal class Program
        {
            private const string ConnectionString =
                "Server=localhost,1433;Database=Orion;User Id=sa;Password=Ggrt190724;TrustServerCertificate=True;";
            private const string OllamaBaseUrl = "http://localhost:11434";

            static async Task Main(string[] args)
            {
                Console.WriteLine("=== Türk Finansal RAG Asistanı ===\n");

                try
                {
                    // Ollama bağlantısını kontrol et
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromMilliseconds(10000);  // 10 saniye timeout
                        try
                        {
                            var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
                            if (!response.IsSuccessStatusCode)
                            {
                                Console.WriteLine("❌ Ollama sunucusu erişilemez!");
                                Console.WriteLine("   1. Ollama'yı başlatınız: ollama serve");
                                Console.WriteLine("   2. Modelleri indirin:");
                                Console.WriteLine("      - ollama pull llama3.1:8b");
                                Console.WriteLine("      - ollama pull bge-m3");
                                return;
                            }
                        }
                        catch (System.Net.Http.HttpRequestException)
                        {
                            Console.WriteLine("❌ Ollama sunucusuna bağlanılamadı!");
                            Console.WriteLine("   localhost:11434 üzerinde Ollama çalıştırıldığından emin olun.");
                            Console.WriteLine("   Komut: ollama serve");
                            return;
                        }
                    }

                    var ragService = new RagService(OllamaBaseUrl, ConnectionString);

                    Console.WriteLine("✅ Asistan hazır! Soru sormaya başlayabilirsiniz.");
                    Console.WriteLine("   Çıkmak için 'exit' yazın.\n");
                    Console.WriteLine("📝 Kullanılan modeller:");
                    Console.WriteLine("   - Chat: llama3.1:8b");
                    Console.WriteLine("   - Embedding: bge-m3:latest\n");

                    while (true)
                    {
                        Console.Write("Soru: ");
                        string? userQuery = Console.ReadLine()?.Trim();

                        if (string.IsNullOrEmpty(userQuery) || userQuery.ToLower() == "exit")
                            break;

                        Console.WriteLine("\n⏳ Cevap hazırlanıyor...\n");

                        try
                        {
                            string answer = await ragService.GetAnswerAsync(userQuery);
                            Console.WriteLine(answer);
                        }
                        catch (System.Net.Http.HttpRequestException ex)
                        {
                            Console.WriteLine($"❌ Ollama API hatası: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Hata: {ex.Message}");
                        }

                        Console.WriteLine("\n" + new string('-', 90) + "\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Hata: {ex.Message}");
                }
            }
        }

        public class RagService
        {
            private readonly OllamaApiClient _ollamaClient;
            private readonly string _connectionString;

            public RagService(string ollamaBaseUrl, string connectionString)
            {
                _ollamaClient = new OllamaApiClient(new Uri(ollamaBaseUrl));
                _connectionString = connectionString;
            }

            public async Task<string> GetAnswerAsync(string userQuery)
            {
                var retrievedDocs = await RetrieveAsync(userQuery, topK: 5);

                if (retrievedDocs.Count == 0)
                    return "Üzgünüm, sorunuzla ilgili yeterli bilgi bulamadım.";

                string context = string.Join("\n\n---\n\n",
                    retrievedDocs.Select((doc, i) => $"Doküman {i + 1}: {doc.Title}\n{doc.Content}"));
            
            string prompt = $"""
        Sen çok titiz, doğru bilgi veren ve asla halüsinasyon yapmayan profesyonel bir Türk finansal eğitim asistanısın.

        Kullanıcının sorusu: {userQuery}

        KULLANABİLECEĞİN TEK BAĞLAM:
        {context}

        KESİN KURALLAR (Mutlaka uy, aksi takdirde cevap verme):
        - Sadece yukarıdaki bağlamda geçen bilgileri kullan. Bağlamda olmayan hiçbir bilgi, tanım, örnek veya sayı uydurma.
        - Tanım yaparken çok net ve doğru ol.
        - Cevabı şu yapıya göre ver:
        1. Kısa ve net tanım
        2. Avantajlar (madde ile)
        3. Riskler (madde ile)
        4. Pratik bilgi (varsa)
        - Sade, akıcı ve profesyonel Türkçe kullan.
        - Kesinlikle uydurma bilgi ekleme.
        - Sonunda mutlaka şu cümleyi ekle: "this metin sadece eğitim ve bilgilendirme amaçlıdır. Yatırım tavsiyesi niteliği taşımaz."

        Cevap:
        """;





                var responseBuilder = new System.Text.StringBuilder();
                
                var generateRequest = new GenerateRequest
                {
                    Model = "llama3.1:8b",
                    Prompt = prompt,
                    Stream = true,
                    
                };
                
                await foreach (var chunk in _ollamaClient.GenerateAsync(generateRequest))
                {
                    if (chunk?.Response != null)
                        responseBuilder.Append(chunk.Response);
                }
                return responseBuilder.ToString().Trim();
            }

            private async Task<List<RetrievedDocument>> RetrieveAsync(string userQuery, int topK = 3)
            {
                // Generate embedding for the query using OllamaSharp
                var request = new EmbedRequest
                {
                    Model = "bge-m3:latest",
                    
                    Input = new List<string> { userQuery }
                };
                
                var embeddingResponse = await _ollamaClient.EmbedAsync(request);
                var queryEmbedding = embeddingResponse.Embeddings?.FirstOrDefault() ?? Array.Empty<float>();

                var documents = new List<RetrievedDocument>();

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Convert embedding to SQL Server vector format [value1, value2, ...]
                string embeddingVector = "[" + string.Join(",", queryEmbedding.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

                string sql = """
                    SELECT TOP(@topK) 
                        Id, Title, Content, Category, RiskLevel, TargetAudience, 
                        SourceType, CreatedDate,
                        VECTOR_DISTANCE('cosine', ContentVector, CAST(@queryEmbedding AS VECTOR(1024))) AS Distance
                    FROM dbo.FinancialDocuments 
                    ORDER BY Distance ASC;
                    """;

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@topK", topK);
                command.Parameters.AddWithValue("@queryEmbedding", embeddingVector);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    documents.Add(new RetrievedDocument
                    {
                        Id = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        Content = reader.GetString(2),
                        Category = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        RiskLevel = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        TargetAudience = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        SourceType = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        CreatedDate = reader.GetDateTime(7),
                        Distance = reader.GetDouble(8)
                    });
                }

                return documents;
            }
        }

        public class RetrievedDocument
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string RiskLevel { get; set; } = string.Empty;
            public string TargetAudience { get; set; } = string.Empty;
            public string SourceType { get; set; } = string.Empty;
            public DateTime CreatedDate { get; set; }
            public double Distance { get; set; }
        }
    }