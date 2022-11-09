using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using TwitchVor.Configuration;
using TwitchVor.Utility;

namespace TwitchVor.Communication.Email
{
    public class Emailer
    {
        readonly ILogger _logger;

        readonly EmailConfig config;

        const string subjectBase = "TwitchVor";

        public Emailer(EmailConfig config, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.config = config;
        }

        public async Task<bool> ValidateAsync()
        {
            try
            {
                _logger.LogInformation("Авторизуемся...");

                using (var client = new SmtpClient())
                {
                    client.Timeout = 10000;
                    client.Connect("smtp.gmail.com", 465, true);

                    // Note: only needed if the SMTP server requires authentication
                    await client.AuthenticateAsync(config.Email, config.Password);

                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation("Авторизовались.");

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not validate email.");
                return false;
            }
        }

        public async Task SendCriticalErrorAsync(string more)
        {
            if (!config.NotifyOnCriticalError)
                return;

            await SendAsync(subjectBase, $"ОЧЕНЬ ПЛОХО\n{more}");
        }

        public async Task SendFinishSuccessAsync()
        {
            if (!config.NotifyOnFinishSuccess)
                return;

            await SendAsync(subjectBase, "Всё в поряде, чувачечек");
        }

        /// <param name="subject"></param>
        /// <param name="messageText"></param>
        private async Task SendAsync(string subject, string messageText)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(config.Name, config.Email));
                message.To.Add(new MailboxAddress(config.Name, config.Email));
                message.Subject = subject;

                message.Body = new TextPart("plain")
                {
                    Text = messageText
                };

                using (var client = new SmtpClient())
                {
                    client.Connect("smtp.gmail.com", 465, true);

                    // Note: only needed if the SMTP server requires authentication
                    await client.AuthenticateAsync(config.Email, config.Password);

                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not send email.");
            }
        }
    }
}