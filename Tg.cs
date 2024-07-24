using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Security.Cryptography;

namespace TgReaderBot
{
    public class Tg
    {
        const string Token = "7334347471:AAEzLP6Q-Fg2_Ogk6iBnmMLUBGUXiejkLHU";

        public Db Db { get; } = new ();
        public TelegramBotClient Bot { get; private set; }
        public User Me { get; private set; }

        public async Task Start()
        {
            using var cts = new CancellationTokenSource();

            Bot = new TelegramBotClient(Token, cancellationToken: cts.Token);
            Me = await Bot.GetMeAsync(cancellationToken: cts.Token);

            Bot.StartReceiving(OnUpdate, OnError, cancellationToken: cts.Token);

            Console.WriteLine($"@{Me.Username} is running... Press Escape to terminate");
            while (Console.ReadKey(true).Key != ConsoleKey.Escape)
            { }

            await cts.CancelAsync();
        }

        private async Task OnError(ITelegramBotClient client, Exception exception, CancellationToken ct)
        {
            Console.WriteLine(exception);
            await Task.Delay(2000, ct);
        }

        private async Task OnUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            await (update switch
            {
                { Message: { } message } => OnMessage(message),
                { EditedMessage: { } message } => OnMessage(message, true),
                { CallbackQuery: { } callbackQuery } => OnCallbackQuery(callbackQuery),
                _ => OnUnhandledUpdate(update)
            });
        }

        private Task OnUnhandledUpdate(Update update)
        {
            Console.WriteLine($"Received unhandled update {update.Type}");
            return Task.CompletedTask;
        }

        private async Task OnMessage(Message msg, bool edited = false)
        {
            if (msg.Text is not { } text)
            {
                Console.WriteLine($"Received a message of type {msg.Type}");

                if (msg.Type == MessageType.Document)
                {
                    bool isFb2 = msg.Document.FileName.Contains(".fb2");

                    if (!isFb2)
                    {
                        await Bot.SendTextMessageAsync(msg.Chat, $"The book is not in .fb2 format", replyMarkup: new ReplyKeyboardRemove());
                    }

                    else
                    {
                        await Db.AddBook(msg.Chat.Id, msg.Document.FileUniqueId, msg.Document.FileName, msg.Document.FileId);
                        await Db.SetCurrentBook(msg.Chat.Id, msg.Document.FileUniqueId);

                        await Bot.SendTextMessageAsync(msg.Chat, $"The book {msg.Document.FileName} is ready", replyMarkup: new ReplyKeyboardRemove());
                    }
                }
            }

            else if (text.StartsWith('/'))
            {
                var space = text.IndexOf(' ');

                if (space < 0)
                    space = text.Length;

                var command = text[..space].ToLower();

                if (command.LastIndexOf('@') is > 0 and int at) // it's a targeted command
                    if (command[(at + 1)..].Equals(Me.Username, StringComparison.OrdinalIgnoreCase))
                        command = command[..at];
                    else
                        return; // command was not targeted at me

                await OnCommand(command, text[space..].TrimStart(), msg);
            }
            else
                await OnTextMessage(msg);
        }

        private async Task OnTextMessage(Message msg) // received a text message that is not a command
        {
            Console.WriteLine($"Received text '{msg.Text}' in {msg.Chat}");

            await OnCommand("/start", "", msg); // for now, we redirect to command /start
        }

        private async Task OnCommand(string command, string args, Message msg)
        {
            Console.WriteLine($"Received command: {command} {args}");

            switch (command)
            {
                case "/start":
                    bool isUserExists = await Db.CheckUserExists(msg.Chat.Id);
                    if (!isUserExists)
                    {
                        await Db.AddUser(msg.Chat.Id, msg.Chat.Username);
                    }

                    await Bot.SendTextMessageAsync(msg.Chat, """
                        <b><u>Bot menu</u></b>:
                        /read   - read current book
                        /new    - read a new book
                        /my     - my current book
                        """, parseMode: ParseMode.Html, linkPreviewOptions: true,
                        replyMarkup: new ReplyKeyboardRemove()); // also remove keyboard to clean-up things
                    break;

                case "/read":
                    string myBook = await Db.GetCurrentBookTitle(msg.Chat.Id);

                    if (string.IsNullOrWhiteSpace(myBook))
                    {
                        await Bot.SendTextMessageAsync(msg.Chat, "You have no books. Please upload any.", replyMarkup: new ReplyKeyboardRemove());
                    }

                    else
                    {
                        await Bot.SendTextMessageAsync(msg.Chat, $"Reading: {myBook}", replyMarkup: new ReplyKeyboardRemove());
                    }

                    break;

                case "/new":
                    await Bot.SendTextMessageAsync(msg.Chat, $"Please upload a book in .fb format", replyMarkup: new ReplyKeyboardRemove());

                    break;

                case "/my":
                    string currentBook = await Db.GetCurrentBookTitle(msg.Chat.Id);

                    if (string.IsNullOrWhiteSpace(currentBook))
                    {
                        await Bot.SendTextMessageAsync(msg.Chat, "You have no books. Please upload any.", replyMarkup: new ReplyKeyboardRemove());
                    }

                    else
                    {
                        await Bot.SendTextMessageAsync(msg.Chat, $"My current book: {currentBook}", replyMarkup: new ReplyKeyboardRemove());
                    }

                    break;

                case "/inline_buttons":
                    List<List<InlineKeyboardButton>> buttons =
                    [
                        ["1.1", "1.2", "1.3"],
                        [
                            InlineKeyboardButton.WithCallbackData("WithCallbackData", "CallbackData"),
                            InlineKeyboardButton.WithUrl("WithUrl", "https://github.com/TelegramBots/Telegram.Bot")
                        ],
                    ];
                    await Bot.SendTextMessageAsync(msg.Chat, "Inline buttons:", replyMarkup: new InlineKeyboardMarkup(buttons));
                    break;

                case "/keyboard":
                    List<List<KeyboardButton>> keys =
                    [
                        ["1.1", "1.2", "1.3"],
                        ["2.1", "2.2"],
                    ];
                    await Bot.SendTextMessageAsync(msg.Chat, "Keyboard buttons:", replyMarkup: new ReplyKeyboardMarkup(keys) { ResizeKeyboard = true });
                    break;

                case "/remove":
                    await Bot.SendTextMessageAsync(msg.Chat, "Removing keyboard", replyMarkup: new ReplyKeyboardRemove());
                    break;
            }
        }

        private async Task OnCallbackQuery(CallbackQuery callbackQuery)
        {
            await Bot.AnswerCallbackQueryAsync(callbackQuery.Id, $"You selected {callbackQuery.Data}");
            await Bot.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Received callback from inline button {callbackQuery.Data}");
        }
    }
}
