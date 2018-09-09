using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.RepositoryTgBot.Settings.JobSettings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    public delegate void Timeout();

    public class TimeoutHandler : TimerPeriod
    {
        public event Timeout Timeout;

        private readonly ILog _log;
        private bool firstRun = true;

        [Obsolete]
        public TimeoutHandler(ILog log) :
            base(nameof(TimeoutHandler), (int)TimeSpan.FromSeconds(RepositoryTgBotJobSettings.TimeoutPeriodSeconds).TotalMilliseconds, log)
        {
            _log = log;
        }

        public TimeoutHandler(ILogFactory logFactory) : 
            base(TimeSpan.FromSeconds(RepositoryTgBotJobSettings.TimeoutPeriodSeconds), logFactory)
        {
            _log = logFactory.CreateLog(this);
        }

        public override async Task Execute()
        {
            if (!firstRun)
            {
                firstRun = true;
                Timeout();
            }
            else
            {
                firstRun = false;
            }
            await Task.CompletedTask;
        }

        public override void Stop()
        {
            firstRun = true;
            base.Stop();
        }
    }
}

