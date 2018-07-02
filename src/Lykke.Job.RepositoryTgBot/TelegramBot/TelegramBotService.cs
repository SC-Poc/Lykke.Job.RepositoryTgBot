using Autofac;
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
        #region Repositories

        private readonly ITelegramBotHistoryRepository _telegramBotHistoryRepository;

        #endregion
        private readonly ILog _log;
        private readonly ITelegramBotClient _bot;
        private static readonly TelegramBotActions _actions = new TelegramBotActions(RepositoryTgBotJobSettings.OrgainzationName, RepositoryTgBotJobSettings.GitToken);

        #region Constants

        private string _mainMenu = "Create new repository";
        private const string _createGithubRepo = "CreateGithubRepo";
        private const string _chooseTeam = "What is your team?";
        private const string _questionEnterName = "Enter repository name";
        private const string _questionEnterDesc = "Enter repository description";
        private const string _questionSecurity = "Will service interact with sensitive data, finance operations or includes other security risks?";
        private const string _questionMultipleTeams = "Is it a common service which will be used by multiple teams?";
        #endregion

        private static List<Team> teams;

        private TimeoutHelper CurrentUser = new TimeoutHelper();
        private TimeoutHandler TimeoutTimer;

        public TelegramBotService(RepositoryTgBotJobSettings settings, ILog log, ITelegramBotHistoryRepository telegramBotHistoryRepository)
        {

            _telegramBotHistoryRepository = telegramBotHistoryRepository;

            _log = log;
            _bot = new TelegramBotClient(settings.BotToken);

            _bot.OnMessage += BotOnMessageReceived;
            _bot.OnMessageEdited += BotOnMessageReceived;
            _bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            _bot.OnReceiveError += BotOnReceiveError;

            TimeoutTimer = new TimeoutHandler(_log);
            TimeoutTimer.Timeout += Timeout;
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
#if DEBUG
            Console.WriteLine("BotOnMessageReceived - " + messageEventArgs.Message.Text);
#endif

            var message = messageEventArgs.Message;

            var result = await CheckForGroupAccess(message.Chat.Id, message.Chat.Title);
            if (!result) return;

            if (message == null || message.Type != MessageType.Text) return;

            // get repository name and checking user
            if (message.ReplyToMessage?.Text == $"@{message.From.Username} \n" + _questionEnterName && TimeoutTimer.Working && CurrentUser.User.Id == message.From.Id)
            {
                TimeoutTimer.Stop();
                var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == message.From.Id);
                if (prevQuestion != null && prevQuestion.Question == _questionEnterName)
                {


                    var repoIsAlreadyExists = await _actions.RepositoryIsExist(message.Text);
                    if (!Regex.IsMatch(message.Text, @"^[a-zA-Z0-9._-]+$"))
                    {
                        await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Incorrect format.");
                        await _bot.SendTextMessageAsync(message.Chat.Id, $"@{message.From.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });
                    }
                    else if (repoIsAlreadyExists)
                    {
                        await SendTextToUser(message.Chat.Id, $"@{message.From.Username} \n" + "Repository with this name already exists.");
                        await _bot.SendTextMessageAsync(message.Chat.Id, $"@{message.From.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });
                    }
                    else
                    {
                        await _bot.SendTextMessageAsync(message.Chat.Id, $"@{message.From.Username} \n" + _questionEnterDesc, replyMarkup: new ForceReplyMarkup { Selective = true });
                        await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, _questionEnterDesc, message.Text);
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
                var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == message.From.Id);

                var teamName = await GetTeamName(message.Chat.Id, message.From.Id);
                if (prevQuestion != null && prevQuestion.Question == _questionEnterDesc && teamName != RepositoryTgBotJobSettings.SecurityTeam)
                {
                    var inlineMessage = new InlineMessage
                    {
                        Text = $"@{message.From.Username} \n" + _questionSecurity,
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

                    TimeoutTimer.Start();
                }
                else if (prevQuestion != null && prevQuestion.Question == _questionEnterDesc && teamName == RepositoryTgBotJobSettings.SecurityTeam)
                {
                    CallbackQuery callbackQuery = new CallbackQuery
                    {
                        Data = "NoSecurity",
                        From = new Telegram.Bot.Types.User
                        {
                            Id = message.From.Id,
                            Username = message.From.Username
                        }
                    };

                    await CreateBotHistory(message.Chat.Id, message.From.Id, message.From.Username, _questionSecurity, message.Text);
                    await SendResponseMarkup(callbackQuery, message);
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
                var firstWord = message.Text.Split(' ').First();
                var command = firstWord.IndexOf('@') == -1 ? firstWord : firstWord.Substring(0, firstWord.IndexOf('@'));
                switch (command)
                {
                    // send inline menu keyboard
                    case "/create":

                        var TeamId = await GetUserTeamId(message.Chat.Id, message.From.Id);
                        string addTeam = "";
                        if (TeamId != 0)
                        {
                            var teamName = await _actions.GetTeamById(TeamId);
                            addTeam = $"\nYour team is \"{teamName}\"";
                        }

                        var inlineMessage = new InlineMessage();
                        inlineMessage.Text = $"@{message.From.Username} \n" + _mainMenu + addTeam;
                        inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                        {
                            new [] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("Create Repo", _createGithubRepo),
                                //InlineKeyboardButton.WithCallbackData("Test","Test")
                            }
                        });

                        await _bot.SendTextMessageAsync(message.Chat.Id, inlineMessage.Text, replyMarkup: inlineMessage.ReplyMarkup);
                        var botHistory = new TelegramBotHistory
                        {
                            RowKey = Guid.NewGuid().ToString(),
                            ChatId = message.Chat.Id,
                            UserId = message.From.Id,
                            TelegramUserName = message.From.Username,
                            Question = inlineMessage.Text
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

        private Task RepositoryIsExist(string v, object repoName)
        {
            throw new NotImplementedException();
        }

        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            //Here implements actions on reciving
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var result = await CheckForGroupAccess(callbackQuery.Message.Chat.Id, callbackQuery.Message.Chat.Title);
            if (!result) return;

            if (callbackQuery.Message.Text == $"@{callbackQuery.From.Username} \n" + _chooseTeam)
            {
                TimeoutTimer.Stop();
                var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == callbackQuery.Message.Chat.Id && x.UserId == callbackQuery.From.Id);
                if (prevQuestion == null || prevQuestion.Question == _chooseTeam)
                {
                    await _bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"@{callbackQuery.From.Username} \n" + _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = true });
                    await CreateBotHistory(callbackQuery.Message.Chat.Id, callbackQuery.From.Id, callbackQuery.From.Username, _questionEnterName, callbackQuery.Data);
                    TimeoutTimer.Start();
                }
                else
                {
                    await SendTextToUser(callbackQuery.Message.Chat.Id);
                }
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

        private void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message);
        }

        private async Task SendResponseMarkup(CallbackQuery callbackQuery, Message message)
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

                switch (callbackQuery.Data)
                {
                    case _createGithubRepo:
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
                            var maxRowLength = 2;
                            var githubTeams = teams;
                            if (githubTeams.Any())
                            {
                                inlineMessage.Text = $"@{callbackQuery.From.Username} \n" + _chooseTeam;
                                question = _chooseTeam;
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

                                await _bot.EditMessageTextAsync(message.Chat.Id, message.MessageId, inlineMessage.Text, ParseMode.Default,
                                    false, inlineMessage.ReplyMarkup);
                            }

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
                        var prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == callbackQuery.From.Id);

                        if (prevQuestion == null || prevQuestion.Question != _questionSecurity)
                        {
                            Console.WriteLine(prevQuestion.Question);
                            await SendTextToUser(message.Chat.Id);
                            break;
                        }

                        var teamName = await GetTeamName(message.Chat.Id, message.From.Id);
                        question = _questionMultipleTeams;
                        if (teamName != RepositoryTgBotJobSettings.CoreTeam)
                        {
                            inlineMessage.Text = $"@{callbackQuery.From.Username} \n" + _questionMultipleTeams;
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
                            TimeoutTimer.Start();
                        }
                        else
                        {
                            await CreateBotHistory(message.Chat.Id, callbackQuery.From.Id, callbackQuery.From.Username, question, callbackQuery.Data);
                            historyCreateSkipped = true;
                            callbackQuery.Data = "NoCore";
                            await SendResponseMarkup(callbackQuery, message);
                        }
                        break;
                    case "Core":
                    case "NoCore":
                        TimeoutTimer.Stop();
                        prevQuestion = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == message.Chat.Id && x.UserId == callbackQuery.From.Id);
                        if (prevQuestion == null || prevQuestion.Question != _questionMultipleTeams)
                        {
                            await SendTextToUser(message.Chat.Id);
                            break;
                        }
                        prevQuestion.Answer = callbackQuery.Data;
                        await _telegramBotHistoryRepository.SaveAsync(prevQuestion);

                        await SendTextToUser(message.Chat.Id, $"@{callbackQuery.From.Username} \n" + "Creating repository. Please wait...");

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

                if(!historyCreateSkipped)
                    await CreateBotHistory(message.Chat.Id, callbackQuery.From.Id, callbackQuery.From.Username, question, callbackQuery.Data);
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
/create - to start creating repository";

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
                var history = await _telegramBotHistoryRepository.GetLatestAsync(x => x.Question == question && x.ChatId == chatId && x.UserId == userId);
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

        public async Task<string> GetTeamName(long chatId, long userId)
        {
            var entity = await _telegramBotHistoryRepository.GetLatestAsync(x =>
                x.Question == _chooseTeam && x.ChatId == chatId && x.UserId == userId);
            if (entity == null)
                return String.Empty;

            var teamId = entity.Answer;

            return await _actions.GetTeamById(Convert.ToInt32(teamId));
        }

        private async Task<int> GetUserTeamId(long chatId, long userId)
        {
            var entity = await _telegramBotHistoryRepository.GetLatestAsync(x => x.ChatId == chatId && x.UserId == userId && x.Question == _chooseTeam);
            return entity?.Answer.ParseIntOrDefault(0) ?? 0;
        }

        private async Task<bool> CheckForGroupAccess(long chatId, string groupName)
        {
            if (!String.IsNullOrWhiteSpace(RepositoryTgBotJobSettings.AllowedGroupName) && groupName != RepositoryTgBotJobSettings.AllowedGroupName)
            {
                await SendTextToUser(chatId, "Access denied");
                return false;
            }

            return true;
        }

        #endregion
        public static async Task UpdateListOfTeams()
        {
            teams = await _actions.GetTeamsAsync();
            teams.Sort(new TeamByNameComparer());
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

        class TeamByNameComparer : IComparer<Team>
        {
            public int Compare(Team a, Team b)
            {
                return a.Name.CompareTo(b.Name);
            }
        }
    }
}
