using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Telegram.Bot.Types;

namespace TgReaderBot
{
    public class Db
    {
        private readonly string _connectionString = "Data Source=tg_reader_bot.db";

        public async Task CreateDb()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
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
                        filename TEXT NOT NULL,
                        title TEXT NOT NULL,
                        user_id INTEGER,
                        is_read INTEGER DEFAULT FALSE,
                        FOREIGN KEY (user_id) REFERENCES Users (user_id)
                    );";

                var createPagesTable = @"
                    CREATE TABLE IF NOT EXISTS Pages (
                        book_id TEXT PRIMARY KEY,
                        total_pages INTEGER
                        current_page INTEGER DEFAULT 1,
                        is_read INTEGER DEFAULT FALSE,
                        last_access TEXT,
                        FOREIGN KEY (book_id) REFERENCES Books (book_id)
                    );";

                using (var command = new SqliteCommand(createUsersTable, connection))
                {
                    await command.ExecuteScalarAsync();
                }

                using (var command = new SqliteCommand(createBooksTable, connection))
                {
                    await command.ExecuteScalarAsync();
                }

                using (var command = new SqliteCommand(createPagesTable, connection))
                {
                    await command.ExecuteScalarAsync();
                }
            }
        }

        public async Task AddUser(long userId, string? username)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var insertUserCommand = @"
                    INSERT INTO Users (user_id, username)
                    VALUES (@user_id, @username);";

                using (var command = new SqliteCommand(insertUserCommand, connection))
                {
                    command.Parameters.AddWithValue("@user_id", userId);
                    command.Parameters.AddWithValue("@username", username);

                    await command.ExecuteScalarAsync();
                }
            }
        }

        public async Task<bool> CheckUserExists(long userId)
        {
            object? user;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"SELECT @user_id FROM Users
                                    WHERE user_id = @user_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@user_id", userId);

                    user = await command.ExecuteScalarAsync();
                }
            }

            return user != null;
        }

        public async Task<string> AddBook(long userId, string bookId, string filename, string fileId)
        {
            string title = Path.GetFileNameWithoutExtension(filename);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var insertFileCommand = @"
                    INSERT INTO Books (book_id, file_id, filename, title, user_id)
                    VALUES (@book_id, @file_id, @filename, @title, @user_id)
                    RETURNING book_id;";

                using (var command = new SqliteCommand(insertFileCommand, connection))
                {
                    command.Parameters.AddWithValue("@book_id", userId);
                    command.Parameters.AddWithValue("@file_id", fileId);
                    command.Parameters.AddWithValue("@filename", filename);
                    command.Parameters.AddWithValue("@title", title);
                    command.Parameters.AddWithValue("@user_id", userId);

                    await command.ExecuteNonQueryAsync();
                }
            }

            return bookId;
        }

        public async Task SetCurrentBook(long userId, string bookId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"UPDATE Users
                                    SET current_book_id = @book_id
                                    WHERE user_id = @user_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@user_id", userId);
                    command.Parameters.AddWithValue("@book_id", bookId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<string?> GetCurrentBookId(long userId)
        {
            object? bookId;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"SELECT current_book FROM Users
                                    WHERE user_id = @user_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@user_id", userId);

                    bookId = await command.ExecuteScalarAsync();
                }
            }

            return (string?)bookId;
        }

        public async Task<string?> GetCurrentBookTitle(long userId)
        {
            object? book;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"SELECT title 
                                    FROM Books
                                    WHERE user_id = @user_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@user_id", userId);

                    book = await command.ExecuteScalarAsync();
                }
            }

            return (string?)book;
        }

        public async Task SetBookIsRead(string bookId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"UPDATE Books
                                    SET is_read = TRUE
                                    WHERE book_id = @book_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@book_id", bookId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> CheckBookIsRead(string bookId)
        {
            string isRead;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"SELECT is_read FROM Books
                                    WHERE book_id = @book_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@book_id", bookId);

                    isRead = (string)await command.ExecuteScalarAsync();
                }
            }

            return isRead == "TRUE";
        }

        public async Task UpdateCurrentPage(string bookId, int newCurrentPage)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var updatePageCommand = @"
                    UPDATE Pages
                    SET current_page = @current_page
                    WHERE book_id = @book_id;";

                using (var command = new SqliteCommand(updatePageCommand, connection))
                {
                    command.Parameters.AddWithValue("@current_page", newCurrentPage);
                    command.Parameters.AddWithValue("@book_id", bookId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<int> GetCurrentPage(string bookId)
        {
            int currentPage = 0;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var readPageCommand = @"
                    SELECT current_page FROM Pages
                    WHERE book_id = @book_id;";

                using (var command = new SqliteCommand(readPageCommand, connection))
                {
                    command.Parameters.AddWithValue("@book_id", bookId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            currentPage = reader.GetInt32(0);
                        }
                    }
                }
            }

            return currentPage;
        }

        public async Task SetTotalPages(string bookId, int totalPages)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"UPDATE Pages
                                    SET total_pages = @total_pages
                                    WHERE book_id = @book_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@book_id", bookId);
                    command.Parameters.AddWithValue("@total_pages", totalPages);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task SetCurentPageIsRead(string bookId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"UPDATE Pages
                                    SET is_read = TRUE
                                    WHERE book_id = @book_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@book_id", bookId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task SetPageLastAccess(string bookId, string timestamp)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"UPDATE Pages
                                    SET last_access = @last_access
                                    WHERE book_id = @book_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@book_id", bookId);
                    command.Parameters.AddWithValue("@last_access", timestamp);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<string?> GetPageLastAccess(string bookId)
        {
            string? timestamp = string.Empty;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"SELECT last_access FROM Pages
                                    WHERE book_id = @book_id";

                using (var command = new SqliteCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@book_id", bookId);

                    timestamp = (string)await command.ExecuteScalarAsync();
                }
            }

            return timestamp;
        }
    }
}
