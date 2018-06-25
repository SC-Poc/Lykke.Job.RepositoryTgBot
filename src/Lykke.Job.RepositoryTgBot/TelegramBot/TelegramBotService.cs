using Autofac;
using Common;
using Common.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Job.RepositoryTgBot.AzureRepositories.TelegramBotHistory;
using Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory;
using Octokit;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using Lykke.Job.RepositoryTgBot.Settings.JobSettings;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    class InlineMessage
    {
        public string Text { get; set; }
        public InlineKeyboardMarkup ReplyMarkup { get; set; }
    }

    public class TelegramBotService : IStopable, IStartable
    {
        #region Repositories

        private readonly ITelegramBotHistoryRepository _telegramBotHistoryRepository;

        #endregion
        private readonly ILog _log;
        private readonly ITelegramBotClient _bot;
        private readonly TelegramBotActions _actions;
        private static readonly List<RepoToCreate> RepoToCreateList = new List<RepoToCreate>();

        #region Constants
        private const string _createGithubRepo = "CreateGithubRepo";
        private const string _questionAssignToGit = "Do you assigned to some GitHub team?";
        private const string _chooseTeam = "What is your team?"; 
        private const string _chooseAssignedTeam = "For which team do you want to create a repository?"; 
        private const string _questionEnterName = "Enter repository name";
        private const string _questionEnterDesc = "Enter repository description";
        private const string _questionSecurity = "Will service interact with sensitive data, finance operations or includes other security risks?";
        private const string _questionMultipleTeams = "Is it a common service which will be used by multiple teams?";
        #endregion

        public TelegramBotService(RepositoryTgBotSettings settings, ILog log)
        {

            _telegramBotHistoryRepository = telegramBotHistoryRepository;

            _log = log;
            _bot = new TelegramBotClient(settings.BotToken);
            _actions = new TelegramBotActions(settings.OrgainzationName, settings.GitToken);

            _bot.OnMessage += BotOnMessageReceived;
            _bot.OnMessageEdited += BotOnMessageReceived;
            _bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            _bot.OnReceiveError += BotOnReceiveError;
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            Console.WriteLine("BotOnMessageReceived - " + messageEventArgs.Message.Text);

            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text) return;

            // get repository name
            if (message.ReplyToMessage?.Text == _questionEnterName)
            {
                var repoToCreate = GetOrCreateRepo(message);
                repoToCreate.RepoName = message.Text;
                Console.WriteLine(" 1 - " + repoToCreate.RepoName + " 2 - " + message.Text + " 3 - " + message.Chat.Id + " 4 - " + message.From.Id);
                await _bot.SendTextMessageAsync(message.Chat.Id, _questionEnterDesc, replyMarkup: new ForceReplyMarkup { Selective = false });
            }

            // get repository description
            else if (message.ReplyToMessage?.Text == _questionEnterDesc)
            {
                var repoToCreate = await GetIfExistRepoAsync(message);
                if (repoToCreate != null)
                {
                    repoToCreate.Description = message.Text;

                    var inlineMessage = new InlineMessage();
                    inlineMessage.Text = _questionSecurity;
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                    {
                    new [] // first row
                    {
                        InlineKeyboardButton.WithCallbackData("Yes", "Security"),
                        InlineKeyboardButton.WithCallbackData("No", "NoSecurity")
                    }
                });

                    await _bot.SendTextMessageAsync(
                        message.Chat.Id,
                        inlineMessage.Text,
                        replyMarkup: inlineMessage.ReplyMarkup);
                }
                
            }

            // read commands
            else
            {
                RemoveRepoToCreate(message);

                switch (message.Text.Split(' ').First())
                {
                    // send inline menu keyboard
                    case "/menu":                        

                        var inlineMessage = new InlineMessage();
                        inlineMessage.Text = "This is main menu. Please, select submenu.";
                        inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                        {
                            new [] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("Create Repo", _createGithubRepo),
                                InlineKeyboardButton.WithCallbackData("Test","Test")
                            }
                        });

                        await _bot.SendTextMessageAsync(
                        message.Chat.Id,
                        inlineMessage.Text,
                        replyMarkup: inlineMessage.ReplyMarkup);
                        break;
                    default:
                        //send default answer
                        await SendTextToUser(message.Chat.Id);
                        break;
                }
            }
        }

        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        { 
            //Here implements actions on reciving
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            if (callbackQuery.Message.Text == _chooseTeam)
            {
                var repoToCreate = await GetIfExistRepoAsync(callbackQuery.Message);
                if (repoToCreate != null)
                {
                    repoToCreate.TeamId = Convert.ToInt32(callbackQuery.Data);
                    var userName = callbackQuery.From.Username;
                    var userResult = await _actions.AddUserInTeam(userName, Convert.ToInt32(callbackQuery.Data));
                    if (!userResult.Success)
                    {
                        await SendTextToUser(callbackQuery.Message.Chat.Id, userResult.Message);
                    }
                    
                    await _bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = false });
                }                
            }
            else
            {
                await SendResponseMarkup(callbackQuery.Data, callbackQuery.Message);
            }
            
        }

        private void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message);
        }

        private async Task SendResponseMarkup(string callbackData, Message message)
        {
            var inlineMessage = new InlineMessage();
            switch (callbackData)
            {
                case "Main menu":
                    inlineMessage.Text = "This is main menu. Please, select submenu.";
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                    {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Create Repo", _createGithubRepo),
                            InlineKeyboardButton.WithCallbackData("Test","Test")
                        }
                    });                    
                    await _bot.EditMessageTextAsync(
                            message.Chat.Id,
                            message.MessageId,
                            inlineMessage.Text,
                            ParseMode.Default,
                            false,
                            inlineMessage.ReplyMarkup);
                    break;
                case _createGithubRepo:

                    //creating instance of RepoToCreate
                    var repoToCreate = GetOrCreateRepo(message);

                    inlineMessage.Text = _questionAssignToGit;
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                    {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Yes", _questionEnterName), 
                            InlineKeyboardButton.WithCallbackData("No",_questionAssignToGit)
                        }
                    });
                    await _bot.EditMessageTextAsync(
                            message.Chat.Id,
                            message.MessageId,
                            inlineMessage.Text,
                            ParseMode.Default,
                            false,
                            inlineMessage.ReplyMarkup);
                    break;
                case _chooseAssignedTeam:
                case _questionAssignToGit:
                    var maxRowLength = 2;
                    var githubTeams = await _actions.GetTeamsAsync();
                    if (githubTeams.Any())
                    {
                        inlineMessage.Text = _chooseTeam;

                        var inlineKeyBoardButtons = new List<List<InlineKeyboardButton>>();
                        var buttons = new List<InlineKeyboardButton>();
                        foreach (var team in githubTeams)
                        {
                            if (buttons.Count == maxRowLength)
                            {
                                inlineKeyBoardButtons.Add(buttons.Select(x => x).ToList());
                                buttons.RemoveRange(0, maxRowLength);
                            }

                            buttons.Add(InlineKeyboardButton.WithCallbackData(team.Name, team.Id.ToString()));
                        }
                        inlineKeyBoardButtons.Add(buttons);

                        var keyboardMarkup = new InlineKeyboardMarkup(inlineKeyBoardButtons);

                        inlineMessage.ReplyMarkup = keyboardMarkup;

                        await _bot.EditMessageTextAsync(
                            message.Chat.Id,
                            message.MessageId,
                            inlineMessage.Text,
                            ParseMode.Default,
                            false,
                            inlineMessage.ReplyMarkup);
                    }
                    break;
                case _questionEnterName:                    
                        await _bot.SendTextMessageAsync(message.Chat.Id, _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = false });                   

                    break;
                case "Test":
                    inlineMessage.Text = "This is Test.";
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                    {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Sub_Test","Sub_Test"),
                            InlineKeyboardButton.WithCallbackData("Back","Main menu")
                        }
                    });
                    await _bot.EditMessageTextAsync(
                            message.Chat.Id,
                            message.MessageId,
                            inlineMessage.Text,
                            ParseMode.Default,
                            false,
                            inlineMessage.ReplyMarkup);
                    break;
                case "Security":
                case "NoSecurity":
                    repoToCreate = await GetIfExistRepoAsync(message);

                    if (repoToCreate != null)
                    {
                        repoToCreate.AddSecurityTeam = callbackData == "Security";

                        inlineMessage.Text = _questionMultipleTeams;
                        inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                        {
                            new [] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("Yes", "Core"),
                                InlineKeyboardButton.WithCallbackData("No", "NoCore")
                            }
                        });

                        await _bot.EditMessageTextAsync(
                                message.Chat.Id,
                                message.MessageId,
                                inlineMessage.Text,
                                ParseMode.Default,
                                false,
                                inlineMessage.ReplyMarkup);
                    }                        
                    break;
                case "Core":
                case "NoCore":
                    repoToCreate = await GetIfExistRepoAsync(message);
                    if (repoToCreate != null)
                    {
                        repoToCreate.AddCoreTeam = callbackData == "Core";

                        var result = await _actions.CreateRepo(repoToCreate);
                        await SendTextToUser(message.Chat.Id, result.Message);
                        Console.WriteLine($"CreatingRepo! \n " +
                            $"repoToCreate.AddCoreTeam: {repoToCreate.AddCoreTeam} \n " +
                            $"repoToCreate.AddSecurityTeam: {repoToCreate.AddSecurityTeam} \n " +
                            $"repoToCreate.ChatId: {repoToCreate.ChatId} \n " +
                            $"repoToCreate.Description: {repoToCreate.Description} \n " +
                            $"repoToCreate.RepoName: {repoToCreate.RepoName} \n " +
                            $"repoToCreate.TeamId: {repoToCreate.TeamId} \n " +
                            $"repoToCreate.UserId: {repoToCreate.UserId} ");

                        RemoveRepoToCreate(message);
                    }
                    
                    break;
            }
        }

        public void Start()
        {
            var me = _bot.GetMeAsync().Result;
            Console.Title = me.Username;

            _bot.StartReceiving(Array.Empty<UpdateType>());
            _log.WriteInfo(nameof(TelegramBotService), nameof(TelegramBotService), $"Start listening for @{me.Username}");
        }

        public void Stop()
        {
            _bot.StopReceiving();
            _log.WriteInfo(nameof(TelegramBotService), nameof(TelegramBotService), "Stop listening.");
        }

        public void Dispose()
        {
            Dispose();
        }

        #region Private Methods
        private async Task SendTextToUser(long chatId, string text = "")
        {
            const string usage = @"
Usage:
/menu - to show menu";

            await _bot.SendTextMessageAsync(
                chatId,
                String.IsNullOrWhiteSpace(text) ? usage : text,
                replyMarkup: new ReplyKeyboardRemove());
        }

        private RepoToCreate GetOrCreateRepo(Message message)
        {
            var repo = RepoToCreateList.FirstOrDefault(r => (r.ChatId == message.Chat.Id ));
            if (repo == null)
            {
                repo = new RepoToCreate() { ChatId = message.Chat.Id, UserId = message.From.Id };
                RepoToCreateList.Add(repo);
            }
            return repo;
        }

        private async Task<RepoToCreate> GetIfExistRepoAsync(Message message)
        {
            var repo = RepoToCreateList.FirstOrDefault(r => (r.ChatId == message.Chat.Id ));
            if (repo == null)
            {
                await SendTextToUser(message.Chat.Id);
            }
            return repo;
        }
        private static void RemoveRepoToCreate(Message message)
        {
            var repo = RepoToCreateList.FirstOrDefault(r => (r.ChatId == message.Chat.Id ));
            if (repo == null)
            {
                RepoToCreateList.Remove(repo);
            }
        }
        #endregion
    }
}
