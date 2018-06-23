using Autofac;
using Common;
using Common.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    class InlineMessage
    {
        public string Text { get; set; }
        public InlineKeyboardMarkup ReplyMarkup { get; set; }
    }

    public class TelegramBotService : IStopable, IStartable
    {
        private readonly ILog _log;
        private readonly ITelegramBotClient _bot;
        Message _oldMessage = null;
        private readonly TelegramBotActions _actions = new TelegramBotActions("TgBotTestOrg", "");

        #region Constants
        private const string _createGithubRepo = "CreateGithubRepo";
        private const string _chooseTeam = "What is your team?";
        private const string _questionEnterName = "Enter repository name";
        private const string _questionEnterDesc = "Enter repository description";
        private const string _questionSecurity = "Will service interact with sensitive data, finance operations or includes other security risks?";
        private const string _questionMultipleTeams = "Is it a common service which will be used by multiple teams?";
        #endregion

        public TelegramBotService(string token, ILog log)
        {

            _log = log;
            _bot = new TelegramBotClient(token);

            _bot.OnMessage += BotOnMessageReceived;
            _bot.OnMessageEdited += BotOnMessageReceived;
            _bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            _bot.OnInlineQuery += BotOnInlineQueryReceived;
            _bot.OnInlineResultChosen += BotOnChosenInlineResultReceived;
            _bot.OnReceiveError += BotOnReceiveError;
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            Console.WriteLine("BotOnMessageReceived");

            if (_oldMessage != null)
            {

                Console.WriteLine(_oldMessage.Text);
            }

            _oldMessage = messageEventArgs.Message;

            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text) return;

            // get repository name
            if (message.ReplyToMessage?.Text == _questionEnterName)
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, _questionEnterDesc, replyMarkup: new ForceReplyMarkup { Selective = false });
            }
            // get repository description
            else if (message.ReplyToMessage?.Text == _questionEnterDesc)
            {
                ReplyKeyboardMarkup securityKeyboard = new[]
                {
                    new[] {"Yes, secured", "Not secured"}
                };

                securityKeyboard.ResizeKeyboard = true;

                await _bot.SendTextMessageAsync(
                    message.Chat.Id,
                    _questionSecurity,
                    replyMarkup: securityKeyboard);
            }
            // get security question's answer
            else if (message.Text == "Yes, secured")
            {
                ReplyKeyboardMarkup multipleTeams = new[]
                {
                    new[] {"Yes", "No"}
                };

                multipleTeams.ResizeKeyboard = true;

                await _bot.SendTextMessageAsync(
                    message.Chat.Id,
                    _questionMultipleTeams,
                    replyMarkup: multipleTeams);
            }
            // get multiple teams question's answer
            else if (message.Text == "Yes")
            {
                await SendTextToUser(message.Chat.Id);
            }
            // read commands
            else
            {
                switch (message.Text.Split(' ').First())
                {
                    // send inline keyboard
                    case "/inline":
                        await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                        await Task.Delay(500); // simulate longer running task

                        await SendResposeMarkup("Main menu", message);
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

            Console.WriteLine("BotOnCallbackQueryReceived - " + callbackQuery.Message.Text);

            if (callbackQuery.Data == _createGithubRepo)
            {
                await SendResposeMarkup(_createGithubRepo, callbackQuery.Message);
            }
            else if (callbackQuery.Message.Text == _chooseTeam)
            {
                var userName = callbackQuery.From.Username;
                var userResult = await _actions.AddUserInTeam(userName, Convert.ToInt32(callbackQuery.Data));
                if (!userResult.Success)
                {
                    await SendTextToUser(callbackQuery.Message.Chat.Id, userResult.Message);
                }
                else
                {
                    var repoResult =
                        await _actions.AddRepositoryInTeam(callbackQuery.Message?.Text, "asd", Permission.Admin);
                    if (!repoResult.Success)
                    {
                        await SendTextToUser(callbackQuery.Message.Chat.Id, repoResult.Message);
                    }
                }
            }
        }

        private async void BotOnInlineQueryReceived(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {
            Console.WriteLine($"Received inline query from: {inlineQueryEventArgs.InlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                new InlineQueryResultLocation(
                    id: "1",
                    latitude: 40.7058316f,
                    longitude: -74.2581888f,
                    title: "New York")   // displayed result
                    {
                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 40.7058316f,
                            longitude: -74.2581888f)    // message if result is selected
                    },

                new InlineQueryResultLocation(
                    id: "2",
                    latitude: 13.1449577f,
                    longitude: 52.507629f,
                    title: "Berlin") // displayed result
                    {
                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 13.1449577f,
                            longitude: 52.507629f)   // message if result is selected
                    }
            };

            await _bot.AnswerInlineQueryAsync(
                inlineQueryEventArgs.InlineQuery.Id,
                results,
                isPersonal: true,
                cacheTime: 0);
        }

        private void BotOnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResultEventArgs.ChosenInlineResult.ResultId}");
        }

        private void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message);
        }

        private async Task SendResposeMarkup(string callbackData, Message message)
        {
            var inlineMessage = new InlineMessage();
            switch (callbackData)
            {
                case "Main menu":
                    inlineMessage.Text = "This is main menu. Please, select submenu.";
                    var replyMarkup = new InlineKeyboardMarkup(new[]
                    {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Create Repo", _createGithubRepo),
                            InlineKeyboardButton.WithCallbackData("Test","Test")
                        }
                    });

                    await _bot.SendTextMessageAsync(
                        message.Chat.Id,
                        "This is main menu. Please, select submenu.",
                        replyMarkup: replyMarkup);
                    break;
                case _createGithubRepo:
                    var maxColLength = 4;
                    var githubTeams = await _actions.GetTeamsAsync();
                    if (githubTeams.Any())
                    {
                        inlineMessage.Text = _chooseTeam;

                        var inlineKeyBoardButtons = new List<List<InlineKeyboardButton>>();
                        var buttons = new List<InlineKeyboardButton>();
                        foreach (var team in githubTeams)
                        {
                            if (buttons.Count == maxColLength)
                            {
                                inlineKeyBoardButtons.Add(buttons.Select(x => x).ToList());
                                buttons.RemoveRange(0, maxColLength);
                            }

                            buttons.Add(InlineKeyboardButton.WithCallbackData(team.Name, team.Id.ToString()));
                        }

                        var keyboardMarkup = new InlineKeyboardMarkup(inlineKeyBoardButtons);

                        inlineMessage.ReplyMarkup = keyboardMarkup;

                        await _bot.EditMessageTextAsync(
                            message.Chat.Id,
                            message.MessageId,
                            _chooseTeam,
                            ParseMode.Default,
                            false,
                            keyboardMarkup);
                    }
                    else
                    {
                        await _bot.SendTextMessageAsync(message.Chat.Id, _questionEnterName, replyMarkup: new ForceReplyMarkup { Selective = false });
                    }

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
/inline - send inline keyboard";

            await _bot.SendTextMessageAsync(
                chatId,
                String.IsNullOrWhiteSpace(text) ? usage : text,
                replyMarkup: new ReplyKeyboardRemove());
        }
        #endregion
    }
}
