using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.RepositoryTgBot.Models
{
    class TelegramBotHistoryModel
    {
        public long ChatId { get; set; }

        public long UserId { get; set; }

        public string TelegramUserName { get; set; }

        public string GithubUserName { get; set; }

        public string Question { get; set; }

        public string Answer { get; set; }
    }
}
