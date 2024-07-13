using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace TgReaderBot
{
    internal class Db
    {
        private readonly string _connectionString = "Data Source=tg_reader_bot.db";

        public void CreateDb()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        user_id INTEGER PRIMARY KEY,
                        username TEXT,
                        chat_id INTEGER NOT NULL,
                        current_book INTEGER
                    );";

                var createBooksTable = @"
                    CREATE TABLE IF NOT EXISTS Books (
                        book_id INTEGER PRIMARY KEY AUTOINCREMENT,
                        file_path TEXT NOT NULL,
                        title TEXT NOT NULL,
                        user_id INTEGER,
                        is_readed INTEGER DEFAULT FALSE,
                        FOREIGN KEY (user_id) REFERENCES Users (user_id)
                    );";

                var createPagesTable = @"
                    CREATE TABLE IF NOT EXISTS Pages (
                        book_id INTEGER PRIMARY KEY,
                        total_pages INTEGER
                        current_page INTEGER DEFAULT 1,
                        is_readed INTEGER DEFAULT FALSE,
                        last_access TEXT,
                        FOREIGN KEY (book_id) REFERENCES Books (book_id)
                    );";

                using (var command = new SqliteCommand(createUsersTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new SqliteCommand(createBooksTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new SqliteCommand(createPagesTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void AddUser(int userId, string username, long chatId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var insertUserCommand = @"
                    INSERT INTO Users (user_id, username, chat_id)
                    VALUES (@user_id, @username, @chat_id);";

                using (var command = new SqliteCommand(insertUserCommand, connection))
                {
                    command.Parameters.AddWithValue("@user_id", 12345);
                    command.Parameters.AddWithValue("@username", "john_doe");
                    command.Parameters.AddWithValue("@chat_id", 67890);

                    command.ExecuteNonQuery();
                }
            }
        }

        public void AddBook(int userId, string filePath)
        {
            string title = string.Empty;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var insertFileCommand = @"
                    INSERT INTO Books (user_id, file_path, title)
                    VALUES (@user_id, @file_path, @title);";

                using (var command = new SqliteCommand(insertFileCommand, connection))
                {
                    command.Parameters.AddWithValue("@user_id", 12345);
                    command.Parameters.AddWithValue("@file_path", "path/to/file.fb2");
                    command.Parameters.AddWithValue("@title", "title");

                    command.ExecuteNonQuery();
                }
            }
        }

        public void SetCurrentBook(int userId, int bookId)
        {

        }

        public void GetCurrentBook(int userId)
        {

        }

        public void SetBookIsRead(int userId, int bookId)
        {

        }

        public bool CheckBookIsRead(int bookId)
        {

            return false;
        }

        public List<int> SelectAllUserBooks(int userId)
        {

            return new List<int> { 0 };
        }

        public void UpdateCurrentPage(int bookId, int newCurrentPage)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var updatePageCommand = @"
                    UPDATE Pages
                    SET current_page = @current_page
                    WHERE book_id = @book_id;";

                using (var command = new SqliteCommand(updatePageCommand, connection))
                {
                    command.Parameters.AddWithValue("@current_page", 5);
                    command.Parameters.AddWithValue("@book_id", 3);

                    command.ExecuteNonQuery();
                }
            }
        }

        public void GetCurrentPage(int bookId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var readPageCommand = @"
                    SELECT current_page FROM Pages
                    WHERE book_id = @book_id;";

                using (var command = new SqliteCommand(readPageCommand, connection))
                {
                    command.Parameters.AddWithValue("@book_id", 12345);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var currentPage = reader.GetInt32(0);
                            Console.WriteLine($"Current page: {currentPage}");
                        }
                    }
                }
            }
        }

        public void SetTotalPages()
        {

        }

        public void SetCurentPageIsRead()
        {

        }

        public void SetPageLastAccess()
        {

        }

        public void GetPageLastAccess()
        {

        }
    }
}
