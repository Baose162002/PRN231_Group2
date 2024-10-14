using BusinessObject.Dto.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Net;

namespace PRN231_2_EventFlowerExchange_FE.Pages.UserPages
{
    public class UserDetailModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseApiUrl;

        public UserDetailModel(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseApiUrl = configuration["ApiSettings:BaseUrl"];
        }

        public UserResponseDto User { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var token = HttpContext.Session.GetString("JWTToken");

            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login/Login");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"{_baseApiUrl}/user/{id}");

            if (response.IsSuccessStatusCode)
            {
                User = await response.Content.ReadFromJsonAsync<UserResponseDto>();
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ModelState.AddModelError(string.Empty, "Token is invalid or has expired.");
                return RedirectToPage("/Login/Login");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"Error loading user details: {response.ReasonPhrase}");
                return RedirectToPage("/UserPages/UserIndex");
            }

            return Page();
        }
    }
}