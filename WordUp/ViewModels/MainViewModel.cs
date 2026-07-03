using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WordUp.Models;
using WordUp.Services;

namespace WordUp.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly SampleDataService dataService = new();
    private readonly LocalStorageService storageService = new();
    private readonly SrsService srsService = new();
    private readonly QuizService quizService = new();
    private string currentView = "Dashboard";
    private string pendingAuthenticatedView = "Dashboard";
    private string searchText = "";
    private bool isFlashcardBackVisible;
    private bool isStudyFlashcardOpen;
    private bool isAddLessonOpen;
    private int currentWordIndex;
    private int currentQuizIndex;
    private string selectedTab = "Dashboard";
    private string lessonSearchText = "";
    private Deck? selectedStudyDeck;
    private Deck? editingLesson;
    private string newLessonName = "";
    private string newLessonWord = "";
    private string newLessonMeaning = "";
    private string newLessonType = "";
    private string newLessonFilePath = "";
    private string newLessonMessage = "";
    private string lessonWordSearchText = "";
    private VocabularyWord? editingLessonWord;
    private VocabularyWord? selectedWord;
    private string editorWord = "";
    private string editorType = "";
    private string editorMeaning = "";
    private string editorVietnameseMeaning = "";
    private string editorExample = "";
    private string wordManagerMessage = "Chọn một dòng để sửa, hoặc nhập thông tin để thêm từ mới.";
    private string loginEmail = "student@university.edu";
    private string loginPassword = "";
    private string registerFullName = "";
    private string registerEmail = "";
    private string registerPhone = "";
    private string registerPassword = "";
    private string registerConfirmPassword = "";
    private string forgotPasswordEmail = "student@university.edu";
    private string forgotPasswordNewPassword = "";
    private string forgotPasswordConfirmPassword = "";
    private string forgotPasswordAccountEmail = "";
    private bool isForgotPasswordVerified;
    private string authMessage = "";
    private bool isAuthMessageSuccess;
    private bool isAuthenticated;
    private string profileFullName = "";
    private string profileEmail = "";
    private string profilePhone = "";
    private string profileMessage = "";
    private double audioVolume = 75;
    private bool dailyReminders = true;
    private bool autoPlayAudio;
    private bool offlineMode;
    private string settingsMessage = "";
    private VocabularyWord? todayWord;
    private IReadOnlyList<string> todayWordChoices = ["", "", "", ""];
    private int todayWordCorrectIndex;
    private int todayWordSelectedIndex = -1;

    public MainViewModel()
    {
        var savedState = storageService.Load();

        User = savedState.User;
        Decks = new ObservableCollection<Deck>(savedState.Decks);
        Words = new ObservableCollection<VocabularyWord>(savedState.Words);
        EnsureLessonIds();
        NewLessonWords = new ObservableCollection<VocabularyWord>();
        QuizQuestions = new ObservableCollection<QuizQuestion>(quizService.CreateQuestions(Words));
        Achievements = new ObservableCollection<Achievement>(dataService.Achievements);

        AudioVolume = savedState.Settings.AudioVolume;
        DailyReminders = savedState.Settings.DailyReminders;
        AutoPlayAudio = savedState.Settings.AutoPlayAudio;
        OfflineMode = savedState.Settings.OfflineMode;

        NavigateCommand = new RelayCommand(parameter => Navigate(parameter?.ToString() ?? "Dashboard"));
        SelectTabCommand = new RelayCommand(parameter => SelectTab(parameter?.ToString() ?? "Dashboard"));
        StartLessonCommand = new RelayCommand(parameter => StartLesson(parameter));
        ShowLessonListCommand = new RelayCommand(_ => ShowLessonList());
        AddLessonCommand = new RelayCommand(_ => OpenAddLesson());
        EditLessonCommand = new RelayCommand(parameter => EditLesson(parameter));
        DeleteLessonCommand = new RelayCommand(parameter => DeleteLesson(parameter));
        EditLessonWordCommand = new RelayCommand(parameter => EditLessonWord(parameter));
        DeleteLessonWordCommand = new RelayCommand(parameter => DeleteLessonWord(parameter));
        AddLessonWordCommand = new RelayCommand(_ => AddLessonWord());
        ImportLessonFileCommand = new RelayCommand(_ => ImportLessonFile());
        SaveNewLessonCommand = new RelayCommand(_ => SaveNewLesson());
        CancelNewLessonCommand = new RelayCommand(_ => ShowLessonList());
        FlipFlashcardCommand = new RelayCommand(_ => IsFlashcardBackVisible = !IsFlashcardBackVisible);
        NextWordCommand = new RelayCommand(parameter => ReviewCurrentWord(parameter));
        SelectQuizAnswerCommand = new RelayCommand(parameter => SelectQuizAnswer(parameter));
        NextQuizCommand = new RelayCommand(_ => NextQuiz());
        NewWordCommand = new RelayCommand(_ => ClearWordEditor());
        SaveWordCommand = new RelayCommand(_ => SaveWord());
        DeleteWordCommand = new RelayCommand(_ => DeleteSelectedWord());
        LoginCommand = new RelayCommand(_ => Login());
        RegisterCommand = new RelayCommand(_ => Register());
        ForgotPasswordCommand = new RelayCommand(_ => HandleForgotPassword());
        SaveProfileCommand = new RelayCommand(_ => SaveProfile());
        CancelProfileCommand = new RelayCommand(_ => LoadProfileEditor());
        ResetQuizCommand = new RelayCommand(_ => ResetQuiz());
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        LogoutCommand = new RelayCommand(_ => Logout());
        DeleteAccountCommand = new RelayCommand(_ => DeleteAccount());
        SelectTodayWordAnswerCommand = new RelayCommand(parameter => SelectTodayWordAnswer(parameter));

        LoadProfileEditor();
        RefreshTodayWord();
    }

    public UserProfile User { get; }
    public ObservableCollection<Deck> Decks { get; }
    public ObservableCollection<VocabularyWord> Words { get; }
    public ObservableCollection<VocabularyWord> NewLessonWords { get; }
    public ObservableCollection<QuizQuestion> QuizQuestions { get; }
    public ObservableCollection<Achievement> Achievements { get; }

    public ICommand NavigateCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand StartLessonCommand { get; }
    public ICommand ShowLessonListCommand { get; }
    public ICommand AddLessonCommand { get; }
    public ICommand EditLessonCommand { get; }
    public ICommand DeleteLessonCommand { get; }
    public ICommand EditLessonWordCommand { get; }
    public ICommand DeleteLessonWordCommand { get; }
    public ICommand AddLessonWordCommand { get; }
    public ICommand ImportLessonFileCommand { get; }
    public ICommand SaveNewLessonCommand { get; }
    public ICommand CancelNewLessonCommand { get; }
    public ICommand FlipFlashcardCommand { get; }
    public ICommand NextWordCommand { get; }
    public ICommand SelectQuizAnswerCommand { get; }
    public ICommand NextQuizCommand { get; }
    public ICommand NewWordCommand { get; }
    public ICommand SaveWordCommand { get; }
    public ICommand DeleteWordCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ForgotPasswordCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand CancelProfileCommand { get; }
    public ICommand ResetQuizCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand DeleteAccountCommand { get; }
    public ICommand SelectTodayWordAnswerCommand { get; }

    public string CurrentView
    {
        get => currentView;
        set
        {
            if (SetProperty(ref currentView, value))
            {
                OnPropertyChanged(nameof(IsLoginView));
                OnPropertyChanged(nameof(IsRegisterView));
                OnPropertyChanged(nameof(IsForgotPasswordView));
                OnPropertyChanged(nameof(IsDashboardView));
                OnPropertyChanged(nameof(IsStudyView));
                OnPropertyChanged(nameof(IsQuizView));
                OnPropertyChanged(nameof(IsQuizResultView));
                OnPropertyChanged(nameof(IsWordManagerView));
                OnPropertyChanged(nameof(IsAnalyticsView));
                OnPropertyChanged(nameof(IsAccountView));
                OnPropertyChanged(nameof(IsSettingsView));
                OnPropertyChanged(nameof(IsProfileView));
                OnPropertyChanged(nameof(IsAboutView));
                OnPropertyChanged(nameof(ShowBottomNavigation));
            }
        }
    }

    public string SelectedTab
    {
        get => selectedTab;
        set => SetProperty(ref selectedTab, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                OnPropertyChanged(nameof(FilteredWords));
            }
        }
    }

    public string LessonSearchText
    {
        get => lessonSearchText;
        set
        {
            if (SetProperty(ref lessonSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredDecks));
                OnPropertyChanged(nameof(ShowLessonSearchPlaceholder));
            }
        }
    }

    public bool ShowLessonSearchPlaceholder => string.IsNullOrEmpty(LessonSearchText);

    public VocabularyWord? SelectedWord
    {
        get => selectedWord;
        set
        {
            if (SetProperty(ref selectedWord, value) && value is not null)
            {
                EditorWord = value.Word;
                EditorType = value.Type;
                EditorMeaning = value.Meaning;
                EditorVietnameseMeaning = value.VietnameseMeaning;
                EditorExample = value.Example;
                WordManagerMessage = $"Đang sửa {value.Word}.";
            }
        }
    }

    public string EditorWord
    {
        get => editorWord;
        set => SetProperty(ref editorWord, value);
    }

    public string EditorType
    {
        get => editorType;
        set => SetProperty(ref editorType, value);
    }

    public string EditorMeaning
    {
        get => editorMeaning;
        set => SetProperty(ref editorMeaning, value);
    }

    public string EditorVietnameseMeaning
    {
        get => editorVietnameseMeaning;
        set => SetProperty(ref editorVietnameseMeaning, value);
    }

    public string EditorExample
    {
        get => editorExample;
        set => SetProperty(ref editorExample, value);
    }

    public string WordManagerMessage
    {
        get => wordManagerMessage;
        set => SetProperty(ref wordManagerMessage, value);
    }

    public string LoginEmail
    {
        get => loginEmail;
        set => SetProperty(ref loginEmail, value);
    }

    public string LoginPassword
    {
        get => loginPassword;
        set => SetProperty(ref loginPassword, value);
    }

    public string RegisterFullName
    {
        get => registerFullName;
        set => SetProperty(ref registerFullName, value);
    }

    public string RegisterEmail
    {
        get => registerEmail;
        set => SetProperty(ref registerEmail, value);
    }

    public string RegisterPhone
    {
        get => registerPhone;
        set => SetProperty(ref registerPhone, value);
    }

    public string RegisterPassword
    {
        get => registerPassword;
        set => SetProperty(ref registerPassword, value);
    }

    public string RegisterConfirmPassword
    {
        get => registerConfirmPassword;
        set => SetProperty(ref registerConfirmPassword, value);
    }

    public string ForgotPasswordEmail
    {
        get => forgotPasswordEmail;
        set
        {
            if (SetProperty(ref forgotPasswordEmail, value) && IsForgotPasswordVerified)
            {
                IsForgotPasswordVerified = false;
                forgotPasswordAccountEmail = "";
                ForgotPasswordNewPassword = "";
                ForgotPasswordConfirmPassword = "";
                AuthMessage = "";
            }
        }
    }

    public string ForgotPasswordNewPassword
    {
        get => forgotPasswordNewPassword;
        set => SetProperty(ref forgotPasswordNewPassword, value);
    }

    public string ForgotPasswordConfirmPassword
    {
        get => forgotPasswordConfirmPassword;
        set => SetProperty(ref forgotPasswordConfirmPassword, value);
    }

    public bool IsForgotPasswordVerified
    {
        get => isForgotPasswordVerified;
        set
        {
            if (SetProperty(ref isForgotPasswordVerified, value))
            {
                OnPropertyChanged(nameof(ForgotPasswordActionText));
            }
        }
    }

    public string ForgotPasswordActionText => IsForgotPasswordVerified ? "Đổi mật khẩu" : "Gửi yêu cầu";

    public string AuthMessage
    {
        get => authMessage;
        set => SetProperty(ref authMessage, value);
    }

    public bool IsAuthMessageSuccess
    {
        get => isAuthMessageSuccess;
        set => SetProperty(ref isAuthMessageSuccess, value);
    }

    public bool IsAuthenticated
    {
        get => isAuthenticated;
        set
        {
            if (SetProperty(ref isAuthenticated, value))
            {
                OnPropertyChanged(nameof(IsGuest));
                OnPropertyChanged(nameof(DashboardGreeting));
                OnPropertyChanged(nameof(DashboardSubtitle));
            }
        }
    }

    public bool IsGuest => !IsAuthenticated;

    public string ProfileFullName
    {
        get => profileFullName;
        set => SetProperty(ref profileFullName, value);
    }

    public string ProfileEmail
    {
        get => profileEmail;
        set => SetProperty(ref profileEmail, value);
    }

    public string ProfilePhone
    {
        get => profilePhone;
        set => SetProperty(ref profilePhone, value);
    }

    public string ProfileMessage
    {
        get => profileMessage;
        set => SetProperty(ref profileMessage, value);
    }

    public double AudioVolume
    {
        get => audioVolume;
        set => SetProperty(ref audioVolume, value);
    }

    public bool DailyReminders
    {
        get => dailyReminders;
        set => SetProperty(ref dailyReminders, value);
    }

    public bool AutoPlayAudio
    {
        get => autoPlayAudio;
        set => SetProperty(ref autoPlayAudio, value);
    }

    public bool OfflineMode
    {
        get => offlineMode;
        set => SetProperty(ref offlineMode, value);
    }

    public string SettingsMessage
    {
        get => settingsMessage;
        set => SetProperty(ref settingsMessage, value);
    }

    public bool IsFlashcardBackVisible
    {
        get => isFlashcardBackVisible;
        set
        {
            if (SetProperty(ref isFlashcardBackVisible, value))
            {
                OnPropertyChanged(nameof(FlashcardHint));
            }
        }
    }

    public bool IsStudyFlashcardOpen
    {
        get => isStudyFlashcardOpen;
        set
        {
            if (SetProperty(ref isStudyFlashcardOpen, value))
            {
                OnPropertyChanged(nameof(IsStudyLessonListVisible));
                OnPropertyChanged(nameof(StudyHeaderTitle));
            }
        }
    }

    public bool IsAddLessonOpen
    {
        get => isAddLessonOpen;
        set
        {
            if (SetProperty(ref isAddLessonOpen, value))
            {
                OnPropertyChanged(nameof(IsStudyLessonListVisible));
                OnPropertyChanged(nameof(StudyHeaderTitle));
            }
        }
    }

    public bool IsStudyLessonListVisible => !IsStudyFlashcardOpen && !IsAddLessonOpen;

    public Deck? SelectedStudyDeck
    {
        get => selectedStudyDeck;
        set
        {
            if (SetProperty(ref selectedStudyDeck, value))
            {
                OnPropertyChanged(nameof(StudyHeaderTitle));
                OnPropertyChanged(nameof(CurrentStudyWords));
                OnPropertyChanged(nameof(CurrentWord));
                OnPropertyChanged(nameof(StudyProgressText));
                OnPropertyChanged(nameof(StudyProgressValue));
            }
        }
    }

    public string NewLessonName
    {
        get => newLessonName;
        set => SetProperty(ref newLessonName, value);
    }

    public string NewLessonWord
    {
        get => newLessonWord;
        set => SetProperty(ref newLessonWord, value);
    }

    public string NewLessonMeaning
    {
        get => newLessonMeaning;
        set => SetProperty(ref newLessonMeaning, value);
    }

    public string NewLessonType
    {
        get => newLessonType;
        set => SetProperty(ref newLessonType, value);
    }

    public string NewLessonFilePath
    {
        get => newLessonFilePath;
        set => SetProperty(ref newLessonFilePath, value);
    }

    public string NewLessonMessage
    {
        get => newLessonMessage;
        set => SetProperty(ref newLessonMessage, value);
    }

    public string LessonWordSearchText
    {
        get => lessonWordSearchText;
        set
        {
            if (SetProperty(ref lessonWordSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredNewLessonWords));
                OnPropertyChanged(nameof(ShowLessonWordSearchPlaceholder));
            }
        }
    }

    public bool ShowLessonWordSearchPlaceholder => string.IsNullOrEmpty(LessonWordSearchText);

    public string LessonEditorTitle => editingLesson is null ? "Thêm bài học" : "Sửa bài học";
    public string SaveLessonButtonText => editingLesson is null ? "Lưu bài học" : "Cập nhật bài học";
    public string LessonWordEditorButtonText => editingLessonWord is null ? "Thêm từ vào bài" : "Cập nhật từ";

    public int CurrentWordIndex
    {
        get => currentWordIndex;
        set
        {
            if (SetProperty(ref currentWordIndex, value))
            {
                OnPropertyChanged(nameof(CurrentWord));
                OnPropertyChanged(nameof(StudyProgressText));
                OnPropertyChanged(nameof(StudyProgressValue));
            }
        }
    }

    public int CurrentQuizIndex
    {
        get => currentQuizIndex;
        set
        {
            if (SetProperty(ref currentQuizIndex, value))
            {
                OnPropertyChanged(nameof(CurrentQuizQuestion));
                OnPropertyChanged(nameof(QuizProgressText));
                OnPropertyChanged(nameof(QuizProgressValue));
            }
        }
    }

    public IReadOnlyList<VocabularyWord> CurrentStudyWords
    {
        get
        {
            if (SelectedStudyDeck is null)
            {
                return Words;
            }

            var lessonWords = Words.Where(word => word.LessonId == SelectedStudyDeck.Id).ToList();
            return lessonWords.Count > 0 ? lessonWords : Words;
        }
    }

    public VocabularyWord CurrentWord => CurrentStudyWords.Count == 0
        ? new VocabularyWord()
        : CurrentStudyWords[Math.Clamp(CurrentWordIndex, 0, CurrentStudyWords.Count - 1)];
    public IEnumerable<Deck> FilteredDecks => string.IsNullOrWhiteSpace(LessonSearchText)
        ? Decks
        : Decks.Where(deck => deck.Name.Contains(LessonSearchText, StringComparison.OrdinalIgnoreCase));
    public IEnumerable<VocabularyWord> FilteredNewLessonWords => string.IsNullOrWhiteSpace(LessonWordSearchText)
        ? NewLessonWords
        : NewLessonWords.Where(word => word.Word.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase)
            || word.Meaning.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase)
            || word.VietnameseMeaning.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase)
            || word.Type.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase));
    public QuizQuestion CurrentQuizQuestion => QuizQuestions[CurrentQuizIndex];
    public IEnumerable<VocabularyWord> FilteredWords => string.IsNullOrWhiteSpace(SearchText)
        ? Words
        : Words.Where(word => word.Word.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || word.Meaning.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || word.VietnameseMeaning.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || word.Type.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public bool IsLoginView => CurrentView == "Login";
    public bool IsRegisterView => CurrentView == "Register";
    public bool IsForgotPasswordView => CurrentView == "ForgotPassword";
    public bool IsDashboardView => CurrentView == "Dashboard";
    public bool IsStudyView => CurrentView == "Study";
    public bool IsQuizView => CurrentView == "Quiz";
    public bool IsQuizResultView => CurrentView == "QuizResult";
    public bool IsWordManagerView => CurrentView == "WordManager";
    public bool IsAnalyticsView => CurrentView == "Analytics";
    public bool IsAccountView => CurrentView == "Account";
    public bool IsSettingsView => CurrentView == "Settings";
    public bool IsProfileView => CurrentView == "Profile";
    public bool IsAboutView => CurrentView == "About";
    public bool ShowBottomNavigation => IsDashboardView || IsStudyView || IsQuizView || IsAccountView;

    public string DashboardGreeting => IsAuthenticated ? $"Chào mừng, {User.FullName}!" : "Chào mừng, bạn!";
    public string DashboardSubtitle => IsAuthenticated ? $"Cấp độ: {User.Level}" : "Welcome to WordUp";
    public string FlashcardHint => IsFlashcardBackVisible ? "Đánh giá mức độ ghi nhớ" : "Chạm để lật thẻ";
    public string StudyHeaderTitle => IsStudyFlashcardOpen
        ? SelectedStudyDeck?.Name ?? "Học từ"
        : IsAddLessonOpen
            ? LessonEditorTitle
        : "Bài học";
    public string StudyProgressText => CurrentStudyWords.Count == 0 ? "0/0" : $"{CurrentWordIndex + 1}/{CurrentStudyWords.Count}";
    public double StudyProgressValue => CurrentStudyWords.Count == 0 ? 0 : ((CurrentWordIndex + 1) / (double)CurrentStudyWords.Count) * 100;
    public string QuizProgressText => $"CÂU {CurrentQuizIndex + 1}/{QuizQuestions.Count}";
    public double QuizProgressValue => ((CurrentQuizIndex + 1) / (double)QuizQuestions.Count) * 100;
    public int WordsDueToday => Words.Count(word => word.NextReviewDate.Date <= DateTime.Today);
    public int MasteredWords => Words.Count(word => word.MasteryLevel >= 5);
    public int StudyStreakDays => CalculateStudyStreakDays();
    public int TotalLessons => Decks.Count;
    public int TotalVocabularyWords => Words.Count;
    public int LearnedOrRememberedWords => Words.Count(word => word.LastReviewedAt is not null || word.MasteryLevel >= 5);
    public IEnumerable<Deck> RecentDecks => Decks.Take(3);
    public Deck? ContinueLearningDeck => Decks.FirstOrDefault();
    public string ContinueLearningProgress => ContinueLearningDeck?.ProgressText ?? "Chưa có bài học nào";
    public string TodayWordText => todayWord?.Word ?? "WordUp";
    public string TodayWordHint => todayWord?.Ipa ?? "";
    public string TodayWordPrompt => "Chọn nghĩa đúng của từ này";
    public string TodayChoiceA => todayWordChoices.Count > 0 ? todayWordChoices[0] : "";
    public string TodayChoiceB => todayWordChoices.Count > 1 ? todayWordChoices[1] : "";
    public string TodayChoiceC => todayWordChoices.Count > 2 ? todayWordChoices[2] : "";
    public string TodayChoiceD => todayWordChoices.Count > 3 ? todayWordChoices[3] : "";
    public Brush TodayChoiceABackground => GetTodayChoiceBackground(0);
    public Brush TodayChoiceBBackground => GetTodayChoiceBackground(1);
    public Brush TodayChoiceCBackground => GetTodayChoiceBackground(2);
    public Brush TodayChoiceDBackground => GetTodayChoiceBackground(3);
    public Brush TodayChoiceABorder => GetTodayChoiceBorder(0);
    public Brush TodayChoiceBBorder => GetTodayChoiceBorder(1);
    public Brush TodayChoiceCBorder => GetTodayChoiceBorder(2);
    public Brush TodayChoiceDBorder => GetTodayChoiceBorder(3);
    public bool CanSelectTodayWordAnswer => todayWordSelectedIndex < 0;
    public string TodayWordResultText => todayWordSelectedIndex < 0
        ? "Chọn một đáp án để kiểm tra."
        : todayWordSelectedIndex == todayWordCorrectIndex
            ? "Chính xác."
            : $"Chưa đúng. Đáp án đúng là {GetTodayChoiceLabel(todayWordCorrectIndex)}.";
    public int TodayGoalPercentage => Math.Min(100, (int)Math.Round(Words.Count(word => word.LastReviewedAt?.Date == DateTime.Today) / 10.0 * 100));
    public string TodayGoalText => $"{Words.Count(word => word.LastReviewedAt?.Date == DateTime.Today)} / 10 từ";
    public IReadOnlyList<int> WeeklyProgressValues => [35, 48, 60, 42, 55, 30, TodayGoalPercentage];
    public int QuizScorePercentage => (int)Math.Round(QuizQuestions.Count(q => q.IsCorrect) / (double)QuizQuestions.Count * 100);
    public string QuizScoreText => $"Đúng {QuizQuestions.Count(q => q.IsCorrect)}/{QuizQuestions.Count}";

    private void Navigate(string view)
    {
        if (RequiresAuthentication(view) && !RequireAuthentication(view))
        {
            return;
        }

        if (view == "Profile")
        {
            LoadProfileEditor();
        }

        if (view is "Login" or "Register" or "ForgotPassword")
        {
            AuthMessage = "";
            IsAuthMessageSuccess = false;
            if (view == "ForgotPassword")
            {
                IsForgotPasswordVerified = false;
                forgotPasswordAccountEmail = "";
                ForgotPasswordNewPassword = "";
                ForgotPasswordConfirmPassword = "";
            }
        }

        CurrentView = view;
    }

    private void SelectTab(string tab)
    {
        if (RequiresAuthentication(tab) && !RequireAuthentication(tab))
        {
            return;
        }

        SelectedTab = tab;
        if (tab == "Study")
        {
            ShowLessonList();
        }

        CurrentView = tab;
    }

    private void StartLesson(object? parameter)
    {
        SelectedStudyDeck = parameter as Deck ?? Decks.FirstOrDefault();
        CurrentWordIndex = 0;
        IsFlashcardBackVisible = false;
        IsAddLessonOpen = false;
        IsStudyFlashcardOpen = true;
    }

    private void ShowLessonList()
    {
        IsStudyFlashcardOpen = false;
        IsAddLessonOpen = false;
        IsFlashcardBackVisible = false;
        editingLesson = null;
        OnLessonEditorChanged();
    }

    private void OpenAddLesson()
    {
        editingLesson = null;
        IsStudyFlashcardOpen = false;
        IsAddLessonOpen = true;
        NewLessonName = $"Bài học mới {Decks.Count + 1}";
        NewLessonWord = "";
        NewLessonMeaning = "";
        NewLessonType = "";
        NewLessonFilePath = "";
        LessonWordSearchText = "";
        NewLessonMessage = "Nhập từ thủ công hoặc chọn tệp để thêm nhiều từ.";
        editingLessonWord = null;
        NewLessonWords.Clear();
        OnLessonEditorChanged();
    }

    private void EditLesson(object? parameter)
    {
        if (parameter is not Deck lesson)
        {
            return;
        }

        editingLesson = lesson;
        IsStudyFlashcardOpen = false;
        IsAddLessonOpen = true;
        NewLessonName = lesson.Name;
        NewLessonWord = "";
        NewLessonMeaning = "";
        NewLessonType = "";
        NewLessonFilePath = "";
        LessonWordSearchText = "";
        NewLessonMessage = "Chỉnh tên bài học, thêm từ mới hoặc xóa từ trong bài.";
        editingLessonWord = null;
        NewLessonWords.Clear();

        foreach (var word in Words.Where(word => word.LessonId == lesson.Id))
        {
            NewLessonWords.Add(CloneVocabularyWord(word));
        }

        OnLessonEditorChanged();
    }

    private void EditLessonWord(object? parameter)
    {
        if (parameter is not VocabularyWord word)
        {
            return;
        }

        editingLessonWord = word;
        NewLessonWord = word.Word;
        NewLessonMeaning = string.IsNullOrWhiteSpace(word.VietnameseMeaning) ? word.Meaning : word.VietnameseMeaning;
        NewLessonType = word.Type;
        NewLessonMessage = $"Đang sửa từ {word.Word}.";
        OnLessonEditorChanged();
    }

    private void DeleteLesson(object? parameter)
    {
        if (parameter is not Deck lesson)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Bạn có chắc muốn xóa bài học \"{lesson.Name}\" không?",
            "Xác nhận xóa bài học",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Decks.Remove(lesson);
        foreach (var word in Words.Where(word => word.LessonId == lesson.Id).ToList())
        {
            Words.Remove(word);
        }

        if (ReferenceEquals(SelectedStudyDeck, lesson))
        {
            SelectedStudyDeck = Decks.FirstOrDefault();
            ShowLessonList();
        }

        OnPropertyChanged(nameof(FilteredWords));
        OnPropertyChanged(nameof(FilteredDecks));
        OnPropertyChanged(nameof(RecentDecks));
        OnPropertyChanged(nameof(CurrentStudyWords));
        OnPropertyChanged(nameof(CurrentWord));
        OnPropertyChanged(nameof(StudyProgressText));
        OnPropertyChanged(nameof(StudyProgressValue));
        OnProgressChanged();
        RefreshQuizQuestions();
        SaveAppState();
    }

    private void DeleteLessonWord(object? parameter)
    {
        if (parameter is not VocabularyWord word)
        {
            return;
        }

        NewLessonWords.Remove(word);
        OnPropertyChanged(nameof(FilteredNewLessonWords));
        if (ReferenceEquals(editingLessonWord, word))
        {
            ClearLessonWordEditor();
        }

        NewLessonMessage = $"Còn {NewLessonWords.Count} từ trong bài học.";
    }

    private void AddLessonWord()
    {
        if (string.IsNullOrWhiteSpace(NewLessonWord) || string.IsNullOrWhiteSpace(NewLessonMeaning))
        {
            NewLessonMessage = "Từ và nghĩa là bắt buộc.";
            return;
        }

        if (editingLessonWord is not null)
        {
            editingLessonWord.Word = NewLessonWord.Trim();
            editingLessonWord.Meaning = NewLessonMeaning.Trim();
            editingLessonWord.VietnameseMeaning = NewLessonMeaning.Trim();
            editingLessonWord.Type = string.IsNullOrWhiteSpace(NewLessonType) ? "word" : NewLessonType.Trim();
            NewLessonMessage = $"Đã cập nhật từ {editingLessonWord.Word}.";
            OnPropertyChanged(nameof(FilteredNewLessonWords));
            ClearLessonWordEditor();
            return;
        }

        NewLessonWords.Add(new VocabularyWord
        {
            Word = NewLessonWord.Trim(),
            Meaning = NewLessonMeaning.Trim(),
            VietnameseMeaning = NewLessonMeaning.Trim(),
            Type = string.IsNullOrWhiteSpace(NewLessonType) ? "word" : NewLessonType.Trim()
        });

        ClearLessonWordEditor();
        OnPropertyChanged(nameof(FilteredNewLessonWords));
        NewLessonMessage = $"Đã thêm {NewLessonWords.Count} từ vào bài học.";
    }

    private void ImportLessonFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn tệp từ vựng",
            Filter = "Vocabulary files (*.txt;*.csv;*.docx;*.xlsx)|*.txt;*.csv;*.docx;*.xlsx|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        NewLessonFilePath = dialog.FileName;
        var importedWords = ReadVocabularyFile(dialog.FileName);
        foreach (var word in importedWords)
        {
            NewLessonWords.Add(word);
        }

        OnPropertyChanged(nameof(FilteredNewLessonWords));
        NewLessonMessage = importedWords.Count == 0
            ? "Không tìm thấy dòng hợp lệ. Mỗi dòng nên có: từ, nghĩa, loại từ."
            : $"Đã nhập {importedWords.Count} từ từ tệp.";
    }

    private void SaveNewLesson()
    {
        if (string.IsNullOrWhiteSpace(NewLessonName))
        {
            NewLessonMessage = "Tên bài học là bắt buộc.";
            return;
        }

        if (NewLessonWords.Count == 0)
        {
            NewLessonMessage = "Thêm ít nhất một từ trước khi lưu bài học.";
            return;
        }

        var lesson = editingLesson ?? new Deck();
        lesson.Name = NewLessonName.Trim();
        lesson.TotalWords = NewLessonWords.Count;
        lesson.UpdatedAt = DateTime.Now;

        if (editingLesson is null)
        {
            lesson.CreatedAt = lesson.UpdatedAt;
            lesson.LearnedWords = 0;
            Decks.Add(lesson);
        }
        else
        {
            lesson.LearnedWords = Math.Min(lesson.LearnedWords, lesson.TotalWords);
            foreach (var oldWord in Words.Where(word => word.LessonId == lesson.Id).ToList())
            {
                Words.Remove(oldWord);
            }
        }

        foreach (var word in NewLessonWords)
        {
            var savedWord = CloneVocabularyWord(word);
            savedWord.LessonId = lesson.Id;
            Words.Add(savedWord);
        }

        SelectedStudyDeck = lesson;
        OnPropertyChanged(nameof(FilteredWords));
        OnPropertyChanged(nameof(FilteredDecks));
        OnPropertyChanged(nameof(RecentDecks));
        OnPropertyChanged(nameof(CurrentStudyWords));
        OnPropertyChanged(nameof(CurrentWord));
        OnPropertyChanged(nameof(StudyProgressText));
        OnPropertyChanged(nameof(StudyProgressValue));
        OnProgressChanged();
        RefreshQuizQuestions();
        SaveAppState();
        ShowLessonList();
    }

    private void ClearLessonWordEditor()
    {
        editingLessonWord = null;
        NewLessonWord = "";
        NewLessonMeaning = "";
        NewLessonType = "";
        OnLessonEditorChanged();
    }

    private void EnsureLessonIds()
    {
        if (Decks.Count == 0 || Words.Count == 0)
        {
            return;
        }

        var index = 0;
        foreach (var word in Words.Where(word => string.IsNullOrWhiteSpace(word.LessonId)))
        {
            word.LessonId = Decks[index % Decks.Count].Id;
            index++;
        }

        foreach (var deck in Decks)
        {
            if (deck.CreatedAt == default)
            {
                deck.CreatedAt = DateTime.Now;
            }

            if (deck.UpdatedAt == default)
            {
                deck.UpdatedAt = deck.CreatedAt;
            }

            deck.TotalWords = Words.Count(word => word.LessonId == deck.Id);
            deck.LearnedWords = Math.Min(deck.LearnedWords, deck.TotalWords);
        }

        OnPropertyChanged(nameof(FilteredDecks));
    }

    private void OnLessonEditorChanged()
    {
        OnPropertyChanged(nameof(LessonEditorTitle));
        OnPropertyChanged(nameof(SaveLessonButtonText));
        OnPropertyChanged(nameof(LessonWordEditorButtonText));
        OnPropertyChanged(nameof(StudyHeaderTitle));
    }

    private static VocabularyWord CloneVocabularyWord(VocabularyWord word)
    {
        return new VocabularyWord
        {
            Word = word.Word,
            Ipa = word.Ipa,
            Type = word.Type,
            Meaning = word.Meaning,
            VietnameseMeaning = word.VietnameseMeaning,
            Example = word.Example,
            LessonId = word.LessonId,
            MasteryLevel = word.MasteryLevel,
            ReviewCount = word.ReviewCount,
            CorrectQuizCount = word.CorrectQuizCount,
            IncorrectQuizCount = word.IncorrectQuizCount,
            LastReviewedAt = word.LastReviewedAt,
            NextReviewDate = word.NextReviewDate
        };
    }

    private IReadOnlyList<VocabularyWord> ReadVocabularyFile(string path)
    {
        try
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var rows = extension switch
            {
                ".txt" or ".csv" => File.ReadAllLines(path),
                ".docx" => ReadDocxRows(path),
                ".xlsx" => ReadXlsxRows(path),
                _ => []
            };

            return rows
                .Select(ParseVocabularyRow)
                .Where(word => word is not null)
                .Cast<VocabularyWord>()
                .ToList();
        }
        catch (Exception ex)
        {
            NewLessonMessage = $"Không đọc được tệp: {ex.Message}";
            return [];
        }
    }

    private static VocabularyWord? ParseVocabularyRow(string row)
    {
        if (string.IsNullOrWhiteSpace(row))
        {
            return null;
        }

        var parts = row
            .Split(['\t', '|', ';', ','], StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length < 2)
        {
            return null;
        }

        return new VocabularyWord
        {
            Word = parts[0],
            Meaning = parts[1],
            VietnameseMeaning = parts[1],
            Type = parts.Length > 2 ? parts[2] : "word",
            Example = parts.Length > 3 ? parts[3] : ""
        };
    }

    private static IReadOnlyList<string> ReadDocxRows(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var textNamespace = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");

        return document
            .Descendants(textNamespace + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(textNamespace + "t").Select(text => text.Value)))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static IReadOnlyList<string> ReadXlsxRows(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var sharedStrings = ReadXlsxSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        if (sheetEntry is null)
        {
            return [];
        }

        using var stream = sheetEntry.Open();
        var sheet = XDocument.Load(stream);
        var spreadsheetNamespace = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var rows = new List<string>();

        foreach (var row in sheet.Descendants(spreadsheetNamespace + "row"))
        {
            var values = row
                .Elements(spreadsheetNamespace + "c")
                .Select(cell => ReadXlsxCellValue(cell, sharedStrings, spreadsheetNamespace))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
            {
                rows.Add(string.Join('\t', values));
            }
        }

        return rows;
    }

    private static IReadOnlyList<string> ReadXlsxSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var spreadsheetNamespace = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");

        return document
            .Descendants(spreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string ReadXlsxCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace spreadsheetNamespace)
    {
        var value = cell.Element(spreadsheetNamespace + "v")?.Value ?? "";
        if ((string?)cell.Attribute("t") == "s"
            && int.TryParse(value, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private void NextWord()
    {
        if (CurrentStudyWords.Count == 0)
        {
            return;
        }

        CurrentWordIndex = (CurrentWordIndex + 1) % CurrentStudyWords.Count;
        IsFlashcardBackVisible = false;
    }

    private void ReviewCurrentWord(object? parameter)
    {
        if (CurrentStudyWords.Count == 0)
        {
            return;
        }

        var rating = int.TryParse(parameter?.ToString(), out var parsedRating) ? parsedRating : 1;
        srsService.ApplyReview(CurrentWord, rating);
        OnProgressChanged();
        SaveAppState();
        NextWord();
    }

    private void SelectQuizAnswer(object? parameter)
    {
        if (CurrentQuizQuestion.SelectedIndex >= 0)
        {
            return;
        }

        if (int.TryParse(parameter?.ToString(), out var selectedIndex))
        {
            CurrentQuizQuestion.SelectedIndex = selectedIndex;
            OnPropertyChanged(nameof(QuizScorePercentage));
            OnPropertyChanged(nameof(QuizScoreText));
        }
    }

    private void SelectTodayWordAnswer(object? parameter)
    {
        if (todayWordSelectedIndex >= 0)
        {
            return;
        }

        if (!int.TryParse(parameter?.ToString(), out var selectedIndex))
        {
            return;
        }

        todayWordSelectedIndex = selectedIndex;
        OnTodayWordChanged();
    }

    private void NextQuiz()
    {
        if (CurrentQuizIndex >= QuizQuestions.Count - 1)
        {
            CurrentView = "QuizResult";
            return;
        }

        CurrentQuizIndex++;
    }

    private void Login()
    {
        if (string.IsNullOrWhiteSpace(LoginEmail))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Email không hợp lệ.";
            return;
        }

        if (string.IsNullOrWhiteSpace(LoginPassword))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Vui lòng nhập mật khẩu.";
            return;
        }

        if (!storageService.ValidateLogin(LoginEmail, LoginPassword, User))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Email hoặc mật khẩu không đúng.";
            return;
        }

        var accountState = storageService.LoadAccount(User.Email);
        if (accountState is not null)
        {
            ApplyAccountState(accountState);
        }

        LoadProfileEditor();
        AuthMessage = "";
        IsAuthMessageSuccess = false;
        IsAuthenticated = true;

        NavigateAfterAuthentication();
    }

    private void Register()
    {
        if (string.IsNullOrWhiteSpace(RegisterFullName))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Vui lòng nhập họ và tên.";
            return;
        }

        if (!IsValidEmail(RegisterEmail))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Email đăng ký không hợp lệ.";
            return;
        }

        if (string.IsNullOrWhiteSpace(RegisterPhone))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Vui lòng nhập số điện thoại.";
            return;
        }

        if (string.IsNullOrWhiteSpace(RegisterPassword) || RegisterPassword.Length < 8)
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Mật khẩu cần tối thiểu 8 ký tự.";
            return;
        }

        if (RegisterPassword != RegisterConfirmPassword)
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Xác nhận mật khẩu không khớp.";
            return;
        }

        var registeredUser = new UserProfile
        {
            FullName = RegisterFullName.Trim(),
            Email = RegisterEmail.Trim(),
            Phone = RegisterPhone.Trim(),
            Level = "Học giả"
        };

        if (!storageService.RegisterAccount(registeredUser, RegisterPassword))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Email này đã được đăng ký.";
            return;
        }

        User.FullName = registeredUser.FullName;
        User.Email = registeredUser.Email;
        User.Phone = registeredUser.Phone;
        User.Level = registeredUser.Level;
        ClearLearningData();
        LoadProfileEditor();
        SaveAppState();
        LoginEmail = User.Email;
        LoginPassword = "";
        RegisterPassword = "";
        RegisterConfirmPassword = "";
        RegisterPhone = "";
        IsAuthMessageSuccess = false;
        AuthMessage = "";
        IsAuthenticated = true;

        NavigateAfterAuthentication();
    }

    private void SendPasswordReset()
    {
        IsAuthMessageSuccess = IsValidEmail(ForgotPasswordEmail);
        AuthMessage = IsAuthMessageSuccess
            ? "Hướng dẫn khôi phục đã được gửi đến email của bạn."
            : "Vui lòng nhập email hợp lệ.";
    }

    private void HandleForgotPassword()
    {
        if (!IsForgotPasswordVerified)
        {
            var accountEmail = storageService.FindAccountEmailByContact(ForgotPasswordEmail);
            if (accountEmail is null)
            {
                IsAuthMessageSuccess = false;
                AuthMessage = "Không tìm thấy tài khoản với email hoặc số điện thoại này.";
                return;
            }

            forgotPasswordAccountEmail = accountEmail;
            IsForgotPasswordVerified = true;
            IsAuthMessageSuccess = true;
            AuthMessage = "Tài khoản hợp lệ. Hãy nhập mật khẩu mới.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ForgotPasswordNewPassword) || ForgotPasswordNewPassword.Length < 8)
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Mật khẩu mới cần tối thiểu 8 ký tự.";
            return;
        }

        if (ForgotPasswordNewPassword != ForgotPasswordConfirmPassword)
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Mật khẩu xác nhận không khớp.";
            return;
        }

        if (!storageService.UpdatePassword(forgotPasswordAccountEmail, ForgotPasswordNewPassword))
        {
            IsAuthMessageSuccess = false;
            AuthMessage = "Không thể đổi mật khẩu. Vui lòng thử lại.";
            return;
        }

        LoginEmail = forgotPasswordAccountEmail;
        LoginPassword = "";
        ForgotPasswordNewPassword = "";
        ForgotPasswordConfirmPassword = "";
        IsForgotPasswordVerified = false;
        forgotPasswordAccountEmail = "";
        IsAuthMessageSuccess = true;
        AuthMessage = "Đã đổi mật khẩu. Bạn có thể đăng nhập bằng mật khẩu mới.";
        CurrentView = "Login";
    }

    private void LoadProfileEditor()
    {
        ProfileFullName = User.FullName;
        ProfileEmail = User.Email;
        ProfilePhone = User.Phone;
        ProfileMessage = "";
    }

    private void SaveProfile()
    {
        if (string.IsNullOrWhiteSpace(ProfileFullName))
        {
            ProfileMessage = "Họ và tên không được để trống.";
            return;
        }

        if (!IsValidEmail(ProfileEmail))
        {
            ProfileMessage = "Email không hợp lệ.";
            return;
        }

        if (!ProfileEmail.Trim().Equals(User.Email, StringComparison.OrdinalIgnoreCase))
        {
            ProfileMessage = "Email đăng nhập không thể đổi trong hồ sơ.";
            ProfileEmail = User.Email;
            return;
        }

        User.FullName = ProfileFullName.Trim();
        User.Email = ProfileEmail.Trim();
        User.Phone = ProfilePhone.Trim();
        ProfileMessage = "Đã lưu thông tin hồ sơ.";
        SaveAppState();
    }

    private void SaveSettings()
    {
        SaveAppState();
        SettingsMessage = "Đã lưu cài đặt.";
    }

    private void Logout()
    {
        IsAuthenticated = false;
        pendingAuthenticatedView = "Dashboard";
        LoginPassword = "";
        AuthMessage = "";
        ClearCurrentUser();
        SelectTab("Dashboard");
    }

    private void DeleteAccount()
    {
        var emailToDelete = User.Email;
        IsAuthenticated = false;
        pendingAuthenticatedView = "Dashboard";
        storageService.Delete(emailToDelete);
        ClearCurrentUser();
        AudioVolume = 75;
        DailyReminders = true;
        AutoPlayAudio = false;
        OfflineMode = false;
        CurrentWordIndex = 0;
        ClearWordEditor();
        LoadProfileEditor();
        IsAuthMessageSuccess = true;
        AuthMessage = "Tài khoản đã được xóa trên máy này.";
        SelectTab("Dashboard");
    }

    private void ResetQuiz()
    {
        if (!RequireAuthentication("Quiz"))
        {
            return;
        }

        RefreshQuizQuestions();

        foreach (var question in QuizQuestions)
        {
            question.SelectedIndex = -1;
        }

        CurrentQuizIndex = 0;
        OnPropertyChanged(nameof(QuizScorePercentage));
        OnPropertyChanged(nameof(QuizScoreText));
        CurrentView = "Quiz";
    }

    private static bool IsValidEmail(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Contains('@', StringComparison.Ordinal)
            && trimmed.Contains('.', StringComparison.Ordinal)
            && trimmed.IndexOf('@') > 0
            && trimmed.IndexOf('@') < trimmed.LastIndexOf('.');
    }

    private int CalculateStudyStreakDays()
    {
        var reviewedDates = Words
            .Where(word => word.LastReviewedAt is not null)
            .Select(word => word.LastReviewedAt!.Value.Date)
            .Distinct()
            .ToHashSet();

        var streak = 0;
        var date = DateTime.Today;

        while (reviewedDates.Contains(date))
        {
            streak++;
            date = date.AddDays(-1);
        }

        return streak;
    }

    private void NavigateAfterAuthentication()
    {
        var targetView = pendingAuthenticatedView;
        pendingAuthenticatedView = "Dashboard";

        if (IsTabView(targetView))
        {
            SelectTab(targetView);
            return;
        }

        Navigate(targetView);
    }

    private bool RequireAuthentication(string targetView)
    {
        if (IsAuthenticated)
        {
            return true;
        }

        pendingAuthenticatedView = targetView;
        AuthMessage = "Vui lòng đăng nhập hoặc đăng ký để tiếp tục.";
        IsAuthMessageSuccess = false;
        CurrentView = "Login";
        return false;
    }

    private static bool RequiresAuthentication(string view)
    {
        return view is "Study" or "Quiz" or "QuizResult" or "WordManager" or "Analytics" or "Account" or "Profile" or "Settings" or "About";
    }

    private static bool IsTabView(string view)
    {
        return view is "Dashboard" or "Study" or "Quiz" or "Account";
    }

    private void ClearWordEditor()
    {
        SelectedWord = null;
        EditorWord = "";
        EditorType = "";
        EditorMeaning = "";
        EditorVietnameseMeaning = "";
        EditorExample = "";
        WordManagerMessage = "Sẵn sàng thêm từ mới.";
    }

    private void SaveWord()
    {
        if (string.IsNullOrWhiteSpace(EditorWord) || string.IsNullOrWhiteSpace(EditorMeaning))
        {
            WordManagerMessage = "Từ vựng và nghĩa là bắt buộc.";
            return;
        }

        if (SelectedWord is null)
        {
            var word = new VocabularyWord
            {
                Word = EditorWord.Trim(),
                Type = string.IsNullOrWhiteSpace(EditorType) ? "word" : EditorType.Trim(),
                Meaning = EditorMeaning.Trim(),
                VietnameseMeaning = string.IsNullOrWhiteSpace(EditorVietnameseMeaning) ? EditorMeaning.Trim() : EditorVietnameseMeaning.Trim(),
                Example = EditorExample.Trim()
            };

            Words.Add(word);
            SelectedWord = word;
            WordManagerMessage = $"Đã thêm {word.Word}.";
        }
        else
        {
            SelectedWord.Word = EditorWord.Trim();
            SelectedWord.Type = string.IsNullOrWhiteSpace(EditorType) ? "word" : EditorType.Trim();
            SelectedWord.Meaning = EditorMeaning.Trim();
            SelectedWord.VietnameseMeaning = string.IsNullOrWhiteSpace(EditorVietnameseMeaning) ? EditorMeaning.Trim() : EditorVietnameseMeaning.Trim();
            SelectedWord.Example = EditorExample.Trim();
            WordManagerMessage = $"Đã cập nhật {SelectedWord.Word}.";
        }

        OnPropertyChanged(nameof(FilteredWords));
        OnPropertyChanged(nameof(CurrentWord));
        OnProgressChanged();
        RefreshQuizQuestions();
        SaveAppState();
    }

    private void DeleteSelectedWord()
    {
        if (SelectedWord is null)
        {
            WordManagerMessage = "Chọn một từ trước khi xóa.";
            return;
        }

        var deletedWord = SelectedWord.Word;
        var deletedIndex = Words.IndexOf(SelectedWord);
        Words.Remove(SelectedWord);
        CurrentWordIndex = Words.Count == 0 ? 0 : Math.Min(CurrentWordIndex, Words.Count - 1);
        ClearWordEditor();
        WordManagerMessage = $"Đã xóa {deletedWord}.";

        if (deletedIndex >= 0)
        {
            OnPropertyChanged(nameof(FilteredWords));
            OnPropertyChanged(nameof(CurrentWord));
            OnPropertyChanged(nameof(StudyProgressText));
            OnPropertyChanged(nameof(StudyProgressValue));
        }

        OnProgressChanged();
        RefreshQuizQuestions();
        SaveAppState();
    }

    private void RefreshQuizQuestions()
    {
        QuizQuestions.Clear();
        foreach (var question in quizService.CreateQuestions(Words))
        {
            QuizQuestions.Add(question);
        }

        CurrentQuizIndex = 0;
        OnPropertyChanged(nameof(CurrentQuizQuestion));
        OnPropertyChanged(nameof(QuizProgressText));
        OnPropertyChanged(nameof(QuizProgressValue));
        OnPropertyChanged(nameof(QuizScorePercentage));
        OnPropertyChanged(nameof(QuizScoreText));
    }

    private void RefreshTodayWord()
    {
        var learnedWords = Words
            .Where(word => word.LastReviewedAt is not null || word.MasteryLevel >= 5)
            .Where(word => !string.IsNullOrWhiteSpace(word.Word) && !string.IsNullOrWhiteSpace(word.VietnameseMeaning))
            .ToList();

        var sourceWords = learnedWords.Count > 0
            ? learnedWords
            : Words.Where(word => !string.IsNullOrWhiteSpace(word.Word) && !string.IsNullOrWhiteSpace(word.VietnameseMeaning)).ToList();

        if (sourceWords.Count == 0)
        {
            todayWord = null;
            todayWordChoices = ["Chưa có dữ liệu", "Thêm từ mới", "Học một bài", "Quay lại sau"];
            todayWordCorrectIndex = 0;
            todayWordSelectedIndex = -1;
            OnTodayWordChanged();
            return;
        }

        var random = new Random();
        todayWord = sourceWords[random.Next(sourceWords.Count)];

        var choices = sourceWords
            .Where(word => !ReferenceEquals(word, todayWord))
            .Select(word => word.VietnameseMeaning)
            .Where(meaning => !string.IsNullOrWhiteSpace(meaning) && meaning != todayWord.VietnameseMeaning)
            .Distinct()
            .OrderBy(_ => random.Next())
            .Take(3)
            .ToList();

        foreach (var fallback in new[] { "Một từ học thuật", "Một cụm giao tiếp", "Một nghĩa trái ngược", "Một hành động quen thuộc" })
        {
            if (choices.Count == 3)
            {
                break;
            }

            if (fallback != todayWord.VietnameseMeaning && !choices.Contains(fallback))
            {
                choices.Add(fallback);
            }
        }

        choices.Insert(random.Next(choices.Count + 1), todayWord.VietnameseMeaning);
        todayWordChoices = choices.Take(4).ToList();
        todayWordCorrectIndex = todayWordChoices.ToList().IndexOf(todayWord.VietnameseMeaning);
        todayWordSelectedIndex = -1;
        OnTodayWordChanged();
    }

    private Brush GetTodayChoiceBackground(int index)
    {
        if (todayWordSelectedIndex < 0)
        {
            return Brushes.White;
        }

        if (index == todayWordCorrectIndex)
        {
            return new SolidColorBrush(Color.FromRgb(226, 247, 235));
        }

        if (index == todayWordSelectedIndex)
        {
            return new SolidColorBrush(Color.FromRgb(253, 229, 232));
        }

        return Brushes.White;
    }

    private Brush GetTodayChoiceBorder(int index)
    {
        if (todayWordSelectedIndex < 0)
        {
            return new SolidColorBrush(Color.FromRgb(116, 89, 217));
        }

        if (index == todayWordCorrectIndex)
        {
            return new SolidColorBrush(Color.FromRgb(37, 150, 90));
        }

        if (index == todayWordSelectedIndex)
        {
            return new SolidColorBrush(Color.FromRgb(200, 32, 47));
        }

        return new SolidColorBrush(Color.FromRgb(222, 226, 235));
    }

    private static string GetTodayChoiceLabel(int index)
    {
        return index switch
        {
            0 => "A",
            1 => "B",
            2 => "C",
            3 => "D",
            _ => ""
        };
    }

    private void OnTodayWordChanged()
    {
        OnPropertyChanged(nameof(TodayWordText));
        OnPropertyChanged(nameof(TodayWordHint));
        OnPropertyChanged(nameof(TodayChoiceA));
        OnPropertyChanged(nameof(TodayChoiceB));
        OnPropertyChanged(nameof(TodayChoiceC));
        OnPropertyChanged(nameof(TodayChoiceD));
        OnPropertyChanged(nameof(TodayChoiceABackground));
        OnPropertyChanged(nameof(TodayChoiceBBackground));
        OnPropertyChanged(nameof(TodayChoiceCBackground));
        OnPropertyChanged(nameof(TodayChoiceDBackground));
        OnPropertyChanged(nameof(TodayChoiceABorder));
        OnPropertyChanged(nameof(TodayChoiceBBorder));
        OnPropertyChanged(nameof(TodayChoiceCBorder));
        OnPropertyChanged(nameof(TodayChoiceDBorder));
        OnPropertyChanged(nameof(CanSelectTodayWordAnswer));
        OnPropertyChanged(nameof(TodayWordResultText));
    }

    private void OnProgressChanged()
    {
        OnPropertyChanged(nameof(WordsDueToday));
        OnPropertyChanged(nameof(MasteredWords));
        OnPropertyChanged(nameof(StudyStreakDays));
        OnPropertyChanged(nameof(TotalLessons));
        OnPropertyChanged(nameof(TotalVocabularyWords));
        OnPropertyChanged(nameof(LearnedOrRememberedWords));
        OnPropertyChanged(nameof(RecentDecks));
        OnPropertyChanged(nameof(ContinueLearningDeck));
        OnPropertyChanged(nameof(ContinueLearningProgress));
        OnPropertyChanged(nameof(TodayGoalPercentage));
        OnPropertyChanged(nameof(TodayGoalText));
        OnPropertyChanged(nameof(WeeklyProgressValues));
    }

    private void ApplyAccountState(AppState state)
    {
        User.FullName = state.User.FullName;
        User.Email = state.User.Email;
        User.Phone = state.User.Phone;
        User.Level = state.User.Level;

        Decks.Clear();
        foreach (var deck in state.Decks)
        {
            Decks.Add(deck);
        }

        Words.Clear();
        foreach (var word in state.Words)
        {
            Words.Add(word);
        }

        AudioVolume = state.Settings.AudioVolume;
        DailyReminders = state.Settings.DailyReminders;
        AutoPlayAudio = state.Settings.AutoPlayAudio;
        OfflineMode = state.Settings.OfflineMode;
        SelectedStudyDeck = Decks.FirstOrDefault();
        CurrentWordIndex = 0;
        ClearWordEditor();
        RefreshQuizQuestions();
        RefreshTodayWord();
        OnPropertyChanged(nameof(FilteredDecks));
        OnPropertyChanged(nameof(FilteredWords));
        OnPropertyChanged(nameof(CurrentStudyWords));
        OnPropertyChanged(nameof(CurrentWord));
        OnProgressChanged();
    }

    private void ClearCurrentUser()
    {
        User.FullName = "";
        User.Email = "";
        User.Phone = "";
        User.Level = "";
        ClearLearningData();
        LoadProfileEditor();
    }

    private void ClearLearningData()
    {
        Decks.Clear();
        Words.Clear();
        SelectedStudyDeck = null;
        CurrentWordIndex = 0;
        ClearWordEditor();
        RefreshQuizQuestions();
        RefreshTodayWord();
        OnPropertyChanged(nameof(FilteredDecks));
        OnPropertyChanged(nameof(FilteredWords));
        OnPropertyChanged(nameof(CurrentStudyWords));
        OnPropertyChanged(nameof(CurrentWord));
        OnProgressChanged();
    }

    private void SaveAppState()
    {
        storageService.Save(new AppState
        {
            User = User,
            Decks = Decks.ToList(),
            Words = Words.ToList(),
            Settings = new SettingsState
            {
                AudioVolume = AudioVolume,
                DailyReminders = DailyReminders,
                AutoPlayAudio = AutoPlayAudio,
                OfflineMode = OfflineMode
            }
        });
    }
}
