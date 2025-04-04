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
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestData = new
            {
                numbers = new[] { trackingNumber }
            };

            var jsonData = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(ApiUrl, content);
            var result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Deserialize response as JObject first
                JObject trackingData = JObject.Parse(result);

                // Check if 'data' exists and is an array
                if (trackingData["data"] is JArray dataArray && dataArray.Count > 0)
                {
                    var trackInfo = dataArray[0]?["track"];
                    if (trackInfo != null)
                    {
                        ViewBag.TrackingStatus = trackInfo["latest_status"]?.ToString() ?? "Unknown";
                        ViewBag.Checkpoints = trackInfo["z1"]?.ToObject<List<JObject>>() ?? new List<JObject>();
                    }
                    else
                    {
                        ViewBag.TrackingStatus = "No tracking data available.";
                        ViewBag.Checkpoints = new List<JObject>();
                    }
                }
                else
                {
                    ViewBag.TrackingStatus = "Invalid response format.";
                    ViewBag.Checkpoints = new List<JObject>();
                }
            }
            else
            {
                ViewBag.TrackingStatus = "Tracking failed.";
                ViewBag.Checkpoints = new List<JObject>();
            }

            return View("TrackOrder");
        }


    }

}


