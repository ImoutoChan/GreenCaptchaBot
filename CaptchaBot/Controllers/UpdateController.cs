using System;
using System.Threading.Tasks;
using CaptchaBot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CaptchaBot.Controllers;

[Route("api/captchaupdate")]
public class UpdateController : Controller
{
    private readonly ITelegramBotClient _telegramBot;
    private readonly ILogger<UpdateController> _logger;
    private readonly IWelcomeService _welcomeService;

    public UpdateController(
        ITelegramBotClient telegramBot,
        ILogger<UpdateController> logger,
        IWelcomeService welcomeService)
    {
        _telegramBot = telegramBot;
        _logger = logger;
        _welcomeService = welcomeService;
    }

    // POST api/update
    [HttpPost]
    public async Task<IActionResult> Post([FromBody]Update update)
    {
        try
        {
            if (update?.Message?.Type == MessageType.ChatMembersAdded)
            {
                await _welcomeService.ProcessNewChatMember(update.Message);
            }

            if (update?.Type == UpdateType.CallbackQuery)
            {
                await _welcomeService.ProcessCallback(update.CallbackQuery);
                await _telegramBot.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Проблемы");
        }

        return Ok();
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Ok!");
    }
}
