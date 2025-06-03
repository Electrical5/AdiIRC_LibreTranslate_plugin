using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AdiIRC_LibreTranslate_plugin
{
    internal class LibreTranslate
    {
        string apiUrl;
        string userLanguage = "en";
        JavaScriptSerializer serializer = new JavaScriptSerializer();

        public LibreTranslate(string apiUrl, string userLanguage)
        {
            this.apiUrl = apiUrl;
            this.userLanguage = userLanguage;
        }

        //Main function to translate text, using automatic source detection.
        public async Task<TranslationResponse> translate(string text, string targetLanguage)
        {
            return await _translate(text, targetLanguage);
        }

        //If no language is specified, use the user's language.
        public async Task<TranslationResponse> translate(string text)
        {
            return await _translate(text, userLanguage);
        }
        
        public class TranslationResponse
        {
            public string translatedText { get; set; }
            public List<string> alternatives { get; set; }
            public DetectedLanguage detectedLanguage { get; set; }
        }
        public class DetectedLanguage
        {
            public float confidence { get; set; }
            public string language { get; set; }
        }
        private async System.Threading.Tasks.Task<TranslationResponse> _translate(string text, string targetLanguage)
        {
            using (var client = new HttpClient())
            {
                // Set the timeout duration (for example, 30 seconds)
                client.Timeout = TimeSpan.FromSeconds(20);

                var requestBody = new
                {
                    q = text,
                    source = "auto", // Auto-detect language
                    target = targetLanguage,
                    format = "text"
                };

                
                var jsonRequest = serializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                try
                {
                    // Send the request
                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                    response.EnsureSuccessStatusCode();

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var translationResult = serializer.Deserialize<TranslationResponse>(jsonResponse);

                    return translationResult;
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == CancellationToken.None)
                {
                    // Handle timeout specifically
                    throw new TimeoutException("The translation request timed out.", ex);
                }
                catch (Exception ex)
                {
                    // Handle other exceptions (e.g., network errors)
                    throw new ApplicationException("An error occurred while making the translation request." + ex.ToString(), ex);
                }
            }
        }
    }
}
