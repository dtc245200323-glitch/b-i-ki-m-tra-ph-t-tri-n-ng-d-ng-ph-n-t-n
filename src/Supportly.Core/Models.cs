namespace Supportly.Core;
public record User(string Id, string Name, string Email, string Role, string CreatedAt, bool Enabled);
public record Conversation(string Id, string CustomerId, string CustomerName, string Subject, string Status, string? AgentId, string? AgentName, string CreatedAt, string? ClosedAt);
public record ChatMessage(string Id, string ConversationId, string SenderId, string SenderName, string Text, string SentAt);
public record ChatEvent(string EventId, string EventType, int Version, string ConversationId, string Timestamp, ChatMessage? Message);
public class DomainException(string message, int status = 400) : Exception(message) { public int Status { get; } = status; }
