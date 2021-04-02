using Mailjet.Client;
using Mailjet.Client.Resources;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace GmrLib
{
    public static class WebHelpers
    {
        #region Email Settings

        private static string FromAddress
        {
            get { return WebConfigurationManager.AppSettings["EmailFromAddress"]; }
        }
        private static string FromName
        {
            get { return WebConfigurationManager.AppSettings["EmailFromName"]; }
        }

        #endregion

        public static string HttpGet(string url, int timeout = 100000)
        {
            var sb = new StringBuilder();
            var buf = new byte[8192];

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = timeout;
            var response = (HttpWebResponse)request.GetResponse();

            using (Stream resStream = response.GetResponseStream())
            {
                string tempString = null;
                int count = 0;
                do
                {
                    count = resStream.Read(buf, 0, buf.Length);
                    if (count != 0)
                    {
                        tempString = Encoding.ASCII.GetString(buf, 0, count);
                        sb.Append(tempString);
                    }
                } while (count > 0);
            }
            return sb.ToString();
        }

        public static async Task<bool> SendEmail(string recipientEmailAddress, string subject, string messageBodyHtml)
        {
            return await SendMailJetEmail(recipientEmailAddress, subject, messageBodyHtml);
        }

        private static async Task<bool> SendMailJetEmail(string recipientEmailAddress, string subject, string messageBodyHtml)
        {
            try
            {
                MailjetClient client = new MailjetClient(
                    WebConfigurationManager.AppSettings["MailJetUsername"],
                    WebConfigurationManager.AppSettings["MailJetPassword"]
                    );

                MailjetRequest request = new MailjetRequest
                {
                    Resource = Send.Resource,
                }
                    .Property(Send.FromEmail, FromAddress)
                    .Property(Send.FromName, FromName)
                    .Property(Send.Subject, subject)
                    .Property(Send.HtmlPart, messageBodyHtml)
                    .Property(Send.Recipients, new JArray
                    {
                        new JObject
                        {
                            {  "Email", recipientEmailAddress }
                        }
                });

                MailjetResponse response = await client.PostAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLogger.WriteLine("MailJet", "Failed to send email!");
                    DebugLogger.WriteLine("MailJet", "Status: " + response.StatusCode);
                    DebugLogger.WriteLine("MailJet", "Content: " + response.Content.ToString());
                }

                return response.IsSuccessStatusCode;

            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Sending email with MailJet to '{0}', {1}", recipientEmailAddress, subject));
            }

            return false;
        }
    }
}