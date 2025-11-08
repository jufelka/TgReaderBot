using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class Tg(string botToken)
{
    private static readonly int ParagraphsPerPage = 10;
    private static readonly Dictionary<long, UserFileData> UserFiles = new();

    private static readonly ReplyKeyboardMarkup QuickMenuKeyboard = new([
        [new("📚 Menu")]
    ])
    {
        ResizeKeyboard = true,
        OneTimeKeyboard = false
    };

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

    private InlineKeyboardMarkup BuildMainInlineMenu()
    {
        var rows = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📖 Read", "menu:read"),
                InlineKeyboardButton.WithCallbackData("➕ New", "menu:new"),
                InlineKeyboardButton.WithCallbackData("👤 My Book", "menu:my")
            }
        };

        return new InlineKeyboardMarkup(rows);
    }

    private ReplyKeyboardMarkup BuildQuickMenuButton()
    {
        var keys = new[]
        {
            new[]
            {
                new KeyboardButton("📚 Menu")
            }
        };

        return new ReplyKeyboardMarkup(keys) { ResizeKeyboard = true, OneTimeKeyboard = false };
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

                var inlineMenu = BuildMainInlineMenu();
                await Bot.SendMessage(msg.Chat, "Choose an action:", replyMarkup: inlineMenu);

                await Bot.SendMessage(msg.Chat, "︆", replyMarkup: QuickMenuKeyboard);

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
                await Bot.SendMessage(msg.Chat, $"Please upload a book in fb2 or epub format", replyMarkup: new ReplyKeyboardRemove());

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

        if (data.StartsWith("menu:"))
        {
            var action = data[(data.IndexOf(':') + 1)..];

            switch (action)
            {
                case "read":
                    {
                        // Inform user with a small popup and then open reading
                        //await Bot.AnswerCallbackQuery(callbackQuery.Id, "Opening your book...", showAlert: false);

                        string? currentBookId = await Db.GetCurrentBookId(chatId);
                        if (string.IsNullOrWhiteSpace(currentBookId))
                        {
                            // show alert to ask for upload
                            await Bot.AnswerCallbackQuery(callbackQuery.Id, "You have no books. Upload a book (.fb2 or .epub).", showAlert: true);
                            return;
                        }

                        // call ReadBook using the original message as context
                        await ReadBook(callbackQuery.Message);
                        break;
                    }

                case "new":
                    {
                        // popup instructing user to upload
                        await Bot.AnswerCallbackQuery(callbackQuery.Id, "Please upload a .fb2 or .epub file.", showAlert: true);
                        // also send a persistent message in chat
                        await Bot.SendMessage(chatId, "Please upload a book in fb2 or epub format", replyMarkup: new ReplyKeyboardRemove());
                        break;
                    }

                case "my":
                    {
                        string? currentBookId = await Db.GetCurrentBookId(chatId);
                        if (string.IsNullOrWhiteSpace(currentBookId))
                        {
                            await Bot.AnswerCallbackQuery(callbackQuery.Id, "You have no books.", showAlert: true);
                            return;
                        }

                        string? currentBook = await Db.GetCurrentBookTitle(chatId, currentBookId);
                        var text = string.IsNullOrWhiteSpace(currentBook) ? "Current book: (unknown title)" : $"Current book: {currentBook}";

                        // show current book title as a popup and also send in chat
                        await Bot.AnswerCallbackQuery(callbackQuery.Id, text, showAlert: true);
                        await Bot.SendMessage(chatId, text, replyMarkup: new ReplyKeyboardRemove());
                        break;
                    }

                default:
                    await Bot.AnswerCallbackQuery(callbackQuery.Id, $"Unknown menu action: {action}", showAlert: true);
                    break;
            }

            return;
        }

        //string? currentBookId = await Db.GetCurrentBookId(chatId);

        // navigation and other callbacks handled below
        string? currentBookIdNav = await Db.GetCurrentBookId(chatId);

        //await Bot.AnswerCallbackQuery(callbackQuery.Id, $"Selected: {data}");

        //await Bot.AnswerCallbackQuery(callbackQuery.Id, $"Selected: {data}");

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
                    await Db.UpdateCurrentPage(currentBookIdNav, userData.CurrentPage); // todo

                    await ReadBook(callbackQuery.Message);

                    break;

                case "prev":
                    if (userData.CurrentPage > 0)
                    {
                        userData.CurrentPage--;
                        await Db.UpdateCurrentPage(currentBookIdNav, userData.CurrentPage); // todo

                        await ReadBook(callbackQuery.Message);
                    }
                    else
                    {
                        await Bot.SendMessage(chatId, "Already at the start of the book.");
                    }

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
        if (msg.Document == null)
        {
            await Bot.SendMessage(msg.Chat, "Please send a book file (.fb2 or .epub).", replyMarkup: new ReplyKeyboardRemove());
            return;
        }

        var fileName = msg.Document.FileName ?? string.Empty;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".fb2" || ext == ".epub")
        {
            var file = await Bot.GetFile(msg.Document.FileId);
            var filePath = $"{msg.Document.FileId}{ext}";

            await using (var saveStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await Bot.DownloadFile(file.FilePath!, saveStream);
            }

            if (ext == ".fb2")
            {
                var xdoc = XDocument.Load(filePath);
                UserFiles[msg.Chat.Id] = new UserFileData { FilePath = filePath, Document = xdoc, EpubParagraphs = null, CurrentPage = 0 };
            }
            else // .epub
            {
                var paragraphs = ParseEpubToParagraphs(filePath);
                UserFiles[msg.Chat.Id] = new UserFileData { FilePath = filePath, Document = null, EpubParagraphs = paragraphs, CurrentPage = 0 };
            }

            await Db.AddBook(msg.Chat.Id, msg.Document.FileUniqueId, msg.Document.FileName, msg.Document.FileId);
            await Db.SetCurrentBook(msg.Chat.Id, msg.Document.FileUniqueId);

            await Bot.SendMessage(msg.Chat, "File received! Use 'Read' from menu to start reading.", replyMarkup: new ReplyKeyboardRemove());
        }
        else
        {
            await Bot.SendMessage(msg.Chat, "Unsupported file format. Please send .fb2 or .epub file.", replyMarkup: new ReplyKeyboardRemove());
        }
    }

    private async Task<bool> HandleExistingBook(Message msg)
    {
        string bookId = await Db.GetCurrentBookId(msg.Chat.Id) ?? string.Empty;

        if (string.IsNullOrEmpty(bookId))
            return false;

        string fileId = await Db.GetBookFileId(msg.Chat.Id, bookId) ?? string.Empty;

        var filePathFb2 = $"{fileId}.fb2";
        var filePathEpub = $"{fileId}.epub";

        if (File.Exists(filePathFb2))
        {
            var xdoc = XDocument.Load(filePathFb2);
            int currentPage = await Db.GetCurrentPage(bookId);

            UserFiles[msg.Chat.Id] = new UserFileData { FilePath = filePathFb2, Document = xdoc, EpubParagraphs = null, CurrentPage = currentPage };
            return true;
        }
        else if (File.Exists(filePathEpub))
        {
            var paragraphs = ParseEpubToParagraphs(filePathEpub);
            int currentPage = await Db.GetCurrentPage(bookId);

            UserFiles[msg.Chat.Id] = new UserFileData { FilePath = filePathEpub, Document = null, EpubParagraphs = paragraphs, CurrentPage = currentPage };
            
            return true;
        }
        else
        {
            await Db.RemoveBook(bookId);
            await Db.SetCurrentBook(msg.Chat.Id, "");

            await Bot.SendMessage(msg.Chat, "Please send a .fb2 or .epub file.", replyMarkup: new ReplyKeyboardRemove());
            
            return false;
        }
    }

    private async Task ReadBook(Message msg)
    {
        bool isActiveBook = UserFiles.TryGetValue(msg.Chat.Id, out var userData);

        if (!isActiveBook)
        {
            bool isExisting = await HandleExistingBook(msg);
            isActiveBook = isExisting;
        }

        userData = UserFiles[msg.Chat.Id];

        // FB2 flow
        if (userData.Document != null)
        {
            var paragraphs = userData.Document.Descendants("{http://www.gribuser.ru/xml/fictionbook/2.0}p").ToList();
            var start = userData.CurrentPage * ParagraphsPerPage;
            var end = start + ParagraphsPerPage;

            if (start >= paragraphs.Count)
            {
                await Bot.SendMessage(msg.Chat.Id, "You've reached the end of the book.");
                return;
            }

            var pageContent = string.Join("\n\n", paragraphs.Skip(start).Take(end - start).Select(p => p.Value));

            var nav = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("◀️ Prev", "nav:prev"),
                    InlineKeyboardButton.WithCallbackData("Next ▶️", "nav:next")
                }
            };

            await Bot.SendMessage(msg.Chat.Id, pageContent, replyMarkup: new InlineKeyboardMarkup(nav));
            return;
        }

        // EPUB flow
        if (userData.EpubParagraphs != null)
        {
            var paragraphs = userData.EpubParagraphs;
            var start = userData.CurrentPage * ParagraphsPerPage;

            if (start >= paragraphs.Count)
            {
                await Bot.SendMessage(msg.Chat.Id, "You've reached the end of the book.");
                return;
            }

            var pageContent = string.Join("\n\n", paragraphs.Skip(start).Take(ParagraphsPerPage));

            var nav = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("◀️ Prev", "nav:prev"),
                    InlineKeyboardButton.WithCallbackData("Next ▶️", "nav:next")
                }
            };

            await Bot.SendMessage(msg.Chat.Id, pageContent, replyMarkup: new InlineKeyboardMarkup(nav));
            return;
        }

        // fallback
        await Bot.SendMessage(msg.Chat.Id, "Unsupported book format stored in session.");
    }

    // Basic EPUB -> paragraphs extractor without external libraries.
    // It unzips EPUB and extracts text from <p> and <h*> tags from .xhtml/.html files.
    private static List<string> ParseEpubToParagraphs(string epubFilePath)
    {
        var paragraphs = new List<string>();

        try
        {
            using var archive = ZipFile.OpenRead(epubFilePath);

            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.ToLowerInvariant();

                if (!name.EndsWith(".xhtml") && !name.EndsWith(".html") && !name.EndsWith(".htm"))
                    continue;

                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var html = reader.ReadToEnd();

                // Extract <p>...</p> and heading tags
                foreach (Match m in Regex.Matches(html, @"<(p|h[1-6])\b[^>]*>(.*?)</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
                {
                    var inner = m.Groups[2].Value;
                    // strip any remaining tags inside
                    var text = Regex.Replace(inner, "<.*?>", string.Empty).Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // normalize multiple whitespace/newlines
                        text = Regex.Replace(text, @"\s{2,}", " ");
                        paragraphs.Add(text);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse epub {epubFilePath}: {ex}");
        }

        return paragraphs;
    }
}
