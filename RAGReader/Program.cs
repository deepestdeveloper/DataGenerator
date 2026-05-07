using Microsoft.Extensions.AI;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGReader
{
    public class RagRetriever
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly string _connectionString; 

        public RagRetriever(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, string connectionString)
        {
            _embeddingGenerator = embeddingGenerator;
            _connectionString = connectionString;
        }

        public async Task<List<RetrievedDocument>> RetrieveAsync(string userQuery, int topK = 6)
        {
            var embeddings = await _embeddingGenerator.GenerateAsync(new[] { userQuery });
            var queryEmbedding = embeddings[0].Vector.ToArray();

            var documents = new List<RetrievedDocument>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql = """
                SELECT
                    Id,
                    Title,
                    Content,
                    ContentVector,
                    Category,
                    RiskLevel,
                    TargetAudience,
                    SourceType,
                    CreatedDate
                FROM dbo.FinancialDocuments;
                """;

            using var command = new SqlCommand(sql, connection);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new RetrievedDocument
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Content = reader.GetString(2),
                    Category = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    RiskLevel = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    TargetAudience = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    SourceType = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    CreatedDate = reader.GetDateTime(8)
                };

                if (!reader.IsDBNull(3))
                {
                    var vectorBytes = (byte[])reader.GetValue(3);
                    row.Embedding = ConvertBytesToVector(vectorBytes);
                    row.Distance = ComputeCosineDistance(queryEmbedding, row.Embedding);
                    documents.Add(row);
                }
            }

            return documents.OrderBy(x => x.Distance).Take(topK).ToList();
        }

        private static float[] ConvertBytesToVector(byte[] bytes)
        {
            var vector = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
            return vector;
        }

        private static double ComputeCosineDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                return double.MaxValue;
            }

            double dot = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0 || normB == 0)
            {
                return double.MaxValue;
            }

            return 1.0 - (dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
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
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public double Distance { get; set; }

        public override string ToString() => $"[{Distance:F4}] {Title}";
    }

    internal class Program
    {
        private const string ConnectionString = "Server=localhost,1433;Database=Orion;User Id=sa;Password=Ggrt190724;TrustServerCertificate=True;";

        static async Task Main(string[] args)
        {
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = new OllamaEmbeddingGenerator(new Uri("http://localhost:11434"), "bge-m3");
            var retriever = new RagRetriever(embeddingGenerator, ConnectionString);

            var results = await retriever.RetrieveAsync("Gram altın almak mantıklı mı?", topK: 3);
            foreach (var doc in results)
            {
                Console.WriteLine(doc);
            }
        }
    }
}
