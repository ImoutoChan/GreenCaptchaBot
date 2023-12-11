using Microsoft.Extensions.Options;

namespace CaptchaBot.Services.Translation;

public interface ITranslationService
{
    string GetWelcomeMessage(string prettyUserName, in int answer);
}

public class TranslationService : ITranslationService
{
    public TranslationService(IOptions<TranslationSettings> options)
    {
        var settings = options.Value;
        
        NumberTexts = settings.NumberTexts.Split(',');
        WelcomeMessageTemplate = settings.WelcomeMessageTemplate;
    }

    private string[] NumberTexts { get; }

    private string WelcomeMessageTemplate { get; }
    
    private string GetRequestedNumberText(in int answer) => NumberTexts[answer];
    
    public string GetWelcomeMessage(string prettyUserName, in int answer) 
        => string.Format(WelcomeMessageTemplate, prettyUserName, GetRequestedNumberText(answer));
}
