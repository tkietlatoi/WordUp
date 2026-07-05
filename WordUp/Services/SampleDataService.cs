using WordUp.Models;

namespace WordUp.Services;

public sealed class SampleDataService
{
    public UserProfile User { get; } = new()
    {
        FullName = "Nguyễn Văn A",
        Email = "",
        Phone = "+84 987654321",
        Note = "Mục tiêu hôm nay: ôn lại từ vựng và hoàn thành bài học gần nhất."
    };

    public IReadOnlyList<Deck> Decks { get; } =
    [
        new() { Name = "IELTS Essentials", LearnedWords = 45, TotalWords = 100 },
        new() { Name = "Daily English", LearnedWords = 200, TotalWords = 500 },
        new() { Name = "TOEFL Prep", LearnedWords = 10, TotalWords = 300 }
    ];

    public IReadOnlyList<VocabularyWord> Words { get; } =
    [
        new()
        {
            Word = "Abysmal",
            Ipa = "/uh-BIZ-muhl/",
            Type = "adj.",
            Meaning = "Very bad; appalling.",
            VietnameseMeaning = "Rất tệ; thảm hại.",
            MasteryLevel = 1,
            ReviewCount = 2,
            LastReviewedAt = DateTime.Today.AddDays(-1),
            NextReviewDate = DateTime.Today
        },
        new()
        {
            Word = "Benevolent",
            Ipa = "/buh-NEV-uh-luhnt/",
            Type = "adj.",
            Meaning = "Well meaning and kindly.",
            VietnameseMeaning = "Nhân hậu; có lòng tốt.",
            MasteryLevel = 3,
            ReviewCount = 5,
            LastReviewedAt = DateTime.Today,
            NextReviewDate = DateTime.Today.AddDays(7)
        },
        new()
        {
            Word = "Cacophony",
            Ipa = "/kuh-KOF-uh-nee/",
            Type = "noun",
            Meaning = "A harsh, discordant mixture of sounds.",
            VietnameseMeaning = "Âm thanh hỗn loạn, chói tai.",
            MasteryLevel = 5,
            ReviewCount = 9,
            LastReviewedAt = DateTime.Today.AddDays(-10),
            NextReviewDate = DateTime.Today.AddDays(20)
        },
        new()
        {
            Word = "Ubiquitous",
            Ipa = "/yoo-BIK-wuh-tuhs/",
            Type = "adj.",
            Meaning = "Present everywhere.",
            VietnameseMeaning = "Có mặt ở khắp nơi.",
            MasteryLevel = 2,
            ReviewCount = 3,
            LastReviewedAt = DateTime.Today.AddDays(-3),
            NextReviewDate = DateTime.Today
        }
    ];

    public IReadOnlyList<QuizQuestion> QuizQuestions { get; } =
    [
        new()
        {
            Term = "Cacophony",
            Prompt = "Chọn định nghĩa chính xác nhất cho từ phía trên.",
            Choices =
            [
                "A harsh, discordant mixture of sounds.",
                "A pleasant, melodious tune that creates harmony.",
                "The study of sound waves and acoustics.",
                "A type of musical instrument used in ancient Greece."
            ],
            CorrectIndex = 0
        },
        new()
        {
            Term = "Benevolent",
            Prompt = "Chọn định nghĩa chính xác nhất cho từ phía trên.",
            Choices =
            [
                "Kind and well meaning.",
                "Having a harmful or hostile intent.",
                "A classical musical instrument.",
                "A noisy and chaotic state."
            ],
            CorrectIndex = 0
        }
    ];

    public IReadOnlyList<Achievement> Achievements { get; } =
    [
        new() { Name = "Lính mới", IsUnlocked = false },
        new() { Name = "Chăm chỉ", IsUnlocked = true },
        new() { Name = "Học giả", IsUnlocked = false }
    ];
}
