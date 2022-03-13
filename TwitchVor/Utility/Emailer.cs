using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace TwitchVor.Utility
{
    public class Emailer
    {
        readonly string name;
        readonly string email;
        readonly string password;

        public Emailer(string name, string email, string password)
        {
            this.name = name;
            this.email = email;
            this.password = password;
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
                    await client.AuthenticateAsync(email, password);

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

        /// <param name="subject"></param>
        /// <param name="messageText"></param>
        public async Task SendAsync(string subject, string messageText)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(name, email));
                message.To.Add(new MailboxAddress(name, email));
                message.Subject = subject;

                message.Body = new TextPart("plain")
                {
                    Text = messageText
                };

                using (var client = new SmtpClient())
                {
                    client.Connect("smtp.gmail.com", 465, true);

                    // Note: only needed if the SMTP server requires authentication
                    await client.AuthenticateAsync(email, password);

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