using WordUp.Models;

namespace WordUp.Services;

public sealed class SrsService
{
    private static readonly int[] ReviewIntervalsInDays = [0, 1, 3, 7, 14, 30];

    public void ApplyReview(VocabularyWord word, int rating)
    {
        var normalizedRating = Math.Clamp(rating, 0, 2);

        word.ReviewCount++;
        word.LastReviewedAt = DateTime.Today;

        word.MasteryLevel = normalizedRating switch
        {
            0 => Math.Max(0, word.MasteryLevel - 1),
            1 => Math.Min(5, word.MasteryLevel + 1),
            _ => Math.Min(5, word.MasteryLevel + 2)
        };

        word.NextReviewDate = DateTime.Today.AddDays(ReviewIntervalsInDays[word.MasteryLevel]);
    }
}
