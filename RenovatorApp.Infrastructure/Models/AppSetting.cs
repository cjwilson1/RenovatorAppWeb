namespace RenovatorApp.Infrastructure.Models;

public sealed class AppSetting : IRenoCompanyEntity
{
    public Guid AppSettingId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
