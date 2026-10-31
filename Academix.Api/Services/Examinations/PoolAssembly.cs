using System.Security.Cryptography;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Examinations;

// Pure planning logic shared between readiness checks and actual publication.
// Explicit difficulty pools are allocated before wildcard pools, preventing wildcard
// allocation from exhausting questions needed by a more restrictive pool.
public static class PoolAssembly
{
    public sealed record Candidate(Guid QuestionId, int RevisionNumber, string Kind, string Difficulty);
    public sealed record Allocation(int PoolPosition, Guid QuestionId, int RevisionNumber);

    public static IReadOnlyList<ExamPoolView> Validate(ExamReplacePoolsRequest? request)
    {
        if (request is null) throw ApiFault.Bad("Pool selection is required.");
        if (request.Pools is null) throw ApiFault.Bad("Pools array is required (empty array clears pools).");
        if (request.Pools.Count > 20) throw ApiFault.Bad("An exam supports up to 20 pools.");
        var pools = new List<ExamPoolView>();
        for (var i = 0; i < request.Pools.Count; i++)
        {
            var pool = request.Pools[i];
            if (pool is null) throw ApiFault.Bad("Pool must not be null.");
            var kind = pool.Kind?.Trim() ?? "";
            var difficulty = pool.Difficulty?.Trim();
            if (string.IsNullOrEmpty(difficulty)) difficulty = null;
            if (kind is not ("SingleChoice" or "MultipleChoice" or "TrueFalse" or "ShortText" or "Essay"))
                throw ApiFault.Bad("Unsupported pool question kind.");
            if (difficulty is not (null or "Easy" or "Medium" or "Hard"))
                throw ApiFault.Bad("Difficulty must be Easy, Medium, Hard, or null.");
            if (pool.Quantity is < 1 or > 200) throw ApiFault.Bad("Pool quantity must be 1-200.");
            if (pool.Points <= 0 || pool.Points > 1000 || decimal.Round(pool.Points, 2) != pool.Points)
                throw ApiFault.Bad("Pool points must be 0.01-1000 with at most two decimals.");
            pools.Add(new ExamPoolView(i + 1, kind, difficulty, pool.Quantity, pool.Points));
        }
        if (pools.Sum(p => p.Quantity) > 200) throw ApiFault.Bad("At most 200 pooled questions are allowed.");
        if (pools.Sum(p => p.Quantity * p.Points) > 10000m) throw ApiFault.Bad("Total pooled points cannot exceed 10000.");
        return pools;
    }

    public static void ValidateCombined(int manualCount, decimal manualPoints, IReadOnlyList<ExamPoolView> pools)
    {
        if (manualCount + pools.Sum(p => p.Quantity) > 200)
            throw ApiFault.Bad("Exam cannot contain more than 200 questions.");
        if (manualPoints + pools.Sum(p => p.Quantity * p.Points) > 10000m)
            throw ApiFault.Bad("Exam total cannot exceed 10000 points.");
    }

    // The draw is deliberately not stored until Publish. This method never mutates the bank.
    // Manual questions are excluded, as are questions selected by earlier pools.
    public static IReadOnlyList<Allocation> Allocate(IReadOnlyList<ExamPoolView> pools,
        IReadOnlyList<Candidate> candidates, IEnumerable<Guid> excludedQuestionIds,
        Func<int, int>? randomIndex = null)
    {
        randomIndex ??= RandomNumberGenerator.GetInt32;
        var excluded = excludedQuestionIds.ToHashSet();
        var allocations = new List<Allocation>();
        foreach (var pool in pools.OrderBy(p => p.Difficulty is null ? 1 : 0).ThenBy(p => p.Position))
        {
            var eligible = candidates.Where(q => !excluded.Contains(q.QuestionId) &&
                    q.Kind == pool.Kind && (pool.Difficulty is null || q.Difficulty == pool.Difficulty))
                .GroupBy(q => q.QuestionId).Select(g => g.First()).ToList();
            if (eligible.Count < pool.Quantity)
                throw ApiFault.Conflict($"Pool {pool.Position} needs {pool.Quantity} questions but only {eligible.Count} are available.");
            // Partial Fisher-Yates shuffle, using cryptographic random indices.
            for (var i = 0; i < pool.Quantity; i++)
            {
                var offset = randomIndex(eligible.Count - i);
                if (offset < 0 || offset >= eligible.Count - i)
                    throw new ArgumentOutOfRangeException(nameof(randomIndex));
                var pick = i + offset;
                (eligible[i], eligible[pick]) = (eligible[pick], eligible[i]);
                var selected = eligible[i];
                excluded.Add(selected.QuestionId);
                allocations.Add(new Allocation(pool.Position, selected.QuestionId, selected.RevisionNumber));
            }
        }
        return allocations.OrderBy(x => x.PoolPosition).ToList();
    }
}
