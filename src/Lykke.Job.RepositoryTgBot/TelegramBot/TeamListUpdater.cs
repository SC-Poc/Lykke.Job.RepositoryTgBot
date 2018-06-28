using Common;
using Common.Log;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    public class TeamListUpdater : TimerPeriod
    {
        private readonly ILog _log;

        public TeamListUpdater(ILog log) :
            base(nameof(TeamListUpdater), (int)TimeSpan.FromSeconds(3600).TotalMilliseconds, log)
        {
            _log = log;
        }

        public override async Task Execute()
        {
            await TelegramBotService.UpdateListOfTeams();
            await Task.CompletedTask;
        }
    }
  }
