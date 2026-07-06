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
        NewLessonAudioPath = "";
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
        NewLessonAudioPath = "";
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
        NewLessonAudioPath = word.AudioPath;
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

        DetachPracticeLessonTracking(lesson);
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
            editingLessonWord.AudioPath = NewLessonAudioPath.Trim();
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
            Type = string.IsNullOrWhiteSpace(NewLessonType) ? "word" : NewLessonType.Trim(),
            AudioPath = NewLessonAudioPath.Trim()
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

    private void ImportLessonWordAudio()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file âm thanh",
            Filter = "Audio files (*.mp3;*.wav;*.m4a;*.wma)|*.mp3;*.wav;*.m4a;*.wma|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            NewLessonAudioPath = dialog.FileName;
            NewLessonMessage = $"Đã chọn âm thanh: {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private void SaveNewLesson()
    {
        try
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
            var isEditingLesson = editingLesson is not null;
            var existingWords = isEditingLesson
                ? Words.Where(word => word.LessonId == lesson.Id).ToList()
                : [];

            lesson.Name = NewLessonName.Trim();
            lesson.TotalWords = NewLessonWords.Count;
            lesson.UpdatedAt = DateTime.Now;

            if (!isEditingLesson)
            {
                lesson.CreatedAt = lesson.UpdatedAt;
                Decks.Add(lesson);
                AttachPracticeLessonTracking(lesson);
            }
            else if (!HaveSameLessonWords(existingWords, NewLessonWords))
            {
                foreach (var oldWord in existingWords)
                {
                    Words.Remove(oldWord);
                }

                foreach (var word in NewLessonWords)
                {
                    var savedWord = CloneVocabularyWord(word);
                    savedWord.LessonId = lesson.Id;
                    Words.Add(savedWord);
                }
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
            NewLessonMessage = isEditingLesson ? "Đã cập nhật bài học." : "Đã lưu bài học.";
            ShowLessonList();
        }
        catch (Exception ex)
        {
            NewLessonMessage = $"Không thể lưu bài học: {ex.Message}";
        }
    }

    private void ClearLessonWordEditor()
    {
        editingLessonWord = null;
        NewLessonWord = "";
        NewLessonIpa = "";
        NewLessonMeaning = "";
        NewLessonType = "";
        NewLessonAudioPath = "";
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
            AudioPath = word.AudioPath,
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

    private static bool HaveSameLessonWords(
        IReadOnlyList<VocabularyWord> existingWords,
        IReadOnlyList<VocabularyWord> newWords)
    {
        if (existingWords.Count != newWords.Count)
        {
            return false;
        }

        for (var index = 0; index < existingWords.Count; index++)
        {
            if (!AreSameLessonWord(existingWords[index], newWords[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSameLessonWord(VocabularyWord left, VocabularyWord right)
    {
        return string.Equals(left.Word, right.Word, StringComparison.Ordinal)
            && string.Equals(left.Ipa, right.Ipa, StringComparison.Ordinal)
            && string.Equals(left.Type, right.Type, StringComparison.Ordinal)
            && string.Equals(left.Meaning, right.Meaning, StringComparison.Ordinal)
            && string.Equals(left.VietnameseMeaning, right.VietnameseMeaning, StringComparison.Ordinal)
            && string.Equals(left.AudioPath, right.AudioPath, StringComparison.Ordinal)
            && left.MasteryLevel == right.MasteryLevel
            && left.ReviewCount == right.ReviewCount
            && left.CorrectQuizCount == right.CorrectQuizCount
            && left.IncorrectQuizCount == right.IncorrectQuizCount
            && left.PracticeCorrectQuizCount == right.PracticeCorrectQuizCount
            && left.PracticeIncorrectQuizCount == right.PracticeIncorrectQuizCount
            && left.IsFavorite == right.IsFavorite
            && Nullable.Equals(left.LastReviewedAt, right.LastReviewedAt)
            && Nullable.Equals(left.LastPracticeAt, right.LastPracticeAt)
            && left.NextReviewDate == right.NextReviewDate;
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

        return new VocabularyWord
        {
            Word = parts[0],
            Ipa = hasIpa ? parts[1] : "",
            Meaning = parts.Length > meaningIndex ? parts[meaningIndex] : parts[1],
            VietnameseMeaning = parts.Length > meaningIndex ? parts[meaningIndex] : parts[1],
            Type = parts.Length > typeIndex ? parts[typeIndex] : "word"
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

}
