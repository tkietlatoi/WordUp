using WordUp.Models;

namespace WordUp.Services;

public sealed class QuizService
{
    private static readonly string[] FallbackChoices =
    [
        "Một từ thường dùng trong tiếng Anh học thuật.",
        "Một cụm từ dùng trong giao tiếp hằng ngày.",
        "Một cấu trúc ngữ pháp dùng để nối ý.",
        "Một thành ngữ mang nghĩa bóng."
    ];

    public IReadOnlyList<QuizQuestion> CreateQuestions(IEnumerable<VocabularyWord> words, int questionLimit = 10)
    {
        var wordList = words
            .Where(word => !string.IsNullOrWhiteSpace(word.Word) && !string.IsNullOrWhiteSpace(word.Meaning))
            .ToList();

        Shuffle(wordList);
        wordList = wordList
            .Take(Math.Clamp(questionLimit, 10, 50))
            .ToList();

        if (wordList.Count == 0)
        {
            return
            [
                new()
                {
                    Term = "WordUp",
                    Prompt = "Thêm từ vựng trong màn Từ vựng để tạo câu hỏi cá nhân hóa.",
                    Choices = FallbackChoices,
                    CorrectIndex = 0
                }
            ];
        }

        return wordList
            .OrderBy(_ => Random.Shared.Next())
            .Select((word, index) => CreateQuestion(word, wordList, index))
            .ToList();
    }

    private static QuizQuestion CreateQuestion(VocabularyWord word, IReadOnlyList<VocabularyWord> allWords, int index)
    {
        var distractors = allWords
            .Where(candidate => !ReferenceEquals(candidate, word))
            .Select(candidate => candidate.Meaning)
            .Where(meaning => !string.IsNullOrWhiteSpace(meaning) && meaning != word.Meaning)
            .Distinct()
            .Take(3)
            .ToList();

        foreach (var fallback in FallbackChoices)
        {
            if (distractors.Count == 3)
            {
                break;
            }

            if (fallback != word.Meaning && !distractors.Contains(fallback))
            {
                distractors.Add(fallback);
            }
        }

        var choices = new List<string> { word.Meaning };
        choices.AddRange(distractors.Take(3));
        choices = choices.OrderBy(_ => Random.Shared.Next()).ToList();
        var correctIndex = choices.IndexOf(word.Meaning);

        return new QuizQuestion
        {
            Term = word.Word,
            Prompt = "Chọn định nghĩa chính xác nhất cho từ phía trên.",
            Choices = choices,
            CorrectIndex = correctIndex,
            SourceWord = word
        };
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
