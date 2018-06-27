using Microsoft.Extensions.DependencyInjection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common.Log;
using Lykke.Job.RepositoryTgBot.Core.Services;
using Lykke.Job.RepositoryTgBot.Settings.JobSettings;
using Lykke.Job.RepositoryTgBot.Services;
using Lykke.SettingsReader;
using Lykke.Job.RepositoryTgBot.TelegramBot;
using Common;

namespace Lykke.Job.RepositoryTgBot.Modules
{
    public class JobModule : Module
    {
        private readonly RepositoryTgBotJobSettings _settings;
        private readonly IReloadingManager<RepositoryTgBotJobSettings> _settingsManager;
        private readonly ILog _log;
        // NOTE: you can remove it if you don't need to use IServiceCollection extensions to register service specific dependencies
        private readonly IServiceCollection _services;

        public JobModule(RepositoryTgBotJobSettings settings, IReloadingManager<RepositoryTgBotJobSettings> settingsManager, ILog log)
        {
            _settings = settings;
            _log = log;
            _settingsManager = settingsManager;
            _services = new ServiceCollection();
        }

        protected override void Load(ContainerBuilder builder)
        {
            // NOTE: Do not register entire settings in container, pass necessary settings to services which requires them
            // ex:
            // builder.RegisterType<QuotesPublisher>()
            //  .As<IQuotesPublisher>()
            //  .WithParameter(TypedParameter.From(_settings.Rabbit.ConnectionString))

            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();

            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            builder.RegisterType<StartupManager>()
                .As<IStartupManager>();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>();

            builder.RegisterType<TelegramBotService>()
                .As<IStartable>()
                .As<IStopable>()
                .WithParameter(TypedParameter.From(_settings))
                .SingleInstance(); 

            // TODO: Add your dependencies here

            builder.Populate(_services);
        }
    }
}
