using Lykke.Job.RepositoryTgBot.Core.Services;
using Telegram.Bot;

namespace Lykke.Job.RepositoryTgBot.Services
{
    public class BotService : IBotService
    {
        public TelegramBotClient Client { get; }
    }
}
