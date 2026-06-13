namespace RenovatorApp.Infrastructure.Models;

public sealed class UserInvitation
{
    public Guid UserInvitationId { get; set; } = Guid.NewGuid();
    public Guid UserID { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string SentToEmail { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? CreatedByUserID { get; set; }
    public RenoUser User { get; set; } = null!;
}
