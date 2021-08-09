using System;

namespace CaptchaBot
{
    public enum JoinMessageDeletePolicy
    {
        All,
        Unsuccessful,
        None
    }
    
    public class AppSettings
    {
        public string BotToken { get; set; }
        public string WebHookAddress { get; set; }
        
        /// <summary>If this time has been passed since the user enter event, the event won't be processed.</summary>
        /// <remarks>
        /// Useful for cases when the bot goes offline for a significant amount of time, and receives outdated events
        /// after getting back online.
        /// </remarks>
        public TimeSpan ProcessEventTimeout { get; set; } = TimeSpan.FromMinutes(1.0);

        public JoinMessageDeletePolicy DeleteJoinMessages { get; set; } = JoinMessageDeletePolicy.All;
    }
}