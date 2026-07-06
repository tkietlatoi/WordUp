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

public sealed partial class MainViewModel
{
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
                OnThemeChanged();
                OnTodayWordChanged();
                if (!isApplyingAccountState)
                {
                    SaveAppState();
                }
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

    public string NewLessonAudioPath
    {
        get => newLessonAudioPath;
        set
        {
            if (SetProperty(ref newLessonAudioPath, value))
            {
                OnPropertyChanged(nameof(NewLessonAudioFileName));
            }
        }
    }

    public string NewLessonAudioFileName => string.IsNullOrWhiteSpace(NewLessonAudioPath)
        ? "Chưa chọn file âm thanh"
        : Path.GetFileName(NewLessonAudioPath);

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
            if (SetProperty(ref quizQuestionLimit, Math.Clamp(value, 10, 50)))
            {
                OnPropertyChanged(nameof(QuizQuestionLimitText));
                OnPropertyChanged(nameof(QuizQuestionLimitHintText));
            }
        }
    }

    public string QuizQuestionLimitText => $"{QuizQuestionLimit} từ";
    public string QuizQuestionLimitHintText => "Tối thiểu 10 từ.";

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
                OnPropertyChanged(nameof(IsLessonPracticeSource));
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
        "Lesson" => "Trong bài học",
        _ => "Những từ đã học"
    };

    public bool IsLessonPracticeSource
    {
        get => PracticeWordSource == "Lesson";
        set
        {
            if (value)
            {
                PracticeWordSource = "Lesson";
            }
        }
    }

    public string PracticeSelectedLessonId
    {
        get => practiceSelectedLessonId;
        set
        {
            if (SetProperty(ref practiceSelectedLessonId, value))
            {
                OnPropertyChanged(nameof(SelectedPracticeLesson));
                OnPropertyChanged(nameof(SelectedPracticeLessonName));
                OnPropertyChanged(nameof(PracticeWordSourceText));
            }
        }
    }

    public Deck? SelectedPracticeLesson => Decks.FirstOrDefault(deck => deck.Id == PracticeSelectedLessonId);
    public string SelectedPracticeLessonName => SelectedPracticeLesson?.Name ?? "Trong bài học";

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
    public string QuizHeaderTitleText => IsPracticeQuiz ? "Luyện tập" : "Bài kiểm tra";
    public string QuizResultTitleText => IsPracticeQuiz
        ? "Kết quả bài luyện tập"
        : "Kết quả bài học";
    public string QuizCompletionMessage => IsPracticeQuiz
        ? "Bạn đã hoàn thành bài luyện tập."
        : "Bạn đã hoàn thành bài kiểm tra.";
    public string QuizResultBackButtonText => IsPracticeQuiz
        ? "Quay lại Luyện tập"
        : "Quay lại bài học";
    public int AnsweredQuizCount => QuizQuestions.Count(question => question.IsAnswered);
    public bool IsQuizSubmitted => QuizQuestions.Count > 0 && QuizQuestions.All(question => question.IsSubmitted);
    public bool IsPracticeQuiz => isPracticeQuiz;
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

}
