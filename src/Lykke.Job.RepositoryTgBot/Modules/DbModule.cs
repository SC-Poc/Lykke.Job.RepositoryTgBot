using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using AzureStorage.Tables;
using Common.Log;
using Lykke.Job.RepositoryTgBot.AzureRepositories.TelegramBotHistory;
using Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory;
using Lykke.Job.RepositoryTgBot.Settings;
using Lykke.SettingsReader;

namespace Lykke.Job.RepositoryTgBot.Modules
{
    public class DbModule : Module
    {
        private readonly IReloadingManager<AppSettings> _settings;
        private readonly ILog _log;

        public DbModule(IReloadingManager<AppSettings> settings, ILog log)
        {
            _settings = settings;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var connectionString = _settings.ConnectionString(x => x.ConnectionString);

            builder.RegisterInstance(
                new TelegramBotHistoryRepository(AzureTableStorage<TelegramBotHistory>.Create(connectionString, "TelegramBotHistory", _log))
            ).As<ITelegramBotHistoryRepository>().SingleInstance();
        }
    }
}
