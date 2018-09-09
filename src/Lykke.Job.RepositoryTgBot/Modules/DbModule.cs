using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using AzureStorage.Tables;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.RepositoryTgBot.AzureRepositories.TelegramBotHistory;
using Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory;
using Lykke.Job.RepositoryTgBot.Settings;
using Lykke.SettingsReader;

namespace Lykke.Job.RepositoryTgBot.Modules
{
    public class DbModule : Module
    {
        private readonly IReloadingManager<AppSettings> _settings;

        public DbModule(IReloadingManager<AppSettings> settings)
        {
            _settings = settings;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var connectionString = _settings.ConnectionString(x => x.RepositoryTgBotJob.Db.ConnectionString);

            builder.Register(c=>
                new TelegramBotHistoryRepository(AzureTableStorage<TelegramBotHistory>.Create(connectionString,
                "TelegramBotHistory",
                c.Resolve<ILogFactory>())))
                .As<ITelegramBotHistoryRepository>()
                .SingleInstance();
        }
    }
}
