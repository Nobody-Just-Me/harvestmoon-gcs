namespace HarvestmoonGCS.Core.Models.AI;

/// <summary>
/// Urgency level for chat messages, matching PIGEON React UI styling
/// </summary>
public enum ChatUrgency
{
    Normal,
    Warning,
    Critical,
    Success
}

/// <summary>
/// Message role in a chat conversation
/// </summary>
public enum ChatRole
{
    User,
    Assistant
}

/// <summary>
/// Chat message with full metadata for PIA chat panel
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = $"msg-{DateTime.UtcNow.Ticks}";
    public ChatRole Role { get; set; } = ChatRole.User;
    public string Content { get; set; } = string.Empty;
    public ChatUrgency Urgency { get; set; } = ChatUrgency.Normal;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? PendingCommand { get; set; }
    public bool RequireConfirmation { get; set; }
    public bool? Confirmed { get; set; } // null=pending, true=confirmed, false=cancelled
}

/// <summary>
/// Quick command for the chat panel shortcuts
/// </summary>
public class QuickCommand
{
    public string Label { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}
