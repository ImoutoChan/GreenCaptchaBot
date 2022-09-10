using Telegram.Bot.Types;

namespace CaptchaBot.Services;

public record NewUser(long ChatId,
    long Id,
    DateTimeOffset JoinDateTime,
    int InviteMessageId,
    int JoinMessageId,
    string PrettyUserName,
    int CorrectAnswer,
    ChatMember ChatMember);