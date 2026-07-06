using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using WordUp.Models;

namespace WordUp.Services;

public sealed partial class LocalStorageService
{
    private void InitializeDatabase()
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenConnection();
        MigrateLegacySharedSchema(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS accounts (
                email TEXT PRIMARY KEY COLLATE NOCASE,
                full_name TEXT NOT NULL,
                phone TEXT NOT NULL DEFAULT '',
                note TEXT NOT NULL DEFAULT '',
                avatar_path TEXT NOT NULL DEFAULT '',
                password_hash TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS lessons (
                id TEXT PRIMARY KEY,
                account_email TEXT NOT NULL COLLATE NOCASE,
                name TEXT NOT NULL,
                learned_words INTEGER NOT NULL DEFAULT 0,
                total_words INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (account_email) REFERENCES accounts(email) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS lesson_words (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                lesson_id TEXT NOT NULL,
                word TEXT NOT NULL,
                ipa TEXT NOT NULL DEFAULT '',
                type TEXT NOT NULL DEFAULT '',
                meaning TEXT NOT NULL,
                vietnamese_meaning TEXT NOT NULL DEFAULT '',
                audio_path TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS word_progress (
                lesson_word_id INTEGER PRIMARY KEY,
                mastery_level INTEGER NOT NULL DEFAULT 0,
                review_count INTEGER NOT NULL DEFAULT 0,
                last_reviewed_at TEXT NULL,
                next_review_date TEXT NOT NULL,
                FOREIGN KEY (lesson_word_id) REFERENCES lesson_words(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS word_practice_stats (
                lesson_word_id INTEGER PRIMARY KEY,
                correct_quiz_count INTEGER NOT NULL DEFAULT 0,
                incorrect_quiz_count INTEGER NOT NULL DEFAULT 0,
                practice_correct_quiz_count INTEGER NOT NULL DEFAULT 0,
                practice_incorrect_quiz_count INTEGER NOT NULL DEFAULT 0,
                last_practice_at TEXT NULL,
                FOREIGN KEY (lesson_word_id) REFERENCES lesson_words(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS favorites (
                account_email TEXT NOT NULL COLLATE NOCASE,
                lesson_word_id INTEGER NOT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (account_email, lesson_word_id),
                FOREIGN KEY (account_email) REFERENCES accounts(email) ON DELETE CASCADE,
                FOREIGN KEY (lesson_word_id) REFERENCES lesson_words(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS settings (
                account_email TEXT PRIMARY KEY COLLATE NOCASE,
                audio_volume REAL NOT NULL DEFAULT 75,
                daily_reminders INTEGER NOT NULL DEFAULT 1,
                auto_play_audio INTEGER NOT NULL DEFAULT 0,
                offline_mode INTEGER NOT NULL DEFAULT 0,
                is_dark_mode INTEGER NOT NULL DEFAULT 0,
                practice_session_count INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (account_email) REFERENCES accounts(email) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS practice_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                account_email TEXT NOT NULL COLLATE NOCASE,
                started_at TEXT NOT NULL,
                completed_at TEXT NOT NULL,
                duration_seconds INTEGER NOT NULL DEFAULT 0,
                total_questions INTEGER NOT NULL DEFAULT 0,
                correct_answers INTEGER NOT NULL DEFAULT 0,
                word_source TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (account_email) REFERENCES accounts(email) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS practice_answers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL,
                lesson_id TEXT NOT NULL DEFAULT '',
                word TEXT NOT NULL,
                selected_choice TEXT NOT NULL DEFAULT '',
                correct_choice TEXT NOT NULL DEFAULT '',
                is_correct INTEGER NOT NULL DEFAULT 0,
                answered_at TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES practice_sessions(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_lessons_account_email ON lessons(account_email);
            CREATE INDEX IF NOT EXISTS ix_lesson_words_lesson_id ON lesson_words(lesson_id);
            CREATE INDEX IF NOT EXISTS ix_word_progress_lesson_word_id ON word_progress(lesson_word_id);
            CREATE INDEX IF NOT EXISTS ix_word_practice_stats_lesson_word_id ON word_practice_stats(lesson_word_id);
            CREATE INDEX IF NOT EXISTS ix_practice_sessions_account_email ON practice_sessions(account_email);
            CREATE INDEX IF NOT EXISTS ix_practice_answers_session_id ON practice_answers(session_id);
            CREATE INDEX IF NOT EXISTS ix_favorites_account_email ON favorites(account_email);
            """;
        command.ExecuteNonQuery();
        EnsureCleanAccountsSchema(connection);
        EnsureAccountNoteColumn(connection);
        EnsureAccountAvatarPathColumn(connection);
        EnsureSettingsDarkModeColumn(connection);
        EnsureSettingsPracticeSessionCountColumn(connection);
        EnsureLessonWordAudioPathColumn(connection);
        EnsureCleanLessonWordsSchema(connection);
        BackfillLegacyWordData(connection);
        ClearSharedAccountAvatars(connection);
    }

    private static void MigrateLegacySharedSchema(SqliteConnection connection)
    {
        var legacyOwnerEmail = GetLegacyOwnerEmail(connection);

        if (TableExists(connection, "lessons") && !ColumnExists(connection, "lessons", "account_email"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE lessons ADD COLUMN account_email TEXT NOT NULL DEFAULT '';";
            command.ExecuteNonQuery();

            if (!string.IsNullOrWhiteSpace(legacyOwnerEmail))
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = """
                    UPDATE lessons
                    SET account_email = $accountEmail
                    WHERE account_email = '';
                    """;
                updateCommand.Parameters.AddWithValue("$accountEmail", legacyOwnerEmail);
                updateCommand.ExecuteNonQuery();
            }
        }

        if (TableExists(connection, "settings") && !ColumnExists(connection, "settings", "account_email"))
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "ALTER TABLE settings ADD COLUMN account_email TEXT NOT NULL DEFAULT '';";
                command.ExecuteNonQuery();
            }

            if (!string.IsNullOrWhiteSpace(legacyOwnerEmail))
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = """
                    UPDATE settings
                    SET account_email = $accountEmail
                    WHERE account_email = '';
                    """;
                updateCommand.Parameters.AddWithValue("$accountEmail", legacyOwnerEmail);
                updateCommand.ExecuteNonQuery();
            }

            using var dedupeCommand = connection.CreateCommand();
            dedupeCommand.CommandText = """
                DELETE FROM settings
                WHERE rowid NOT IN (
                    SELECT MIN(rowid)
                    FROM settings
                    GROUP BY account_email
                );
                """;
            dedupeCommand.ExecuteNonQuery();

            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ux_settings_account_email ON settings(account_email);";
            indexCommand.ExecuteNonQuery();
        }
    }

    private static string GetLegacyOwnerEmail(SqliteConnection connection)
    {
        if (!TableExists(connection, "accounts"))
        {
            return "";
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT email
            FROM accounts
            ORDER BY lower(email)
            LIMIT 1;
            """;

        return command.ExecuteScalar() as string ?? "";
    }

    private static void EnsureCleanAccountsSchema(SqliteConnection connection)
    {
        if (!TableExists(connection, "accounts"))
        {
            return;
        }

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = "DELETE FROM accounts WHERE lower(email) = lower($email);";
            deleteCommand.Parameters.AddWithValue("$email", "student@university.edu");
            deleteCommand.ExecuteNonQuery();
        }

        if (ColumnExists(connection, "accounts", "level"))
        {
            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = "ALTER TABLE accounts DROP COLUMN level;";
            dropCommand.ExecuteNonQuery();
        }

        if (ColumnExists(connection, "accounts", "created_at"))
        {
            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = "ALTER TABLE accounts DROP COLUMN created_at;";
            dropCommand.ExecuteNonQuery();
        }

        if (ColumnExists(connection, "accounts", "updated_at"))
        {
            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = "ALTER TABLE accounts DROP COLUMN updated_at;";
            dropCommand.ExecuteNonQuery();
        }
    }

    private static void EnsureAccountNoteColumn(SqliteConnection connection)
    {
        if (!TableExists(connection, "accounts") || ColumnExists(connection, "accounts", "note"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE accounts ADD COLUMN note TEXT NOT NULL DEFAULT '';";
        command.ExecuteNonQuery();
    }

    private static void EnsureAccountAvatarPathColumn(SqliteConnection connection)
    {
        if (!TableExists(connection, "accounts") || ColumnExists(connection, "accounts", "avatar_path"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE accounts ADD COLUMN avatar_path TEXT NOT NULL DEFAULT '';";
        command.ExecuteNonQuery();
    }

    private static void EnsureSettingsDarkModeColumn(SqliteConnection connection)
    {
        if (!TableExists(connection, "settings") || ColumnExists(connection, "settings", "is_dark_mode"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE settings ADD COLUMN is_dark_mode INTEGER NOT NULL DEFAULT 0;";
        command.ExecuteNonQuery();
    }

    private static void EnsureSettingsPracticeSessionCountColumn(SqliteConnection connection)
    {
        if (!TableExists(connection, "settings") || ColumnExists(connection, "settings", "practice_session_count"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE settings ADD COLUMN practice_session_count INTEGER NOT NULL DEFAULT 0;";
        command.ExecuteNonQuery();
    }

    private static void EnsureLessonWordAudioPathColumn(SqliteConnection connection)
    {
        if (!TableExists(connection, "lesson_words"))
        {
            return;
        }

        AddLessonWordColumnIfMissing(connection, "audio_path", "TEXT NOT NULL DEFAULT ''");
    }

    private static void EnsureCleanLessonWordsSchema(SqliteConnection connection)
    {
        if (TableExists(connection, "lesson_words") && ColumnExists(connection, "lesson_words", "example"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE lesson_words DROP COLUMN example;";
            command.ExecuteNonQuery();
        }
    }

    private static void BackfillLegacyWordData(SqliteConnection connection)
    {
        if (!TableExists(connection, "word_progress") || !TableExists(connection, "word_practice_stats") || !TableExists(connection, "favorites"))
        {
            return;
        }

        if (!ColumnExists(connection, "lesson_words", "mastery_level"))
        {
            return;
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT OR IGNORE INTO word_progress (
                    lesson_word_id, mastery_level, review_count, last_reviewed_at, next_review_date)
                SELECT
                    id, mastery_level, review_count, last_reviewed_at, next_review_date
                FROM lesson_words;
                """;
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT OR IGNORE INTO word_practice_stats (
                    lesson_word_id, correct_quiz_count, incorrect_quiz_count,
                    practice_correct_quiz_count, practice_incorrect_quiz_count, last_practice_at)
                SELECT
                    id, correct_quiz_count, incorrect_quiz_count,
                    practice_correct_quiz_count, practice_incorrect_quiz_count, last_practice_at
                FROM lesson_words;
                """;
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT OR IGNORE INTO favorites (account_email, lesson_word_id)
                SELECT l.account_email, lw.id
                FROM lesson_words lw
                INNER JOIN lessons l ON l.id = lw.lesson_id
                WHERE lw.is_favorite = 1;
                """;
            command.ExecuteNonQuery();
        }
    }

    private static void AddLessonWordColumnIfMissing(SqliteConnection connection, string columnName, string definition)
    {
        if (ColumnExists(connection, "lesson_words", columnName))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE lesson_words ADD COLUMN {columnName} {definition};";
        command.ExecuteNonQuery();
    }

    private static void ClearSharedAccountAvatars(SqliteConnection connection)
    {
        if (!TableExists(connection, "accounts") || !ColumnExists(connection, "accounts", "avatar_path"))
        {
            return;
        }

        using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = """
            SELECT email, avatar_path
            FROM accounts
            WHERE avatar_path <> ''
            ORDER BY lower(avatar_path), lower(email);
            """;

        var rows = new List<(string Email, string AvatarPath)>();
        using (var reader = selectCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((
                    reader.GetString(0),
                    reader.GetString(1)));
            }
        }

        var emailsToClear = rows
            .GroupBy(row => row.AvatarPath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group
                .OrderBy(row => row.Email.Length)
                .ThenBy(row => row.Email, StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Select(row => row.Email))
            .ToList();

        foreach (var email in emailsToClear)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = """
                UPDATE accounts
                SET avatar_path = ''
                WHERE lower(email) = lower($email);
                """;
            updateCommand.Parameters.AddWithValue("$email", email);
            updateCommand.ExecuteNonQuery();
        }
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1;";
        command.Parameters.AddWithValue("$tableName", tableName);
        return command.ExecuteScalar() is not null;
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DropTable(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {tableName};";
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
        return connection;
    }

}
