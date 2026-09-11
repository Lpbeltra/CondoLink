using CondoLink.Api.Features.CondominiumAssistant;

namespace CondoLink.Tests;

public sealed class BroadFallbackPlanTests
{
    [Fact]
    public void Same_candidate_set_skips_duplicate_rerank_and_reuses_primary()
    {
        var primary = Chunks(3);
        var plan = BroadFallbackPlan.Create(true, primary, primary.Reverse().ToArray());
        Assert.True(plan.Considered); Assert.False(plan.Execute);
        Assert.True(plan.SkippedNoNewCandidates); Assert.Equal(0, plan.NewCandidateCount);
    }

    [Fact]
    public void New_broad_candidate_keeps_full_broad_rerank()
    {
        var primary = Chunks(2); var broad = primary.Append(Chunk()).ToArray();
        var plan = BroadFallbackPlan.Create(true, primary, broad);
        Assert.True(plan.Execute); Assert.False(plan.SkippedNoNewCandidates);
        Assert.Equal(1, plan.NewCandidateCount);
    }

    [Fact]
    public void No_second_pass_does_not_execute()
    {
        var plan = BroadFallbackPlan.Create(false, Chunks(2), Chunks(3));
        Assert.False(plan.Considered); Assert.False(plan.Execute); Assert.Equal(0, plan.NewCandidateCount);
    }

    private static RankedChunk[] Chunks(int count) => Enumerable.Range(0, count).Select(_ => Chunk()).ToArray();
    private static RankedChunk Chunk() => new(Guid.NewGuid(), Guid.NewGuid(), "d", 1, null, "x", .2, .1, .3);
}
