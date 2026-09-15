using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IRandomProvider"/>: encapsulates a
/// non-deterministic external dependency (same rationale as the repositories living in
/// this project).
/// </summary>
public class SystemRandomProvider : IRandomProvider
{
    public int NextInt(int minInclusive, int maxInclusive)
    {
        return Random.Shared.Next(minInclusive, maxInclusive + 1);
    }
}
