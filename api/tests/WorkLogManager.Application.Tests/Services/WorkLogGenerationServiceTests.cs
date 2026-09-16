using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.Tests.Services;

/// <summary>
/// Test double for <see cref="IRandomProvider"/> that returns a fixed, pre-queued sequence
/// of values regardless of the requested range - lets tests deterministically control every
/// offset sorted by <see cref="WorkLogGenerationService"/> and assert the exact formula
/// documented in the approved plan.
/// </summary>
public class QueuedRandomProvider : IRandomProvider
{
    private readonly Queue<int> _values;

    public QueuedRandomProvider(params int[] values)
    {
        _values = new Queue<int>(values);
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (_values.Count == 0)
        {
            throw new InvalidOperationException("No more queued random values - the service called NextInt more times than expected.");
        }

        return _values.Dequeue();
    }
}

public class WorkLogGenerationServiceTests
{
    private static readonly DateOnly Date = new(2026, 1, 5);

    [Fact]
    public void GenerateWorkday_JourneyThreeHours_ReturnsSinglePeriodWithoutBreak()
    {
        // startOffset = 2, endOffset = NextInt(2, 4) = 4.
        var randomProvider = new QueuedRandomProvider(2, 4);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 3m);

        Assert.Single(workday.RegularAttendancePeriods);
        Assert.Null(workday.BreakPeriod);

        var period = workday.RegularAttendancePeriods[0];
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 7, 2, 0, TimeSpan.FromHours(-3)), period.Start);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 10, 4, 0, TimeSpan.FromHours(-3)), period.End);

        var workedMinutes = (period.End - period.Start).TotalMinutes;
        Assert.True(workedMinutes >= 3d * 60);
    }

    [Fact]
    public void GenerateWorkday_JourneyExactlyFourHours_FallsIntoShortJourneyBranchWithoutBreak()
    {
        var randomProvider = new QueuedRandomProvider(-1, 3);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 4m);

        Assert.Single(workday.RegularAttendancePeriods);
        Assert.Null(workday.BreakPeriod);

        var period = workday.RegularAttendancePeriods[0];
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 6, 59, 0, TimeSpan.FromHours(-3)), period.Start);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 11, 3, 0, TimeSpan.FromHours(-3)), period.End);
    }

    [Fact]
    public void GenerateWorkday_JourneySixHours_NeutralBreakOffsets_MatchesLegacyBehavior()
    {
        // startOffset = 0, breakStartOffset = 0, breakEndOffset = 0.
        // requiredEndOffset = 0 - 0 + 0 = 0; endOffsetUpperBound = 4; endOffset = NextInt(0, 4) = 2.
        var randomProvider = new QueuedRandomProvider(0, 0, 0, 2);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 6m);

        Assert.Equal(2, workday.RegularAttendancePeriods.Count);
        Assert.NotNull(workday.BreakPeriod);

        var morning = workday.RegularAttendancePeriods[0];
        var afternoon = workday.RegularAttendancePeriods[1];
        var breakPeriod = workday.BreakPeriod!.Value;

        Assert.Equal(new DateTimeOffset(2026, 1, 5, 7, 0, 0, TimeSpan.FromHours(-3)), morning.Start);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 11, 0, 0, TimeSpan.FromHours(-3)), morning.End);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 11, 0, 0, TimeSpan.FromHours(-3)), breakPeriod.Start);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.FromHours(-3)), breakPeriod.End);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.FromHours(-3)), afternoon.Start);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 14, 2, 0, TimeSpan.FromHours(-3)), afternoon.End);

        var workedMinutes = (morning.End - morning.Start).TotalMinutes + (afternoon.End - afternoon.Start).TotalMinutes;
        Assert.Equal(6d * 60 + 2, workedMinutes);
    }

    [Fact]
    public void GenerateWorkday_JourneySixHours_BreakVariesEarlier_NeverFallsBelowEffectiveHours()
    {
        // startOffset = 0, breakStartOffset = -4, breakEndOffset = -4.
        // requiredEndOffset = 0 - (-4) + (-4) = 0; endOffsetUpperBound = 4.
        foreach (var endOffset in new[] { 0, 4 })
        {
            var randomProvider = new QueuedRandomProvider(0, -4, -4, endOffset);
            var service = new WorkLogGenerationService(randomProvider);

            var workday = service.GenerateWorkday(Date, 6m);

            var morning = workday.RegularAttendancePeriods[0];
            var afternoon = workday.RegularAttendancePeriods[1];

            var workedMinutes = (morning.End - morning.Start).TotalMinutes + (afternoon.End - afternoon.Start).TotalMinutes;
            Assert.True(workedMinutes >= 6d * 60, $"Worked minutes {workedMinutes} fell below 360 for endOffset={endOffset}.");
        }
    }

    [Fact]
    public void GenerateWorkday_JourneySixHours_BreakVariesLater_NeverFallsBelowEffectiveHours()
    {
        // startOffset = 0, breakStartOffset = 4, breakEndOffset = 4.
        // requiredEndOffset = 0 - 4 + 4 = 0; endOffsetUpperBound = 4.
        foreach (var endOffset in new[] { 0, 4 })
        {
            var randomProvider = new QueuedRandomProvider(0, 4, 4, endOffset);
            var service = new WorkLogGenerationService(randomProvider);

            var workday = service.GenerateWorkday(Date, 6m);

            var morning = workday.RegularAttendancePeriods[0];
            var afternoon = workday.RegularAttendancePeriods[1];

            var workedMinutes = (morning.End - morning.Start).TotalMinutes + (afternoon.End - afternoon.Start).TotalMinutes;
            Assert.True(workedMinutes >= 6d * 60, $"Worked minutes {workedMinutes} fell below 360 for endOffset={endOffset}.");
        }
    }

    [Fact]
    public void GenerateWorkday_JourneySixHours_WorstCaseCombination_ExtrapolatesNominalCapButPreservesExactEffectiveHours()
    {
        // startOffset = 4, breakStartOffset = -4, breakEndOffset = 4.
        // requiredEndOffset = 4 - (-4) + 4 = 12 (above the nominal 4-minute cap).
        // endOffsetUpperBound = max(12, 4) = 12; endOffset deterministically forced to 12
        // (zero-width sampling range).
        var randomProvider = new QueuedRandomProvider(4, -4, 4, 12);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 6m);

        var morning = workday.RegularAttendancePeriods[0];
        var afternoon = workday.RegularAttendancePeriods[1];

        var workedMinutes = (morning.End - morning.Start).TotalMinutes + (afternoon.End - afternoon.Start).TotalMinutes;
        Assert.Equal(6d * 60, workedMinutes);
    }

    [Theory]
    [InlineData(-4, -4, -4, -4)]
    [InlineData(-4, -4, -4, 4)]
    [InlineData(4, -4, 4, 0)]
    [InlineData(4, 4, 4, -4)]
    [InlineData(0, 0, 4, -4)]
    [InlineData(-2, 3, 4, 1)]
    public void GenerateWorkday_JourneyEightHours_VariedOffsets_NeverFallsBelowEffectiveHours(
        int startOffset, int breakStartOffset, int breakEndOffsetRaw, int endOffsetRaw)
    {
        // breakEndOffsetRaw/endOffsetRaw are only meaningful if they respect the
        // constructive floors used by the service (breakEndOffset >= breakStartOffset,
        // endOffset >= requiredEndOffset) - clamp them here the same way NextInt's
        // arguments would, so this queued double stays a faithful stand-in.
        var breakEndOffset = Math.Max(breakEndOffsetRaw, breakStartOffset);
        var requiredEndOffset = startOffset - breakStartOffset + breakEndOffset;
        var endOffset = Math.Max(endOffsetRaw, requiredEndOffset);

        var randomProvider = new QueuedRandomProvider(startOffset, breakStartOffset, breakEndOffset, endOffset);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 8m);

        var morning = workday.RegularAttendancePeriods[0];
        var afternoon = workday.RegularAttendancePeriods[1];

        var workedMinutes = (morning.End - morning.Start).TotalMinutes + (afternoon.End - afternoon.Start).TotalMinutes;
        Assert.True(workedMinutes >= 8d * 60);
    }

    [Fact]
    public void GenerateWorkday_JourneySevenAndAHalfHours_NonNeutralBreakOffsets_MatchesFormula()
    {
        var randomProvider = new QueuedRandomProvider(-3, 2, 3, 4);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 7.5m);

        Assert.NotNull(workday.BreakPeriod);
        var morning = workday.RegularAttendancePeriods[0];
        var afternoon = workday.RegularAttendancePeriods[1];

        var requiredEndOffset = -3 - 2 + 3;
        var endOffset = 4;
        var expectedWorkedMinutes = 7.5m * 60 + (endOffset - requiredEndOffset);

        var workedMinutes = (morning.End - morning.Start).TotalMinutes + (afternoon.End - afternoon.Start).TotalMinutes;
        Assert.Equal((double)expectedWorkedMinutes, workedMinutes);
    }

    [Theory]
    [InlineData(-4, -4)]
    [InlineData(-4, 4)]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(-2, 3)]
    public void GenerateWorkday_BreakDuration_NeverBelowSixtyMinutes(int breakStartOffset, int breakEndOffsetRaw)
    {
        var breakEndOffset = Math.Max(breakEndOffsetRaw, breakStartOffset);
        var randomProvider = new QueuedRandomProvider(0, breakStartOffset, breakEndOffset, 0);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 6m);

        var breakPeriod = workday.BreakPeriod!.Value;
        var breakMinutes = (breakPeriod.End - breakPeriod.Start).TotalMinutes;
        Assert.True(breakMinutes >= 60);
    }

    [Fact]
    public void GenerateWorkday_ShortJourney_NeverCallsRandomProviderForBreakOffsets()
    {
        var randomProvider = new QueuedRandomProvider(1, 2);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 2m);

        Assert.Null(workday.BreakPeriod);
    }

    [Fact]
    public void GenerateWorkday_BaseStart_IsSevenAmBusinessLocalTime()
    {
        var randomProvider = new QueuedRandomProvider(0, 0, 0, 0);
        var service = new WorkLogGenerationService(randomProvider);

        var workday = service.GenerateWorkday(Date, 6m);

        var period = workday.RegularAttendancePeriods[0];
        Assert.Equal(TimeSpan.Zero, period.Start.Offset);
        Assert.Equal(7, TimeZoneInfo.ConvertTime(period.Start, BusinessTimeZone.Value).Hour);
    }
}
