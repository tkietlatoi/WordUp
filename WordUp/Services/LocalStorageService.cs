using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using WordUp.Models;

namespace WordUp.Services;

public sealed class LocalStorageService
{
    private readonly string databasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WordUp",
        "wordup.db");

    private string ConnectionString => $"Data Source={databasePath}";

    public AppState Load()
    {
        InitializeDatabase();
        return new AppState();
    }

    public AppState? LoadAccount(string email)
    {
        InitializeDatabase();

        using var connection = OpenConnection();
        var user = LoadUser(connection, email);
        if (user is null)
        {
            return null;
        }

        return new AppState
        {
            User = user,
            Decks = LoadDecks(connection, user.Email),
            Words = LoadWords(connection, user.Email),
            Settings = LoadSettings(connection, user.Email)
        };
    }

    public void Save(AppState state)
    {
        if (string.IsNullOrWhiteSpace(state.User.Email))
        {
            return;
        }

        InitializeDatabase();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        UpsertAccount(connection, transaction, state.User, existingPasswordHash: null);
        ReplaceLessons(connection, transaction, state.User.Email, state.Decks);
        ReplaceLessonWords(connection, transaction, state.User.Email, state.Words);
        UpsertSettings(connection, transaction, state.User.Email, state.Settings);

        transaction.Commit();
    }

    public bool RegisterAccount(UserProfile user, string password)
    {
        InitializeDatabase();

        using var connection = OpenConnection();
        if (AccountExists(connection, user.Email) || PhoneExists(connection, user.Phone))
        {
            return false;
        }

        using var transaction = connection.BeginTransaction();
        UpsertAccount(connection, transaction, user, HashPassword(password));
        UpsertSettings(connection, transaction, user.Email, new SettingsState());
        transaction.Commit();
        return true;
    }

    public bool ValidateLogin(string contact, string password, UserProfile targetUser)
    {
        InitializeDatabase();

        var trimmedContact = contact.Trim();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT full_name, email, phone, level, password_hash
            FROM accounts
            WHERE lower(email) = lower($contact)
               OR phone = $contact
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$contact", trimmedContact);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || !VerifyPassword(password, reader.GetString(4)))
        {
            return false;
        }

        targetUser.FullName = reader.GetString(0);
        targetUser.Email = reader.GetString(1);
        targetUser.Phone = reader.GetString(2);
        targetUser.Level = reader.GetString(3);
        return true;
    }

    public string? FindAccountEmailByContact(string contact)
    {
        if (string.IsNullOrWhiteSpace(contact))
        {
            return null;
        }

        InitializeDatabase();

        var trimmedContact = contact.Trim();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT email
            FROM accounts
            WHERE lower(email) = lower($contact)
               OR phone = $contact
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$contact", trimmedContact);

        var result = command.ExecuteScalar();
        return result as string;
    }

    public bool UpdatePassword(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        InitializeDatabase();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE accounts
            SET password_hash = $passwordHash,
                updated_at = CURRENT_TIMESTAMP
            WHERE lower(email) = lower($email);
            """;
        command.Parameters.AddWithValue("$email", email.Trim());
        command.Parameters.AddWithValue("$passwordHash", HashPassword(password));
        return command.ExecuteNonQuery() > 0;
    }

    public void Delete(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        InitializeDatabase();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM accounts WHERE lower(email) = lower($email);";
        command.Parameters.AddWithValue("$email", email.Trim());
        command.ExecuteNonQuery();
    }

    public void Delete()
    {
        InitializeDatabase();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var table in new[] { "lesson_words", "lessons", "settings", "accounts" })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table};";
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void InitializeDatabase()
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenConnection();
        ResetLegacySchemaIfNeeded(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS accounts (
                email TEXT PRIMARY KEY COLLATE NOCASE,
                full_name TEXT NOT NULL,
                phone TEXT NOT NULL DEFAULT '',
                level TEXT NOT NULL DEFAULT '',
                password_hash TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
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
                example TEXT NOT NULL DEFAULT '',
                mastery_level INTEGER NOT NULL DEFAULT 0,
                review_count INTEGER NOT NULL DEFAULT 0,
                correct_quiz_count INTEGER NOT NULL DEFAULT 0,
                incorrect_quiz_count INTEGER NOT NULL DEFAULT 0,
                last_reviewed_at TEXT NULL,
                next_review_date TEXT NOT NULL,
                FOREIGN KEY (lesson_id) REFERENCES lessons(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS settings (
                account_email TEXT PRIMARY KEY COLLATE NOCASE,
                audio_volume REAL NOT NULL DEFAULT 75,
                daily_reminders INTEGER NOT NULL DEFAULT 1,
                auto_play_audio INTEGER NOT NULL DEFAULT 0,
                offline_mode INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (account_email) REFERENCES accounts(email) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_lessons_account_email ON lessons(account_email);
            CREATE INDEX IF NOT EXISTS ix_lesson_words_lesson_id ON lesson_words(lesson_id);
            """;
        command.ExecuteNonQuery();
    }

    private static void ResetLegacySchemaIfNeeded(SqliteConnection connection)
    {
        using var foreignKeysOff = connection.CreateCommand();
        foreignKeysOff.CommandText = "PRAGMA foreign_keys = OFF;";
        foreignKeysOff.ExecuteNonQuery();

        if (TableExists(connection, "lessons") && !ColumnExists(connection, "lessons", "account_email"))
        {
            DropTable(connection, "lesson_words");
            DropTable(connection, "lessons");
        }

        if (TableExists(connection, "settings") && !ColumnExists(connection, "settings", "account_email"))
        {
            DropTable(connection, "settings");
        }

        using var foreignKeysOn = connection.CreateCommand();
        foreignKeysOn.CommandText = "PRAGMA foreign_keys = ON;";
        foreignKeysOn.ExecuteNonQuery();
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
        return connection;
    }

    private static UserProfile? LoadUser(SqliteConnection connection, string email)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT full_name, email, phone, level
            FROM accounts
            WHERE lower(email) = lower($email)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$email", email.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new UserProfile
        {
            FullName = reader.GetString(0),
            Email = reader.GetString(1),
            Phone = reader.GetString(2),
            Level = reader.GetString(3)
        };
    }

    private static List<Deck> LoadDecks(SqliteConnection connection, string accountEmail)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, learned_words, total_words, created_at, updated_at
            FROM lessons
            WHERE lower(account_email) = lower($accountEmail)
            ORDER BY datetime(updated_at) DESC;
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());

        using var reader = command.ExecuteReader();
        var decks = new List<Deck>();
        while (reader.Read())
        {
            decks.Add(new Deck
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                LearnedWords = reader.GetInt32(2),
                TotalWords = reader.GetInt32(3),
                CreatedAt = ParseDateTime(reader.GetString(4)),
                UpdatedAt = ParseDateTime(reader.GetString(5))
            });
        }

        return decks;
    }

    private static List<VocabularyWord> LoadWords(SqliteConnection connection, string accountEmail)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT lw.lesson_id, lw.word, lw.ipa, lw.type, lw.meaning, lw.vietnamese_meaning, lw.example,
                   lw.mastery_level, lw.review_count, lw.correct_quiz_count, lw.incorrect_quiz_count,
                   lw.last_reviewed_at, lw.next_review_date
            FROM lesson_words lw
            INNER JOIN lessons l ON l.id = lw.lesson_id
            WHERE lower(l.account_email) = lower($accountEmail)
            ORDER BY lw.id;
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());

        using var reader = command.ExecuteReader();
        var words = new List<VocabularyWord>();
        while (reader.Read())
        {
            words.Add(new VocabularyWord
            {
                LessonId = reader.GetString(0),
                Word = reader.GetString(1),
                Ipa = reader.GetString(2),
                Type = reader.GetString(3),
                Meaning = reader.GetString(4),
                VietnameseMeaning = reader.GetString(5),
                Example = reader.GetString(6),
                MasteryLevel = reader.GetInt32(7),
                ReviewCount = reader.GetInt32(8),
                CorrectQuizCount = reader.GetInt32(9),
                IncorrectQuizCount = reader.GetInt32(10),
                LastReviewedAt = reader.IsDBNull(11) ? null : ParseDateTime(reader.GetString(11)),
                NextReviewDate = ParseDateTime(reader.GetString(12))
            });
        }

        return words;
    }

    private static SettingsState LoadSettings(SqliteConnection connection, string accountEmail)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT audio_volume, daily_reminders, auto_play_audio, offline_mode
            FROM settings
            WHERE lower(account_email) = lower($accountEmail);
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new SettingsState();
        }

        return new SettingsState
        {
            AudioVolume = reader.GetDouble(0),
            DailyReminders = reader.GetInt32(1) == 1,
            AutoPlayAudio = reader.GetInt32(2) == 1,
            OfflineMode = reader.GetInt32(3) == 1
        };
    }

    private static bool AccountExists(SqliteConnection connection, string email)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM accounts WHERE lower(email) = lower($email) LIMIT 1;";
        command.Parameters.AddWithValue("$email", email.Trim());
        return command.ExecuteScalar() is not null;
    }

    private static bool PhoneExists(SqliteConnection connection, string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM accounts WHERE phone = $phone LIMIT 1;";
        command.Parameters.AddWithValue("$phone", phone.Trim());
        return command.ExecuteScalar() is not null;
    }

    private static void UpsertAccount(
        SqliteConnection connection,
        SqliteTransaction transaction,
        UserProfile user,
        string? existingPasswordHash)
    {
        var passwordHash = existingPasswordHash ?? LoadPasswordHash(connection, transaction, user.Email) ?? HashPassword("password123");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO accounts (email, full_name, phone, level, password_hash, updated_at)
            VALUES ($email, $fullName, $phone, $level, $passwordHash, CURRENT_TIMESTAMP)
            ON CONFLICT(email) DO UPDATE SET
                full_name = excluded.full_name,
                phone = excluded.phone,
                level = excluded.level,
                updated_at = CURRENT_TIMESTAMP;
            """;
        command.Parameters.AddWithValue("$email", user.Email.Trim());
        command.Parameters.AddWithValue("$fullName", user.FullName.Trim());
        command.Parameters.AddWithValue("$phone", user.Phone.Trim());
        command.Parameters.AddWithValue("$level", user.Level.Trim());
        command.Parameters.AddWithValue("$passwordHash", passwordHash);
        command.ExecuteNonQuery();
    }

    private static string? LoadPasswordHash(SqliteConnection connection, SqliteTransaction transaction, string email)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT password_hash FROM accounts WHERE lower(email) = lower($email) LIMIT 1;";
        command.Parameters.AddWithValue("$email", email.Trim());
        return command.ExecuteScalar() as string;
    }

    private static void ReplaceLessons(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string accountEmail,
        IEnumerable<Deck> decks)
    {
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM lessons WHERE lower(account_email) = lower($accountEmail);";
            deleteCommand.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var deck in decks)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO lessons (id, account_email, name, learned_words, total_words, created_at, updated_at)
                VALUES ($id, $accountEmail, $name, $learnedWords, $totalWords, $createdAt, $updatedAt);
                """;
            command.Parameters.AddWithValue("$id", deck.Id);
            command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
            command.Parameters.AddWithValue("$name", deck.Name);
            command.Parameters.AddWithValue("$learnedWords", deck.LearnedWords);
            command.Parameters.AddWithValue("$totalWords", deck.TotalWords);
            command.Parameters.AddWithValue("$createdAt", FormatDateTime(deck.CreatedAt));
            command.Parameters.AddWithValue("$updatedAt", FormatDateTime(deck.UpdatedAt));
            command.ExecuteNonQuery();
        }
    }

    private static void ReplaceLessonWords(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string accountEmail,
        IEnumerable<VocabularyWord> words)
    {
        var validLessonIds = LoadLessonIds(connection, transaction, accountEmail);

        foreach (var word in words.Where(word => validLessonIds.Contains(word.LessonId)))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO lesson_words (
                    lesson_id, word, ipa, type, meaning, vietnamese_meaning, example,
                    mastery_level, review_count, correct_quiz_count, incorrect_quiz_count,
                    last_reviewed_at, next_review_date)
                VALUES (
                    $lessonId, $word, $ipa, $type, $meaning, $vietnameseMeaning, $example,
                    $masteryLevel, $reviewCount, $correctQuizCount, $incorrectQuizCount,
                    $lastReviewedAt, $nextReviewDate);
                """;
            command.Parameters.AddWithValue("$lessonId", word.LessonId);
            command.Parameters.AddWithValue("$word", word.Word);
            command.Parameters.AddWithValue("$ipa", word.Ipa);
            command.Parameters.AddWithValue("$type", word.Type);
            command.Parameters.AddWithValue("$meaning", word.Meaning);
            command.Parameters.AddWithValue("$vietnameseMeaning", word.VietnameseMeaning);
            command.Parameters.AddWithValue("$example", word.Example);
            command.Parameters.AddWithValue("$masteryLevel", word.MasteryLevel);
            command.Parameters.AddWithValue("$reviewCount", word.ReviewCount);
            command.Parameters.AddWithValue("$correctQuizCount", word.CorrectQuizCount);
            command.Parameters.AddWithValue("$incorrectQuizCount", word.IncorrectQuizCount);
            command.Parameters.AddWithValue("$lastReviewedAt", word.LastReviewedAt is null ? DBNull.Value : FormatDateTime(word.LastReviewedAt.Value));
            command.Parameters.AddWithValue("$nextReviewDate", FormatDateTime(word.NextReviewDate));
            command.ExecuteNonQuery();
        }
    }

    private static HashSet<string> LoadLessonIds(SqliteConnection connection, SqliteTransaction transaction, string accountEmail)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM lessons WHERE lower(account_email) = lower($accountEmail);";
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());

        using var reader = command.ExecuteReader();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static void UpsertSettings(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string accountEmail,
        SettingsState settings)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO settings (account_email, audio_volume, daily_reminders, auto_play_audio, offline_mode)
            VALUES ($accountEmail, $audioVolume, $dailyReminders, $autoPlayAudio, $offlineMode)
            ON CONFLICT(account_email) DO UPDATE SET
                audio_volume = excluded.audio_volume,
                daily_reminders = excluded.daily_reminders,
                auto_play_audio = excluded.auto_play_audio,
                offline_mode = excluded.offline_mode;
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
        command.Parameters.AddWithValue("$audioVolume", settings.AudioVolume);
        command.Parameters.AddWithValue("$dailyReminders", settings.DailyReminders ? 1 : 0);
        command.Parameters.AddWithValue("$autoPlayAudio", settings.AutoPlayAudio ? 1 : 0);
        command.Parameters.AddWithValue("$offlineMode", settings.OfflineMode ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private static string HashPassword(string password)
    {
        const int iterations = 100_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"pbkdf2-sha256:{iterations}:{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 4
            || parts[0] != "pbkdf2-sha256"
            || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromHexString(parts[2]);
        var expectedHash = Convert.FromHexString(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    private static DateTime ParseDateTime(string value)
    {
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToLocalTime()
            : DateTime.Now;
    }
}
