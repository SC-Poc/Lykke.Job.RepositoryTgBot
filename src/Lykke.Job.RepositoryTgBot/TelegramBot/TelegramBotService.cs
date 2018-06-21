using Autofac;
using Common;
using Common.Log;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        Message oldMessage = null;
        TelegramBotActions actions = new TelegramBotActions("LykkeCity", "470ad0b7f6e017f98f83ef5d292ad49dd9110c63");

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
            if (oldMessage != null)
            {

                Console.WriteLine(oldMessage.Text);
            }

            oldMessage = messageEventArgs.Message;



            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text) return;

            switch (message.Text.Split(' ').First())
            {
                // send inline keyboard
                case "/inline":
                    await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                    await Task.Delay(500); // simulate longer running task

                    var inlineKeyboard = GetInlineKeyboardMarkup("Main menu");
                    
                    await _bot.SendTextMessageAsync(
                        message.Chat.Id,
                        inlineKeyboard.Text,
                        replyMarkup: inlineKeyboard.ReplyMarkup);
                    break;

                // send custom keyboard
                case "/keyboard":
                    ReplyKeyboardMarkup ReplyKeyboard = new[]
                    {
                        new[] { "1.1", "1.2" },
                        new[] { "2.1", "2.2" },
                    };

                    await _bot.SendTextMessageAsync(
                        message.Chat.Id,
                        "Choose",
                        replyMarkup: ReplyKeyboard);
                    break;

                // send a photo
                case "/photo":
                    await _bot.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto); 

                    const string file = @"TelegramBot/image.png";

                    var fileName = file.Split(Path.DirectorySeparatorChar).Last();

                    using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await _bot.SendPhotoAsync(
                            message.Chat.Id,
                            fileStream,
                            "Nice Picture");
                    }
                    break;

                // request location or contact
                case "/request":
                    var teamNames = actions.GetTeamsAsync();
                    teamNames.Result.Sort();

                    var answer = "";
                    foreach (var name in teamNames.Result)
                    {
                        answer += name + "\n";
                    }

                    await _bot.SendTextMessageAsync(
                        message.Chat.Id, answer);

                    //var RequestReplyKeyboard = new ReplyKeyboardMarkup(new[]
                    //{
                    //    KeyboardButton.WithRequestLocation("Location"),
                    //    KeyboardButton.WithRequestContact("Contact"),
                    //});

                    //await _bot.SendTextMessageAsync(
                    //    message.Chat.Id,
                    //    "Who or Where are you?",
                    //    replyMarkup: RequestReplyKeyboard);
                    break;

                default:
                    const string usage = @"
Usage:
/inline   - send inline keyboard
/keyboard - send custom keyboard
/photo    - send a photo
/request  - request location or contact";

                    await _bot.SendTextMessageAsync(
                        message.Chat.Id,
                        usage,
                        replyMarkup: new ReplyKeyboardRemove());
                    break;
            }
        }

        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            //Here implements actions on reciving
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;
            
            var inlineKeyboard = GetInlineKeyboardMarkup(callbackQuery.Data);
            if (inlineKeyboard.Text != null && inlineKeyboard.ReplyMarkup != null)
            {
                await _bot.EditMessageTextAsync(
                    callbackQuery.Message.Chat.Id, 
                    callbackQuery.Message.MessageId,
                    inlineKeyboard.Text, 
                    ParseMode.Default,
                    false,
                    inlineKeyboard.ReplyMarkup); 
            }
                
            //Send message back 
            //await Bot.SendTextMessageAsync(
            //    callbackQuery.Message.Chat.Id,
            //    $"Received {callbackQuery.Data}");
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

        private InlineMessage GetInlineKeyboardMarkup(string callbackData)
        {
            var inlineMessage = new InlineMessage();
            switch (callbackData)
            {
                case "Main menu":
                    inlineMessage.Text = @"This is main menu.
Plaese, select submenu.";
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                     {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Menu#1","Menu1"),
                            InlineKeyboardButton.WithCallbackData("Menu#2","Menu2"),
                        },
                        new [] // second row
                        {
                            InlineKeyboardButton.WithCallbackData("Menu#3","Menu3"),
                            InlineKeyboardButton.WithCallbackData("Menu#4","Menu4"),
                        }
                    });
                    break; 
                case "Menu1":
                    inlineMessage.Text = @"This is Menu #1.
Plaese, select options.";
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                     {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Menu1 Option#1","Menu1Option1"),
                        },
                        new [] // second row
                        {
                            InlineKeyboardButton.WithCallbackData("Menu1 Option#2","Menu1Option2"),
                            InlineKeyboardButton.WithCallbackData("Back to main","Main menu"),
                        }
                    });
                    break;
                case "Menu2":
                    inlineMessage.Text = @"This is Menu #2.
Plaese, select options.";
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                     {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Menu2 Option#1","Menu2Option1"),
                            InlineKeyboardButton.WithCallbackData("Menu2 Option#2","Menu2Option2"),
                        },
                        new [] // second row
                        {
                            InlineKeyboardButton.WithCallbackData("Back to main","Main menu"),
                        }
                    });
                    break;
                case "Menu3":
                    inlineMessage.Text = @"This is Menu #3.
Plaese, select options.";
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                     {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Menu3 Option#1","Menu3Option1"),
                            InlineKeyboardButton.WithCallbackData("Menu3 Option#2","Menu3Option2"),
                            InlineKeyboardButton.WithCallbackData("Menu3 Option#3","Menu3Option3"),
                        },
                        new [] // second row
                        {

                            InlineKeyboardButton.WithCallbackData("Back to main","Main menu"),
                        }
                    });
                    break;
                case "Menu4":
                    inlineMessage.Text = @"This is Menu #4.
Plaese, select options.";
                    inlineMessage.ReplyMarkup = new InlineKeyboardMarkup(new[]
                     {
                            InlineKeyboardButton.WithCallbackData("Menu4 Option#1","Menu4Option1"),
                            InlineKeyboardButton.WithCallbackData("Menu4 Option#2","Menu4Option2"),
                            InlineKeyboardButton.WithCallbackData("Menu4 Option#3","Menu4Option3"),
                            InlineKeyboardButton.WithCallbackData("Back to main","Main menu"),

                    });
                    break;
            }
            
            return inlineMessage;
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
    }
}
