using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.Core.Services
{
    public interface IShutdownManager
    {
        Task StopAsync();
    }
}
