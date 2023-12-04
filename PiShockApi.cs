using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LethalShock
{
    public class PiShockApi
    {
        // create class and set variables
        public string username { private get; set; }
        public string apiKey { private get; set; }
        public string code { private get; set; }
        public string senderName { private get; set; }

        private string apiEndpoint = "https://do.pishock.com/api/apioperate/";


        public async Task Shock(int intensity, int duration)
        {
            using (HttpClient client = new HttpClient())
            {
                // Request data
                var requestData = new
                {
                    Username = username,
                    Name = senderName,
                    Code = code,
                    Intensity = intensity,
                    Duration = duration,
                    Apikey = apiKey,
                    Op = 0
                };

                // Serialize the request data to JSON
                string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

                // Create StringContent with the correct content type
                using (HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                {
                    // Send the POST request
                    HttpResponseMessage response = await client.PostAsync(apiEndpoint, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Request sent successfully.");

                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");

                        string responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Response Content: {responseContent}");
                    }
                }
            }
        }

        public async Task Vibrate(int intensity, int duration)
        {
            using (HttpClient client = new HttpClient())
            {
                // Request data
                var requestData = new
                {
                    Username = username,
                    Name = senderName,
                    Code = code,
                    Intensity = intensity,
                    Duration = duration,
                    Apikey = apiKey,
                    Op = 1
                };

                // Serialize the request data to JSON
                string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

                // Create StringContent with the correct content type
                using (HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                {
                    // Send the POST request
                    HttpResponseMessage response = await client.PostAsync(apiEndpoint, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Request sent successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");

                        // Print the response content for further debugging
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Response Content: {responseContent}");
                    }
                }
            }
        }

        public async Task Beep(int duration)
        {
            using (HttpClient client = new HttpClient())
            {
                // Request data
                var requestData = new
                {
                    Username = username,
                    Name = senderName,
                    Code = code,
                    Duration = duration,
                    Apikey = apiKey,
                    Op = 2
                };

                // Serialize the request data to JSON
                string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

                // Create StringContent with the correct content type
                using (HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                {
                    // Send the POST request
                    HttpResponseMessage response = await client.PostAsync(apiEndpoint, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Request sent successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");

                        string responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Response Content: {responseContent}");
                    }
                }
            }
        }

    }
}
