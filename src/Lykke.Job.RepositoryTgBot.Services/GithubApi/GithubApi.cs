using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Lykke.Job.RepositoryTgBot.Services.GithubApi
{
    public class GitHubApi : IGitHubApi
    {
        private string _token;
        private readonly string _appName;
        private readonly string _appVersion;

        public GitHubApi(string token, string appName, string appVersion)
        {
            _token = token;
            _appName = appName;
            _appVersion = appVersion;
        }

        public HttpClient GetClient()
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri("https://api.github.com");

            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(_appName, _appVersion));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", _token);
            return client;
        }

        public async Task SetSecret(string owner, string repository, string key, string value, HttpClient client = null)
        {
            client = client ?? GetClient();
            var pubkey = await GetSecretPublicKey(owner, repository, client);


            var secretValue = System.Text.Encoding.UTF8.GetBytes(value);
            var publicKey = Convert.FromBase64String(pubkey.key);

            var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(secretValue, publicKey);

            var encryptedValue = Convert.ToBase64String(sealedPublicKeyBox);

            var content = JsonConvert.SerializeObject(new { key_id = pubkey.key_id, encrypted_value = encryptedValue });
            var response = await client.PutAsync($"/repos/{owner}/{repository}/actions/secrets/{key}", new StringContent(content, Encoding.UTF8, "application/json"));
            var data = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                var error = new Exception($"Cannot get key to {owner}/{repository}. Response status code: {response.StatusCode}.");
                error.Data.Add("Error content", data);
                throw error;
            }
        }

        public async Task<PubKey> GetSecretPublicKey(string owner, string repository, HttpClient client = null)
        {
            client = client ?? GetClient();

            var response = await client.GetAsync($"/repos/{owner}/{repository}/actions/secrets/public-key");
            var data = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var error = new Exception($"Cannot get key to {owner}/{repository}. Response status code: {response.StatusCode}.");
                error.Data.Add("Error content", data);
                throw error;
            }

            var key = JsonConvert.DeserializeObject<PubKey>(data);

            return key;
        }


    }
}
