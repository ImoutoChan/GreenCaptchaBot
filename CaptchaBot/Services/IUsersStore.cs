using System.Collections.Generic;
using Telegram.Bot.Types;

namespace CaptchaBot.Services
{
    public interface IUsersStore
    {
        void Add(User user, Message message, int sentMessageId, int answer);

        IReadOnlyCollection<NewUser> GetAll();

        NewUser Get(long chatId, int userId);
        
        void Remove(NewUser user);
    }
}