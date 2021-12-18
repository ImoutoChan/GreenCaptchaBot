namespace CaptchaBot.Services;

public struct ChatUser
{
    public long ChatId { get; }

    public long UserId { get; }

    public ChatUser(long chatId, long userId)
    {
        ChatId = chatId;
        UserId = userId;
    }
}