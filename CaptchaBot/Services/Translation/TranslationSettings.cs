namespace CaptchaBot.Services.Translation;

public class TranslationSettings
{
    public string NumberTexts { get; set; } = "ноль,один,два,три,четыре,пять,шесть,семь,восемь";

    public string WelcomeMessageTemplate { get; set; } = "Привет, {0}, нажми кнопку {1}, чтобы тебя не забанили!";
}