namespace WorkLogManager.Application.Interfaces;

/// <summary>
/// Port isolating the source of randomness so it can be mocked/controlled in tests.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a pseudo-random integer within [<paramref name="minInclusive"/>, <paramref name="maxInclusive"/>].
    /// </summary>
    int NextInt(int minInclusive, int maxInclusive);
}
