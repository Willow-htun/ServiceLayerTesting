using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using ServiceLayerTesting.Core;

namespace ServiceLayerTesting.HelperMethod
{
    public static class EmailSender
    {
        public static void Send(string subject, string body)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var host = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
            var port = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out var p) ? p : 587;
            var user = ConfigurationManager.AppSettings["SmtpUser"];
            var pass = ConfigurationManager.AppSettings["SmtpAppPassword"];
            var from = ConfigurationManager.AppSettings["SmtpFrom"] ?? user;
            var fromName = ConfigurationManager.AppSettings["SmtpFromName"];
            //Doesn't work
            var cfgdefaultSubject = ConfigurationManager.AppSettings["SmtpSubject"];

            //Doesn't work
            if (string.IsNullOrWhiteSpace(subject))
                subject = cfgdefaultSubject;
            else if (string.IsNullOrWhiteSpace(subject))
                subject = "(no subject)";

            var toList = (ConfigurationManager.AppSettings["SmtpTo"] ?? "")
                         .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => s.Length > 0)
                         .ToList();

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) ||
                string.IsNullOrWhiteSpace(from) || toList.Count == 0)
            {
                Logger.WriteError("Email not sent: missing SMTP settings or no recipients.");
                return;
            }

            try
            {
                using (var client = new SmtpClient(host, port))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(user, pass);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 30000;

                    foreach (var addr in toList)
                    {
                        using (var mail = new MailMessage())
                        {
                            mail.From = new MailAddress(from, fromName);
                            mail.To.Add(addr);
                            mail.Subject = subject;
                            mail.Body = body ?? string.Empty;
                            mail.IsBodyHtml = false;

                            client.Send(mail);
                            Logger.WriteLog($"Email sent to {addr}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError("Email send error: " + ex.Message);
                if (ex.InnerException != null) Logger.WriteError("Inner: " + ex.InnerException.Message);
                throw;
            }
        }
    }
}