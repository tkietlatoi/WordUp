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

    public void SavePracticeSession(
        string accountEmail,
        TimeSpan duration,
        string wordSource,
        IReadOnlyCollection<QuizQuestion> questions)
    {
        if (string.IsNullOrWhiteSpace(accountEmail) || questions.Count == 0)
        {
            return;
        }

        InitializeDatabase();

        var completedAt = DateTime.Now;
        var startedAt = completedAt.Subtract(duration);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var sessionCommand = connection.CreateCommand();
        sessionCommand.Transaction = transaction;
        sessionCommand.CommandText = """
            INSERT INTO practice_sessions (
                account_email, started_at, completed_at, duration_seconds,
                total_questions, correct_answers, word_source)
            VALUES (
                $accountEmail, $startedAt, $completedAt, $durationSeconds,
                $totalQuestions, $correctAnswers, $wordSource);
            SELECT last_insert_rowid();
            """;
        sessionCommand.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
        sessionCommand.Parameters.AddWithValue("$startedAt", FormatDateTime(startedAt));
        sessionCommand.Parameters.AddWithValue("$completedAt", FormatDateTime(completedAt));
        sessionCommand.Parameters.AddWithValue("$durationSeconds", (int)Math.Round(duration.TotalSeconds));
        sessionCommand.Parameters.AddWithValue("$totalQuestions", questions.Count);
        sessionCommand.Parameters.AddWithValue("$correctAnswers", questions.Count(question => question.IsCorrect));
        sessionCommand.Parameters.AddWithValue("$wordSource", wordSource);

        var sessionId = Convert.ToInt64(sessionCommand.ExecuteScalar());

        foreach (var question in questions)
        {
            using var answerCommand = connection.CreateCommand();
            answerCommand.Transaction = transaction;
            answerCommand.CommandText = """
                INSERT INTO practice_answers (
                    session_id, lesson_id, word, selected_choice, correct_choice,
                    is_correct, answered_at)
                VALUES (
                    $sessionId, $lessonId, $word, $selectedChoice, $correctChoice,
                    $isCorrect, $answeredAt);
                """;
            answerCommand.Parameters.AddWithValue("$sessionId", sessionId);
            answerCommand.Parameters.AddWithValue("$lessonId", question.SourceWord?.LessonId ?? "");
            answerCommand.Parameters.AddWithValue("$word", question.Term);
            answerCommand.Parameters.AddWithValue("$selectedChoice", question.SelectedChoice);
            answerCommand.Parameters.AddWithValue("$correctChoice", question.CorrectChoice);
            answerCommand.Parameters.AddWithValue("$isCorrect", question.IsCorrect ? 1 : 0);
            answerCommand.Parameters.AddWithValue("$answeredAt", FormatDateTime(completedAt));
            answerCommand.ExecuteNonQuery();
        }

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
            SELECT full_name, email, phone, level, note, avatar_path, password_hash
            FROM accounts
            WHERE lower(email) = lower($contact)
               OR phone = $contact
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$contact", trimmedContact);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || !VerifyPassword(password, reader.GetString(6)))
        {
            return false;
        }

        targetUser.FullName = reader.GetString(0);
        targetUser.Email = reader.GetString(1);
        targetUser.Phone = reader.GetString(2);
        targetUser.Level = reader.GetString(3);
        targetUser.Note = reader.GetString(4);
        targetUser.AvatarPath = reader.GetString(5);
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

        foreach (var table in new[] { "practice_answers", "practice_sessions", "lesson_words", "lessons", "settings", "accounts" })
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
        MigrateLegacySharedSchema(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS accounts (
                email TEXT PRIMARY KEY COLLATE NOCASE,
                full_name TEXT NOT NULL,
                phone TEXT NOT NULL DEFAULT '',
                level TEXT NOT NULL DEFAULT '',
                note TEXT NOT NULL DEFAULT '',
                avatar_path TEXT NOT NULL DEFAULT '',
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
        EnsureAccountNoteColumn(connection);
        EnsureAccountAvatarPathColumn(connection);
        EnsureSettingsDarkModeColumn(connection);
        EnsureSettingsPracticeSessionCountColumn(connection);
        EnsureLessonWordAudioPathColumn(connection);
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
            ORDER BY datetime(updated_at) DESC, datetime(created_at) DESC, lower(email)
            LIMIT 1;
            """;

        return command.ExecuteScalar() as string ?? "";
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
            SELECT email, avatar_path, updated_at
            FROM accounts
            WHERE avatar_path <> ''
            ORDER BY lower(avatar_path), datetime(updated_at), lower(email);
            """;

        var rows = new List<(string Email, string AvatarPath, DateTime UpdatedAt)>();
        using (var reader = selectCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    ParseDateTime(reader.GetString(2))));
            }
        }

        var emailsToClear = rows
            .GroupBy(row => row.AvatarPath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group
                .OrderBy(row => row.Email.Length)
                .ThenBy(row => row.UpdatedAt)
                .ThenBy(row => row.Email, StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Select(row => row.Email))
            .ToList();

        foreach (var email in emailsToClear)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = """
                UPDATE accounts
                SET avatar_path = '',
                    updated_at = CURRENT_TIMESTAMP
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

    private static UserProfile? LoadUser(SqliteConnection connection, string email)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT full_name, email, phone, level, note, avatar_path
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
            Level = reader.GetString(3),
            Note = reader.GetString(4),
            AvatarPath = reader.GetString(5)
        };
    }

    private static List<Deck> LoadDecks(SqliteConnection connection, string accountEmail)
    {
        var hasAccountEmailColumn = ColumnExists(connection, "lessons", "account_email");
        var decks = hasAccountEmailColumn
            ? LoadDecksByAccountEmail(connection, accountEmail)
            : LoadLegacyDecks(connection);

        if (decks.Count == 0 && hasAccountEmailColumn && !string.IsNullOrWhiteSpace(accountEmail))
        {
            decks = LoadDecksByAccountEmail(connection, "");
        }

        return decks;
    }

    private static List<VocabularyWord> LoadWords(SqliteConnection connection, string accountEmail)
    {
        var hasAccountEmailColumn = ColumnExists(connection, "lessons", "account_email");
        var words = hasAccountEmailColumn
            ? LoadWordsByAccountEmail(connection, accountEmail)
            : LoadLegacyWords(connection);

        if (words.Count == 0 && hasAccountEmailColumn && !string.IsNullOrWhiteSpace(accountEmail))
        {
            words = LoadWordsByAccountEmail(connection, "");
        }

        return words;
    }

    private static SettingsState LoadSettings(SqliteConnection connection, string accountEmail)
    {
        var hasAccountEmailColumn = ColumnExists(connection, "settings", "account_email");
        var settings = hasAccountEmailColumn
            ? LoadSettingsByAccountEmail(connection, accountEmail)
            : LoadLegacySettings(connection);

        if (settings is null && hasAccountEmailColumn && !string.IsNullOrWhiteSpace(accountEmail))
        {
            settings = LoadSettingsByAccountEmail(connection, "");
        }

        return settings ?? new SettingsState();
    }

    private static List<Deck> LoadDecksByAccountEmail(SqliteConnection connection, string accountEmail)
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

    private static List<VocabularyWord> LoadWordsByAccountEmail(SqliteConnection connection, string accountEmail)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT lw.lesson_id, lw.word, lw.ipa, lw.type, lw.meaning, lw.vietnamese_meaning, lw.audio_path,
                   COALESCE(p.mastery_level, 0), COALESCE(p.review_count, 0),
                   COALESCE(s.correct_quiz_count, 0), COALESCE(s.incorrect_quiz_count, 0),
                   COALESCE(s.practice_correct_quiz_count, 0), COALESCE(s.practice_incorrect_quiz_count, 0),
                   CASE WHEN f.lesson_word_id IS NULL THEN 0 ELSE 1 END,
                   p.last_reviewed_at, s.last_practice_at, COALESCE(p.next_review_date, $today)
            FROM lesson_words lw
            INNER JOIN lessons l ON l.id = lw.lesson_id
            LEFT JOIN word_progress p ON p.lesson_word_id = lw.id
            LEFT JOIN word_practice_stats s ON s.lesson_word_id = lw.id
            LEFT JOIN favorites f ON f.lesson_word_id = lw.id AND lower(f.account_email) = lower($accountEmail)
            WHERE lower(l.account_email) = lower($accountEmail)
            ORDER BY lw.id;
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
        command.Parameters.AddWithValue("$today", FormatDateTime(DateTime.Today));

        return ReadWords(command);
    }

    private static SettingsState? LoadSettingsByAccountEmail(SqliteConnection connection, string accountEmail)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT audio_volume, daily_reminders, auto_play_audio, offline_mode, is_dark_mode, practice_session_count
            FROM settings
            WHERE lower(account_email) = lower($accountEmail);
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var storedPracticeSessionCount = reader.GetInt32(5);
        var savedPracticeSessionCount = CountPracticeSessions(connection, accountEmail);

        return new SettingsState
        {
            AudioVolume = reader.GetDouble(0),
            DailyReminders = reader.GetInt32(1) == 1,
            AutoPlayAudio = reader.GetInt32(2) == 1,
            OfflineMode = reader.GetInt32(3) == 1,
            IsDarkMode = reader.GetInt32(4) == 1,
            PracticeSessionCount = Math.Max(storedPracticeSessionCount, savedPracticeSessionCount)
        };
    }

    private static List<VocabularyWord> ReadWords(SqliteCommand command)
    {
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
                AudioPath = reader.GetString(6),
                MasteryLevel = reader.GetInt32(7),
                ReviewCount = reader.GetInt32(8),
                CorrectQuizCount = reader.GetInt32(9),
                IncorrectQuizCount = reader.GetInt32(10),
                PracticeCorrectQuizCount = reader.GetInt32(11),
                PracticeIncorrectQuizCount = reader.GetInt32(12),
                IsFavorite = reader.GetInt32(13) == 1,
                LastReviewedAt = reader.IsDBNull(14) ? null : ParseDateTime(reader.GetString(14)),
                LastPracticeAt = reader.IsDBNull(15) ? null : ParseDateTime(reader.GetString(15)),
                NextReviewDate = ParseDateTime(reader.GetString(16))
            });
        }

        return words;
    }

    private static List<Deck> LoadLegacyDecks(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, learned_words, total_words, created_at, updated_at
            FROM lessons
            ORDER BY datetime(updated_at) DESC;
            """;

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

    private static List<VocabularyWord> LoadLegacyWords(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT lw.lesson_id, lw.word, lw.ipa, lw.type, lw.meaning, lw.vietnamese_meaning, lw.example,
                   COALESCE(lw.mastery_level, 0), COALESCE(lw.review_count, 0),
                   COALESCE(lw.correct_quiz_count, 0), COALESCE(lw.incorrect_quiz_count, 0),
                   COALESCE(lw.practice_correct_quiz_count, 0), COALESCE(lw.practice_incorrect_quiz_count, 0),
                   COALESCE(lw.is_favorite, 0), lw.last_reviewed_at, lw.last_practice_at,
                   COALESCE(lw.next_review_date, $today)
            FROM lesson_words lw
            INNER JOIN lessons l ON l.id = lw.lesson_id
            ORDER BY lw.id;
            """;
        command.Parameters.AddWithValue("$today", FormatDateTime(DateTime.Today));

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
                AudioPath = "",
                MasteryLevel = reader.GetInt32(7),
                ReviewCount = reader.GetInt32(8),
                CorrectQuizCount = reader.GetInt32(9),
                IncorrectQuizCount = reader.GetInt32(10),
                PracticeCorrectQuizCount = reader.GetInt32(11),
                PracticeIncorrectQuizCount = reader.GetInt32(12),
                IsFavorite = reader.GetInt32(13) == 1,
                LastReviewedAt = reader.IsDBNull(14) ? null : ParseDateTime(reader.GetString(14)),
                LastPracticeAt = reader.IsDBNull(15) ? null : ParseDateTime(reader.GetString(15)),
                NextReviewDate = ParseDateTime(reader.GetString(16))
            });
        }

        return words;
    }

    private static SettingsState LoadLegacySettings(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT audio_volume, daily_reminders, auto_play_audio, offline_mode, is_dark_mode, practice_session_count
            FROM settings
            ORDER BY rowid DESC
            LIMIT 1;
            """;

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
            OfflineMode = reader.GetInt32(3) == 1,
            IsDarkMode = reader.GetInt32(4) == 1,
            PracticeSessionCount = reader.GetInt32(5)
        };
    }

    private static int CountPracticeSessions(SqliteConnection connection, string accountEmail)
    {
        if (!TableExists(connection, "practice_sessions"))
        {
            return 0;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM practice_sessions
            WHERE lower(account_email) = lower($accountEmail);
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
        return Convert.ToInt32(command.ExecuteScalar());
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
            INSERT INTO accounts (email, full_name, phone, level, note, avatar_path, password_hash, updated_at)
            VALUES ($email, $fullName, $phone, $level, $note, $avatarPath, $passwordHash, CURRENT_TIMESTAMP)
            ON CONFLICT(email) DO UPDATE SET
                full_name = excluded.full_name,
                phone = excluded.phone,
                level = excluded.level,
                note = excluded.note,
                avatar_path = excluded.avatar_path,
                updated_at = CURRENT_TIMESTAMP;
            """;
        command.Parameters.AddWithValue("$email", user.Email.Trim());
        command.Parameters.AddWithValue("$fullName", user.FullName.Trim());
        command.Parameters.AddWithValue("$phone", user.Phone.Trim());
        command.Parameters.AddWithValue("$level", user.Level.Trim());
        command.Parameters.AddWithValue("$note", user.Note.Trim());
        command.Parameters.AddWithValue("$avatarPath", user.AvatarPath.Trim());
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
            long lessonWordId;
            var useLegacyLessonWordColumns = ColumnExists(connection, "lesson_words", "next_review_date")
                || ColumnExists(connection, "lesson_words", "mastery_level")
                || ColumnExists(connection, "lesson_words", "example")
                || ColumnExists(connection, "lesson_words", "is_favorite");
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = useLegacyLessonWordColumns
                    ? """
                        INSERT INTO lesson_words (
                            lesson_id, word, ipa, type, meaning, vietnamese_meaning, example,
                            mastery_level, review_count, correct_quiz_count, incorrect_quiz_count,
                            practice_correct_quiz_count, practice_incorrect_quiz_count, is_favorite,
                            last_reviewed_at, last_practice_at, next_review_date, audio_path)
                        VALUES (
                            $lessonId, $word, $ipa, $type, $meaning, $vietnameseMeaning, $example,
                            $masteryLevel, $reviewCount, $correctQuizCount, $incorrectQuizCount,
                            $practiceCorrectQuizCount, $practiceIncorrectQuizCount, $isFavorite,
                            $lastReviewedAt, $lastPracticeAt, $nextReviewDate, $audioPath);
                        SELECT last_insert_rowid();
                        """
                    : """
                        INSERT INTO lesson_words (
                            lesson_id, word, ipa, type, meaning, vietnamese_meaning, audio_path)
                        VALUES (
                            $lessonId, $word, $ipa, $type, $meaning, $vietnameseMeaning, $audioPath);
                        SELECT last_insert_rowid();
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
                command.Parameters.AddWithValue("$practiceCorrectQuizCount", word.PracticeCorrectQuizCount);
                command.Parameters.AddWithValue("$practiceIncorrectQuizCount", word.PracticeIncorrectQuizCount);
                command.Parameters.AddWithValue("$isFavorite", word.IsFavorite ? 1 : 0);
                command.Parameters.AddWithValue("$lastReviewedAt", word.LastReviewedAt is null ? DBNull.Value : FormatDateTime(word.LastReviewedAt.Value));
                command.Parameters.AddWithValue("$lastPracticeAt", word.LastPracticeAt is null ? DBNull.Value : FormatDateTime(word.LastPracticeAt.Value));
                command.Parameters.AddWithValue("$nextReviewDate", FormatDateTime(word.NextReviewDate));
                command.Parameters.AddWithValue("$audioPath", word.AudioPath);
                lessonWordId = Convert.ToInt64(command.ExecuteScalar());
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO word_progress (
                        lesson_word_id, mastery_level, review_count, last_reviewed_at, next_review_date)
                    VALUES (
                        $lessonWordId, $masteryLevel, $reviewCount, $lastReviewedAt, $nextReviewDate);
                    """;
                command.Parameters.AddWithValue("$lessonWordId", lessonWordId);
                command.Parameters.AddWithValue("$masteryLevel", word.MasteryLevel);
                command.Parameters.AddWithValue("$reviewCount", word.ReviewCount);
                command.Parameters.AddWithValue("$lastReviewedAt", word.LastReviewedAt is null ? DBNull.Value : FormatDateTime(word.LastReviewedAt.Value));
                command.Parameters.AddWithValue("$nextReviewDate", FormatDateTime(word.NextReviewDate));
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO word_practice_stats (
                        lesson_word_id, correct_quiz_count, incorrect_quiz_count,
                        practice_correct_quiz_count, practice_incorrect_quiz_count, last_practice_at)
                    VALUES (
                        $lessonWordId, $correctQuizCount, $incorrectQuizCount,
                        $practiceCorrectQuizCount, $practiceIncorrectQuizCount, $lastPracticeAt);
                    """;
                command.Parameters.AddWithValue("$lessonWordId", lessonWordId);
                command.Parameters.AddWithValue("$correctQuizCount", word.CorrectQuizCount);
                command.Parameters.AddWithValue("$incorrectQuizCount", word.IncorrectQuizCount);
                command.Parameters.AddWithValue("$practiceCorrectQuizCount", word.PracticeCorrectQuizCount);
                command.Parameters.AddWithValue("$practiceIncorrectQuizCount", word.PracticeIncorrectQuizCount);
                command.Parameters.AddWithValue("$lastPracticeAt", word.LastPracticeAt is null ? DBNull.Value : FormatDateTime(word.LastPracticeAt.Value));
                command.ExecuteNonQuery();
            }

            if (word.IsFavorite)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT OR REPLACE INTO favorites (account_email, lesson_word_id)
                    VALUES ($accountEmail, $lessonWordId);
                    """;
                command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
                command.Parameters.AddWithValue("$lessonWordId", lessonWordId);
                command.ExecuteNonQuery();
            }
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
            INSERT INTO settings (account_email, audio_volume, daily_reminders, auto_play_audio, offline_mode, is_dark_mode, practice_session_count)
            VALUES ($accountEmail, $audioVolume, $dailyReminders, $autoPlayAudio, $offlineMode, $isDarkMode, $practiceSessionCount)
            ON CONFLICT(account_email) DO UPDATE SET
                audio_volume = excluded.audio_volume,
                daily_reminders = excluded.daily_reminders,
                auto_play_audio = excluded.auto_play_audio,
                offline_mode = excluded.offline_mode,
                is_dark_mode = excluded.is_dark_mode,
                practice_session_count = excluded.practice_session_count;
            """;
        command.Parameters.AddWithValue("$accountEmail", accountEmail.Trim());
        command.Parameters.AddWithValue("$audioVolume", settings.AudioVolume);
        command.Parameters.AddWithValue("$dailyReminders", settings.DailyReminders ? 1 : 0);
        command.Parameters.AddWithValue("$autoPlayAudio", settings.AutoPlayAudio ? 1 : 0);
        command.Parameters.AddWithValue("$offlineMode", settings.OfflineMode ? 1 : 0);
        command.Parameters.AddWithValue("$isDarkMode", settings.IsDarkMode ? 1 : 0);
        command.Parameters.AddWithValue("$practiceSessionCount", settings.PracticeSessionCount);
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
