using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AdiIRC_LibreTranslate_plugin
{
    /** LibreTranslate API client for AdiIRC plugin
     * Given an API URL and a user language, this class can translate text using the LibreTranslate API.
     * The response includes a boolean indicating succes, the translated text, detected language and confidence level.
     */
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
        public class TranslationResponse
        {
            public string translatedText { get; set; }
            public List<string> alternatives { get; set; }
            public DetectedLanguage detectedLanguage { get; set; }
            public String printableResponse { get; set; }
            public Boolean success { get; set; } = false;
        }
        public class DetectedLanguage
        {
            public float confidence { get; set; }
            public string language { get; set; }
        }

        /*
         * Translates the given text to the user language set in the constructor.
         * If the user language is not set, it defaults to English.
         * Returns a TranslationResponse object containing the translated text and other details.
         */
        public async Task<TranslationResponse> translate(string text)
        {
            return await _translate(text, userLanguage);
        }

        /*
         * Translates the given text to the specified target language.
         * Returns a TranslationResponse object containing the translated text and other details.
         */
        public async Task<TranslationResponse> translate(string text, string targetLanguage)
        {
            return await _translate(text, targetLanguage);
        }

        /*
         * Internal method to perform the actual translation using the LibreTranslate API.
         * Handles HTTP requests, response parsing, and error handling.
         * Returns a TranslationResponse object containing the translated text and other details.
         */
        private async Task<TranslationResponse> _translate(string text, string targetLanguage)
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

                    string translatedMessage = translationResult?.translatedText;
                    string sourceLanguage = translationResult?.detectedLanguage?.language;
                    string confidence = translationResult?.detectedLanguage?.confidence.ToString("0") ?? "N/A";
                    translationResult.printableResponse = $"[Translated {sourceLanguage.ToUpper()}|{targetLanguage.ToUpper()} {confidence}%]: {translatedMessage}";

                    if (!string.IsNullOrEmpty(translatedMessage))
                    {
                        string output = $"[Translated {sourceLanguage.ToUpper()}|{targetLanguage.ToUpper()} {confidence}%]: {translatedMessage}";
                        //Only consider it a success if it was actually translated to a different language.
                        //This is to avoid English being translated to English and shown as a message.
                        translationResult.success = !sourceLanguage.ToUpper().Equals(targetLanguage.ToUpper());
                        return translationResult;
                    }

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
