GreenCaptchaBot
===============

This is a bot that will ask the users coming to a Telegram chat to press a random numbered button. 

Configuration
-------------

```json
{
    "Configuration": {
        "BotToken": "0000000000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        "WebHookAddress": "https://yourserver.example.com/api/captchaupdate",
        "DeleteJoinMessages": "All"
    },
    "Translation": {
        "NumberTexts": "zero,one,two,three,four,five,six,seven,eight",
        "WelcomeMessageTemplate": "Hi, {0}, press the button {1}, to avoid getting banned!"
    }
}
```

`DeleteJoinMessages`:
- `All`: will delete join messages for all users (default)
- `None`: will not delete join messages at all
- `Unsuccessful`: will only delete join messages after unsuccessful captcha solving (i.e. only the messages from the banned users will be deleted)

`Translation`:
- `NumberTexts` - localized text of numbers
- `WelcomeMessageTemplate` - template of the message that is sent to new users
