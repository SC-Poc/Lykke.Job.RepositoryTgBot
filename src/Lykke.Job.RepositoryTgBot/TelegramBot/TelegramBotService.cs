﻿using Autofac;
using Common;
using Common.Log;
using Lykke.Job.RepositoryTgBot.AzureRepositories.TelegramBotHistory;
using Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory;
using Lykke.Job.RepositoryTgBot.Settings.JobSettings;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lykke.Common.Log;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    class InlineMessage
    {
        public string Text { get; set; }
        public InlineKeyboardMarkup ReplyMarkup { get; set; }
    }

    class TimeoutHelper
    {
        public Telegram.Bot.Types.User User { get; set; }
        public long ChatId { get; set; }
    }

    public class TelegramBotService : IStopable, IStartable
    {
        #region Statics
        public static ILog _log;
        private static List<TelegramBotHistory> _history = new List<TelegramBotHistory>();
        #endregion

        #region Repositories

        private readonly ITelegramBotHistoryRepository _telegramBotHistoryRepository;

        #endregion

        private readonly ITelegramBotClient _bot;
        private static TelegramBotActions _actions;

        #region Constants

        private const string _mainMenu = "Create new repository";

        private const string _createGithubRepo = "CreateGithubRepo";
        private const string _createLibraryRepo = "CreateLibraryRepo";
        private const string _resetTeam = "ResetTeam";

        private const string _chooseTeam = "What is your team?";

        private const string _questionEnterName = "Enter repository name";
        private const string _questionEnterDesc = "Enter repository description";
        private const string _questionEnterGitAcc = "Enter your GitHub account";

        private const string _questionSecurity = "Will service interact with sensitive data, finance operations or includes other security risks?";
        private const string _questionMultipleTeams = "Is it a common service which will be used by multiple teams?";
        #endregion

        private TimeoutHelper CurrentUser = new TimeoutHelper();
        private TimeoutHandler TimeoutTimer;
        private RepositoryTgBotJobSettings _settings;

        public TelegramBotService(RepositoryTgBotJobSettings settings, ILog log, ITelegramBotHistoryRepository telegramBotHistoryRepository)
        {
            _actions = new TelegramBotActions(settings.OrganizationName, settings.GitToken,
                settings.Secrets.NugetToken,
                settings.Secrets.DockerUsername,
                settings.Secrets.DockerSecret,
                settings.CommonDevelopersTeam);

            _telegramBotHistoryRepository = telegramBotHistoryRepository;

            _log = log;
            _bot = new TelegramBotClient(settings.BotToken);

            _bot.OnMessage += BotOnMessageReceived;
            _bot.OnMessageEdited += BotOnMessageReceived;
            _bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            _bot.OnReceiveError += BotOnReceiveError;

            TimeoutTimer = new TimeoutHandler(_log);
            TimeoutTimer.Timeout += Timeout;

            _settings = settings;
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            try
            {
                if (!CheckTotalTimeLimit(messageEventArgs.Message.Date)) return;

#if DEBUG
                Console.WriteLine("BotOnMessageReceived - " + messageEventArgs.Message.Text);
#endif

                var message = messageEventArgs.Message;

                if (message.Chat.Id != _settings.CommandChatId)
                {
                    _log.WriteWarning("BotOnMessageReceived", $"chat id: {message.Chat.Id}, chat name: {message.Chat.Title}, sender: {message.From.Username}",
                        "Receive message from wronf chat");
                    return;
                }


                if (message == null || message.Type != MessageType.Text) return;

                var firstWord = message.Text.Split(' ').First();
                var command = firstWord.IndexOf('@') == -1 ? firstWord : firstWord.Substring(0, firstWord.IndexOf('@'));

                if (command == "/create" || command == "/resetMyTeam" || command == "/groupId")
                    _history.Clear();

                if (command == "/groupId")
                {
                    await SendTextToUser(message.Chat.Id, $"Group Id: {message.Chat.Id}");
                    return;
                }

                var result = await CheckForGroupAccess(message.Chat.Id, message.Chat.Id);
                if (!result) return;

                // get repository name and checking user
                if (message.ReplyToMessage?.Text == $"@{message.From.Username} \n" + _questionEnterName && TimeoutTimer.Working && CurrentUser.User.Id == message.From.Id)
                {
                    TimeoutTimer.Stop();
                    // var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == message.From.Id);
                    var prevQuestion = _history.LastOrDefault();

                    if (prevQuestion != null && prevQuestion.Question == _questionEnterName)
                    {
                        if (!Regex.IsMatch(message.Text, @"^[a-zA-Z0-9._-]+$"))
                        {
                            await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Incorrect format.");
                            await _bot.SendTextMessageAsync(message.Chat.Id, $"@{message.From.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });
                        }
                        else
                        {
                            var repoIsAlreadyExists = await _actions.IsRepositoryExist(message.Text);
                            if (repoIsAlreadyExists)
                            {
                                await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Repository with this name already exists.");
                                await _bot.SendTextMessageAsync(message.Chat.Id, $"@{message.From.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });
                            }
                            else
                            {
                                await _bot.SendTextMessageAsync(message.Chat.Id, $"@{message.From.Username} \n" + _questionEnterDesc, replyMarkup: new ForceReplyMarkup { Selective = true });
                                await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, _questionEnterDesc, message.Text);
                            }
                        }

                        TimeoutTimer.Start();

                    }
                    else
                    {
                        await SendTextToUser(message.Chat.Id);
                    }

                }

                // get repository description and checking user
                else if (message.ReplyToMessage?.Text == $"@{message.From.Username} \n" + _questionEnterDesc && TimeoutTimer.Working && CurrentUser.User.Id == message.From.Id)
                {
                    TimeoutTimer.Stop();
                    //var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == message.From.Id);
                    var prevQuestion = _history.LastOrDefault();

                    var checkMenuType = await GetMenuAction(message.Chat.Id, message.From.Id);
                    if (checkMenuType == _createLibraryRepo)
                    {
                        await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, "Creating library repo", message.Text);
                        await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Creating library repository. Please wait...");
                        var question = await CreateRepoAsync(message.Chat.Id, message.From.Id);
                        await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, question, message.Text);
                        await SaveHistory();
                    }
                    else if (checkMenuType == _createGithubRepo)
                    {
                        await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, "Creating repository");

                        await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Creating repository. Please wait...");

                        var question = await CreateRepoAsync(message.Chat.Id, message.From.Id);

                        await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, question, message.Text);
                        await SaveHistory();
                    }
                    else
                    {
                        await SendTextToUser(message.Chat.Id);
                    }

                }

                else if (message.ReplyToMessage?.Text == $"@{message.From.Username} \n" + _questionEnterGitAcc && TimeoutTimer.Working && CurrentUser.User.Id == message.From.Id)
                {
                    TimeoutTimer.Stop();
                    // var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == message.From.Id);
                    var prevQuestion = _history.LastOrDefault();
                    if (prevQuestion != null && prevQuestion.Question == _questionEnterGitAcc)
                    {
                        if (!Regex.IsMatch(message.Text, @"^[a-zA-Z0-9._-]+$"))
                        {
                            await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Incorrect format.");
                            await _bot.SendTextMessageAsync(message.Chat.Id, $"@{message.From.Username} \n" + _questionEnterGitAcc, replyMarkup: new ForceReplyMarkup { Selective = true });
                        }
                        else
                        {
                            await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Please, wait a second...");
                            await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, _chooseTeam, message.Text);
                            var gitUserTeamList = await _actions.UserHasTeamCheckAsync(message.Text);

                            Console.WriteLine(gitUserTeamList.Count);

                            if (gitUserTeamList.Count == 0)
                            {
                                var inlineMessage = TeamListToSend(message.From.Username, new List<Team>(), $"You not assign to any team in organisation {_settings.OrganizationName}\n");
                                await _bot.SendTextMessageAsync(message.Chat.Id, inlineMessage.Text, replyMarkup: inlineMessage.ReplyMarkup);
                            }
                            else if (gitUserTeamList.Count == 1)
                            {
                                var userTeam = gitUserTeamList.FirstOrDefault();
                                await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + $"Your team is \"{userTeam.Name}\".");
                                await TeamSelected(message.Chat.Id, message.From, userTeam.Id.ToString());

                            }
                            else
                            {
                                var inlineMessage = TeamListToSend(message.From.Username, gitUserTeamList, "You are assigned to multiple teams.\n");
                                await _bot.SendTextMessageAsync(message.Chat.Id, inlineMessage.Text, replyMarkup: inlineMessage.ReplyMarkup);

                            }
                        }

                        TimeoutTimer.Start();
                    }
                    else
                    {
                        await SendTextToUser(message.Chat.Id);
                    }
                }
                else if (TimeoutTimer.Working && CurrentUser.User.Id != message.From.Id)
                {
                    await SendTextToUser(message.Chat.Id, $"@{message.From.Username} Please, wait for  user @{CurrentUser.User.Username} finishes creating repository");
                }

                // read commands
                else
                {
                    if (TimeoutTimer.Working && CurrentUser.User.Id == message.From.Id)
                        TimeoutTimer.Stop();
                    switch (command)
                    {

                        // send inline menu keyboard
                        case "/create":

                            var TeamId = await GetUserTeamId(message.Chat.Id, message.From.Id);
                            string addTeam = "";
                            var inlineMessage = new InlineMessage();

                            var inlineKeyboard = new List<IEnumerable<InlineKeyboardButton>>()
                            {
                                new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData("Create Repo", _createGithubRepo),
                                    InlineKeyboardButton.WithCallbackData("Create Library Repo", _createLibraryRepo)
                                }
                            };

                            //if (TeamId != 0)
                            //{
                            //    var teamName = await _actions.GetTeamById(TeamId);
                            //    addTeam = $"\nYour team is \"{teamName}\"";
                            //    inlineKeyboard.Add(
                            //    new[] // first row
                            //    {
                            //    InlineKeyboardButton.WithCallbackData("Reset my team", _resetTeam)
                            //    });
                            //    await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, _chooseTeam, message.Text);
                            //}

                            inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(inlineKeyboard);

                            inlineMessage.Text = $"@{message.From.Username} \n" + _mainMenu + addTeam;

                            await _bot.SendTextMessageAsync(message.Chat.Id, inlineMessage.Text, replyMarkup: inlineMessage.ReplyMarkup);

                            await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, inlineMessage.Text, (TeamId != 0)? TeamId.ToString() : "");

                            break;

                        case "/resetMyTeam":
                            await ClearTeam(message.Chat.Id, message.From);
                            break;
                        default:
                            //send default answer
                            await SendTextToUser(message.Chat.Id);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotService.BotOnMessageReceived", messageEventArgs.Message.ReplyToMessage?.Text, ex);
                return;
            }
        }

        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            try
            {
                if (!CheckTotalTimeLimit(callbackQueryEventArgs.CallbackQuery.Message.Date)) return;

                //Here implements actions on reciving
                var callbackQuery = callbackQueryEventArgs.CallbackQuery;

                var result = await CheckForGroupAccess(callbackQuery.Message.Chat.Id, callbackQuery.Message.Chat.Id);
                if (!result) return;

                if (TimeoutTimer.Working && callbackQuery.Message.Text.Contains(_chooseTeam))
                {
                    TimeoutTimer.Stop();
                    await TeamSelected(callbackQuery.Message.Chat.Id, callbackQuery.From, callbackQuery.Data);
                    TimeoutTimer.Start();
                }
                else if (TimeoutTimer.Working && CurrentUser.User.Id != callbackQuery.From.Id)
                {
                    await SendTextToUser(callbackQuery.Message.Chat.Id, $"@{callbackQuery.From.Username} Please, wait for  user @{CurrentUser.User.Username} finishes creating repository");
                }
                else
                {
                    await SendResponseMarkup(callbackQuery, callbackQuery.Message);
                }
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotService.BotOnCallbackQueryReceived", callbackQueryEventArgs.CallbackQuery.Message.Text, ex);
                return;
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
            try
            {
                var historyCreateSkipped = false;
                if (TimeoutTimer.Working && CurrentUser.User.Id != callbackQuery.From.Id)
                {
                    await SendTextToUser(callbackQuery.Message.Chat.Id, $"@{callbackQuery.From.Username} Please, wait for  user @{CurrentUser.User.Username} finishes creating repository");
                }
                else
                {
                    var inlineMessage = new InlineMessage();
                    var question = String.Empty;
                    var saveHistory = false;

                    switch (callbackQuery.Data)
                    {
                        case _createGithubRepo:
                        case _createLibraryRepo:

                            TimeoutTimer.Stop();
                            //Getting userTeamId
                            var userTeamId = await GetUserTeamId(message.Chat.Id, callbackQuery.From.Id);

                            if (userTeamId != 0)
                            {
                                question = _questionEnterName;
                                await _bot.SendTextMessageAsync(message.Chat.Id, $"@{callbackQuery.From.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });
                            }
                            else
                            {
                                question = _questionEnterGitAcc;
                                await _bot.SendTextMessageAsync(message.Chat.Id, $"@{callbackQuery.From.Username} \n" + _questionEnterGitAcc, replyMarkup: new ForceReplyMarkup { Selective = true });
                            }

                            CurrentUser.User = callbackQuery.From;
                            CurrentUser.ChatId = message.Chat.Id;
                            TimeoutTimer.Start();

                            break;
                        case _questionEnterName:
                            TimeoutTimer.Stop();
                            question = _questionEnterName;
                            await _bot.SendTextMessageAsync(message.Chat.Id, $"@{callbackQuery.From.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });

                            TimeoutTimer.Start();
                            break;
                        case "Security":
                        case "NoSecurity":
                            TimeoutTimer.Stop();
                            // var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == callbackQuery.From.Id);
                            var prevQuestion = _history.LastOrDefault();

                            if (prevQuestion == null || prevQuestion.Question != _questionSecurity)
                            {
                                await SendTextToUser(message.Chat.Id);
                                break;
                            }

                            
                            await CreateBotHistory(message.Chat.Id, callbackQuery.From.Id, callbackQuery.From.Username, question, callbackQuery.Data);
                            historyCreateSkipped = true;
                            callbackQuery.Data = "NoCore";
                            await SendResponseMarkup(callbackQuery, message);
                            
                            break;
                        case "Core":
                        case "NoCore":
                            TimeoutTimer.Stop();
                            // prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == callbackQuery.From.Id);
                            prevQuestion = _history.LastOrDefault();
                            if (prevQuestion == null || prevQuestion.Question != _questionMultipleTeams)
                            {
                                await SendTextToUser(message.Chat.Id);
                                break;
                            }
                            prevQuestion.Answer = callbackQuery.Data;
                            await CreateBotHistory(message.Chat.Id, callbackQuery.From.Id, callbackQuery.From.Username, "Creating repository");

                            await SendTextToUser(message.Chat.Id, $"@{callbackQuery.From.Username} \n" + "Creating repository. Please wait...");

                            question = await CreateRepoAsync(message.Chat.Id, callbackQuery.From.Id);
                            saveHistory = true;

                            break;
                        case _resetTeam:
                            TimeoutTimer.Stop();
                            await ClearTeam(message.Chat.Id, callbackQuery.From);
                            break;
                    }

                    if (!historyCreateSkipped)
                        await CreateBotHistory(message.Chat.Id, callbackQuery.From.Id, callbackQuery.From.Username, question, callbackQuery.Data);

                    if (saveHistory)
                        await SaveHistory();
                }
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotService.SendResponseMarkup", callbackQuery.Data, ex);
                return;
            }
        }

        public void Start()
        {
            try
            {
                _log.WriteInfo("TelegramBotService.Start", "Start", "");

                var me = _bot.GetMeAsync().Result;
                Console.Title = me.Username;

                _bot.StartReceiving(Array.Empty<UpdateType>());
                _log.WriteInfo(nameof(TelegramBotService), nameof(TelegramBotService), $"Start listening for @{me.Username}");
            }
            catch (Exception ex)
            {
                _log.WriteError("TelegramBotService.Start", "", ex);
                return;
            }
        }

        public void Stop()
        {
            try
            {
                _log.WriteInfo("TelegramBotService.Stop", "Stop", "");

                _bot.StopReceiving();
                _log.WriteInfo(nameof(TelegramBotService), nameof(TelegramBotService), "Stop listening.");
            }
            catch (Exception ex)
            {
                _log.WriteError("TelegramBotService.Stop", "", ex);
                return;
            }
        }

        public void Dispose()
        {
            Dispose();
        }

        #region Private Methods

        private async Task TeamSelected(long chatId, Telegram.Bot.Types.User user, string teamId)
        {
            var data = new { chatId, user, teamId }.ToJson();
            try
            {
                TimeoutTimer.Stop();
                // var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == chatId && x.UserId == user.Id);
                var prevQuestion = _history.LastOrDefault();
                if (prevQuestion == null || prevQuestion.Question == _chooseTeam)
                {
                    await _bot.SendTextMessageAsync(chatId, $"@{user.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });
                    await CreateBotHistory(chatId, user.Id, user.Username, _questionEnterName, teamId);
                    await SaveHistory(false);
                    TimeoutTimer.Start();
                }
                else
                {
                    await SendTextToUser(chatId);
                }
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotService.TeamSelected", data, ex);
                throw;
            }
        }

        private async Task SendTextToUser(long chatId, string text = "")
        {
            const string usage = @"
Usage:
/create - to start creating repository";

            await _bot.SendTextMessageAsync(
                chatId,
                String.IsNullOrWhiteSpace(text) ? usage : text,
                replyMarkup: new ReplyKeyboardRemove());
        }

        private async Task<bool> CreateBotHistory(long chatId, long userId, string telegramUserName, string question, string answer = null)
        {
            try
            {
                var entity = new TelegramBotHistory {
                    ChatId = chatId,
                    TelegramUserName = telegramUserName,
                    UserId = userId,
                    Question = question
                };

                // first of all, we need to add answer for previous question
                if (!String.IsNullOrWhiteSpace(answer))
                {
                    // var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == entity.ChatId && x.UserId == entity.UserId);
                    var prevQuestion = _history.LastOrDefault();
                    if (prevQuestion != null)
                    {
                        prevQuestion.Answer = answer;
                    }
                }

                // add current question
                _history.Add(entity);

                return true;
            }
            catch (Exception ex)
            {
                var data = new { chatId, userId, telegramUserName, question, answer };
                await _log.WriteErrorAsync("TelegramBotActions.CreateBotHistory", data.ToJson(), ex);
                return false;
            }
        }

        private InlineMessage TeamListToSend(string username, List<Team> teamsToShow, string message = "")
        {
            var data = new { username, teamsToShow, message }.ToJson();
            try
            {
                var inlineMessage = new InlineMessage();
                var maxRowLength = 2;
                if (teamsToShow.Any())
                {
                    inlineMessage.Text = $"@{username} \n" + message + _chooseTeam;
                    var inlineKeyBoardButtons = new List<List<InlineKeyboardButton>>();
                    var buttons = new List<InlineKeyboardButton>();
                    foreach (var team in teamsToShow)
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
                }
                return inlineMessage;
            }
            catch (Exception ex)
            {
                _log.WriteError("TelegramBotActions.TeamListToSend", data, ex);
                throw;
            }
        }

        private async Task<string> CreateRepoAsync(long chatId, long userId)
        {
            var data = new { chatId, userId }.ToJson();
            try
            {
                var result = new TelegramBotActionResult();
                var repoToCreate = await GetRepoToCreate(chatId, userId);
                if (repoToCreate.MenuAction == _createGithubRepo)
                {
                    result = await _actions.CreateRepo(repoToCreate);
                    Console.WriteLine($"CreatingRepo! \n " +
                        $"repoToCreate.AddCoreTeam: {repoToCreate.AddCoreTeam} \n " +
                        $"repoToCreate.AddSecurityTeam: {repoToCreate.AddSecurityTeam} \n " +
                        $"repoToCreate.ChatId: {repoToCreate.ChatId} \n " +
                        $"repoToCreate.Description: {repoToCreate.Description} \n " +
                        $"repoToCreate.RepoName: {repoToCreate.RepoName} \n " +
                        $"repoToCreate.TeamId: {repoToCreate.TeamId} \n " +
                        $"repoToCreate.UserId: {repoToCreate.UserId} ");
                }
                else
                {
                    result = await _actions.CreateLibraryRepo(repoToCreate);
                    Console.WriteLine($"CreatingLibraryRepo! \n " +
                        $"repoToCreate.ChatId: {repoToCreate.ChatId} \n " +
                        $"repoToCreate.Description: {repoToCreate.Description} \n " +
                        $"repoToCreate.RepoName: {repoToCreate.RepoName} \n " +
                        $"repoToCreate.TeamId: {repoToCreate.TeamId} \n " +
                        $"repoToCreate.UserId: {repoToCreate.UserId} ");
                }

                await SendTextToUser(chatId, result.Message);
                return result.Message;
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotActions.CreateRepoAsync", data, ex);
                throw;
            }
        }

        private async Task<string> GetMenuAction(long chatId, long userId)
        {
            var data = new { chatId, userId }.ToJson();
            try
            {
                var history = _history.LastOrDefault(x => x.Question.Contains(_mainMenu));

                return history?.Answer;
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotActions.GetMenuAction", data, ex);
                throw;
            }
        }

        private async Task<RepoToCreate> GetRepoToCreate(long chatId, long userId)
        {
            var data = new { chatId, userId }.ToJson();
            try
            {
                var repoToCreate = new RepoToCreate
                {
                    ChatId = chatId,
                    UserId = userId
                };

                var questionsToAnswer = new[] { _chooseTeam, _questionEnterName, _questionEnterDesc };
                foreach (var question in questionsToAnswer)
                {
                    // var history = await _telegramBotHistoryRepository.GetLatestAsync(x => x.Question == question && x.ChatId == chatId && x.UserId == userId);
                    var history = _history.FirstOrDefault(x => x.Question == question);
                    if (history == null) continue;

                    var answer = history.Answer;

                    switch (question)
                    {
                        case _questionEnterName:
                            repoToCreate.RepoName = answer;
                            break;
                        case _questionEnterDesc:
                            repoToCreate.Description = answer;
                            break;
                    }

                    repoToCreate.TeamId = 0;
                    repoToCreate.AddSecurityTeam = false;
                    repoToCreate.AddCoreTeam = true;
                }

                if (repoToCreate.TeamId == 0)
                    repoToCreate.TeamId = await GetUserTeamId(chatId, userId);

                repoToCreate.MenuAction = await GetMenuAction(chatId, userId);

                return repoToCreate;
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotActions.GetRepoToCreate", data, ex);
                throw;
            }
        }

        public async Task ClearTeam(long chatId, Telegram.Bot.Types.User user)
        {
            var data = new { chatId, user }.ToJson();
            try
            {
                var entities = await _telegramBotHistoryRepository.GetAllAsync(x =>
                    x.Question == _chooseTeam && x.ChatId == chatId && x.UserId == user.Id);
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        await _telegramBotHistoryRepository.RemoveAsync(entity.RowKey);
                    }
                }
                await SendTextToUser(chatId, $"@{user.Username} \n" + "Your team was reseted.");
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("TelegramBotActions.ClearTeam", data, ex);
                throw;
            }
        }

        private async Task<int> GetUserTeamId(long chatId, long userId)
        {
            var entity = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == chatId && x.UserId == userId && x.Question == _chooseTeam);
            return entity?.Answer.ParseIntOrDefault(0) ?? 0;
        }

        private async Task<bool> CheckForGroupAccess(long chatId, long groupId)
        {
            if (RepositoryTgBotJobSettings.AllowedGroupId != 0 && groupId != RepositoryTgBotJobSettings.AllowedGroupId)
            {
                await SendTextToUser(chatId, "Access denied");
                return false;
            }

            return true;
        }

        #endregion
        public static async Task UpdateListOfTeams()
        {
            if (_actions == null)
                return;
            
            //teams = await _actions.GetTeamsAsync();
            //teams.Sort(new TeamByNameComparer());
        }

        public async void Timeout()
        {
            if (CurrentUser.ChatId != 0)
            {
                await CreateBotHistory(CurrentUser.ChatId, CurrentUser.User.Id, CurrentUser.User.Username, "Timeout");
                await SendTextToUser(CurrentUser.ChatId, $"@{CurrentUser.User.Username} Sorry, but time is out. Please create your repository again.");
                TimeoutTimer.Stop();
            }
        }

        private bool CheckTotalTimeLimit(DateTime dateTime)
        {
            var exactTimeNow = DateTime.Now.ToUniversalTime();
            var timeSpan = exactTimeNow.Subtract(dateTime);
            if (timeSpan.TotalMinutes > RepositoryTgBotJobSettings.TotalTimeLimitInMinutes)
            {
                return false;
            }

            return true;
        }

        private async Task SaveHistory(bool clearHistory = true)
        {
            foreach(var item in _history)
            {
                item.RowKey = Guid.NewGuid().ToString();
                await _telegramBotHistoryRepository.SaveAsync(item);
            }
            if(clearHistory)
                _history.Clear();
        }

        class TeamByNameComparer : IComparer<Team>
        {
            public int Compare(Team a, Team b)
            {
                return a.Name.CompareTo(b.Name);
            }
        }
    }
}
