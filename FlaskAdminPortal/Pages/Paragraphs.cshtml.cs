using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;

namespace FlaskAdminPortal.Pages
{
    public class ParagraphsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ParagraphsModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public int currentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int pageSize { get; set; } = 50;

        [BindProperty(SupportsGet = true)]
        public string? fileName { get; set; }

        [BindProperty]
        public string ReloadStatus { get; set; }

        public class ParagraphInfo
        {
            public string Id { get; set; }
            public string Text { get; set; }
        }

        public List<ParagraphInfo> Paragraphs { get; set; } = new();
        public List<string> SourceFiles { get; set; } = new();
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public int TotalItems { get; set; }
        public int TotalPages => pageSize <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / pageSize);
        public string AdminApiKeyForClient => _configuration["AdminApi:ApiKey"] ?? string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                if (currentPage < 1)
                {
                    currentPage = 1;
                }

                if (pageSize <= 0)
                {
                    pageSize = 50;
                }

                Paragraphs.Clear();
                var client = CreateAdminClient();

                await LoadSourceFilesAsync(client);

                var url = string.IsNullOrWhiteSpace(fileName)
                    ? $"{GetAdminApiBaseUrl()}/paragraphs?page={currentPage}&pageSize={pageSize}"
                    : $"{GetAdminApiBaseUrl()}/paragraphs-by-file?fileName={Uri.EscapeDataString(fileName)}";

                var response = await client.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    var (code, message) = ParseErrorBody(responseBody);
                    ErrorMessage = $"錯誤：無法取得段落資料（HTTP {(int)response.StatusCode} / {code ?? "UNKNOWN"}）：{message}";
                    return Page();
                }

                var json = JObject.Parse(responseBody);
                var paragraphsNode = json["paragraphs"];

                if (paragraphsNode is JObject paragraphsObj)
                {
                    foreach (var prop in paragraphsObj.Properties())
                    {
                        Paragraphs.Add(new ParagraphInfo
                        {
                            Id = prop.Name,
                            Text = prop.Value?.ToString() ?? string.Empty
                        });
                    }
                }
                else if (paragraphsNode is JArray paragraphsArray)
                {
                    foreach (var item in paragraphsArray.OfType<JObject>())
                    {
                        Paragraphs.Add(new ParagraphInfo
                        {
                            Id = item["id"]?.ToString() ?? string.Empty,
                            Text = item["text"]?.ToString() ?? string.Empty
                        });
                    }
                }

                TotalItems = json["total"]?.Value<int>() ?? Paragraphs.Count;
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = $"錯誤：無法取得段落資料。{ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostReloadAsync()
        {
            try
            {
                var client = CreateAdminClient();
                var res = await client.PostAsync($"{GetAdminApiBaseUrl()}/reload", null);
                var content = await res.Content.ReadAsStringAsync();
                var (code, message) = ParseErrorBody(content);

                if (res.IsSuccessStatusCode)
                {
                    ReloadStatus = $"✅ 重新載入成功：{content}";
                }
                else
                {
                    ReloadStatus = $"❌ 重新載入失敗（HTTP {(int)res.StatusCode} / {code ?? "UNKNOWN"}）：{message}";
                }
            }
            catch (Exception ex)
            {
                ReloadStatus = $"❌ 發生錯誤：{ex.Message}";
            }

            return await OnGetAsync();
        }

        private async Task LoadSourceFilesAsync(HttpClient client)
        {
            var res = await client.GetAsync($"{GetAdminApiBaseUrl()}/source-files");
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                return;
            }

            var json = JObject.Parse(body);
            var files = (json["source_files"] ?? json["files"]) as JArray;
            if (files == null)
            {
                return;
            }

            SourceFiles = files
                .Select(ExtractFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? ExtractFileName(JToken? token)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.String)
            {
                return token.ToString();
            }

            if (token is JObject obj)
            {
                return obj["file_name"]?.ToString()
                    ?? obj["fileName"]?.ToString()
                    ?? obj["name"]?.ToString();
            }

            return token.ToString();
        }

        private HttpClient CreateAdminClient()
        {
            var client = _httpClientFactory.CreateClient();
            var apiKey = _configuration["AdminApi:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Remove("X-Admin-API-Key");
                client.DefaultRequestHeaders.Add("X-Admin-API-Key", apiKey);
            }

            return client;
        }

        private string GetAdminApiBaseUrl()
            => _configuration["AdminApi:BaseUrl"]?.TrimEnd('/') ?? "https://dict.hochi.org.tw:5165/api/admin";

        private static (string? code, string message) ParseErrorBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return (null, "上游服務未回傳內容");
            }

            try
            {
                var json = JObject.Parse(body);
                var code = json["code"]?.ToString();
                var message = json["message"]?.ToString()
                    ?? json["error"]?.ToString()
                    ?? json["detail"]?.ToString()
                    ?? body;
                return (code, message);
            }
            catch
            {
                return (null, body);
            }
        }
    }
}
