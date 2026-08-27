namespace StaySphere.Application.Common;

/// <summary>Small accumulator for building a <see cref="ValidationException"/> with several field errors.</summary>
internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = new List<string>();
            _errors[field] = messages;
        }

        messages.Add(message);
    }

    public void ThrowIfAny()
    {
        if (!HasErrors)
        {
            return;
        }

        throw new ValidationException(_errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()));
    }
}
