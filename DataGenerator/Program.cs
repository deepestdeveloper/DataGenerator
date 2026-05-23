using Microsoft.SemanticKernel;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Net.Http.Json;

namespace SyntheticDataGenerator
{
    class Program
    {
      
        private const string OllamaEndpoint = "http://localhost:11434";
        private const string LlmModel = "llama3.1:8b";           
        private const string EmbeddingModel = "bge-m3";        
        private const int VectorDimension = 1024;

  
        private const string ConnectionString = "Server=localhost,1433;Database=Orion;User Id=sa;Password=Ggrt190724;TrustServerCertificate=True;";

        private const int TotalDocuments = 100;   

        static async Task Main(string[] args)
        {
            Console.WriteLine($"🔄 {TotalDocuments} adet sentetik finansal belge üretiliyor...\n");

            using var httpClient = new HttpClient();

            var topics = GetSampleTopics();   

            for (int i = 0; i < TotalDocuments; i++)
            {
                var topicInfo = topics[i % topics.Count];

                try
                {
                    var doc = await GenerateDocumentAsync(httpClient, topicInfo);

                    await InsertToSqlServerAsync(doc);

                    Console.WriteLine($" {i + 1:D3} - {doc.Title.Substring(0, Math.Min(70, doc.Title.Length))}...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" Hata [{i + 1}]: {ex.Message}");
                }

           
                await Task.Delay(1200);
            }

            Console.WriteLine("\n Tüm sentetik veri üretimi tamamlandı!");
        }

        // ====================== DOKÜMAN ÜRETME ======================
        private static async Task<FinancialDocument> GenerateDocumentAsync(
            HttpClient httpClient,
            TopicInfo topic)
        {
            string prompt = $"""
                Sen deneyimli, tarafsız ve SPK uyumlu bir Türk finansal eğitimcisisin.

                Aşağıdaki konu hakkında eğitici, anlaşılır ve gerçekçi bir metin yaz.
                - Dil: Akıcı, doğal Türkçe olsun.
                - Uzunluk: Yaklaşık 600-750 kelime.
                - Ton: Bilgilendirici, abartısız ve uyarıcı.

                Konu: {topic.Topic}
                Kategori: {topic.Category}
                Risk Seviyesi: {topic.RiskLevel}
                Hedef Kitle: {topic.TargetAudience}

                Metinde şu unsurları mutlaka bulunsun:
                - Kavramın tanımı ve Türkiye'deki durumu
                - Enflasyon etkisi
                - Avantajlar ve riskler
                - Pratik örnekler
                - Sonunda mutlaka şu cümleyi ekle: "Bu metin sadece eğitim ve bilgilendirme amaçlıdır. Yatırım tavsiyesi niteliği taşımaz."

                Sadece metni yaz, başka hiçbir açıklama ekleme.
                """;

            // İçerik üret - Ollama API'ye çağrı
            var chatRequest = new
            {
                model = LlmModel,
                prompt = prompt,
                stream = false
            };

            var chatResponse = await httpClient.PostAsJsonAsync($"{OllamaEndpoint}/api/generate", chatRequest);
            chatResponse.EnsureSuccessStatusCode();

            var chatJsonString = await chatResponse.Content.ReadAsStringAsync();
            var chatJson = JsonDocument.Parse(chatJsonString).RootElement;
            string content = chatJson.GetProperty("response").GetString() ?? "";

         
            var embeddingRequest = new
            {
                model = EmbeddingModel,
                prompt = content
            };

            var embeddingResponse = await httpClient.PostAsJsonAsync($"{OllamaEndpoint}/api/embeddings", embeddingRequest);
            embeddingResponse.EnsureSuccessStatusCode();

            var embeddingJsonString = await embeddingResponse.Content.ReadAsStringAsync();
            var embeddingJson = JsonDocument.Parse(embeddingJsonString).RootElement;
            var embeddingArray = embeddingJson.GetProperty("embedding").EnumerateArray()
                .Select(x => (float)x.GetDouble())
                .ToArray();

            return new FinancialDocument
            {
                Title = topic.Title,
                Content = content,
                Embedding = embeddingArray,
                Category = topic.Category,
                SubCategory = topic.SubCategory,
                RiskLevel = topic.RiskLevel,
                AssetType = topic.AssetType,
                TargetAudience = topic.TargetAudience,
                GoalType = topic.GoalType,
                Keywords = string.Join(", ", topic.Keywords),
                SourceType = "Sentetik"
            };
        }

    
        private static async Task InsertToSqlServerAsync(FinancialDocument doc)
        {
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = """
                INSERT INTO FinancialDocuments 
                (Title, Content, ContentVector, Category, SubCategory, RiskLevel, 
                 AssetType, TargetAudience, GoalType, Keywords, SourceType, CreatedDate)
                VALUES 
                (@Title, @Content, @Embedding, 
                 @Category, @SubCategory, @RiskLevel, @AssetType, 
                 @TargetAudience, @GoalType, @Keywords, @SourceType, GETDATE())
                """;

            using var command = new SqlCommand(sql, connection);

            // Embedding → byte array
            var embeddingBytes = new byte[doc.Embedding.Length * 4];
            Buffer.BlockCopy(doc.Embedding, 0, embeddingBytes, 0, embeddingBytes.Length);

            command.Parameters.AddWithValue("@Title", doc.Title);
            command.Parameters.AddWithValue("@Content", doc.Content);
            command.Parameters.AddWithValue("@Embedding", embeddingBytes);
            command.Parameters.AddWithValue("@Category", doc.Category ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@SubCategory", doc.SubCategory ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@RiskLevel", doc.RiskLevel);
            command.Parameters.AddWithValue("@AssetType", doc.AssetType ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TargetAudience", doc.TargetAudience);
            command.Parameters.AddWithValue("@GoalType", doc.GoalType ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Keywords", doc.Keywords ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@SourceType", doc.SourceType);

            await command.ExecuteNonQueryAsync();
        }

        // ====================== ÖRNEK KONULAR ======================
       private static List<TopicInfo> GetSampleTopics()
{
    return new List<TopicInfo>
    {
        // Önceki örnekler
        new TopicInfo { Title = "Yüksek Enflasyon Ortamında Gram Altın Yatırımı", Topic = "Yüksek enflasyon ortamında gram altın yatırımı", Category = "Altın", SubCategory = "Kıymetli Madenler", RiskLevel = "Düşük", AssetType = "Altın", TargetAudience = "Yeni Başlayan", GoalType = "Enflasyona Karşı Koruma", Keywords = new[] { "gram altın", "enflasyon", "fiziki altın", "tasarruf" } },
        new TopicInfo { Title = "BIST 100 Endeksi ve Sektörel Dağılım Stratejileri", Topic = "BIST 100 endeksi ve sektörel dağılım stratejileri", Category = "Hisse Senetleri", SubCategory = "Borsa", RiskLevel = "Orta", AssetType = "Hisse", TargetAudience = "Orta Seviye", GoalType = "Uzun Vadeli Büyüme", Keywords = new[] { "BIST 100", "sektör rotasyonu", "bankacılık", "enerji sektörü" } },

        // ====================== YENİ 30 KONU ======================
        new TopicInfo { Title = "Türkiye'de Altın Fonları ve ETF'ler Karşılaştırması", Topic = "Altın fonları ve ETF'ler", Category = "Altın", SubCategory = "Yatırım Araçları", RiskLevel = "Düşük", AssetType = "Fon", TargetAudience = "Yeni Başlayan", GoalType = "Enflasyona Karşı Koruma", Keywords = new[] { "altın fonu", "ETF", "TEFAS" } },
        new TopicInfo { Title = "Enflasyon Muhasebesi ve Bireysel Yatırımcı Etkileri", Topic = "Enflasyon muhasebesi", Category = "Makroekonomi", SubCategory = "Enflasyon", RiskLevel = "Düşük", AssetType = "", TargetAudience = "Orta Seviye", GoalType = "Bilgi", Keywords = new[] { "enflasyon muhasebesi", "TÜFE", "satın alma gücü" } },
        new TopicInfo { Title = "BIST'te Bankacılık Sektörü 2026 Değerlendirmesi", Topic = "Bankacılık sektörü", Category = "Hisse Senetleri", SubCategory = "Sektör Analizi", RiskLevel = "Orta", AssetType = "Hisse", TargetAudience = "Deneyimli", GoalType = "Büyüme", Keywords = new[] { "banka hisseleri", "faiz", "karlılık" } },
        new TopicInfo { Title = "BES Fonları ve Devlet Katkısı Avantajları", Topic = "BES fonları", Category = "Fonlar", SubCategory = "Emeklilik", RiskLevel = "Düşük", AssetType = "Fon", TargetAudience = "Yeni Başlayan", GoalType = "Uzun Vadeli Tasarruf", Keywords = new[] { "BES", "devlet katkısı", "emeklilik" } },
        new TopicInfo { Title = "Dolar ve Euro Yatırımı Riskleri ve Fırsatları", Topic = "Döviz yatırımı", Category = "Döviz", SubCategory = "", RiskLevel = "Orta", AssetType = "Döviz", TargetAudience = "Orta Seviye", GoalType = "Koruma", Keywords = new[] { "USD", "EUR", "döviz kuru" } },
        new TopicInfo { Title = "Portföyde Çeşitlendirme Stratejileri", Topic = "Portföy çeşitlendirme", Category = "Portföy Yönetimi", SubCategory = "", RiskLevel = "Orta", AssetType = "", TargetAudience = "Tüm Seviyeler", GoalType = "Risk Azaltma", Keywords = new[] { "çeşitlendirme", "portföy", "risk yönetimi" } },
        new TopicInfo { Title = "SPK Lisanslı Yatırım Danışmanlığı ve Robo-Advisor Sınırları", Topic = "SPK kuralları", Category = "SPK Kuralları", SubCategory = "Yasal", RiskLevel = "Düşük", AssetType = "", TargetAudience = "Tüm Seviyeler", GoalType = "Bilgi", Keywords = new[] { "SPK", "yatırım danışmanlığı", "robo advisor" } },
        new TopicInfo { Title = "Kısa Vadeli Tahvil ve Bono Yatırımları", Topic = "Tahvil bono", Category = "Sabit Getirili", SubCategory = "", RiskLevel = "Düşük", AssetType = "Tahvil", TargetAudience = "Yeni Başlayan", GoalType = "Likidite", Keywords = new[] { "tahvil", "bono", "sabit getiri" } },
        new TopicInfo { Title = "Teknoloji ve Yazılım Şirketi Hisseleri Analizi", Topic = "Teknoloji sektörü", Category = "Hisse Senetleri", SubCategory = "Sektör Analizi", RiskLevel = "Yüksek", AssetType = "Hisse", TargetAudience = "Deneyimli", GoalType = "Büyüme", Keywords = new[] { "teknoloji hissesi", "yazılım", "BIST" } },
        new TopicInfo { Title = "Yüksek Faiz Ortamında Mevduat vs. Fon Karşılaştırması", Topic = "Mevduat ve fon karşılaştırması", Category = "Sabit Getirili", SubCategory = "", RiskLevel = "Düşük", AssetType = "Mevduat", TargetAudience = "Orta Seviye", GoalType = "Güvenli Getiri", Keywords = new[] { "mevduat", "faiz", "fon" } },

        new TopicInfo { Title = "Altın Fiyatlarını Etkileyen Küresel Faktörler", Topic = "Altın fiyatlarını etkileyen faktörler", Category = "Altın", SubCategory = "Makroekonomi", RiskLevel = "Düşük", AssetType = "Altın", TargetAudience = "Orta Seviye", GoalType = "Bilgi", Keywords = new[] { "altın fiyatı", "ABD doları", "jeopolitik risk" } },
        new TopicInfo { Title = "Gayrimenkul Yatırım Fonları (GYF) ve Riskleri", Topic = "Gayrimenkul yatırım fonları", Category = "Gayrimenkul", SubCategory = "", RiskLevel = "Orta", AssetType = "GYF", TargetAudience = "Deneyimli", GoalType = "Çeşitlendirme", Keywords = new[] { "GYF", "gayrimenkul", "REIT" } },
        new TopicInfo { Title = "TCMB Faiz Kararları ve Borsa Etkisi", Topic = "TCMB faiz kararları", Category = "Makroekonomi", SubCategory = "", RiskLevel = "Orta", AssetType = "", TargetAudience = "Orta Seviye", GoalType = "Bilgi", Keywords = new[] { "TCMB", "faiz", "BIST etkisi" } },
        new TopicInfo { Title = "Yabancı Yatırımcı ve BIST Akımları", Topic = "Yabancı yatırımcı akımları", Category = "Borsa", SubCategory = "", RiskLevel = "Yüksek", AssetType = "Hisse", TargetAudience = "Deneyimli", GoalType = "Piyasa Takibi", Keywords = new[] { "yabancı yatırımcı", "portföy akımı" } },
        new TopicInfo { Title = "Emeklilik Fonlarında Hisse Oranı Optimizasyonu", Topic = "Emeklilik fonlarında hisse oranı", Category = "Fonlar", SubCategory = "Emeklilik", RiskLevel = "Orta", AssetType = "Fon", TargetAudience = "Orta Seviye", GoalType = "Uzun Vadeli", Keywords = new[] { "BES", "hisse oranı", "fon optimizasyonu" } },
        new TopicInfo { Title = "Kripto Paralar ve Regülasyon Durumu", Topic = "Kripto paralar", Category = "Kripto", SubCategory = "", RiskLevel = "Çok Yüksek", AssetType = "Kripto", TargetAudience = "Deneyimli", GoalType = "Spekülasyon", Keywords = new[] { "Bitcoin", "kripto", "SPK regülasyon" } },
        new TopicInfo { Title = "Enflasyona Karşı En Etkili 5 Yatırım Aracı", Topic = "Enflasyona karşı etkili araçlar", Category = "Portföy Yönetimi", SubCategory = "", RiskLevel = "Orta", AssetType = "", TargetAudience = "Yeni Başlayan", GoalType = "Koruma", Keywords = new[] { "enflasyon", "altın", "döviz", "hisse" } },
        new TopicInfo { Title = "Stopaj Vergisi ve Yatırım Maliyetleri", Topic = "Stopaj vergisi", Category = "Vergi", SubCategory = "", RiskLevel = "Düşük", AssetType = "", TargetAudience = "Tüm Seviyeler", GoalType = "Maliyet Optimizasyonu", Keywords = new[] { "stopaj", "vergi", "yatırım maliyeti" } },
        new TopicInfo { Title = "Davranışsal Finans ve Yaygın Yatırım Hataları", Topic = "Davranışsal finans", Category = "Davranışsal Finans", SubCategory = "", RiskLevel = "Düşük", AssetType = "", TargetAudience = "Tüm Seviyeler", GoalType = "Eğitim", Keywords = new[] { "FOMO", "panic selling", "davranışsal hata" } },
        new TopicInfo { Title = "Enerji Sektörü Hisseleri ve Küresel Petrol Fiyatları", Topic = "Enerji sektörü", Category = "Hisse Senetleri", SubCategory = "Sektör Analizi", RiskLevel = "Yüksek", AssetType = "Hisse", TargetAudience = "Deneyimli", GoalType = "Büyüme", Keywords = new[] { "enerji hissesi", "petrol fiyatı" } },
        new TopicInfo { Title = "Perakende Sektörü ve Tüketici Güven Endeksi", Topic = "Perakende sektörü", Category = "Hisse Senetleri", SubCategory = "Sektör Analizi", RiskLevel = "Orta", AssetType = "Hisse", TargetAudience = "Orta Seviye", GoalType = "Büyüme", Keywords = new[] { "perakende", "tüketici güveni" } },

        new TopicInfo { Title = "2026 TCMB Enflasyon Hedefleri ve Etkileri", Topic = "TCMB enflasyon hedefleri", Category = "Makroekonomi", SubCategory = "", RiskLevel = "Düşük", AssetType = "", TargetAudience = "Orta Seviye", GoalType = "Bilgi", Keywords = new[] { "TCMB", "enflasyon hedefi", "2026" } },
        new TopicInfo { Title = "Fon Alım-Satım Maliyetleri ve Teşvikler", Topic = "Fon alım-satım maliyetleri", Category = "Fonlar", SubCategory = "", RiskLevel = "Düşük", AssetType = "Fon", TargetAudience = "Yeni Başlayan", GoalType = "Maliyet", Keywords = new[] { "fon maliyeti", "TEFAS", "teşvik" } },
        new TopicInfo { Title = "Jeopolitik Riskler ve Altın Talebi", Topic = "Jeopolitik riskler", Category = "Altın", SubCategory = "", RiskLevel = "Orta", AssetType = "Altın", TargetAudience = "Orta Seviye", GoalType = "Koruma", Keywords = new[] { "jeopolitik", "altın talebi" } },
        new TopicInfo { Title = "Holding Şirketleri ve Çeşitlendirme Avantajı", Topic = "Holding şirketleri", Category = "Hisse Senetleri", SubCategory = "", RiskLevel = "Orta", AssetType = "Hisse", TargetAudience = "Deneyimli", GoalType = "Çeşitlendirme", Keywords = new[] { "holding", "çeşitlendirme" } },
        new TopicInfo { Title = "Yatırımcı Profiline Göre Portföy Örnekleri", Topic = "Yatırımcı profiline göre portföy", Category = "Portföy Yönetimi", SubCategory = "", RiskLevel = "Orta", AssetType = "", TargetAudience = "Tüm Seviyeler", GoalType = "Kişiselleştirme", Keywords = new[] { "portföy örneği", "risk profili" } },
        new TopicInfo { Title = "Merkez Bankası Rezervleri ve TL Değeri", Topic = "Merkez bankası rezervleri", Category = "Makroekonomi", SubCategory = "", RiskLevel = "Orta", AssetType = "", TargetAudience = "Orta Seviye", GoalType = "Bilgi", Keywords = new[] { "rezerv", "TL değeri" } },
        new TopicInfo { Title = "Sürdürülebilirlik ve Yeşil Finans Ürünleri", Topic = "Yeşil finans", Category = "Sürdürülebilirlik", SubCategory = "", RiskLevel = "Orta", AssetType = "Fon", TargetAudience = "Deneyimli", GoalType = "Sürdürülebilirlik", Keywords = new[] { "yeşil tahvil", "sürdürülebilir fon" } },
        new TopicInfo { Title = "Kısa Vadeli Ticaret ve Teknik Analiz Temelleri", Topic = "Teknik analiz", Category = "Borsa", SubCategory = "", RiskLevel = "Yüksek", AssetType = "Hisse", TargetAudience = "Deneyimli", GoalType = "Kısa Vadeli", Keywords = new[] { "teknik analiz", "trend", "destek direnç" } },
        new TopicInfo { Title = "Yatırım Fonlarında Yönetim Ücreti ve Performans", Topic = "Fon yönetim ücreti", Category = "Fonlar", SubCategory = "", RiskLevel = "Düşük", AssetType = "Fon", TargetAudience = "Yeni Başlayan", GoalType = "Maliyet", Keywords = new[] { "yönetim ücreti", "fon performansı" } },
        new TopicInfo { Title = "2026 Ekonomik Görünüm ve Yatırım Stratejileri", Topic = "2026 ekonomik görünüm", Category = "Makroekonomi", SubCategory = "", RiskLevel = "Orta", AssetType = "", TargetAudience = "Orta Seviye", GoalType = "Strateji", Keywords = new[] { "2026 ekonomi", "görünüm" } }
    };
}
    }


    public class FinancialDocument
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string Category { get; set; } = string.Empty;
        public string? SubCategory { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string? AssetType { get; set; }
        public string TargetAudience { get; set; } = string.Empty;
        public string? GoalType { get; set; }
        public string? Keywords { get; set; }
        public string SourceType { get; set; } = "Sentetik";
    }

    public class TopicInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public string GoalType { get; set; } = string.Empty;
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }
}
