using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;

namespace Lykke.Job.RepositoryTgBot.Core.Services
{
    public interface IBotService
    {
        TelegramBotClient Client { get; }
    }
}
