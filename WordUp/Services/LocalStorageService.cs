using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using WordUp.Models;

namespace WordUp.Services;

public sealed partial class LocalStorageService
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
            SELECT full_name, email, phone, note, COALESCE(avatar_path, ''), password_hash
            FROM accounts
            WHERE lower(email) = lower($contact)
               OR phone = $contact
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$contact", trimmedContact);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || !VerifyPassword(password, reader.GetString(5)))
        {
            return false;
        }

        targetUser.FullName = reader.GetString(0);
        targetUser.Email = reader.GetString(1);
        targetUser.Phone = reader.GetString(2);
        targetUser.Note = reader.GetString(3);
        targetUser.AvatarPath = reader.GetString(4);
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
            SET password_hash = $passwordHash
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

}
