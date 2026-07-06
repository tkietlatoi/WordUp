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
                VietnameseMeaning = string.IsNullOrWhiteSpace(EditorVietnameseMeaning) ? EditorMeaning.Trim() : EditorVietnameseMeaning.Trim()
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

    private void RefreshQuizQuestions(IEnumerable<VocabularyWord>? sourceWords = null, int? questionLimit = null)
    {
        QuizQuestions.Clear();
        var limit = Math.Clamp(questionLimit ?? QuizQuestionLimit, 10, 50);
        foreach (var question in quizService.CreateQuestions(sourceWords ?? Words, limit))
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

        if (TryPlayAudioFile(word.AudioPath))
        {
            return;
        }

        SpeakText(word.Word);
    }

    private bool TryPlayAudioFile(string audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
        {
            return false;
        }

        try
        {
            audioPlayer ??= new MediaPlayer();
            audioPlayer.Stop();
            audioPlayer.Open(new Uri(audioPath, UriKind.Absolute));
            audioPlayer.Volume = Math.Clamp(AudioVolume, 0, 100) / 100.0;
            audioPlayer.Play();
            return true;
        }
        catch
        {
            return false;
        }
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
        isApplyingAccountState = true;
        try
        {
            User.FullName = state.User.FullName;
            User.Email = state.User.Email;
            User.Phone = state.User.Phone;
            User.Note = state.User.Note;
            User.AvatarPath = state.User.AvatarPath;

            Decks.Clear();
            foreach (var deck in state.Decks)
            {
                Decks.Add(deck);
                AttachPracticeLessonTracking(deck);
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
            lastQuizSourceWords = [];
            lastQuizQuestionLimit = QuizQuestionLimit;
            RefreshQuizQuestions();
            RefreshTodayWord();
            EnsurePracticeLessonSelection();
            LoadProfileEditor();
            OnPropertyChanged(nameof(DashboardGreeting));
            OnPropertyChanged(nameof(DashboardSubtitle));
            OnPropertyChanged(nameof(FilteredDecks));
            OnPropertyChanged(nameof(FilteredWords));
            OnPropertyChanged(nameof(CurrentStudyWords));
            OnPropertyChanged(nameof(CurrentWord));
            OnProgressChanged();
        }
        finally
        {
            isApplyingAccountState = false;
        }
    }

    private void ClearCurrentUser()
    {
        User.FullName = "";
        User.Email = "";
        User.Phone = "";
        User.Note = "";
        User.AvatarPath = "";
        ClearLearningData();
        LoadProfileEditor();
        OnPropertyChanged(nameof(DashboardGreeting));
        OnPropertyChanged(nameof(DashboardSubtitle));
    }

    private void ClearLearningData()
    {
        foreach (var deck in Decks)
        {
            DetachPracticeLessonTracking(deck);
        }
        Decks.Clear();
        Words.Clear();
        PracticeSessionCount = 0;
        PracticeSelectedLessonId = "";
        lastQuizSourceWords = [];
        lastQuizQuestionLimit = QuizQuestionLimit;
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

    private void EnsurePracticeLessonSelection()
    {
        if (Decks.Count == 0)
        {
            PracticeSelectedLessonId = "";
            return;
        }

        if (Decks.Any(deck => deck.IsPracticeSelected))
        {
            return;
        }

        var firstDeck = Decks.First();
        firstDeck.IsPracticeSelected = true;
        PracticeSelectedLessonId = firstDeck.Id;
    }

    private void AttachPracticeLessonTracking(Deck deck)
    {
        deck.PropertyChanged += OnPracticeLessonDeckChanged;
    }

    private void DetachPracticeLessonTracking(Deck deck)
    {
        deck.PropertyChanged -= OnPracticeLessonDeckChanged;
    }

    private void OnPracticeLessonDeckChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Deck.IsPracticeSelected))
        {
            return;
        }

        PracticeWordSource = "Lesson";
        EnsurePracticeLessonSelection();
    }
}
