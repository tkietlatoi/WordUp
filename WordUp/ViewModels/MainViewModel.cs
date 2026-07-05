using System.Collections.ObjectModel;
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

public sealed class MainViewModel : ViewModelBase
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
    private string profileNote = "";
    private string profileMessage = "";
    private double audioVolume = 75;
    private bool dailyReminders = true;
    private bool autoPlayAudio;
    private bool offlineMode;
    private bool isDarkMode;
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

    public string CurrentView
    {
        get => currentView;
        set
        {
            if (value is not "Quiz" and not "QuizResult")
            {
                StopQuizTimer();
            }

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
                EditorIpa = value.Ipa;
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
        set
        {
            if (SetProperty(ref editorWord, value))
            {
                if (editorIpaWasAutoFilled)
                {
                    editorIpaWasAutoFilled = false;
                    EditorIpa = "";
                }

                _ = FillEditorIpaAsync(value);
            }
        }
    }

    public string EditorIpa
    {
        get => editorIpa;
        set
        {
            if (SetProperty(ref editorIpa, value))
            {
                editorIpaWasAutoFilled = false;
            }
        }
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

    public bool IsDeleteWordDialogOpen
    {
        get => isDeleteWordDialogOpen;
        set => SetProperty(ref isDeleteWordDialogOpen, value);
    }

    public string DeleteWordDialogText => pendingDeleteWord is null
        ? "Chọn một từ trước khi xóa."
        : $"Bạn có chắc muốn xóa từ \"{pendingDeleteWord.Word}\" không? Thao tác này không thể hoàn tác.";

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

    public string ProfileNote
    {
        get => profileNote;
        set => SetProperty(ref profileNote, value);
    }

    public string ProfileMessage
    {
        get => profileMessage;
        set => SetProperty(ref profileMessage, value);
    }

    public double AudioVolume
    {
        get => audioVolume;
        set
        {
            if (SetProperty(ref audioVolume, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(AudioVolumePercent));
            }
        }
    }

    public string AudioVolumePercent => $"{AudioVolume:0}%";

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

    public bool IsDarkMode
    {
        get => isDarkMode;
        set
        {
            if (SetProperty(ref isDarkMode, value))
            {
                ApplyTheme();
                OnTodayWordChanged();
                SaveAppState();
            }
        }
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

    public bool IsStudyAutoRunning
    {
        get => isStudyAutoRunning;
        set
        {
            if (SetProperty(ref isStudyAutoRunning, value))
            {
                OnPropertyChanged(nameof(StudyAutoButtonText));
            }
        }
    }

    public bool IsLessonCompleteDialogOpen
    {
        get => isLessonCompleteDialogOpen;
        set => SetProperty(ref isLessonCompleteDialogOpen, value);
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
                if (!value)
                {
                    StopStudyAuto();
                }
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
                OnPropertyChanged(nameof(StudyFavoriteIcon));
                OnPropertyChanged(nameof(StudyFavoriteForeground));
                OnPropertyChanged(nameof(StudyProgressText));
                OnPropertyChanged(nameof(StudyProgressValue));
            }
        }
    }

    public bool IsDeleteLessonDialogOpen => pendingDeleteLesson is not null;
    public string DeleteLessonDialogTitle => pendingDeleteLesson is null ? "Xóa bài học" : $"Xóa \"{pendingDeleteLesson.Name}\"?";
    public string DeleteLessonDialogMessage => pendingDeleteLesson is null
        ? ""
        : $"Bài học này có {pendingDeleteLesson.TotalWords} từ. Sau khi xóa, các từ trong bài cũng sẽ bị xóa khỏi hệ thống.";

    public string NewLessonName
    {
        get => newLessonName;
        set => SetProperty(ref newLessonName, value);
    }

    public string NewLessonWord
    {
        get => newLessonWord;
        set
        {
            if (SetProperty(ref newLessonWord, value))
            {
                if (newLessonIpaWasAutoFilled)
                {
                    newLessonIpaWasAutoFilled = false;
                    NewLessonIpa = "";
                }

                _ = FillNewLessonIpaAsync(value);
            }
        }
    }

    public string NewLessonIpa
    {
        get => newLessonIpa;
        set
        {
            if (SetProperty(ref newLessonIpa, value))
            {
                newLessonIpaWasAutoFilled = false;
            }
        }
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

    public bool IsDeleteLessonWordDialogOpen
    {
        get => isDeleteLessonWordDialogOpen;
        set => SetProperty(ref isDeleteLessonWordDialogOpen, value);
    }

    public string DeleteLessonWordDialogText => pendingDeleteLessonWord is null
        ? "Chọn một từ trước khi xóa."
        : $"Bạn có chắc muốn xóa từ \"{pendingDeleteLessonWord.Word}\" khỏi bài học này không?";

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
                OnPropertyChanged(nameof(StudyFavoriteIcon));
                OnPropertyChanged(nameof(StudyFavoriteForeground));
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

    public TimeSpan QuizElapsedTime
    {
        get => quizElapsedTime;
        set
        {
            if (SetProperty(ref quizElapsedTime, value))
            {
                OnPropertyChanged(nameof(QuizElapsedTimeText));
            }
        }
    }

    public TimeSpan QuizCompletedTime
    {
        get => quizCompletedTime;
        set
        {
            if (SetProperty(ref quizCompletedTime, value))
            {
                OnPropertyChanged(nameof(QuizCompletedTimeText));
            }
        }
    }

    public string QuizSubmitMessage
    {
        get => quizSubmitMessage;
        set => SetProperty(ref quizSubmitMessage, value);
    }

    public bool IsIncompleteQuizSubmitDialogOpen
    {
        get => isIncompleteQuizSubmitDialogOpen;
        set => SetProperty(ref isIncompleteQuizSubmitDialogOpen, value);
    }

    public bool IsQuizInProgress
    {
        get => isQuizInProgress;
        set
        {
            if (SetProperty(ref isQuizInProgress, value))
            {
                OnPropertyChanged(nameof(IsQuizIntroVisible));
            }
        }
    }

    public bool IsQuizIntroVisible => !IsQuizInProgress;

    public bool IsQuizSettingsOpen
    {
        get => isQuizSettingsOpen;
        set => SetProperty(ref isQuizSettingsOpen, value);
    }

    public int QuizQuestionLimit
    {
        get => quizQuestionLimit;
        set
        {
            if (SetProperty(ref quizQuestionLimit, Math.Clamp(value, 5, 30)))
            {
                OnPropertyChanged(nameof(QuizQuestionLimitText));
            }
        }
    }

    public string QuizQuestionLimitText => $"{QuizQuestionLimit} từ";

    public string PracticeTab
    {
        get => practiceTab;
        set
        {
            if (SetProperty(ref practiceTab, value))
            {
                OnPropertyChanged(nameof(IsPracticeTab));
                OnPropertyChanged(nameof(IsPracticeStatsTab));
            }
        }
    }

    public bool IsPracticeTab => PracticeTab == "Practice";
    public bool IsPracticeStatsTab => PracticeTab == "Stats";

    public string PracticeWordSource
    {
        get => practiceWordSource;
        set
        {
            if (SetProperty(ref practiceWordSource, value))
            {
                OnPropertyChanged(nameof(IsLearnedPracticeSource));
                OnPropertyChanged(nameof(IsFavoritePracticeSource));
                OnPropertyChanged(nameof(IsAllPracticeSource));
                OnPropertyChanged(nameof(PracticeWordSourceText));
                OnPropertyChanged(nameof(LearnedPracticeSourceBorder));
                OnPropertyChanged(nameof(FavoritePracticeSourceBorder));
                OnPropertyChanged(nameof(AllPracticeSourceBorder));
            }
        }
    }

    public bool IsLearnedPracticeSource
    {
        get => PracticeWordSource == "Learned";
        set
        {
            if (value)
            {
                PracticeWordSource = "Learned";
            }
        }
    }

    public bool IsFavoritePracticeSource
    {
        get => PracticeWordSource == "Favorite";
        set
        {
            if (value)
            {
                PracticeWordSource = "Favorite";
            }
        }
    }

    public bool IsAllPracticeSource
    {
        get => PracticeWordSource == "All";
        set
        {
            if (value)
            {
                PracticeWordSource = "All";
            }
        }
    }
    public string PracticeWordSourceText => PracticeWordSource switch
    {
        "Favorite" => "Từ đã đánh dấu sao",
        "All" => "Tất cả các từ",
        _ => "Những từ đã học"
    };

    public int PracticeSessionCount
    {
        get => practiceSessionCount;
        private set => SetProperty(ref practiceSessionCount, Math.Max(0, value));
    }
    public Brush LearnedPracticeSourceBorder => GetPracticeSourceBorder(IsLearnedPracticeSource);
    public Brush FavoritePracticeSourceBorder => GetPracticeSourceBorder(IsFavoritePracticeSource);
    public Brush AllPracticeSourceBorder => GetPracticeSourceBorder(IsAllPracticeSource);

    public bool IsDeleteAccountDialogOpen
    {
        get => isDeleteAccountDialogOpen;
        set => SetProperty(ref isDeleteAccountDialogOpen, value);
    }

    public bool IsAvatarDialogOpen
    {
        get => isAvatarDialogOpen;
        set => SetProperty(ref isAvatarDialogOpen, value);
    }

    public bool IsLogoutDialogOpen
    {
        get => isLogoutDialogOpen;
        set => SetProperty(ref isLogoutDialogOpen, value);
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
            || word.Ipa.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase)
            || word.Meaning.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase)
            || word.VietnameseMeaning.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase)
            || word.Type.Contains(LessonWordSearchText, StringComparison.OrdinalIgnoreCase));
    public QuizQuestion CurrentQuizQuestion => QuizQuestions.Count == 0
        ? EmptyQuizQuestion
        : QuizQuestions[Math.Clamp(CurrentQuizIndex, 0, QuizQuestions.Count - 1)];
    public IEnumerable<VocabularyWord> FilteredWords => string.IsNullOrWhiteSpace(SearchText)
        ? Words
        : Words.Where(word => word.Word.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || word.Ipa.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
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
    public string DashboardSubtitle => IsAuthenticated
        ? string.IsNullOrWhiteSpace(User.Note) ? "Thêm ghi chú trong hồ sơ cá nhân" : User.Note
        : "Welcome to WordUp";
    public string FlashcardHint => IsFlashcardBackVisible ? "Đánh giá mức độ ghi nhớ" : "Chạm để lật thẻ";
    public string StudyFavoriteIcon => CurrentWord.IsFavorite ? "★" : "☆";
    public Brush StudyFavoriteForeground => CurrentWord.IsFavorite
        ? new SolidColorBrush(Color.FromRgb(234, 179, 8))
        : new SolidColorBrush(Color.FromRgb(148, 153, 168));
    public string StudyAutoButtonText => IsStudyAutoRunning ? "⏸" : "▶";
    public string StudyHeaderTitle => IsStudyFlashcardOpen
        ? SelectedStudyDeck?.Name ?? "Học từ"
        : IsAddLessonOpen
            ? LessonEditorTitle
        : "Bài học";
    public string StudyProgressText => CurrentStudyWords.Count == 0 ? "0/0" : $"{CurrentWordIndex + 1}/{CurrentStudyWords.Count}";
    public double StudyProgressValue => CurrentStudyWords.Count == 0 ? 0 : ((CurrentWordIndex + 1) / (double)CurrentStudyWords.Count) * 100;
    public string QuizProgressText => QuizQuestions.Count == 0 ? "CÂU 0/0" : $"CÂU {CurrentQuizIndex + 1}/{QuizQuestions.Count}";
    public double QuizProgressValue => QuizQuestions.Count == 0 ? 0 : ((CurrentQuizIndex + 1) / (double)QuizQuestions.Count) * 100;
    public string QuizElapsedTimeText => FormatQuizTime(QuizElapsedTime);
    public string QuizCompletedTimeText => FormatQuizTime(QuizCompletedTime);
    public int AnsweredQuizCount => QuizQuestions.Count(question => question.IsAnswered);
    public bool IsQuizSubmitted => QuizQuestions.Count > 0 && QuizQuestions.All(question => question.IsSubmitted);
    public string IncompleteQuizSubmitText => $"Bạn đã làm {AnsweredQuizCount}/{QuizQuestions.Count} câu. Bạn muốn quay lại làm tiếp hay nộp bài luôn?";
    public int PracticeTotalWords => Words.Count;
    public int PracticeReviewedWords => Words.Count(word => GetPracticeAttemptCount(word) > 0);
    public int PracticeTotalAttempts => Words.Sum(GetPracticeAttemptCount);
    public int PracticeCorrectAnswers => Words.Sum(word => word.PracticeCorrectQuizCount);
    public int PracticeMasteryPercentage => PracticeTotalAttempts == 0
        ? 0
        : (int)Math.Round(PracticeCorrectAnswers / (double)PracticeTotalAttempts * 100);
    public string PracticeMasteryLabel => "Độ chính xác";
    public string PracticeMasteryText => PracticeTotalAttempts == 0
        ? "Chưa có dữ liệu luyện tập"
        : $"Tỷ lệ trả lời đúng: {PracticeCorrectAnswers}/{PracticeTotalAttempts} câu";
    public string PracticeCoverageText => $"{PracticeReviewedWords}/{PracticeTotalWords} từ đã xuất hiện trong luyện tập";
    public int PracticeStudiedTodayWords => Words.Count(word => word.LastPracticeAt?.Date == DateTime.Today);
    public string PracticeTotalWordsLine => $"Tổng số từ: {PracticeTotalWords}";
    public string PracticeTotalAttemptsLine => $"Số lượt luyện tập: {PracticeSessionCount}";
    public string PracticeStudiedTodayLine => $"Số từ học hôm nay: {PracticeStudiedTodayWords}";
    public string PracticeMasteredWordsLine => $"Tổng số từ đã thuộc: {PracticeGoodWords}";
    public string PracticeLastReviewedLine => $"Lần luyện gần nhất: {PracticeLastReviewedText}";
    public string PracticeLastReviewedText => Words
        .Where(word => word.LastPracticeAt is not null)
        .Select(word => word.LastPracticeAt!.Value)
        .DefaultIfEmpty()
        .Max() is var latest && latest != default
            ? latest.ToString("HH:mm dd/MM/yyyy")
            : "Chưa có";
    public int PracticeWeakWords => CountPracticeBucket(0, 0.5);
    public int PracticeLearningWords => CountPracticeBucket(0.5, 0.8);
    public int PracticeGoodWords => CountPracticeBucket(0.8, 1.0);
    public int PracticeNewWords => Words.Count(word => GetPracticeAttemptCount(word) == 0);
    public string PracticeNewCountText => $"Mới: {PracticeNewWords} từ";
    public string PracticeWeakCountText => $"Yếu: {PracticeWeakWords} từ";
    public string PracticeLearningCountText => $"Đang nhớ: {PracticeLearningWords} từ";
    public string PracticeGoodCountText => $"Tốt: {PracticeGoodWords} từ";
    public double PracticeWeakBar => GetPracticeBucketPercentage(0, 0.5);
    public double PracticeLearningBar => GetPracticeBucketPercentage(0.5, 0.8);
    public double PracticeGoodBar => GetPracticeBucketPercentage(0.8, 1.0);
    public double PracticeNewBar => PracticeTotalWords == 0
        ? 0
        : PracticeNewWords / (double)PracticeTotalWords * 100;
    public int WordsDueToday => Words.Count(word => word.NextReviewDate.Date <= DateTime.Today);
    public int MasteredWords => Words.Count(word => word.MasteryLevel >= 5);
    public int StudyStreakDays => CalculateStudyStreakDays();
    public int TotalLessons => Decks.Count;
    public int TotalVocabularyWords => Words.Count;
    public int LearnedOrRememberedWords => Words.Count(IsLearnedOrRemembered);
    public IEnumerable<Deck> RecentDecks => Decks.OrderByDescending(deck => deck.UpdatedAt).Take(3);
    public Deck? ContinueLearningDeck => Decks
        .OrderByDescending(deck => deck.UpdatedAt)
        .FirstOrDefault(deck => deck.ProgressPercentage is > 0 and < 100)
        ?? Decks.OrderByDescending(deck => deck.UpdatedAt).FirstOrDefault();
    public string ContinueLearningProgress => ContinueLearningDeck?.ProgressText ?? "Chưa có bài học nào";
    public string TodayWordText => todayWord?.Word ?? "WordUp";
    public string TodayWordHint => string.IsNullOrWhiteSpace(todayWord?.Ipa) ? "Chưa có phiên âm" : todayWord.Ipa;
    public string TodayFavoriteIcon => todayWord?.IsFavorite == true ? "★" : "☆";
    public Brush TodayFavoriteForeground => todayWord?.IsFavorite == true
        ? new SolidColorBrush(Color.FromRgb(234, 179, 8))
        : new SolidColorBrush(Color.FromRgb(148, 153, 168));
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
    public Brush TodayChoiceAForeground => GetTodayChoiceForeground(0);
    public Brush TodayChoiceBForeground => GetTodayChoiceForeground(1);
    public Brush TodayChoiceCForeground => GetTodayChoiceForeground(2);
    public Brush TodayChoiceDForeground => GetTodayChoiceForeground(3);
    public bool CanSelectTodayWordAnswer => todayWordSelectedIndex < 0;
    public string TodayWordResultText => todayWordSelectedIndex < 0
        ? "Chọn một đáp án để kiểm tra."
        : todayWordSelectedIndex == todayWordCorrectIndex
            ? "Chính xác."
            : $"Chưa đúng. Đáp án đúng là {GetTodayChoiceLabel(todayWordCorrectIndex)}.";
    public int TodayGoalPercentage => Math.Min(100, (int)Math.Round(Words.Count(word => word.LastReviewedAt?.Date == DateTime.Today) / 10.0 * 100));
    public string TodayGoalText => $"{Words.Count(word => word.LastReviewedAt?.Date == DateTime.Today)} / 10 từ";
    public IReadOnlyList<int> WeeklyProgressValues => [35, 48, 60, 42, 55, 30, TodayGoalPercentage];
    public int QuizScorePercentage => QuizQuestions.Count == 0
        ? 0
        : (int)Math.Round(QuizQuestions.Count(q => q.IsCorrect) / (double)QuizQuestions.Count * 100);
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

        if (tab == "Quiz")
        {
            ShowPracticeIntro();
            return;
        }

        CurrentView = tab;
    }

    private void StartLesson(object? parameter)
    {
        SelectedStudyDeck = parameter as Deck ?? Decks.FirstOrDefault();
        if (SelectedStudyDeck is null)
        {
            SelectTab("Study");
            return;
        }

        SelectedTab = "Study";
        CurrentView = "Study";
        CurrentWordIndex = GetLessonStartIndex(SelectedStudyDeck);
        IsFlashcardBackVisible = false;
        IsLessonCompleteDialogOpen = false;
        IsAddLessonOpen = false;
        IsStudyFlashcardOpen = true;
        AutoPlayCurrentWord();
    }

    private void ShowLessonList()
    {
        StopStudyAuto();
        IsStudyFlashcardOpen = false;
        IsAddLessonOpen = false;
        IsFlashcardBackVisible = false;
        IsLessonCompleteDialogOpen = false;
        editingLesson = null;
        OnLessonEditorChanged();
    }

    private int GetLessonStartIndex(Deck deck)
    {
        var studyWords = CurrentStudyWords;
        if (studyWords.Count == 0 || deck.ProgressPercentage >= 100)
        {
            return 0;
        }

        var firstUnlearnedIndex = studyWords
            .Select((word, index) => new { word, index })
            .FirstOrDefault(item => !IsLearnedOrRemembered(item.word))
            ?.index;

        return firstUnlearnedIndex ?? 0;
    }

    private void TouchSelectedStudyDeck()
    {
        if (SelectedStudyDeck is null)
        {
            return;
        }

        SelectedStudyDeck.UpdatedAt = DateTime.Now;
    }

    private void OpenAddLesson()
    {
        editingLesson = null;
        IsStudyFlashcardOpen = false;
        IsAddLessonOpen = true;
        NewLessonName = $"Bài học mới {Decks.Count + 1}";
        NewLessonWord = "";
        NewLessonIpa = "";
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
        NewLessonIpa = "";
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
        NewLessonIpa = word.Ipa;
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

        pendingDeleteLesson = lesson;
        OnDeleteLessonDialogChanged();
    }

    private void CancelDeleteLesson()
    {
        pendingDeleteLesson = null;
        OnDeleteLessonDialogChanged();
    }

    private void ConfirmDeleteLesson()
    {
        var lesson = pendingDeleteLesson;
        if (lesson is null)
        {
            return;
        }

        pendingDeleteLesson = null;
        OnDeleteLessonDialogChanged();

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

    private void OnDeleteLessonDialogChanged()
    {
        OnPropertyChanged(nameof(IsDeleteLessonDialogOpen));
        OnPropertyChanged(nameof(DeleteLessonDialogTitle));
        OnPropertyChanged(nameof(DeleteLessonDialogMessage));
    }

    private void DeleteLessonWord(object? parameter)
    {
        if (parameter is not VocabularyWord word)
        {
            return;
        }

        pendingDeleteLessonWord = word;
        IsDeleteLessonWordDialogOpen = true;
        OnPropertyChanged(nameof(DeleteLessonWordDialogText));
    }

    private void CancelDeleteLessonWord()
    {
        pendingDeleteLessonWord = null;
        IsDeleteLessonWordDialogOpen = false;
        OnPropertyChanged(nameof(DeleteLessonWordDialogText));
    }

    private void ConfirmDeleteLessonWord()
    {
        if (pendingDeleteLessonWord is null)
        {
            CancelDeleteLessonWord();
            return;
        }

        var word = pendingDeleteLessonWord;
        NewLessonWords.Remove(word);
        pendingDeleteLessonWord = null;
        IsDeleteLessonWordDialogOpen = false;
        OnPropertyChanged(nameof(FilteredNewLessonWords));
        OnPropertyChanged(nameof(DeleteLessonWordDialogText));
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
            editingLessonWord.Ipa = NewLessonIpa.Trim();
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
            Ipa = NewLessonIpa.Trim(),
            Meaning = NewLessonMeaning.Trim(),
            VietnameseMeaning = NewLessonMeaning.Trim(),
            Type = string.IsNullOrWhiteSpace(NewLessonType) ? "word" : NewLessonType.Trim()
        });

        ClearLessonWordEditor();
        OnPropertyChanged(nameof(FilteredNewLessonWords));
        NewLessonMessage = $"Đã thêm {NewLessonWords.Count} từ vào bài học.";
    }

    private async Task FillNewLessonIpaAsync(string word)
    {
        newLessonIpaLookupCancellation?.Cancel();

        var requestedWord = word.Trim();
        if (OfflineMode || string.IsNullOrWhiteSpace(requestedWord) || !string.IsNullOrWhiteSpace(NewLessonIpa))
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        newLessonIpaLookupCancellation = cancellation;

        try
        {
            await Task.Delay(450, cancellation.Token);
            var ipa = await pronunciationService.GetIpaAsync(requestedWord, cancellation.Token);
            if (string.IsNullOrWhiteSpace(ipa)
                || !string.Equals(NewLessonWord.Trim(), requestedWord, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(NewLessonIpa))
            {
                return;
            }

            NewLessonIpa = ipa;
            newLessonIpaWasAutoFilled = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(newLessonIpaLookupCancellation, cancellation))
            {
                newLessonIpaLookupCancellation = null;
            }
        }
    }

    private async Task FillEditorIpaAsync(string word)
    {
        editorIpaLookupCancellation?.Cancel();

        var requestedWord = word.Trim();
        if (OfflineMode || string.IsNullOrWhiteSpace(requestedWord) || !string.IsNullOrWhiteSpace(EditorIpa))
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        editorIpaLookupCancellation = cancellation;

        try
        {
            await Task.Delay(450, cancellation.Token);
            var ipa = await pronunciationService.GetIpaAsync(requestedWord, cancellation.Token);
            if (string.IsNullOrWhiteSpace(ipa)
                || !string.Equals(EditorWord.Trim(), requestedWord, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(EditorIpa))
            {
                return;
            }

            EditorIpa = ipa;
            editorIpaWasAutoFilled = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(editorIpaLookupCancellation, cancellation))
            {
                editorIpaLookupCancellation = null;
            }
        }
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

        _ = FillImportedLessonIpaAsync(importedWords);

        OnPropertyChanged(nameof(FilteredNewLessonWords));
        NewLessonMessage = importedWords.Count == 0
            ? "Không tìm thấy dòng hợp lệ. Mỗi dòng cần ít nhất: từ, nghĩa. Ví dụ: apple, quả táo."
            : $"Đã nhập {importedWords.Count} từ từ tệp.";
    }

    private async Task FillImportedLessonIpaAsync(IReadOnlyCollection<VocabularyWord> importedWords)
    {
        if (OfflineMode || importedWords.Count == 0)
        {
            return;
        }

        var filledCount = 0;
        foreach (var word in importedWords.Where(word => string.IsNullOrWhiteSpace(word.Ipa)))
        {
            var ipa = await pronunciationService.GetIpaAsync(word.Word);
            if (string.IsNullOrWhiteSpace(ipa) || !string.IsNullOrWhiteSpace(word.Ipa))
            {
                continue;
            }

            word.Ipa = ipa;
            filledCount++;
        }

        if (filledCount > 0)
        {
            NewLessonMessage = $"Đã tự điền IPA cho {filledCount} từ.";
        }
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
            Decks.Add(lesson);
        }
        else
        {
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
        SyncLessonProgress();
        OnPropertyChanged(nameof(FilteredWords));
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
        NewLessonIpa = "";
        NewLessonMeaning = "";
        NewLessonType = "";
        OnLessonEditorChanged();
    }

    private void EnsureLessonIds()
    {
        if (Decks.Count == 0)
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
        }

        SyncLessonProgress();
        OnPropertyChanged(nameof(FilteredDecks));
    }

    private void SyncLessonProgress()
    {
        foreach (var deck in Decks)
        {
            var lessonWords = Words.Where(word => word.LessonId == deck.Id).ToList();
            deck.TotalWords = lessonWords.Count;
            deck.LearnedWords = lessonWords.Count(IsLearnedOrRemembered);
        }

        OnPropertyChanged(nameof(FilteredDecks));
        OnPropertyChanged(nameof(RecentDecks));
        OnPropertyChanged(nameof(ContinueLearningDeck));
        OnPropertyChanged(nameof(ContinueLearningProgress));
    }

    private static bool IsLearnedOrRemembered(VocabularyWord word)
    {
        return word.LastReviewedAt is not null || word.MasteryLevel >= 5;
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
            PracticeCorrectQuizCount = word.PracticeCorrectQuizCount,
            PracticeIncorrectQuizCount = word.PracticeIncorrectQuizCount,
            IsFavorite = word.IsFavorite,
            LastReviewedAt = word.LastReviewedAt,
            LastPracticeAt = word.LastPracticeAt,
            NextReviewDate = word.NextReviewDate
        };
    }

    private IReadOnlyList<VocabularyWord> ReadVocabularyFile(string path)
    {
        try
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".doc")
            {
                NewLessonMessage = "Ứng dụng chỉ đọc được Word dạng .docx. Hãy mở file trong Word rồi Save As thành .docx.";
                return [];
            }

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

        var normalizedRow = row.Trim();
        var parts = normalizedRow
            .Split(['\t', '|', ';', ','], StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length < 2)
        {
            parts = SplitLooseVocabularyRow(normalizedRow);
        }

        if (parts.Length < 2 || IsVocabularyHeader(parts))
        {
            return null;
        }

        var hasIpa = LooksLikeIpa(parts[1]);
        var meaningIndex = hasIpa ? 2 : 1;
        var typeIndex = hasIpa ? 3 : 2;
        var exampleIndex = hasIpa ? 4 : 3;

        return new VocabularyWord
        {
            Word = parts[0],
            Ipa = hasIpa ? parts[1] : "",
            Meaning = parts.Length > meaningIndex ? parts[meaningIndex] : parts[1],
            VietnameseMeaning = parts.Length > meaningIndex ? parts[meaningIndex] : parts[1],
            Type = parts.Length > typeIndex ? parts[typeIndex] : "word",
            Example = parts.Length > exampleIndex ? parts[exampleIndex] : ""
        };
    }

    private static bool LooksLikeIpa(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2
            && ((trimmed.StartsWith('/') && trimmed.EndsWith('/'))
                || (trimmed.StartsWith('[') && trimmed.EndsWith(']')));
    }

    private static string[] SplitLooseVocabularyRow(string row)
    {
        foreach (var separator in new[] { " - ", " – ", " — " })
        {
            var parts = row
                .Split(separator, StringSplitOptions.TrimEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();

            if (parts.Length >= 2)
            {
                return parts;
            }
        }

        foreach (var separator in new[] { ": " })
        {
            var index = row.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && index + separator.Length < row.Length)
            {
                return
                [
                    row[..index].Trim(),
                    row[(index + separator.Length)..].Trim()
                ];
            }
        }

        return [];
    }

    private static bool IsVocabularyHeader(IReadOnlyList<string> parts)
    {
        var first = parts[0].Trim();
        var second = parts[1].Trim();
        return IsHeaderName(first, "word", "từ", "tu", "vocab", "vocabulary")
            && IsHeaderName(second, "meaning", "nghĩa", "nghia", "definition", "translation");
    }

    private static bool IsHeaderName(string value, params string[] names)
    {
        return names.Any(name => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
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
        AutoPlayCurrentWord();
    }

    private void PreviousWord()
    {
        if (CurrentStudyWords.Count == 0)
        {
            return;
        }

        CurrentWordIndex = (CurrentWordIndex - 1 + CurrentStudyWords.Count) % CurrentStudyWords.Count;
        IsFlashcardBackVisible = false;
        AutoPlayCurrentWord();
    }

    private void ToggleStudyAuto()
    {
        if (IsStudyAutoRunning)
        {
            StopStudyAuto();
            return;
        }

        if (CurrentStudyWords.Count == 0)
        {
            return;
        }

        IsStudyAutoRunning = true;
        studyAutoTimer.Start();
    }

    private void AdvanceStudyAuto()
    {
        if (CurrentStudyWords.Count == 0)
        {
            StopStudyAuto();
            return;
        }

        if (!IsFlashcardBackVisible)
        {
            IsFlashcardBackVisible = true;
            return;
        }

        CompleteCurrentWordAndMoveNext();
    }

    private void StopStudyAuto()
    {
        studyAutoTimer.Stop();
        IsStudyAutoRunning = false;
    }

    private void CompleteCurrentWordAndMoveNext()
    {
        if (CurrentStudyWords.Count == 0)
        {
            return;
        }

        var isLastCard = CurrentWordIndex >= CurrentStudyWords.Count - 1;

        if (!IsLearnedOrRemembered(CurrentWord))
        {
            srsService.ApplyReview(CurrentWord, 1);
            TouchSelectedStudyDeck();
            SyncLessonProgress();
            OnProgressChanged();
            SaveAppState();
        }

        if (isLastCard)
        {
            ShowLessonCompleteDialog();
            return;
        }

        NextWord();
    }

    private void ShowLessonCompleteDialog()
    {
        StopStudyAuto();
        IsFlashcardBackVisible = true;
        IsLessonCompleteDialogOpen = true;
    }

    private void RestartLesson()
    {
        StopStudyAuto();
        CurrentWordIndex = 0;
        IsFlashcardBackVisible = false;
        IsLessonCompleteDialogOpen = false;
        AutoPlayCurrentWord();
    }

    private void BackToLessonsFromComplete()
    {
        IsLessonCompleteDialogOpen = false;
        ShowLessonList();
    }

    private void StartQuizFromLesson()
    {
        if (!RequireAuthentication("Quiz"))
        {
            return;
        }

        StopStudyAuto();
        IsLessonCompleteDialogOpen = false;
        IsStudyFlashcardOpen = false;
        IsAddLessonOpen = false;
        IsFlashcardBackVisible = false;
        SelectedTab = "Quiz";

        var lessonQuizWords = SelectedStudyDeck is null
            ? CurrentStudyWords
            : Words.Where(word => word.LessonId == SelectedStudyDeck.Id).ToList();

        isPracticeQuiz = false;
        ResetQuiz(lessonQuizWords);
    }

    private void ShowPracticeIntro()
    {
        StopQuizTimer();
        SelectedTab = "Quiz";
        IsQuizInProgress = false;
        IsIncompleteQuizSubmitDialogOpen = false;
        PracticeTab = "Practice";
        CurrentView = "Quiz";
        OnPracticeStatsChanged();
    }

    private void StartPractice()
    {
        IsQuizSettingsOpen = false;
        isPracticeQuiz = true;
        ResetQuiz(GetPracticeWords());
    }

    private void SelectPracticeTab(string tab)
    {
        PracticeTab = tab == "Stats" ? "Stats" : "Practice";
    }

    private void SelectPracticeWordSource(string source)
    {
        PracticeWordSource = source is "Favorite" or "All" ? source : "Learned";
    }

    private void FinishQuizResult()
    {
        if (isPracticeQuiz)
        {
            ShowPracticeIntro();
            return;
        }

        SelectTab("Study");
    }

    private void ViewPracticeStats()
    {
        StopQuizTimer();
        SelectedTab = "Quiz";
        IsQuizInProgress = false;
        IsIncompleteQuizSubmitDialogOpen = false;
        PracticeTab = "Stats";
        CurrentView = "Quiz";
        OnPracticeStatsChanged();
    }

    private IEnumerable<VocabularyWord> GetPracticeWords()
    {
        var selectedWords = PracticeWordSource switch
        {
            "Favorite" => Words.Where(word => word.IsFavorite),
            "All" => Words,
            _ => Words.Where(IsLearnedOrRemembered)
        };

        var list = selectedWords.ToList();
        return list.Count > 0 ? list : Words;
    }

    private void ReviewCurrentWord(object? parameter)
    {
        if (CurrentStudyWords.Count == 0)
        {
            return;
        }

        var rating = int.TryParse(parameter?.ToString(), out var parsedRating) ? parsedRating : 1;
        srsService.ApplyReview(CurrentWord, rating);
        TouchSelectedStudyDeck();
        SyncLessonProgress();
        OnProgressChanged();
        SaveAppState();
        NextWord();
    }

    private void SelectQuizAnswer(object? parameter)
    {
        if (IsQuizSubmitted)
        {
            return;
        }

        if (int.TryParse(parameter?.ToString(), out var selectedIndex))
        {
            CurrentQuizQuestion.SelectedIndex = selectedIndex;
            QuizSubmitMessage = "";
            IsIncompleteQuizSubmitDialogOpen = false;
            OnPropertyChanged(nameof(AnsweredQuizCount));
            OnPropertyChanged(nameof(IncompleteQuizSubmitText));
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

    private void PreviousQuiz()
    {
        if (CurrentQuizIndex <= 0)
        {
            return;
        }

        CurrentQuizIndex--;
    }

    private void NextQuiz()
    {
        if (CurrentQuizIndex < QuizQuestions.Count - 1)
        {
            CurrentQuizIndex++;
        }
    }

    private void SubmitQuiz()
    {
        var answeredCount = AnsweredQuizCount;
        if (answeredCount < QuizQuestions.Count)
        {
            OnPropertyChanged(nameof(IncompleteQuizSubmitText));
            IsIncompleteQuizSubmitDialogOpen = true;
            return;
        }

        CompleteQuizSubmission();
    }

    private void CompleteQuizSubmission()
    {
        StopQuizTimer();
        QuizCompletedTime = QuizElapsedTime;
        var completedAt = DateTime.Now;
        foreach (var question in QuizQuestions)
        {
            question.IsSubmitted = true;
            if (question.SourceWord is not null)
            {
                if (question.IsCorrect)
                {
                    question.SourceWord.CorrectQuizCount++;
                    if (isPracticeQuiz)
                    {
                        question.SourceWord.PracticeCorrectQuizCount++;
                    }
                }
                else
                {
                    question.SourceWord.IncorrectQuizCount++;
                    if (isPracticeQuiz)
                    {
                        question.SourceWord.PracticeIncorrectQuizCount++;
                    }
                }

                if (isPracticeQuiz)
                {
                    question.SourceWord.LastPracticeAt = completedAt;
                }
            }
        }

        if (isPracticeQuiz)
        {
            PracticeSessionCount++;
        }

        QuizSubmitMessage = "";
        IsIncompleteQuizSubmitDialogOpen = false;
        IsQuizInProgress = false;
        OnPropertyChanged(nameof(IsQuizSubmitted));
        OnPropertyChanged(nameof(QuizScorePercentage));
        OnPropertyChanged(nameof(QuizScoreText));
        OnPracticeStatsChanged();
        SaveAppState();
        CurrentView = "QuizResult";
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
        User.Note = "";
        User.AvatarPath = "";
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
        ProfileNote = User.Note;
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
        User.Note = ProfileNote.Trim();
        OnPropertyChanged(nameof(User));
        OnPropertyChanged(nameof(DashboardGreeting));
        OnPropertyChanged(nameof(DashboardSubtitle));
        ProfileMessage = "Đã lưu thông tin hồ sơ.";
        SaveAppState();
    }

    private void SaveSettings()
    {
        SaveAppState();
        SettingsMessage = "Đã lưu cài đặt.";
    }

    private void ImportAvatar()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh đại diện",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        User.AvatarPath = SaveAvatarForCurrentAccount(dialog.FileName);
        IsAvatarDialogOpen = false;
        SaveAppState();
    }

    private string SaveAvatarForCurrentAccount(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(User.Email))
        {
            return sourcePath;
        }

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var avatarDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WordUp",
            "Avatars");
        Directory.CreateDirectory(avatarDirectory);

        var avatarPath = Path.Combine(avatarDirectory, $"{CreateAccountFileName(User.Email)}{extension.ToLowerInvariant()}");
        if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(avatarPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, avatarPath, overwrite: true);
        }

        return avatarPath;
    }

    private static string CreateAccountFileName(string email)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(email.Trim().ToLowerInvariant().Length);
        foreach (var character in email.Trim().ToLowerInvariant())
        {
            builder.Append(invalidCharacters.Contains(character) || character is '@' or '.' ? '_' : character);
        }

        return builder.ToString();
    }

    private void ApplyTheme()
    {
        if (Application.Current is null)
        {
            return;
        }

        if (IsDarkMode)
        {
            SetBrush("AppBackgroundBrush", Color.FromRgb(24, 26, 36));
            SetBrush("SurfaceBrush", Color.FromRgb(35, 38, 52));
            SetBrush("MutedTextBrush", Color.FromRgb(185, 190, 205));
            SetBrush("PrimaryTextBrush", Color.FromRgb(242, 244, 250));
            SetBrush("BorderBrush", Color.FromRgb(67, 72, 94));
            SetBrush("IndigoDarkBrush", Color.FromRgb(204, 211, 255));
            SetBrush("HoverSurfaceBrush", Color.FromRgb(48, 52, 70));
        }
        else
        {
            SetBrush("AppBackgroundBrush", Color.FromRgb(247, 244, 252));
            SetBrush("SurfaceBrush", Colors.White);
            SetBrush("MutedTextBrush", Color.FromRgb(105, 112, 137));
            SetBrush("PrimaryTextBrush", Color.FromRgb(17, 19, 34));
            SetBrush("BorderBrush", Color.FromRgb(221, 217, 235));
            SetBrush("IndigoDarkBrush", Color.FromRgb(21, 32, 138));
            SetBrush("HoverSurfaceBrush", Color.FromRgb(240, 238, 250));
        }
    }

    private static void SetBrush(string resourceKey, Color color)
    {
        if (Application.Current.Resources[resourceKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        Application.Current.Resources[resourceKey] = new SolidColorBrush(color);
    }

    private void Logout()
    {
        IsLogoutDialogOpen = false;
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
        IsDeleteAccountDialogOpen = false;
        IsAuthenticated = false;
        pendingAuthenticatedView = "Dashboard";
        storageService.Delete(emailToDelete);
        ClearCurrentUser();
        AudioVolume = 75;
        DailyReminders = true;
        AutoPlayAudio = false;
        OfflineMode = false;
        IsDarkMode = false;
        CurrentWordIndex = 0;
        ClearWordEditor();
        LoadProfileEditor();
        IsAuthMessageSuccess = true;
        AuthMessage = "Tài khoản đã được xóa trên máy này.";
        SelectTab("Dashboard");
    }

    private void ResetQuiz(IEnumerable<VocabularyWord>? sourceWords = null)
    {
        if (!RequireAuthentication("Quiz"))
        {
            return;
        }

        RefreshQuizQuestions(sourceWords);

        foreach (var question in QuizQuestions)
        {
            question.SelectedIndex = -1;
            question.IsSubmitted = false;
        }

        QuizElapsedTime = TimeSpan.Zero;
        QuizCompletedTime = TimeSpan.Zero;
        QuizSubmitMessage = "";
        IsIncompleteQuizSubmitDialogOpen = false;
        IsQuizInProgress = true;
        CurrentQuizIndex = 0;
        StartQuizTimer();
        OnPropertyChanged(nameof(AnsweredQuizCount));
        OnPropertyChanged(nameof(IsQuizSubmitted));
        OnPropertyChanged(nameof(QuizScorePercentage));
        OnPropertyChanged(nameof(QuizScoreText));
        CurrentView = "Quiz";
    }

    private void StartQuizTimer()
    {
        if (!quizTimer.IsEnabled)
        {
            quizTimer.Start();
        }
    }

    private void StopQuizTimer()
    {
        if (quizTimer.IsEnabled)
        {
            quizTimer.Stop();
        }
    }

    private static string FormatQuizTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private double GetPracticeBucketPercentage(double minInclusive, double maxExclusive)
    {
        if (PracticeTotalWords == 0)
        {
            return 0;
        }

        return CountPracticeBucket(minInclusive, maxExclusive) / (double)PracticeTotalWords * 100;
    }

    private int CountPracticeBucket(double minInclusive, double maxExclusive)
    {
        return Words.Count(word =>
        {
            var attempts = GetPracticeAttemptCount(word);
            if (attempts == 0)
            {
                return false;
            }

            var rate = word.PracticeCorrectQuizCount / (double)attempts;
            return rate >= minInclusive && (rate < maxExclusive || (maxExclusive >= 1.0 && rate <= maxExclusive));
        });
    }

    private static int GetPracticeAttemptCount(VocabularyWord word)
    {
        return word.PracticeCorrectQuizCount + word.PracticeIncorrectQuizCount;
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
        EditorIpa = "";
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
                Ipa = EditorIpa.Trim(),
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
            SelectedWord.Ipa = EditorIpa.Trim();
            SelectedWord.Type = string.IsNullOrWhiteSpace(EditorType) ? "word" : EditorType.Trim();
            SelectedWord.Meaning = EditorMeaning.Trim();
            SelectedWord.VietnameseMeaning = string.IsNullOrWhiteSpace(EditorVietnameseMeaning) ? EditorMeaning.Trim() : EditorVietnameseMeaning.Trim();
            SelectedWord.Example = EditorExample.Trim();
            WordManagerMessage = $"Đã cập nhật {SelectedWord.Word}.";
        }

        SyncLessonProgress();
        OnPropertyChanged(nameof(FilteredWords));
        OnPropertyChanged(nameof(CurrentWord));
        OnProgressChanged();
        RefreshQuizQuestions();
        SaveAppState();
    }

    private void OpenDeleteWordDialog()
    {
        if (SelectedWord is null)
        {
            WordManagerMessage = "Chọn một từ trước khi xóa.";
            return;
        }

        pendingDeleteWord = SelectedWord;
        OnPropertyChanged(nameof(DeleteWordDialogText));
        IsDeleteWordDialogOpen = true;
    }

    private void CancelDeleteWord()
    {
        pendingDeleteWord = null;
        IsDeleteWordDialogOpen = false;
        OnPropertyChanged(nameof(DeleteWordDialogText));
    }

    private void ConfirmDeleteWord()
    {
        if (pendingDeleteWord is null)
        {
            CancelDeleteWord();
            return;
        }

        var deletedWord = pendingDeleteWord.Word;
        var deletedIndex = Words.IndexOf(pendingDeleteWord);
        Words.Remove(pendingDeleteWord);
        pendingDeleteWord = null;
        IsDeleteWordDialogOpen = false;
        CurrentWordIndex = Words.Count == 0 ? 0 : Math.Min(CurrentWordIndex, Words.Count - 1);
        ClearWordEditor();
        WordManagerMessage = $"Đã xóa {deletedWord}.";
        OnPropertyChanged(nameof(DeleteWordDialogText));

        if (deletedIndex >= 0)
        {
            OnPropertyChanged(nameof(FilteredWords));
            OnPropertyChanged(nameof(CurrentWord));
            OnPropertyChanged(nameof(StudyProgressText));
            OnPropertyChanged(nameof(StudyProgressValue));
        }

        OnProgressChanged();
        SyncLessonProgress();
        RefreshQuizQuestions();
        SaveAppState();
    }

    private void RefreshQuizQuestions(IEnumerable<VocabularyWord>? sourceWords = null)
    {
        QuizQuestions.Clear();
        foreach (var question in quizService.CreateQuestions(sourceWords ?? Words, QuizQuestionLimit))
        {
            QuizQuestions.Add(question);
        }

        CurrentQuizIndex = 0;
        OnPropertyChanged(nameof(CurrentQuizQuestion));
        OnPropertyChanged(nameof(QuizProgressText));
        OnPropertyChanged(nameof(QuizProgressValue));
        OnPropertyChanged(nameof(AnsweredQuizCount));
        OnPropertyChanged(nameof(IsQuizSubmitted));
        OnPropertyChanged(nameof(QuizScorePercentage));
        OnPropertyChanged(nameof(QuizScoreText));
    }

    private void RefreshTodayWord()
    {
        var learnedWords = Words
            .Where(IsLearnedOrRemembered)
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

    private void SpeakTodayWord()
    {
        SpeakWord(todayWord);
    }

    private void SpeakWord(VocabularyWord? word)
    {
        if (word is null || string.IsNullOrWhiteSpace(word.Word))
        {
            return;
        }

        SpeakText(word.Word);
    }

    private void AutoPlayCurrentWord()
    {
        if (AutoPlayAudio)
        {
            SpeakWord(CurrentWord);
        }
    }

    private void SpeakText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            speechVoice ??= Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice")!);
            speechVoice?.GetType().InvokeMember(
                "Volume",
                System.Reflection.BindingFlags.SetProperty,
                binder: null,
                target: speechVoice,
                args: [(int)Math.Round(Math.Clamp(AudioVolume, 0, 100))]);
            speechVoice?.GetType().InvokeMember(
                "Speak",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: speechVoice,
                args: [text, 1]);
        }
        catch
        {
            // Speech is optional; missing Windows voices should not break learning.
        }
    }

    private void ToggleTodayFavorite()
    {
        if (todayWord is null)
        {
            return;
        }

        todayWord.IsFavorite = !todayWord.IsFavorite;
        OnPropertyChanged(nameof(TodayFavoriteIcon));
        OnPropertyChanged(nameof(TodayFavoriteForeground));
        SaveAppState();
    }

    private void ToggleCurrentFavorite()
    {
        if (CurrentStudyWords.Count == 0)
        {
            return;
        }

        CurrentWord.IsFavorite = !CurrentWord.IsFavorite;
        OnPropertyChanged(nameof(StudyFavoriteIcon));
        OnPropertyChanged(nameof(StudyFavoriteForeground));
        if (ReferenceEquals(CurrentWord, todayWord))
        {
            OnPropertyChanged(nameof(TodayFavoriteIcon));
            OnPropertyChanged(nameof(TodayFavoriteForeground));
        }

        SaveAppState();
    }

    private Brush GetTodayChoiceBackground(int index)
    {
        if (todayWordSelectedIndex < 0)
        {
            return GetApplicationBrush("SurfaceBrush", Brushes.White);
        }

        if (index == todayWordCorrectIndex)
        {
            if (IsDarkMode)
            {
                return new SolidColorBrush(Color.FromRgb(24, 72, 48));
            }

            return new SolidColorBrush(Color.FromRgb(226, 247, 235));
        }

        if (index == todayWordSelectedIndex)
        {
            if (IsDarkMode)
            {
                return new SolidColorBrush(Color.FromRgb(86, 36, 44));
            }

            return new SolidColorBrush(Color.FromRgb(253, 229, 232));
        }

        return GetApplicationBrush("SurfaceBrush", Brushes.White);
    }

    private Brush GetTodayChoiceBorder(int index)
    {
        if (todayWordSelectedIndex < 0)
        {
            return GetApplicationBrush("IndigoBrush", new SolidColorBrush(Color.FromRgb(116, 89, 217)));
        }

        if (index == todayWordCorrectIndex)
        {
            if (IsDarkMode)
            {
                return new SolidColorBrush(Color.FromRgb(81, 210, 132));
            }

            return new SolidColorBrush(Color.FromRgb(37, 150, 90));
        }

        if (index == todayWordSelectedIndex)
        {
            if (IsDarkMode)
            {
                return new SolidColorBrush(Color.FromRgb(255, 129, 143));
            }

            return new SolidColorBrush(Color.FromRgb(200, 32, 47));
        }

        return GetApplicationBrush("BorderBrush", new SolidColorBrush(Color.FromRgb(222, 226, 235)));
    }

    private Brush GetTodayChoiceForeground(int index)
    {
        if (todayWordSelectedIndex < 0)
        {
            return GetApplicationBrush("IndigoBrush", new SolidColorBrush(Color.FromRgb(41, 57, 184)));
        }

        if (index == todayWordCorrectIndex)
        {
            if (IsDarkMode)
            {
                return new SolidColorBrush(Color.FromRgb(224, 255, 237));
            }

            return new SolidColorBrush(Color.FromRgb(20, 104, 62));
        }

        if (index == todayWordSelectedIndex)
        {
            if (IsDarkMode)
            {
                return new SolidColorBrush(Color.FromRgb(255, 235, 238));
            }

            return new SolidColorBrush(Color.FromRgb(160, 24, 38));
        }

        return GetApplicationBrush("PrimaryTextBrush", Brushes.Black);
    }

    private static Brush GetPracticeSourceBorder(bool isSelected)
    {
        return isSelected
            ? GetApplicationBrush("IndigoBrush", new SolidColorBrush(Color.FromRgb(41, 57, 184)))
            : GetApplicationBrush("BorderBrush", new SolidColorBrush(Color.FromRgb(222, 226, 235)));
    }

    private static Brush GetApplicationBrush(string key, Brush fallback)
    {
        return Application.Current?.Resources[key] as Brush ?? fallback;
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
        OnPropertyChanged(nameof(TodayFavoriteIcon));
        OnPropertyChanged(nameof(TodayFavoriteForeground));
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
        OnPropertyChanged(nameof(TodayChoiceAForeground));
        OnPropertyChanged(nameof(TodayChoiceBForeground));
        OnPropertyChanged(nameof(TodayChoiceCForeground));
        OnPropertyChanged(nameof(TodayChoiceDForeground));
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
        OnPracticeStatsChanged();
    }

    private void OnPracticeStatsChanged()
    {
        OnPropertyChanged(nameof(PracticeTotalWords));
        OnPropertyChanged(nameof(PracticeReviewedWords));
        OnPropertyChanged(nameof(PracticeTotalAttempts));
        OnPropertyChanged(nameof(PracticeCorrectAnswers));
        OnPropertyChanged(nameof(PracticeSessionCount));
        OnPropertyChanged(nameof(PracticeMasteryPercentage));
        OnPropertyChanged(nameof(PracticeMasteryLabel));
        OnPropertyChanged(nameof(PracticeMasteryText));
        OnPropertyChanged(nameof(PracticeCoverageText));
        OnPropertyChanged(nameof(PracticeStudiedTodayWords));
        OnPropertyChanged(nameof(PracticeTotalWordsLine));
        OnPropertyChanged(nameof(PracticeTotalAttemptsLine));
        OnPropertyChanged(nameof(PracticeStudiedTodayLine));
        OnPropertyChanged(nameof(PracticeMasteredWordsLine));
        OnPropertyChanged(nameof(PracticeLastReviewedLine));
        OnPropertyChanged(nameof(PracticeLastReviewedText));
        OnPropertyChanged(nameof(PracticeWeakWords));
        OnPropertyChanged(nameof(PracticeLearningWords));
        OnPropertyChanged(nameof(PracticeGoodWords));
        OnPropertyChanged(nameof(PracticeNewWords));
        OnPropertyChanged(nameof(PracticeNewCountText));
        OnPropertyChanged(nameof(PracticeWeakCountText));
        OnPropertyChanged(nameof(PracticeLearningCountText));
        OnPropertyChanged(nameof(PracticeGoodCountText));
        OnPropertyChanged(nameof(PracticeWeakBar));
        OnPropertyChanged(nameof(PracticeLearningBar));
        OnPropertyChanged(nameof(PracticeGoodBar));
        OnPropertyChanged(nameof(PracticeNewBar));
    }

    private void ApplyAccountState(AppState state)
    {
        User.FullName = state.User.FullName;
        User.Email = state.User.Email;
        User.Phone = state.User.Phone;
        User.Level = state.User.Level;
        User.Note = state.User.Note;
        User.AvatarPath = state.User.AvatarPath;

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
        IsDarkMode = state.Settings.IsDarkMode;
        PracticeSessionCount = state.Settings.PracticeSessionCount;
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
        User.Note = "";
        User.AvatarPath = "";
        ClearLearningData();
        LoadProfileEditor();
    }

    private void ClearLearningData()
    {
        Decks.Clear();
        Words.Clear();
        PracticeSessionCount = 0;
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
                OfflineMode = OfflineMode,
                IsDarkMode = IsDarkMode,
                PracticeSessionCount = PracticeSessionCount
            }
        });
    }
}
