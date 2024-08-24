using System.Net.Http.Headers;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net.Http;
using Smartstore.PayU.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Smartstore.PayU.Services
{
    public class PayUGateAuthorizationService
    {
        private string endpoint;
        private string region;
        private string posid;
        private string clientid;
        private string clientsecret;

        private string accessToken;
        private DateTime? validUntil = null;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public PayUGateAuthorizationService(PayUSettings config)
        {
            UpdateConfiguration(config);
        }

        public void UpdateConfiguration(PayUSettings config)
        {
            endpoint = config.IsSandbox ? config.SandboxEndpoint : config.Endpoint;
            region = config.IsSandbox ? config.SandboxRegion : config.Region;
            posid = config.IsSandbox ? config.SandboxPosID : config.PosID;
            clientid = config.IsSandbox ? config.SandboxClientID : config.ClientID;
            clientsecret = config.IsSandbox ? config.SandboxClientSecret : config.ClientSecret;
        }

        private async Task GetAccessTokenInternal()
        {
            if (string.IsNullOrEmpty(endpoint) ||
                string.IsNullOrEmpty(region) ||
                string.IsNullOrEmpty(posid) ||
                string.IsNullOrEmpty(clientid) ||
                string.IsNullOrEmpty(clientsecret))
            {
                throw new InvalidOperationException("PayU configuration is missing.");
            }

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;
            using (HttpClient hc = new HttpClient(httpClientHandler, true))
            {
                HttpContent content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", clientid),
                    new KeyValuePair<string, string>("client_secret", clientsecret),
                ]);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                HttpResponseMessage response = await hc.PostAsync($"{endpoint}/{region}/standard/user/oauth/authorize", content);

                string result = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Logger.Error("Received non ok response during authorization.");
                    Logger.Error(result);
                    throw new ApplicationException("Received non ok response during authorization.");
                }

                JsonNode data = JsonSerializer.Deserialize<JsonNode>(result);
                accessToken = data["access_token"].GetValue<string>();
                string tokenType = data["token_type"].GetValue<string>();
                int secondsValid = data["expires_in"].GetValue<int>();
                validUntil = DateTime.Now.AddSeconds(secondsValid - 10);

                if (string.IsNullOrEmpty(accessToken) || tokenType != "bearer")
                {
                    Logger.Error("Received invalid token, original response:");
                    Logger.Error(result);
                    throw new ApplicationException("Received invalid token.");
                }
            }
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (validUntil == null || accessToken == null || validUntil < DateTime.Now)
            {
                await GetAccessTokenInternal();
            }
            return accessToken;
        }
    }
}
