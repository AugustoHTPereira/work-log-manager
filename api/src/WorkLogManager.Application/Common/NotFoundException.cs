namespace WorkLogManager.Application.Common;

/// <summary>
/// Thrown when a requested entity does not exist.
/// Translated by the Api layer into an HTTP 404 response.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
