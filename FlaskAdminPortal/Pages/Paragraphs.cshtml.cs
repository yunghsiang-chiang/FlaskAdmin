using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        [BindProperty]
        public string ReloadStatus { get; set; }

        public async Task<IActionResult> OnPostReloadAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
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

            return await OnGetAsync(); // Reload 後重新整理段落資料
        }


        public class ParagraphInfo
        {
            public string Id { get; set; }
            public string Text { get; set; }
        }

        public List<ParagraphInfo> Paragraphs { get; set; } = new();
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public int TotalItems { get; set; }
        public int TotalPages => (int)System.Math.Ceiling((double)TotalItems / pageSize);

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                LogDebug($"🌐 DEBUG: Page = {currentPage}, PageSize = {pageSize}");
                Paragraphs.Clear(); // 清空前一頁的資料

                var url = $"{GetAdminApiBaseUrl()}/paragraphs?page={currentPage}&pageSize={pageSize}";
                LogDebug(url);
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    var (code, message) = ParseErrorBody(responseBody);
                    ErrorMessage = $"錯誤：無法取得段落資料（HTTP {(int)response.StatusCode} / {code ?? "UNKNOWN"}）：{message}";
                    return Page();
                }

                var json = JObject.Parse(responseBody);
                var paragraphsJson = json["paragraphs"] as JObject;
                if (paragraphsJson != null)
                {
                    foreach (var prop in paragraphsJson.Properties())
                    {
                        Paragraphs.Add(new ParagraphInfo
                        {
                            Id = prop.Name,
                            Text = prop.Value?.ToString()
                        });
                    }
                }

                TotalItems = json["total"]?.Value<int>() ?? 0;
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = $"錯誤：無法取得段落資料。{ex.Message}";
            }

            return Page();
        }

        private void LogDebug(string message)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "log.txt");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now}] {message}\n");
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
