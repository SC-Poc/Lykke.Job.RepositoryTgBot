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

        private const string _mainMenu = "This is main menu. Please, select submenu.";
        private const string _createGithubRepo = "CreateGithubRepo";
        private const string _questionAssignToGit = "Do you assigned to some GitHub team?";
        private const string _chooseTeam = "What is your team?"; 
        private const string _questionEnterName = "Enter repository name";
        private const string _questionEnterDesc = "Enter repository description";
        private const string _questionSecurity = "Will service interact with sensitive data, finance operations or includes other security risks?";
        private const string _questionMultipleTeams = "Is it a common service which will be used by multiple teams?";
        #endregion

        public TelegramBotService(RepositoryTgBotSettings settings, ILog log, ITelegramBotHistoryRepository telegramBotHistoryRepository)
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
                await _bot.SendTextMessageAsync(message.Chat.Id, _questionEnterDesc, replyMarkup: new ForceReplyMarkup { Selective = false });
                await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, _questionEnterDesc, message.Text);
            }

            // get repository description
            else if (message.ReplyToMessage?.Text == _questionEnterDesc)
            {
                var inlineMessage = new InlineMessage
                {
                    Text = _questionSecurity,
                    ReplyMarkup = new InlineKeyboardMarkup(new[]
                        {
                            new[] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("Yes", "Security"),
                                InlineKeyboardButton.WithCallbackData("No", "NoSecurity")
                            }
                        })
                };

                await _bot.SendTextMessageAsync(message.Chat.Id, inlineMessage.Text, replyMarkup: inlineMessage.ReplyMarkup);
                await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, _questionSecurity, message.Text);
            }

            // read commands
            else
            {
                switch (message.Text.Split(' ').First())
                {
                    // send inline menu keyboard
                    case "/menu":

                        var inlineMessage = new InlineMessage();
                        inlineMessage.Text = _mainMenu;
                        inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                        {
                            new [] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("Create Repo", _createGithubRepo),
                                InlineKeyboardButton.WithCallbackData("Test","Test")
                            }
                        });

                        await _bot.SendTextMessageAsync(message.Chat.Id, inlineMessage.Text, replyMarkup: inlineMessage.ReplyMarkup);
                        var botHistory = new TelegramBotHistory
                        {
                            RowKey = Guid.NewGuid().ToString(),
                            ChatId = message.Chat.Id,
                            UserId = message.From.Id,
                            TelegramUserName = message.From.Username,
                            Question = _mainMenu
                        };

                        await _telegramBotHistoryRepository.SaveAsync(botHistory);
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
                    //TODO: Save users TeamId to AzureTable
                    //await SaveUserTeamId(Convert.ToInt32(callbackQuery.Data, callbackQuery.Message.Chat.Id, callbackQuery.Message.From.Id); 

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
                await SendResponseMarkup(callbackQuery, callbackQuery.Message);
            }

        }

        private void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message);
        }

        private async Task SendResponseMarkup(CallbackQuery callbackQuery, Message message)
        {
            var inlineMessage = new InlineMessage();
            var question = String.Empty;
            switch (callbackQuery.Data)
            {
                case _createGithubRepo:

                    //creating instance of RepoToCreate
                    var repoToCreate = GetOrCreateRepo(message);

                    //Getting userTeamId
                    int userTeamId = 0;
                    //var userTeamId = GetUserTeamId(message.Chat.Id, message.From.Id);

                    if(userTeamId != 0)
                    {
                        repoToCreate.TeamId = userTeamId;
                        await _bot.SendTextMessageAsync(message.Chat.Id, _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = false });
                    }
                    else
                    {
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
                        
                    }
                    break;
                case _questionEnterName:
                    question = _questionEnterName;
                    await _bot.SendTextMessageAsync(message.Chat.Id, _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = false });

                    break;
                case "Security":
                case "NoSecurity":
                    inlineMessage.Text = _questionMultipleTeams;
                    question = _questionMultipleTeams;
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                    {
                            new [] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("Yes", "Core"),
                                InlineKeyboardButton.WithCallbackData("No", "NoCore")
                            }
                        });

                    await _bot.EditMessageTextAsync(message.Chat.Id, message.MessageId, inlineMessage.Text, ParseMode.Default,
                            false, inlineMessage.ReplyMarkup);
                    break;
                case "Core":
                case "NoCore":
                    var repoToCreate = await GetRepoToCreate(message.Chat.Id, callbackQuery.From.Id);
                    var result = await _actions.CreateRepo(repoToCreate);
                    question = result.Message;
                    await SendTextToUser(message.Chat.Id, result.Message);
                    Console.WriteLine($"CreatingRepo! \n " +
                        $"repoToCreate.AddCoreTeam: {repoToCreate.AddCoreTeam} \n " +
                        $"repoToCreate.AddSecurityTeam: {repoToCreate.AddSecurityTeam} \n " +
                        $"repoToCreate.ChatId: {repoToCreate.ChatId} \n " +
                        $"repoToCreate.Description: {repoToCreate.Description} \n " +
                        $"repoToCreate.RepoName: {repoToCreate.RepoName} \n " +
                        $"repoToCreate.TeamId: {repoToCreate.TeamId} \n " +
                        $"repoToCreate.UserId: {repoToCreate.UserId} ");

                    break;
            }

            await CreateBotHistory(message.Chat.Id, callbackQuery.From.Id, callbackQuery.From.Username, question, callbackQuery.Data);
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

        private async Task<bool> CreateBotHistory(long chatId, long userId, string telegramUserName, string question, string answer = null)
        {
            var entity = new TelegramBotHistory
            {
                RowKey = Guid.NewGuid().ToString(),
                ChatId = chatId,
                UserId = userId,
                TelegramUserName = telegramUserName,
                Question = question
            };

            try
            {
                // first of all, we need to add answer for previous question
                if (!String.IsNullOrWhiteSpace(answer))
                {
                    var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == entity.ChatId && x.UserId == entity.UserId);
                    if (prevQuestion != null)
                    {
                        prevQuestion.Answer = answer;
                        await _telegramBotHistoryRepository.SaveAsync(prevQuestion);
                    }
                }

                // add current question
                await _telegramBotHistoryRepository.SaveAsync(entity);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<RepoToCreate> GetRepoToCreate(long chatId, long userId)
        {
            var repoToCreate = new RepoToCreate
            {
                ChatId = chatId,
                UserId = userId
            };

            var questionsToAnswer = new[] { _chooseTeam, _questionEnterName, _questionEnterDesc, _questionSecurity, _questionMultipleTeams };
            foreach (var question in questionsToAnswer)
            {
                // TODO: get all data for current user and update repoToCreate
                var history = await _telegramBotHistoryRepository.GetLatestAsync(x => x.Question == question);
                if (history == null) continue;

                var answer = history.Answer;

                switch (question)
                {
                    case _chooseTeam:
                        repoToCreate.TeamId = Convert.ToInt32(answer);
                        break;
                    case _questionEnterName:
                        repoToCreate.RepoName = answer;
                        break;
                    case _questionEnterDesc:
                        repoToCreate.Description = answer;
                        break;
                    case _questionSecurity:
                        repoToCreate.AddSecurityTeam = answer == "Security";
                        break;
                    case _questionMultipleTeams:
                        repoToCreate.AddCoreTeam = answer == "Core";
                        break;
                }
            }

            return repoToCreate;
        }
        #endregion
    }
}
