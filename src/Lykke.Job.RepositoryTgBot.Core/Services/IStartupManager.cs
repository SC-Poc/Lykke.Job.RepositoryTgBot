using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.Core.Services
{
    public interface IStartupManager
    {
        Task StartAsync();
    }
}