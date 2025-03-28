using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace ECommerce.Controllers
{
    public class TrackingController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;

        private const string ApiUrl = "https://api.17track.net/track/v2/gettrackinfo"; // Replace with actual 17Track API URL
        //private const string ApiUrl = "https://api.17track.net/v2/trackings/get"; // Replace with actual 17Track API URL
        private const string ApiKey = "2E9641C675F1138500413FA14221A089"; // Replace with your actual API key

        public TrackingController(HttpClient httpClient, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClient;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult TrackOrder()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Track(string trackingNumber)
        {
            if (string.IsNullOrEmpty(trackingNumber))
            {
                ViewBag.TrackingResult = "Please enter a valid tracking number.";
                return View("TrackOrder");
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("17token", ApiKey);

            var requestData = new
            {
                number = new[] { trackingNumber }
            };
            var jsonData = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(ApiUrl, content);
            var result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                ViewBag.TrackingResult = result;
            }
            else
            {
                ViewBag.TrackingResult = $"Error: {response.StatusCode} - {result}";
            }

            return View("TrackOrder");
        }


    }

}


