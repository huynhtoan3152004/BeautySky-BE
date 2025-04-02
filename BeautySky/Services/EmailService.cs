
using MailKit.Net.Smtp;
using MimeKit;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BeautySky.Services
{
    public class EmailService : IEmailService
    {
       
            private readonly string _smtpServer;
            private readonly int _smtpPort;
            private readonly string _smtpUser;
            private readonly string _smtpPass;

            public EmailService(IConfiguration configuration)
            {
                _smtpServer = configuration["Smtp:Server"];
                _smtpPort = int.Parse(configuration["Smtp:Port"]);
                _smtpUser = configuration["Smtp:User"];
                _smtpPass = configuration["Smtp:Pass"];
            }

            public async Task SendEmailAsync(string to, string subject, string body)
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("BeautySky", _smtpUser));
                message.To.Add(new MailboxAddress("", to));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = body };
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_smtpServer, _smtpPort, true);
                    await client.AuthenticateAsync(_smtpUser, _smtpPass);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
        }
    }

