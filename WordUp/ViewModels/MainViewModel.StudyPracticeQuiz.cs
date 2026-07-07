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
        OnPropertyChanged(nameof(IsPracticeQuiz));
        OnPropertyChanged(nameof(QuizHeaderTitleText));
        OnPropertyChanged(nameof(QuizCompletionMessage));
        ResetQuiz(lessonQuizWords, Math.Min(50, lessonQuizWords.Count));
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
        OnPropertyChanged(nameof(IsPracticeQuiz));
        OnPropertyChanged(nameof(QuizHeaderTitleText));
        OnPropertyChanged(nameof(QuizCompletionMessage));
        ResetQuiz(GetPracticeWords().ToList(), QuizQuestionLimit);
    }

    private void SelectPracticeTab(string tab)
    {
        PracticeTab = tab == "Stats" ? "Stats" : "Practice";
    }

    private void SelectPracticeWordSource(string source)
    {
        PracticeWordSource = source is "Favorite" or "All" or "Lesson" ? source : "Learned";
    }

    private void SelectPracticeLesson(string lessonId)
    {
        var lesson = Decks.FirstOrDefault(deck => deck.Id == lessonId);
        if (lesson is null)
        {
            return;
        }

        lesson.IsPracticeSelected = !lesson.IsPracticeSelected;
        PracticeWordSource = "Lesson";
        EnsurePracticeLessonSelection();
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
            "Lesson" => GetSelectedPracticeLessonWords(),
            _ => Words.Where(IsLearnedOrRemembered)
        };

        var list = selectedWords.ToList();
        return list.Count > 0 ? list : Words;
    }

    private IEnumerable<VocabularyWord> GetSelectedPracticeLessonWords()
    {
        var selectedDecks = Decks.Where(deck => deck.IsPracticeSelected).ToList();
        if (selectedDecks.Count == 0)
        {
            yield break;
        }

        var buckets = selectedDecks
            .Select(deck => Words
                .Where(word => word.LessonId == deck.Id)
                .Where(word => !string.IsNullOrWhiteSpace(word.Word) && !string.IsNullOrWhiteSpace(word.Meaning))
                .OrderBy(_ => Random.Shared.Next())
                .ToList())
            .Where(bucket => bucket.Count > 0)
            .ToList();

        var index = 0;
        while (buckets.Any(bucket => index < bucket.Count))
        {
            foreach (var bucket in buckets)
            {
                if (index < bucket.Count)
                {
                    yield return bucket[index];
                }
            }

            index++;
        }
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
            storageService.SavePracticeSession(User.Email, QuizCompletedTime, PracticeWordSource, QuizQuestions.ToList());
        }

        QuizSubmitMessage = "";
        IsIncompleteQuizSubmitDialogOpen = false;
        IsQuizInProgress = false;
        OnPropertyChanged(nameof(IsQuizSubmitted));
        OnPropertyChanged(nameof(QuizScorePercentage));
        OnPropertyChanged(nameof(QuizScoreText));
        OnPropertyChanged(nameof(QuizCompletionMessage));
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
            Phone = RegisterPhone.Trim()
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

        try
        {
            User.AvatarPath = SaveAvatarForCurrentAccount(dialog.FileName);
            IsAvatarDialogOpen = false;
            SaveAppState();
        }
        catch (Exception ex)
        {
            IsAvatarDialogOpen = false;
            ProfileMessage = $"Không thể nhập ảnh đại diện: {ex.Message}";
        }
    }

    private void RemoveAvatar()
    {
        if (string.IsNullOrWhiteSpace(User.AvatarPath))
        {
            IsAvatarDialogOpen = false;
            return;
        }

        User.AvatarPath = "";
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

        var avatarPath = Path.Combine(
            avatarDirectory,
            $"{CreateAccountFileName(User.Email)}_{DateTime.Now:yyyyMMddHHmmssfff}{extension.ToLowerInvariant()}");
        try
        {
            if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(avatarPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, avatarPath, overwrite: true);
            }

            return avatarPath;
        }
        catch
        {
            return sourcePath;
        }
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
            SetBrush("IndigoBrush", Color.FromRgb(115, 127, 255));
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
            SetBrush("IndigoBrush", Color.FromRgb(95, 116, 220));
            SetBrush("HoverSurfaceBrush", Color.FromRgb(240, 238, 250));
        }
    }

    private void OnThemeChanged()
    {
        OnPropertyChanged(nameof(StudyFavoriteForeground));
        OnPropertyChanged(nameof(TodayFavoriteForeground));
        OnPropertyChanged(nameof(LearnedPracticeSourceBorder));
        OnPropertyChanged(nameof(FavoritePracticeSourceBorder));
        OnPropertyChanged(nameof(AllPracticeSourceBorder));
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
        StopStudyAuto();
        StopQuizTimer();
        IsLogoutDialogOpen = false;
        IsDeleteAccountDialogOpen = false;
        IsAvatarDialogOpen = false;
        IsIncompleteQuizSubmitDialogOpen = false;
        IsQuizSettingsOpen = false;
        IsLessonCompleteDialogOpen = false;
        IsStudyFlashcardOpen = false;
        IsAddLessonOpen = false;
        IsFlashcardBackVisible = false;
        IsDeleteWordDialogOpen = false;
        pendingDeleteWord = null;
        IsDeleteLessonWordDialogOpen = false;
        pendingDeleteLesson = null;
        pendingDeleteLessonWord = null;
        OnPropertyChanged(nameof(IsDeleteLessonDialogOpen));
        OnPropertyChanged(nameof(DeleteLessonDialogTitle));
        OnPropertyChanged(nameof(DeleteLessonDialogMessage));
        OnPropertyChanged(nameof(DeleteWordDialogText));
        OnPropertyChanged(nameof(DeleteLessonWordDialogText));
        IsAuthenticated = false;
        pendingAuthenticatedView = "Dashboard";
        LoginPassword = "";
        AuthMessage = "";
        IsAuthMessageSuccess = false;
        ClearCurrentUser();
        SelectedTab = "Dashboard";
        CurrentView = "Dashboard";
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

    private void ResetQuiz(IEnumerable<VocabularyWord>? sourceWords = null, int? questionLimit = null)
    {
        if (!RequireAuthentication("Quiz"))
        {
            return;
        }

        var quizSourceWords = (sourceWords ?? lastQuizSourceWords).ToList();
        if (quizSourceWords.Count == 0)
        {
            quizSourceWords = Words.ToList();
        }

        var effectiveQuestionLimit = Math.Clamp(questionLimit ?? lastQuizQuestionLimit, 10, 50);
        lastQuizSourceWords = quizSourceWords;
        lastQuizQuestionLimit = effectiveQuestionLimit;

        RefreshQuizQuestions(quizSourceWords, effectiveQuestionLimit);

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

}
