using System.Net.Http;
using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.Services.GithubApi
{
    public interface IGitHubApi
    {
        HttpClient GetClient();
        Task SetSecret(string owner, string repository, string key, string value, HttpClient client = null);
        Task<PubKey> GetSecretPublicKey(string owner, string repository, HttpClient client = null);
    }
}
