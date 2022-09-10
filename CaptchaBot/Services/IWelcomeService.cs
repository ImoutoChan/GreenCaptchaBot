using Telegram.Bot.Types;

namespace CaptchaBot.Services;

public interface IWelcomeService
{
    Task ProcessCallback(CallbackQuery query);

    Task ProcessNewChatMember(Message message);
}