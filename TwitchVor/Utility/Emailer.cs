using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using TwitchVor.Configuration;

namespace TwitchVor.Utility
{
    public class Emailer
    {
        readonly EmailConfig config;

        const string subjectBase = "TwitchVor";

        public Emailer(EmailConfig config)
        {
            this.config = config;
        }

        static void LogError(string message)
        {
            ColorLog.LogError(message, "Emailer");
        }

        public async Task<bool> ValidateAsync()
        {
            try
            {
                using (var client = new SmtpClient())
                {
                    client.Connect("smtp.gmail.com", 465, true);

                    // Note: only needed if the SMTP server requires authentication
                    await client.AuthenticateAsync(config.Email, config.Password);

                    await client.DisconnectAsync(true);
                }

                return true;
            }
            catch (Exception e)
            {
                LogError($"Could not validate email.\n{e}");
                return false;
            }
        }

        public async Task SendCriticalErrorAsync(string more)
        {
            if (!config.NotifyOnCriticalError)
                return;

            await SendAsync(subjectBase, $"ОЧЕНЬ ПЛОХО\n{more}");
        }

        public async Task SendVideoUploadAsync()
        {
            if (!config.NotifyOnVideoUpload)
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
                LogError($"Could not send email.\n{e}");
            }
        }
    }
}