namespace ClientFlow.Domain.Settings;

public class Setting
{
    public Guid Id { get; set; }         // <-- MUST be Guid
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}