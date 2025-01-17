﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusinessObject.DTO.Response;
using BusinessObject;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PRN231_2_EventFlowerExchange_FE.Pages.OrderPages
{
    public class OrderUser : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseApiUrl;

        public OrderUser(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseApiUrl = configuration["ApiSettings:BaseUrl"];
        }

        public List<ListOrderDTO> Orders { get; set; }
        public int UsersIds { get; set; }

        public async Task<IActionResult> OnGetAsync(int id, DateTime? startDate, DateTime? endDate)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetString("UserId");

            if (userRole == "Seller")
            {
                var response1 = await _httpClient.GetAsync($"{_baseApiUrl}/api/order/user?userId={userId}&userRole={userRole}");

                if (response1.IsSuccessStatusCode)
                {
                    var orderOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    UsersIds = int.Parse(userId);
                    Orders = await response1.Content.ReadFromJsonAsync<List<ListOrderDTO>>(orderOptions); // Deserialize as a list of orders
                                                                                                          // Apply date filter after fetching orders if needed
                    if (startDate.HasValue || endDate.HasValue)
                    {
                        var filteredOrders = Orders
                            .Where(order =>
                            {
                                DateTime orderDate = DateTime.Parse(order.OrderDate).Date;
                                return (!startDate.HasValue || orderDate >= startDate.Value.Date) &&
                                       (!endDate.HasValue || orderDate <= endDate.Value.Date);
                            })
                            .ToList();

                        Orders = filteredOrders;
                    }
                }
            }
            else
            {
                // Standard query for regular user
                string odataQuery = $"/odata/order?$filter=CustomerId eq {userId} &$expand=Customer,OrderDetails($expand=Flower)";


                // Send the request with OData query
                var response = await _httpClient.GetAsync($"{_baseApiUrl}{odataQuery}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var orderOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    UsersIds = int.Parse(userId);
                    var odataResponse = JsonSerializer.Deserialize<ODataResponse<ListOrderDTO>>(jsonString, orderOptions);
                    Orders = odataResponse.Value;

                    // Apply date filter after fetching orders if needed
                    if (startDate.HasValue || endDate.HasValue)
                    {
                        var filteredOrders = Orders
                            .Where(order =>
                            {
                                DateTime orderDate = DateTime.Parse(order.OrderDate).Date;
                                return (!startDate.HasValue || orderDate >= startDate.Value.Date) &&
                                       (!endDate.HasValue || orderDate <= endDate.Value.Date);
                            })
                            .ToList();

                        Orders = filteredOrders;
                    }
                }
                //else
                //{
                //    return NotFound("Orders not found.");
                //}
            }

            return Page();
        }


        public string GetOrderStatusClass(string status)
        {
            return status switch
            {
                "Pending" => "bg-yellow-100 text-yellow-800",
                "Confirmed" => "bg-blue-100 text-blue-800",
                "Dispatched" => "bg-purple-100 text-purple-800",
                "Delivered" => "bg-green-100 text-green-800",
                _ => "bg-gray-100 text-gray-800"
            };
        }

        public class ODataResponse<T>
        {
            [JsonPropertyName("@odata.context")]
            public string OdataContext { get; set; }
            public List<T> Value { get; set; }
        }
    }
}