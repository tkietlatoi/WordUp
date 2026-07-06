using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using WordUp.Models;

namespace WordUp.Services;

public sealed partial class LocalStorageService
{
    private static UserProfile? LoadUser(SqliteConnection connection, string email)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT full_name, email, phone, note, avatar_path
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
            Note = reader.GetString(3),
            AvatarPath = reader.GetString(4)
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
            SELECT lw.lesson_id, lw.word, lw.ipa, lw.type, lw.meaning, lw.vietnamese_meaning,
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
                MasteryLevel = reader.GetInt32(6),
                ReviewCount = reader.GetInt32(7),
                CorrectQuizCount = reader.GetInt32(8),
                IncorrectQuizCount = reader.GetInt32(9),
                PracticeCorrectQuizCount = reader.GetInt32(10),
                PracticeIncorrectQuizCount = reader.GetInt32(11),
                IsFavorite = reader.GetInt32(12) == 1,
                LastReviewedAt = reader.IsDBNull(13) ? null : ParseDateTime(reader.GetString(13)),
                LastPracticeAt = reader.IsDBNull(14) ? null : ParseDateTime(reader.GetString(14)),
                NextReviewDate = ParseDateTime(reader.GetString(15))
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
            INSERT INTO accounts (email, full_name, phone, note, avatar_path, password_hash)
            VALUES ($email, $fullName, $phone, $note, $avatarPath, $passwordHash)
            ON CONFLICT(email) DO UPDATE SET
                full_name = excluded.full_name,
                phone = excluded.phone,
                note = excluded.note,
                avatar_path = excluded.avatar_path;
            """;
        command.Parameters.AddWithValue("$email", user.Email.Trim());
        command.Parameters.AddWithValue("$fullName", user.FullName.Trim());
        command.Parameters.AddWithValue("$phone", user.Phone.Trim());
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
                || ColumnExists(connection, "lesson_words", "is_favorite");
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = useLegacyLessonWordColumns
                    ? """
                        INSERT INTO lesson_words (
                            lesson_id, word, ipa, type, meaning, vietnamese_meaning,
                            mastery_level, review_count, correct_quiz_count, incorrect_quiz_count,
                            practice_correct_quiz_count, practice_incorrect_quiz_count, is_favorite,
                            last_reviewed_at, last_practice_at, next_review_date, audio_path)
                        VALUES (
                            $lessonId, $word, $ipa, $type, $meaning, $vietnameseMeaning,
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

}
