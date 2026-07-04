using System.ComponentModel;
using System.Windows.Media;

namespace WordUp.Models;

public sealed class QuizQuestion : INotifyPropertyChanged
{
    private static readonly Brush DefaultBackground = Brushes.White;
    private static readonly Brush DefaultBorder = new SolidColorBrush(Color.FromRgb(116, 89, 217));
    private static readonly Brush MutedBorder = new SolidColorBrush(Color.FromRgb(222, 226, 235));
    private static readonly Brush SelectedBackground = new SolidColorBrush(Color.FromRgb(240, 238, 250));
    private static readonly Brush CorrectBackground = new SolidColorBrush(Color.FromRgb(226, 247, 235));
    private static readonly Brush CorrectBorder = new SolidColorBrush(Color.FromRgb(37, 150, 90));
    private static readonly Brush WrongBackground = new SolidColorBrush(Color.FromRgb(253, 229, 232));
    private static readonly Brush WrongBorder = new SolidColorBrush(Color.FromRgb(200, 32, 47));
    private static readonly Brush SuccessText = new SolidColorBrush(Color.FromRgb(34, 160, 107));
    private static readonly Brush DangerText = new SolidColorBrush(Color.FromRgb(200, 32, 47));
    private int selectedIndex = -1;
    private bool isSubmitted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Term { get; init; } = "";
    public string Prompt { get; init; } = "";
    public IReadOnlyList<string> Choices { get; init; } = Array.Empty<string>();
    public int CorrectIndex { get; init; }
    public VocabularyWord? SourceWord { get; init; }

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (selectedIndex == value)
            {
                return;
            }

            selectedIndex = value;
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(IsCorrect));
            OnPropertyChanged(nameof(IsAnswered));
            OnPropertyChanged(nameof(StatusSymbol));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(SelectedChoice));
            OnPropertyChanged(nameof(SelectedChoiceBrush));
            OnPropertyChanged(nameof(ShowCorrectChoice));
            OnPropertyChanged(nameof(CanSelectAnswer));
            OnPropertyChanged(nameof(ChoiceABackground));
            OnPropertyChanged(nameof(ChoiceBBackground));
            OnPropertyChanged(nameof(ChoiceCBackground));
            OnPropertyChanged(nameof(ChoiceDBackground));
            OnPropertyChanged(nameof(ChoiceABorder));
            OnPropertyChanged(nameof(ChoiceBBorder));
            OnPropertyChanged(nameof(ChoiceCBorder));
            OnPropertyChanged(nameof(ChoiceDBorder));
        }
    }

    public bool IsSubmitted
    {
        get => isSubmitted;
        set
        {
            if (isSubmitted == value)
            {
                return;
            }

            isSubmitted = value;
            OnPropertyChanged(nameof(IsSubmitted));
            OnPropertyChanged(nameof(CanSelectAnswer));
            OnPropertyChanged(nameof(IsCorrect));
            OnPropertyChanged(nameof(StatusSymbol));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(SelectedChoiceBrush));
            OnPropertyChanged(nameof(ShowCorrectChoice));
            OnPropertyChanged(nameof(ChoiceABackground));
            OnPropertyChanged(nameof(ChoiceBBackground));
            OnPropertyChanged(nameof(ChoiceCBackground));
            OnPropertyChanged(nameof(ChoiceDBackground));
            OnPropertyChanged(nameof(ChoiceABorder));
            OnPropertyChanged(nameof(ChoiceBBorder));
            OnPropertyChanged(nameof(ChoiceCBorder));
            OnPropertyChanged(nameof(ChoiceDBorder));
        }
    }

    public bool CanSelectAnswer => !IsSubmitted;
    public bool IsAnswered => SelectedIndex >= 0;
    public bool IsCorrect => IsSubmitted && SelectedIndex == CorrectIndex;
    public string StatusSymbol => !IsSubmitted ? "" : IsCorrect ? "✓" : "X";
    public string StatusText => !IsSubmitted ? "Chưa nộp" : IsCorrect ? "Đúng" : "Cần xem lại";
    public Brush StatusBrush => IsCorrect ? SuccessText : DangerText;
    public string CorrectChoice => Choices.Count > CorrectIndex ? Choices[CorrectIndex] : "";
    public string SelectedChoice => Choices.Count > SelectedIndex && SelectedIndex >= 0 ? Choices[SelectedIndex] : "Chưa chọn";
    public Brush SelectedChoiceBrush => !IsSubmitted ? DefaultBorder : IsCorrect ? SuccessText : DangerText;
    public bool ShowCorrectChoice => IsSubmitted && !IsCorrect;
    public Brush ChoiceABackground => GetChoiceBackground(0);
    public Brush ChoiceBBackground => GetChoiceBackground(1);
    public Brush ChoiceCBackground => GetChoiceBackground(2);
    public Brush ChoiceDBackground => GetChoiceBackground(3);
    public Brush ChoiceABorder => GetChoiceBorder(0);
    public Brush ChoiceBBorder => GetChoiceBorder(1);
    public Brush ChoiceCBorder => GetChoiceBorder(2);
    public Brush ChoiceDBorder => GetChoiceBorder(3);

    private Brush GetChoiceBackground(int index)
    {
        if (SelectedIndex < 0)
        {
            return DefaultBackground;
        }

        if (!IsSubmitted)
        {
            return index == SelectedIndex ? SelectedBackground : DefaultBackground;
        }

        if (index == CorrectIndex)
        {
            return CorrectBackground;
        }

        if (index == SelectedIndex)
        {
            return WrongBackground;
        }

        return DefaultBackground;
    }

    private Brush GetChoiceBorder(int index)
    {
        if (SelectedIndex < 0)
        {
            return DefaultBorder;
        }

        if (!IsSubmitted)
        {
            return index == SelectedIndex ? DefaultBorder : MutedBorder;
        }

        if (index == CorrectIndex)
        {
            return CorrectBorder;
        }

        if (index == SelectedIndex)
        {
            return WrongBorder;
        }

        return MutedBorder;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
