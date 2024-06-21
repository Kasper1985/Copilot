using Microsoft.AspNetCore.SignalR;

namespace WebApi.Hubs;

/// <summary>
/// Represents a chat hub for real-time communication.
/// </summary>
/// <param name="logger"></param>
public class MessageRelayHub(ILogger<MessageRelayHub> logger) : Hub
{
    private const string ReceiveMessageClientCall = "ReceiveMessage";
    private const string ReceiveUserTypingStateClientCall = "ReceiveUserTypingState";
    
    /// <summary>
    /// Adds the user to the groups that they are a member of.
    /// Groups are identified by the chat ID.
    /// </summary>
    /// <param name="chatId">The chat ID used as group id for SignalR.</param>
    public async Task AddClientToGroup(string chatId) => await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
    
    /// <summary>
    /// Sends a message to all users except the sender.
    /// </summary>
    /// <param name="chatId">The chat ID used as group id for SignalR.</param>
    /// <param name="senderId">The user ID of the user that sent the message.</param>
    /// <param name="message">The message to send.</param>
    public async Task SendMessage(string chatId, string senderId, object message) => await Clients.OthersInGroup(chatId).SendAsync(ReceiveMessageClientCall, chatId, senderId, message);
    
    /// <summary>
    /// Sends the typing state to all users except the sender.
    /// </summary>
    /// <param name="chatId">The chat ID used as group id for SignalR.</param>
    /// <param name="userId">The user ID of the user who is typing.</param>
    /// <param name="isTyping">Whether the user is typing.</param>
    public async Task SendUserTypingState(string chatId, string userId, bool isTyping) => await Clients.OthersInGroup(chatId).SendAsync(ReceiveUserTypingStateClientCall, chatId, userId, isTyping);
}
