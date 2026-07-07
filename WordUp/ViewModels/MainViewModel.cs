using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WordUp.Models;
using WordUp.Services;

namespace WordUp.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private static readonly QuizQuestion EmptyQuizQuestion = new()
    {
        Term = "",
        Prompt = "",
        Choices = ["", "", "", ""],
        CorrectIndex = 0
    };

    private readonly SampleDataService dataService = new();
    private readonly LocalStorageService storageService = new();
    private readonly SrsService srsService = new();
    private readonly QuizService quizService = new();
    private readonly PronunciationService pronunciationService = new();
    private readonly DispatcherTimer studyAutoTimer;
    private readonly DispatcherTimer quizTimer;
    private object? speechVoice;
    private MediaPlayer? audioPlayer;
    private CancellationTokenSource? newLessonIpaLookupCancellation;
    private CancellationTokenSource? editorIpaLookupCancellation;
    private string currentView = "Dashboard";
    private string pendingAuthenticatedView = "Dashboard";
    private string searchText = "";
    private bool isFlashcardBackVisible;
    private bool isStudyFlashcardOpen;
    private bool isAddLessonOpen;
    private bool isStudyAutoRunning;
    private bool isLessonCompleteDialogOpen;
    private int currentWordIndex;
    private int currentQuizIndex;
    private TimeSpan quizElapsedTime = TimeSpan.Zero;
    private TimeSpan quizCompletedTime = TimeSpan.Zero;
    private string quizSubmitMessage = "";
    private bool isQuizInProgress;
    private bool isQuizSettingsOpen;
    private int quizQuestionLimit = 20;
    private string practiceTab = "Practice";
    private string practiceWordSource = "Learned";
    private int practiceSessionCount;
    private bool isPracticeQuiz;
    private bool isIncompleteQuizSubmitDialogOpen;
    private bool isDeleteAccountDialogOpen;
    private bool isAvatarDialogOpen;
    private bool isLogoutDialogOpen;
    private string selectedTab = "Dashboard";
    private string lessonSearchText = "";
    private Deck? selectedStudyDeck;
    private IReadOnlyList<VocabularyWord> lastQuizSourceWords = [];
    private int lastQuizQuestionLimit = 20;
    private Deck? editingLesson;
    private Deck? pendingDeleteLesson;
    private VocabularyWord? pendingDeleteLessonWord;
    private bool isDeleteLessonWordDialogOpen;
    private string newLessonName = "";
    private string newLessonWord = "";
    private string newLessonIpa = "";
    private bool newLessonIpaWasAutoFilled;
    private string newLessonMeaning = "";
    private string newLessonType = "";
    private string newLessonAudioPath = "";
    private string newLessonFilePath = "";
    private string newLessonMessage = "";
    private string lessonWordSearchText = "";
    private VocabularyWord? editingLessonWord;
    private VocabularyWord? selectedWord;
    private VocabularyWord? pendingDeleteWord;
    private bool isDeleteWordDialogOpen;
    private string editorWord = "";
    private string editorIpa = "";
    private bool editorIpaWasAutoFilled;
    private string editorType = "";
    private string editorMeaning = "";
    private string editorVietnameseMeaning = "";
    private string wordManagerMessage = "Chọn một dòng để sửa, hoặc nhập thông tin để thêm từ mới.";
    private string loginEmail = "";
    private string loginPassword = "";
    private string registerFullName = "";
    private string registerEmail = "";
    private string registerPhone = "";
    private string registerPassword = "";
    private string registerConfirmPassword = "";
    private string forgotPasswordEmail = "";
    private string practiceSelectedLessonId = "";
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
    private string profileNote = "";
    private string profileMessage = "";
    private double audioVolume = 75;
    private bool dailyReminders = true;
    private bool autoPlayAudio;
    private bool offlineMode;
    private bool isDarkMode;
    private bool isApplyingAccountState;
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
        foreach (var deck in Decks)
        {
            AttachPracticeLessonTracking(deck);
        }
        EnsureLessonIds();
        NewLessonWords = new ObservableCollection<VocabularyWord>();
        QuizQuestions = new ObservableCollection<QuizQuestion>(quizService.CreateQuestions(Words));
        Achievements = new ObservableCollection<Achievement>(dataService.Achievements);

        AudioVolume = savedState.Settings.AudioVolume;
        DailyReminders = savedState.Settings.DailyReminders;
        AutoPlayAudio = savedState.Settings.AutoPlayAudio;
        OfflineMode = savedState.Settings.OfflineMode;
        IsDarkMode = savedState.Settings.IsDarkMode;
        practiceSessionCount = savedState.Settings.PracticeSessionCount;

        NavigateCommand = new RelayCommand(parameter => Navigate(parameter?.ToString() ?? "Dashboard"));
        SelectTabCommand = new RelayCommand(parameter => SelectTab(parameter?.ToString() ?? "Dashboard"));
        StartLessonCommand = new RelayCommand(parameter => StartLesson(parameter));
        ShowLessonListCommand = new RelayCommand(_ => ShowLessonList());
        AddLessonCommand = new RelayCommand(_ => OpenAddLesson());
        EditLessonCommand = new RelayCommand(parameter => EditLesson(parameter));
        DeleteLessonCommand = new RelayCommand(parameter => DeleteLesson(parameter));
        CancelDeleteLessonCommand = new RelayCommand(_ => CancelDeleteLesson());
        ConfirmDeleteLessonCommand = new RelayCommand(_ => ConfirmDeleteLesson());
        EditLessonWordCommand = new RelayCommand(parameter => EditLessonWord(parameter));
        DeleteLessonWordCommand = new RelayCommand(parameter => DeleteLessonWord(parameter));
        CancelDeleteLessonWordCommand = new RelayCommand(_ => CancelDeleteLessonWord());
        ConfirmDeleteLessonWordCommand = new RelayCommand(_ => ConfirmDeleteLessonWord());
        AddLessonWordCommand = new RelayCommand(_ => AddLessonWord());
        ImportLessonWordAudioCommand = new RelayCommand(_ => ImportLessonWordAudio());
        ClearLessonWordAudioCommand = new RelayCommand(_ => NewLessonAudioPath = "");
        ImportLessonFileCommand = new RelayCommand(_ => ImportLessonFile());
        SaveNewLessonCommand = new RelayCommand(_ => SaveNewLesson());
        CancelNewLessonCommand = new RelayCommand(_ => ShowLessonList());
        FlipFlashcardCommand = new RelayCommand(_ => IsFlashcardBackVisible = !IsFlashcardBackVisible);
        PreviousWordCommand = new RelayCommand(_ => PreviousWord());
        NextWordCommand = new RelayCommand(_ => CompleteCurrentWordAndMoveNext());
        ToggleStudyAutoCommand = new RelayCommand(_ => ToggleStudyAuto());
        SpeakCurrentWordCommand = new RelayCommand(_ => SpeakWord(CurrentWord));
        ToggleCurrentFavoriteCommand = new RelayCommand(_ => ToggleCurrentFavorite());
        RestartLessonCommand = new RelayCommand(_ => RestartLesson());
        BackToLessonsFromCompleteCommand = new RelayCommand(_ => BackToLessonsFromComplete());
        StartQuizFromLessonCommand = new RelayCommand(_ => StartQuizFromLesson());
        StartPracticeCommand = new RelayCommand(_ => StartPractice());
        ToggleQuizSettingsCommand = new RelayCommand(_ => IsQuizSettingsOpen = !IsQuizSettingsOpen);
        CloseQuizSettingsCommand = new RelayCommand(_ => IsQuizSettingsOpen = false);
        SelectPracticeTabCommand = new RelayCommand(parameter => SelectPracticeTab(parameter?.ToString() ?? "Practice"));
        SelectPracticeWordSourceCommand = new RelayCommand(parameter => SelectPracticeWordSource(parameter?.ToString() ?? "Learned"));
        SelectPracticeLessonCommand = new RelayCommand(parameter => SelectPracticeLesson(parameter?.ToString() ?? ""));
        IncreaseQuizQuestionLimitCommand = new RelayCommand(_ => QuizQuestionLimit += 1);
        DecreaseQuizQuestionLimitCommand = new RelayCommand(_ => QuizQuestionLimit -= 1);
        SelectQuizAnswerCommand = new RelayCommand(parameter => SelectQuizAnswer(parameter));
        PreviousQuizCommand = new RelayCommand(_ => PreviousQuiz());
        NextQuizCommand = new RelayCommand(_ => NextQuiz());
        SubmitQuizCommand = new RelayCommand(_ => SubmitQuiz());
        ContinueQuizCommand = new RelayCommand(_ => IsIncompleteQuizSubmitDialogOpen = false);
        ForceSubmitQuizCommand = new RelayCommand(_ => CompleteQuizSubmission());
        FinishQuizResultCommand = new RelayCommand(_ => FinishQuizResult());
        ViewPracticeStatsCommand = new RelayCommand(_ => ViewPracticeStats());
        NewWordCommand = new RelayCommand(_ => ClearWordEditor());
        SaveWordCommand = new RelayCommand(_ => SaveWord());
        DeleteWordCommand = new RelayCommand(_ => OpenDeleteWordDialog());
        CancelDeleteWordCommand = new RelayCommand(_ => CancelDeleteWord());
        ConfirmDeleteWordCommand = new RelayCommand(_ => ConfirmDeleteWord());
        LoginCommand = new RelayCommand(_ => Login());
        RegisterCommand = new RelayCommand(_ => Register());
        ForgotPasswordCommand = new RelayCommand(_ => HandleForgotPassword());
        SaveProfileCommand = new RelayCommand(_ => SaveProfile());
        CancelProfileCommand = new RelayCommand(_ => LoadProfileEditor());
        ResetQuizCommand = new RelayCommand(_ => ResetQuiz());
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        TestAudioCommand = new RelayCommand(_ => SpeakText("WordUp"));
        LogoutCommand = new RelayCommand(_ => IsLogoutDialogOpen = true);
        CancelLogoutCommand = new RelayCommand(_ => IsLogoutDialogOpen = false);
        ConfirmLogoutCommand = new RelayCommand(_ => Logout());
        DeleteAccountCommand = new RelayCommand(_ => IsDeleteAccountDialogOpen = true);
        CancelDeleteAccountCommand = new RelayCommand(_ => IsDeleteAccountDialogOpen = false);
        ConfirmDeleteAccountCommand = new RelayCommand(_ => DeleteAccount());
        SelectTodayWordAnswerCommand = new RelayCommand(parameter => SelectTodayWordAnswer(parameter));
        SpeakTodayWordCommand = new RelayCommand(_ => SpeakTodayWord());
        ToggleTodayFavoriteCommand = new RelayCommand(_ => ToggleTodayFavorite());
        OpenAvatarDialogCommand = new RelayCommand(_ => IsAvatarDialogOpen = true);
        CloseAvatarDialogCommand = new RelayCommand(_ => IsAvatarDialogOpen = false);
        ImportAvatarCommand = new RelayCommand(_ => ImportAvatar());
        RemoveAvatarCommand = new RelayCommand(_ => RemoveAvatar());

        studyAutoTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        studyAutoTimer.Tick += (_, _) => AdvanceStudyAuto();

        quizTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        quizTimer.Tick += (_, _) =>
        {
            QuizElapsedTime = QuizElapsedTime.Add(TimeSpan.FromSeconds(1));
        };

        EnsurePracticeLessonSelection();
        LoadProfileEditor();
        RefreshTodayWord();
        ApplyTheme();
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
    public ICommand CancelDeleteLessonCommand { get; }
    public ICommand ConfirmDeleteLessonCommand { get; }
    public ICommand EditLessonWordCommand { get; }
    public ICommand DeleteLessonWordCommand { get; }
    public ICommand CancelDeleteLessonWordCommand { get; }
    public ICommand ConfirmDeleteLessonWordCommand { get; }
    public ICommand AddLessonWordCommand { get; }
    public ICommand ImportLessonWordAudioCommand { get; }
    public ICommand ClearLessonWordAudioCommand { get; }
    public ICommand ImportLessonFileCommand { get; }
    public ICommand SaveNewLessonCommand { get; }
    public ICommand CancelNewLessonCommand { get; }
    public ICommand FlipFlashcardCommand { get; }
    public ICommand PreviousWordCommand { get; }
    public ICommand NextWordCommand { get; }
    public ICommand ToggleStudyAutoCommand { get; }
    public ICommand SpeakCurrentWordCommand { get; }
    public ICommand ToggleCurrentFavoriteCommand { get; }
    public ICommand RestartLessonCommand { get; }
    public ICommand BackToLessonsFromCompleteCommand { get; }
    public ICommand StartQuizFromLessonCommand { get; }
    public ICommand StartPracticeCommand { get; }
    public ICommand ToggleQuizSettingsCommand { get; }
    public ICommand CloseQuizSettingsCommand { get; }
    public ICommand SelectPracticeTabCommand { get; }
    public ICommand SelectPracticeWordSourceCommand { get; }
    public ICommand SelectPracticeLessonCommand { get; }
    public ICommand IncreaseQuizQuestionLimitCommand { get; }
    public ICommand DecreaseQuizQuestionLimitCommand { get; }
    public ICommand SelectQuizAnswerCommand { get; }
    public ICommand PreviousQuizCommand { get; }
    public ICommand NextQuizCommand { get; }
    public ICommand SubmitQuizCommand { get; }
    public ICommand ContinueQuizCommand { get; }
    public ICommand ForceSubmitQuizCommand { get; }
    public ICommand FinishQuizResultCommand { get; }
    public ICommand ViewPracticeStatsCommand { get; }
    public ICommand NewWordCommand { get; }
    public ICommand SaveWordCommand { get; }
    public ICommand DeleteWordCommand { get; }
    public ICommand CancelDeleteWordCommand { get; }
    public ICommand ConfirmDeleteWordCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ForgotPasswordCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand CancelProfileCommand { get; }
    public ICommand ResetQuizCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand TestAudioCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand CancelLogoutCommand { get; }
    public ICommand ConfirmLogoutCommand { get; }
    public ICommand DeleteAccountCommand { get; }
    public ICommand CancelDeleteAccountCommand { get; }
    public ICommand ConfirmDeleteAccountCommand { get; }
    public ICommand SelectTodayWordAnswerCommand { get; }
    public ICommand SpeakTodayWordCommand { get; }
    public ICommand ToggleTodayFavoriteCommand { get; }
    public ICommand OpenAvatarDialogCommand { get; }
    public ICommand CloseAvatarDialogCommand { get; }
    public ICommand ImportAvatarCommand { get; }
    public ICommand RemoveAvatarCommand { get; }
}
