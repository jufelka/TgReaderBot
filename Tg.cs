using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class Tg(string botToken)
{
    private static readonly int ParagraphsPerPage = 10;
    private static readonly Dictionary<long, UserFileData> UserFiles = new();

    public Db Db { get; } = new();
    public TelegramBotClient Bot { get; private set; }
    public User Me { get; private set; }

    public async Task Start()
    {
        using var cts = new CancellationTokenSource();

        Bot = new TelegramBotClient(botToken, cancellationToken: cts.Token);
        Bot.StartReceiving(OnUpdate, OnError, cancellationToken: cts.Token);

        Me = await Bot.GetMe(cancellationToken: cts.Token);
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
                await HandleBook(msg);
            }
        }

        else if (text.StartsWith('/'))
        {
            var space = text.IndexOf(' ');

            if (space < 0)
                space = text.Length;

            var command = text[..space].ToLower();

            if (command.LastIndexOf('@') is > 0 and var at) // it's a targeted command
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
        Console.WriteLine($"Received command: {command} {args} in {msg.Chat}");

        switch (command)
        {
            case "/start":
                bool isUserExists = await Db.CheckUserExists(msg.Chat.Id);

                if (!isUserExists)
                {
                    await Db.AddUser(msg.Chat.Id, msg.Chat.Username);
                }

                await Bot.SendMessage(msg.Chat, """
                        <b><u>Bot menu</u></b>:
                        /read   - read current book
                        /new    - read a new book
                        /my     - my current book
                        """,
                    replyMarkup: new ReplyKeyboardRemove()); // remove keyboard to clean-up things

                break;

            case "/read":
                string? myCurrentBookId = await Db.GetCurrentBookId(msg.Chat.Id);

                if (string.IsNullOrWhiteSpace(myCurrentBookId))
                {
                    await Bot.SendMessage(msg.Chat, "You have no books. Please upload any.", replyMarkup: new ReplyKeyboardRemove());
                }

                else
                {
                    await ReadBook(msg);
                }

                break;

            case "/new":
                await Bot.SendMessage(msg.Chat, $"Please upload a book in .fb format", replyMarkup: new ReplyKeyboardRemove());
                await Db.SetCurrentBook(msg.Chat.Id, msg.Document!.FileUniqueId);

                break;

            case "/my":
                string? currentBookId = await Db.GetCurrentBookId(msg.Chat.Id);

                if (string.IsNullOrWhiteSpace(currentBookId))
                {
                    await Bot.SendMessage(msg.Chat, "You have no books. Please upload any.", replyMarkup: new ReplyKeyboardRemove());
                }

                else
                {
                    string? currentBook = await Db.GetCurrentBookTitle(msg.Chat.Id, currentBookId);

                    await Bot.SendMessage(msg.Chat, $"Current book: {currentBook}", replyMarkup: new ReplyKeyboardRemove());
                }

                break;

            case "/keyboard":
                List<List<KeyboardButton>> keys =
                [
                    ["1.1", "1.2", "1.3"],
                        ["2.1", "2.2"],
                    ];
                await Bot.SendMessage(msg.Chat, "Keyboard buttons:", replyMarkup: new ReplyKeyboardMarkup(keys) { ResizeKeyboard = true });

                break;

            // todo: add select book command
            // todo: add list books command
            // todo: add delete book command
            // todo: add jump to page command
        }
    }

    private async Task OnCallbackQuery(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Message == null)
        {
            await Bot.AnswerCallbackQuery(callbackQuery.Id, "No message context.");
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var data = callbackQuery.Data ?? string.Empty;

        string? currentBookId = await Db.GetCurrentBookId(chatId);

        await Bot.AnswerCallbackQuery(callbackQuery.Id, $"Selected: {data}");

        if (data.StartsWith("nav:"))
        {
            if (!UserFiles.TryGetValue(chatId, out var userData))
            {
                //await Bot.SendMessage(chatId, "Please send a .fb2 file first.");
                return;
            }

            var direction = data[(data.IndexOf(':') + 1)..];

            switch (direction)
            {
                case "next":
                    userData.CurrentPage++;
                    await Db.UpdateCurrentPage(currentBookId, userData.CurrentPage); // todo

                    await ReadBook(callbackQuery.Message);

                    break;

                case "prev":
                    if (userData.CurrentPage > 0)
                    {
                        userData.CurrentPage--;
                        await Db.UpdateCurrentPage(currentBookId, userData.CurrentPage); // todo

                        await ReadBook(callbackQuery.Message);
                    }
                    else
                    {
                        await Bot.SendMessage(chatId, "Already at the start of the book.");
                    }

                    break;

                case "close":
                    await Bot.SendMessage(chatId, "Closed reading session.", replyMarkup: new ReplyKeyboardRemove());

                    break;

                default:
                    await Bot.SendMessage(chatId, $"Unknown navigation command: {direction}");

                    break;
            }
        }
        else
        {
            // existing fallback behavior
            await Bot.SendMessage(chatId, $"Received callback from inline button {data}");
        }
    }

    private async Task HandleBook(Message msg)
    {
        if (msg.Document != null && msg.Document.FileName!.EndsWith(".fb2"))
        {
            var file = await Bot.GetFile(msg.Document.FileId);
            var filePath = $"{msg.Document.FileId}.fb2";

            await using var saveStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await Bot.DownloadFile(file.FilePath!, saveStream);
            await saveStream.DisposeAsync();

            var xdoc = XDocument.Load(filePath);

            UserFiles[msg.Chat.Id] = new UserFileData { FilePath = filePath, Document = xdoc, CurrentPage = 0 };

            await Db.AddBook(msg.Chat.Id, msg.Document.FileUniqueId, msg.Document.FileName, msg.Document.FileId);
            await Db.SetCurrentBook(msg.Chat.Id, msg.Document.FileUniqueId);

            await Bot.SendMessage(msg.Chat, "File received! Use /read to start reading.", replyMarkup: new ReplyKeyboardRemove());
        }

        else
        {
            await Bot.SendMessage(msg.Chat, "Please send a .fb2 file.", replyMarkup: new ReplyKeyboardRemove());
        }
    }

    private async Task<bool> HandleExistingBook(Message msg)
    {
        string bookId = await Db.GetCurrentBookId(msg.Chat.Id);
        string fileId = await Db.GetBookFileId(msg.Chat.Id, bookId);

        var filePath = $"{fileId}.fb2";

        if (!File.Exists(filePath))
        {
            await Db.RemoveBook(bookId);
            await Db.SetCurrentBook(msg.Chat.Id, "");

            await Bot.SendMessage(msg.Chat, "Can't find a book", replyMarkup: new ReplyKeyboardRemove());
            return false;
        }
            

        var xdoc = XDocument.Load(filePath);

        int currentPage = await Db.GetCurrentPage(bookId);

        UserFiles[msg.Chat.Id] = new UserFileData { FilePath = filePath, Document = xdoc, CurrentPage = currentPage };
        return true;
    }

    private async Task ReadBook(Message msg)
    {
        bool isActiveBook = UserFiles.TryGetValue(msg.Chat.Id, out var userData);

        if (!isActiveBook)
        {
            bool isExisting = await HandleExistingBook(msg);
            isActiveBook = isExisting;
        }

        if (isActiveBook)
        {
            userData = UserFiles[msg.Chat.Id];

            var paragraphs = userData.Document?.Descendants("{http://www.gribuser.ru/xml/fictionbook/2.0}p").ToList();
            var start = userData.CurrentPage * ParagraphsPerPage;
            var end = start + ParagraphsPerPage;

            if (paragraphs != null && start >= paragraphs.Count)
            {
                await Bot.SendMessage(msg.Chat.Id, "You've reached the end of the book.");
                return;
            }

            if (paragraphs != null)
            {
                var pageContent = string.Join("\n\n", paragraphs.Skip(start).Take(end - start).Select(p => p.Value));

                var nav = new List<InlineKeyboardButton[]>
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("◀️ Prev", "nav:prev"),
                        //InlineKeyboardButton.WithCallbackData("Close", "nav:close"),
                        InlineKeyboardButton.WithCallbackData("Next ▶️", "nav:next")
                    }
                };

                await Bot.SendMessage(msg.Chat.Id, pageContent, replyMarkup: new InlineKeyboardMarkup(nav));
            }
        }
        else
        {
            await Bot.SendMessage(msg.Chat.Id, "Please send a .fb2 file first.");
        }
    }
}
