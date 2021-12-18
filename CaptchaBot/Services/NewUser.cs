using System;
using Telegram.Bot.Types;

namespace CaptchaBot.Services;

public class NewUser
{
    public long ChatId { get; }

    public long Id { get; }

    public DateTimeOffset JoinDateTime { get; }

    public int InviteMessageId { get; }
        
    public int JoinMessageId { get; }

    public string PrettyUserName { get; }

    public int CorrectAnswer { get; }
        
    public ChatMember ChatMember { get; }

    public NewUser(
        long chatId,
        long id,
        DateTimeOffset joinDateTime,
        int inviteMessageId,
        int joinMessageId,
        string prettyUserName,
        int correctAnswer,
        ChatMember chatMember)
    {
        ChatId = chatId;
        Id = id;
        JoinDateTime = joinDateTime;
        InviteMessageId = inviteMessageId;
        JoinMessageId = joinMessageId;
        PrettyUserName = prettyUserName;
        CorrectAnswer = correctAnswer;
        ChatMember = chatMember;
    }
}