using WebApi.Storage.Interfaces;

namespace WebApi.Models.Storage;

/// <summary>
/// A chat participant is a user that is part of a chat.
/// A user can be part of multiple chats, thus a user can have multiple chat participants.
/// </summary>
public class ChatParticipant(string userId, string chatId) : IStorageEntity
{
    /// <summary>
    /// Participant ID that is persistent and unique.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID that is persistent and unique.
    /// </summary>
    public string UserId { get; set; } = userId;
    
    /// <summary>
    /// Chat ID that this participant belongs to.
    /// </summary>
    public string ChatId { get; set; } = chatId;
    
    /// <summary>
    /// The partition key for the source.
    /// </summary>
    public string Partition => UserId;
}
