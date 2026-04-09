using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;

namespace FlaskAdminPortal.Pages
{
    public class SampledFilesModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public SampledFilesModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public List<string> SampledFiles { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public string AdminApiKeyForClient => _configuration["AdminApi:ApiKey"] ?? string.Empty;

        public async Task OnGetAsync()
        {
            try
            {
                var client = CreateAdminClient();
                var res = await client.GetAsync($"{GetAdminApiBaseUrl()}/sampled-files");
                var body = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    ErrorMessage = $"錯誤：無法取得取樣檔案清單（HTTP {(int)res.StatusCode}）。";
                    return;
                }

                var json = JObject.Parse(body);
                var files = (json["sampled_files"] ?? json["files"]) as JArray;
                if (files == null)
                {
                    return;
                }

                SampledFiles = files
                    .Select(ExtractFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = $"錯誤：無法取得取樣檔案清單。{ex.Message}";
            }
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
    }
}
