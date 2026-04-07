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
                using var client = new HttpClient();
                var res = await client.PostAsync("https://dict.hochi.org.tw:5165/api/admin/reload", null);
                var content = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    ReloadStatus = $"✅ 重新載入成功：{content}";
                }
                else
                {
                    ReloadStatus = $"❌ 重新載入失敗：{content}";
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

                var url = $"https://dict.hochi.org.tw:5165/api/admin/paragraphs?page={currentPage}&pageSize={pageSize}";
                LogDebug($"https://dict.hochi.org.tw:5165/api/admin/paragraphs?page={currentPage}&pageSize={pageSize}");
                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseBody);

                var paragraphsJson = json["paragraphs"] as JObject;
                foreach (var prop in paragraphsJson.Properties())
                {
                    Paragraphs.Add(new ParagraphInfo
                    {
                        Id = prop.Name,
                        Text = prop.Value?.ToString()
                    });
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

    }
}
