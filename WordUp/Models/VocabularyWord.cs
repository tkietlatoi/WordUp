using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WordUp.Models;

public sealed class VocabularyWord : INotifyPropertyChanged
{
    private string word = "";
    private string ipa = "";
    private string type = "";
    private string meaning = "";
    private string vietnameseMeaning = "";
    private string example = "";
    private string lessonId = "";
    private int masteryLevel;
    private int reviewCount;
    private int correctQuizCount;
    private int incorrectQuizCount;
    private bool isFavorite;
    private DateTime? lastReviewedAt;
    private DateTime nextReviewDate = DateTime.Today;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Word
    {
        get => word;
        set => SetProperty(ref word, value);
    }

    public string Ipa
    {
        get => ipa;
        set => SetProperty(ref ipa, value);
    }

    public string Type
    {
        get => type;
        set => SetProperty(ref type, value);
    }

    public string Meaning
    {
        get => meaning;
        set => SetProperty(ref meaning, value);
    }

    public string VietnameseMeaning
    {
        get => vietnameseMeaning;
        set => SetProperty(ref vietnameseMeaning, value);
    }

    public string Example
    {
        get => example;
        set => SetProperty(ref example, value);
    }

    public string LessonId
    {
        get => lessonId;
        set => SetProperty(ref lessonId, value);
    }

    public int MasteryLevel
    {
        get => masteryLevel;
        set => SetProperty(ref masteryLevel, Math.Clamp(value, 0, 5));
    }

    public int ReviewCount
    {
        get => reviewCount;
        set => SetProperty(ref reviewCount, Math.Max(0, value));
    }

    public int CorrectQuizCount
    {
        get => correctQuizCount;
        set => SetProperty(ref correctQuizCount, Math.Max(0, value));
    }

    public int IncorrectQuizCount
    {
        get => incorrectQuizCount;
        set => SetProperty(ref incorrectQuizCount, Math.Max(0, value));
    }

    public bool IsFavorite
    {
        get => isFavorite;
        set => SetProperty(ref isFavorite, value);
    }

    public DateTime? LastReviewedAt
    {
        get => lastReviewedAt;
        set => SetProperty(ref lastReviewedAt, value);
    }

    public DateTime NextReviewDate
    {
        get => nextReviewDate;
        set => SetProperty(ref nextReviewDate, value.Date);
    }

    private void SetProperty(ref string storage, string value, [CallerMemberName] string? propertyName = null)
    {
        if (storage == value)
        {
            return;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
