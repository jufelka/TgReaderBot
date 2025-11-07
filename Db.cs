using Microsoft.Data.Sqlite;

public class Db
{
    private readonly string _connectionString = "Data Source=tg_reader_bot.db";

    public async Task CreateDb()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        user_id INTEGER PRIMARY KEY,
                        username TEXT,
                        current_book_id TEXT
                    );";

        var createBooksTable = @"
                    CREATE TABLE IF NOT EXISTS Books (
                        book_id TEXT PRIMARY KEY,
                        file_id TEXT NOT NULL,
                        title TEXT NOT NULL,
                        user_id INTEGER,
                        current_page INTEGER DEFAULT 0,
                        FOREIGN KEY (user_id) REFERENCES Users (user_id)
                    );";

        await using (var command = new SqliteCommand(createUsersTable, connection))
        {
            await command.ExecuteScalarAsync();
        }

        await using (var command = new SqliteCommand(createBooksTable, connection))
        {
            await command.ExecuteScalarAsync();
        }
    }

    public async Task AddUser(long userId, string? username)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var insertUserCommand = @"
                    INSERT INTO Users (user_id, username)
                    VALUES (@user_id, @username);";

        await using var command = new SqliteCommand(insertUserCommand, connection);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@username", username);

        await command.ExecuteScalarAsync();
    }

    public async Task<bool> CheckUserExists(long userId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    SELECT @user_id FROM Users
                    WHERE user_id = @user_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@user_id", userId);

        var user = await command.ExecuteScalarAsync();

        return user != null;
    }

    public async Task AddBook(long userId, string bookId, string filename, string fileId)
    {
        string title = Path.GetFileNameWithoutExtension(filename);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var insertFileCommand = @"
                    INSERT INTO Books (book_id, file_id, title, user_id)
                    VALUES (@book_id, @file_id, @title, @user_id)";

        await using var command = new SqliteCommand(insertFileCommand, connection);
        command.Parameters.AddWithValue("@book_id", bookId);
        command.Parameters.AddWithValue("@file_id", fileId);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@user_id", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SetCurrentBook(long userId, string bookId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    UPDATE Users
                    SET current_book_id = @book_id
                    WHERE user_id = @user_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@book_id", bookId);

        await command.ExecuteNonQueryAsync();
    }

    // todo
    public async Task<List<string>> GetUserBooksList(long userId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    SELECT current_book_id FROM Users
                    WHERE user_id = @user_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@user_id", userId);

        var bookId = await command.ExecuteScalarAsync();

        return null;
    }

    public async Task<string?> GetCurrentBookId(long userId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    SELECT current_book_id FROM Users
                    WHERE user_id = @user_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@user_id", userId);

        var bookId = await command.ExecuteScalarAsync();

        return bookId?.ToString();
    }

    public async Task<string?> GetBookFileId(long userId, string bookId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    SELECT file_id 
                    FROM Books
                    WHERE book_id = @book_id AND user_id = @user_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@book_id", bookId);

        var fileId = await command.ExecuteScalarAsync();

        return fileId?.ToString();
    }

    public async Task RemoveBook(string bookId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var updatePageCommand = @"
                    DELETE FROM Books
                    WHERE book_id = @book_id;";

        await using var command = new SqliteCommand(updatePageCommand, connection);
        command.Parameters.AddWithValue("@book_id", bookId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetCurrentBookTitle(long userId, string bookId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    SELECT title 
                    FROM Books
                    WHERE book_id = @book_id AND user_id = @user_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@book_id", bookId);

        var bookTitle = (string)(await command.ExecuteScalarAsync())!;

        return bookTitle;
    }

    public async Task UpdateCurrentPage(string bookId, int currentPage)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var updatePageCommand = @"
                    UPDATE Books
                    SET current_page = @current_page
                    WHERE book_id = @book_id;";

        await using var command = new SqliteCommand(updatePageCommand, connection);
        command.Parameters.AddWithValue("@current_page", currentPage);
        command.Parameters.AddWithValue("@book_id", bookId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> GetCurrentPage(string bookId)
    {
        int currentPage = 0;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var readPageCommand = @"
                    SELECT current_page FROM Books
                    WHERE book_id = @book_id;";

        await using var command = new SqliteCommand(readPageCommand, connection);
        command.Parameters.AddWithValue("@book_id", bookId);

        await using var reader = await command.ExecuteReaderAsync();

        if (reader.Read())
        {
            currentPage = reader.GetInt32(0);
        }

        return currentPage;
    }

    public async Task SetBookIsRead(string bookId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    UPDATE Books
                    SET is_read = TRUE
                    WHERE book_id = @book_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@book_id", bookId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> CheckBookIsRead(string bookId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
                    SELECT is_read FROM Books
                    WHERE book_id = @book_id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@book_id", bookId);

        var isRead = (string)(await command.ExecuteScalarAsync())!;

        return isRead == "TRUE";
    }
}
