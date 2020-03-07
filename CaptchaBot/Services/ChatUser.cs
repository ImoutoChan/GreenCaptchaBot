namespace CaptchaBot.Services
{
    public struct ChatUser
    {
        public long ChatId { get; }

        public int UserId { get; }

        public ChatUser(long chatId, int userId)
        {
            ChatId = chatId;
            UserId = userId;
        }
    }
}