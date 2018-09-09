using Common;
using Common.Log;
using Lykke.Common.Log;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    public class TeamListUpdater : TimerPeriod
    {
        private readonly ILog _log;

        [Obsolete]
        public TeamListUpdater(ILog log) :
            base(nameof(TeamListUpdater), (int)TimeSpan.FromSeconds(3600).TotalMilliseconds, log)
        {
            _log = log;
        }

        public TeamListUpdater(ILogFactory logFactory) : 
            base(TimeSpan.FromSeconds(3600), logFactory)
        {
            _log = logFactory.CreateLog(this);
        }

        public override async Task Execute()
        {
            await TelegramBotService.UpdateListOfTeams();
            await Task.CompletedTask;
        }
    }
  }
