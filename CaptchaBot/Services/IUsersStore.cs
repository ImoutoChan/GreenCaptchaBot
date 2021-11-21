using System.Collections.Generic;
using Telegram.Bot.Types;

namespace CaptchaBot.Services
{
    public interface IUsersStore
    {
        void Add(
            User user,
            Message message,
            int sentMessageId,
            string prettyUserName,
            int answer,
            ChatMember chatMember);

        IReadOnlyCollection<NewUser> GetAll();

        NewUser Get(long chatId, long userId);
        
        void Remove(NewUser user);
    }
}