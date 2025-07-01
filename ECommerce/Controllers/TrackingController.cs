using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace ECommerce.Controllers
{
    public class TrackingController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;


        private const string ApiUrl = "https://api.17track.net/track/v2/gettrackinfo"; // Replace with actual 17Track API URL
        //private const string ApiUrl = "https://api.17track.net/v2/trackings/get"; // Replace with actual 17Track API URL
        private const string ApiKey = "2E9641C675F1138500413FA14221A089"; // Replace with your actual API key
        public TrackingController(IConfiguration configuration, HttpClient httpClient, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _httpClientFactory = httpClientFactory;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler);

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

            string apiBaseUrl = _configuration["APIURL"];
            string endpoint = "/user/v1/trackorder";

            _httpClient.BaseAddress = new Uri(apiBaseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var content = new StringContent(JsonConvert.SerializeObject(trackingNumber), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            var resultJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                JObject result = JObject.Parse(resultJson);

                if (result["success"]?.Value<bool>() == true)
                {
                    ViewBag.TrackingStatus = result["status"]?.ToString() ?? "Unknown";
                    ViewBag.Checkpoints = result["checkpoints"]?.ToObject<List<JObject>>() ?? new List<JObject>();
                }
                else
                {
                    ViewBag.TrackingStatus = result["error"]?.ToString() ?? "Tracking failed.";
                    ViewBag.Checkpoints = new List<JObject>();
                }
            }
            else
            {
                ViewBag.TrackingStatus = "Tracking failed due to server error.";
                ViewBag.Checkpoints = new List<JObject>();
            }

            return View("TrackOrder");
        }

    }
}