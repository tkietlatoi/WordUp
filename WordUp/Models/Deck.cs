using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WordUp.Models;

public sealed class Deck : INotifyPropertyChanged
{
    private string id = Guid.NewGuid().ToString("N");
    private string name = "";
    private int learnedWords;
    private int totalWords;
    private DateTime createdAt = DateTime.Now;
    private DateTime updatedAt = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public int LearnedWords
    {
        get => learnedWords;
        set
        {
            if (SetProperty(ref learnedWords, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }
    }

    public int TotalWords
    {
        get => totalWords;
        set
        {
            if (SetProperty(ref totalWords, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(TotalWordsText));
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }
    }

    public DateTime CreatedAt
    {
        get => createdAt;
        set
        {
            if (SetProperty(ref createdAt, value))
            {
                OnPropertyChanged(nameof(CreatedAtText));
            }
        }
    }

    public DateTime UpdatedAt
    {
        get => updatedAt;
        set
        {
            if (SetProperty(ref updatedAt, value))
            {
                OnPropertyChanged(nameof(UpdatedAtText));
            }
        }
    }

    [JsonIgnore]
    public int ProgressPercentage => TotalWords == 0 ? 0 : (int)Math.Round(LearnedWords / (double)TotalWords * 100);

    [JsonIgnore]
    public string ProgressText => $"Tiến độ: {ProgressPercentage}%";

    [JsonIgnore]
    public string TotalWordsText => $"Tổng số từ: {TotalWords}";

    [JsonIgnore]
    public string CreatedAtText => $"Tạo: {CreatedAt:dd/MM/yyyy}";

    [JsonIgnore]
    public string UpdatedAtText => $"Cập nhật: {UpdatedAt:dd/MM/yyyy}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
