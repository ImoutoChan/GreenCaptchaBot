using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;

namespace CaptchaBot.Services
{
    public class UsersStore : IUsersStore
    {
        private readonly ConcurrentDictionary<ChatUser, NewUser> _users;

        public UsersStore()
        {
            _users = new ConcurrentDictionary<ChatUser, NewUser>();
        }

        public void Add(User user, Message message, int sentMessageId, string prettyUserName, int answer)
        {
            var key = new ChatUser(message.Chat.Id, user.Id);
            var newValue = new NewUser(
                message.Chat.Id,
                user.Id,
                DateTimeOffset.Now,
                sentMessageId,
                message.MessageId,
                prettyUserName,
                answer);

            _users.AddOrUpdate(key, newValue, (chatUser, newUser) => newValue);
        }

        public IReadOnlyCollection<NewUser> GetAll()
        {
            return _users.Values.ToArray();
        }

        public NewUser Get(long chatId, int userId)
        {
            if (_users.TryGetValue(new ChatUser(chatId, userId), out var newUser)) return newUser;

            return null;
        }

        public void Remove(NewUser user)
        {
            _users.TryRemove(new ChatUser(user.ChatId, user.Id), out _);
        }
    }
}