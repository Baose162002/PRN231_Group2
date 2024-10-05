﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Globalization; // Added for date conversion
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BusinessObject.DTO.Request;
using System.Security.Claims;
using BusinessObject.DTO.Response;
using System.Net.Http.Headers;
using BusinessObject;
using PRN231_2_EventFlowerExchange_FE.Service;

namespace PRN231_2_EventFlowerExchange_FE.Pages.BatchPages
{
    public class CreateBatchModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CreateBatchModel> _logger;

        public CreateBatchModel(HttpClient httpClient, IConfiguration configuration, ILogger<CreateBatchModel> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public CreateBatchDTOUpdateImg Input { get; set; }

        [BindProperty]
        public IFormFile ImageFlower { get; set; }
        public List<CompanyDTO> Companies { get; set; } = new List<CompanyDTO>();

        public async Task<IActionResult> OnGetAsync()
        {
            Input = new CreateBatchDTOUpdateImg();

            var token = HttpContext.Session.GetString("JWTToken");
            var role = HttpContext.Session.GetString("UserRole");


            if (role == "Admin") // Assuming you store the role as a string
            {
                Companies = await GetCompaniesAsync(token); // Fetch companies
            }

            // Additional logic for loading the event details, if applicable
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Input.ImgFlower");

            try
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                var token = HttpContext.Session.GetString("JWTToken");
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("JWT Token is missing from session");
                    ModelState.AddModelError(string.Empty, "You are not authenticated. Please log in.");
                    return Page();
                }

                // First, create the flower
                var flowerInput = new CreateFlowerDTO
                {
                    Name = Request.Form["FlowerName"],
                    Type = Input.FlowerType,
                    Description = Input.Description,
                    PricePerUnit = Input.PricePerUnit,
                    Origin = Request.Form["Origin"],
                    Color = Request.Form["Color"]
                };
                if (ImageFlower != null && ImageFlower.Length > 0)
                {
                    var cloudinaryService = new CloudinaryService(_configuration);
                    var imageUrl = await cloudinaryService.UploadImageAsync(ImageFlower);

                    // Set the uploaded image URL to the flower DTO
                    flowerInput.Image = imageUrl;
                }
                var flowerJson = JsonSerializer.Serialize(flowerInput);
                var flowerContent = new StringContent(flowerJson, Encoding.UTF8, "application/json");

                var flowerApiUrl = $"{_configuration["ApiSettings:BaseUrl"]}/flower"; // Update with your flower API URL
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var flowerResponse = await _httpClient.PostAsync(flowerApiUrl, flowerContent);

                if (flowerResponse.IsSuccessStatusCode)
                {
                    var responseContent = await flowerResponse.Content.ReadAsStringAsync();
                    var createdFlower = JsonSerializer.Deserialize<ListFlowerDTO>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); // Define this class to match the response
                    Input.FlowerId = createdFlower.FlowerId; // Set the FlowerId in the BatchInput
                }
                else
                {
                    var errorContent = await flowerResponse.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"Error creating flower. Status code: {errorContent}");
                    return Page();
                }



                // Convert EntryDate to the required format "dd/MM/yyyy"
                if (DateTime.TryParseExact(Input.EntryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    Input.EntryDate = parsedDate.ToString("dd/MM/yyyy");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid date format.");
                    return Page();
                }

                var userRole = HttpContext.Session.GetString("UserRole");

                if (userRole == "Admin")
                {
                    // If the user is an admin, the CompanyId should be provided by the dropdown
                    // Validate that the CompanyId was selected
                    if (Input.CompanyId <= 0) // or check for null if nullable
                    {
                        ModelState.AddModelError(string.Empty, "Please select a valid company.");
                        return Page();
                    }
                }
                else if (userRole == "Seller")
                {
                    // Logic for fetching CompanyId for Company role remains unchanged
                    var userIdString = HttpContext.Session.GetString("UserId");
                    if (string.IsNullOrEmpty(userIdString))
                    {
                        ModelState.AddModelError(string.Empty, "User ID not found in session.");
                        return Page();
                    }

                    if (!int.TryParse(userIdString, out int userId))
                    {
                        ModelState.AddModelError(string.Empty, "Invalid User ID.");
                        return Page();
                    }

                    var company = await GetCompanyByUserIdAsync(userIdString);
                    if (company == null)
                    {
                        ModelState.AddModelError(string.Empty, "Company not found for the user.");
                        return Page();
                    }
                    Input.CompanyId = company.CompanyId; // Set the Company ID for Company role
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid user role.");
                    return Page();
                }


                var json = JsonSerializer.Serialize(Input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var apiUrl = $"{_configuration["ApiSettings:BaseUrl"]}/batch";

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Batch created successfully!";
                    return RedirectToPage("/BatchPages/BatchIndex");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"Error creating batch. Status code: {errorContent}");
                    return Page();
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while creating batch");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return Page();
            }
        }

        private async Task<List<CompanyDTO>> GetCompaniesAsync(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var apiUrl = $"{_configuration["ApiSettings:BaseUrl"]}/company"; // Adjust API URL accordingly

            var response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CompanyDTO>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return new List<CompanyDTO>(); // Return empty list on failure
        }

        private async Task<CompanyDTO> GetCompanyByUserIdAsync(string userId)
        {
            var token = HttpContext.Session.GetString("JWTToken");
            if (string.IsNullOrEmpty(token))
            {
                throw new UnauthorizedAccessException("JWT Token is missing from session");
            }

            // Thiết lập Header Authorization với JWT Token
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Tạo URL cho API để lấy thông tin công ty
            var apiUrl = $"{_configuration["ApiSettings:BaseUrl"]}/Company/user/{userId}";

            // Gọi API để lấy thông tin công ty
            var response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var company = JsonSerializer.Deserialize<CompanyDTO>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return company;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error fetching company information. Status code: {response.StatusCode}, Content: {errorContent}");
                return null;



            }
        }

    }


}
