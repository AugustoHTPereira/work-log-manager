namespace WorkLogManager.Application.Common;

/// <summary>
/// Thrown when a domain invariant is violated (e.g. invalid entity state, invalid input).
/// Translated by the Api layer into an HTTP 400 response.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
