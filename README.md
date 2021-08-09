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
    }
}
```

`DeleteJoinMessages`:
- `All`: will delete join messages for all users (default)
- `None`: will not delete join messages at all
- `Unsuccessful`: will only delete join messages after unsuccessful captcha solving (i.e. only the messages from the banned users will be deleted)
