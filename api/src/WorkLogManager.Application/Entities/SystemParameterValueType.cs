namespace WorkLogManager.Application.Entities;

/// <summary>
/// The logical shape of a <see cref="SystemParameter"/> value, used by the front-end to
/// decide how to render/parse the opaque <see cref="SystemParameter.Value"/> string.
/// Always derived server-side from <see cref="Services.SystemParameterCatalog"/>, never sent
/// by the client.
/// </summary>
public enum SystemParameterValueType
{
    String,
    Int,
    Bool,
    Array
}
