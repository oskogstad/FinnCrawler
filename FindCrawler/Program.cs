using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Specialized;
using Newtonsoft.Json;
using HtmlAgilityPack;

namespace FindCrawler
{
    internal class Program
    {
        private const string FinnFreeGamesUrl = "https://www.finn.no/bap/forsale/search.html?category=0.93&search_type=SEARCH_ID_BAP_FREE&sort=1&sub_category=1.93.3905";
        private const string FinnAdBaseUrl = "https://www.finn.no/bap/forsale/ad.html?finnkode=";
        private const int TwentySecondsInMs = 20000;
        private const int OneHourInMs = 3600000;
        private const string GmailConfigFileName = "gmailConfig.json";
        private const string OldFinnCodesFileName = "oldFinnCodes.json";
        private const string GmailSmtpHost = "smtp.googlemail.com";
        private const string FinnAdXpathFilter = "//div[contains(@class, 'ads__unit__content')]";
        private const string FinnAdDescriptionXpathFilter = "//meta[contains(@name, 'description')]";

        private static readonly Random RandomGenerator = new Random();
        private static int _minimumWaitTimeInMs = TwentySecondsInMs;
        private static Dictionary<string, DateTime> _oldFinnCodes;
        private static GmailConfig _gmailConfig;
        private static NetworkCredential _gmailCredentials;
        private static MailAddress _fromEmail;
        private static MailAddress _toEmail;

        public static void Main()
        {
            Setup();

            void DoWork()
            {
                var finnAdsHtmlNodes = GetFinnAdsHtmlNodes();
                var newFinnAds = ExtractNewFinnAdsInfo(finnAdsHtmlNodes);

                if (!newFinnAds.Any())
                    return;

                var emailBody = CreateEmailBody(newFinnAds);
                var plural = newFinnAds.Count > 1;
                var emailSubject = $"Ny{(plural ? "e" : "")} annonse{(plural ? "r" : "")} i Gis bort|Spill og konsoll";

                SendEmail(emailBody, emailSubject);

                var oldFinnCodesJson = JsonConvert.SerializeObject(_oldFinnCodes);
                File.WriteAllText(OldFinnCodesFileName, oldFinnCodesJson);
            }

            while (true)
            {
                try
                {
                    DoWork();
                    _minimumWaitTimeInMs = TwentySecondsInMs;
                }
                catch (Exception ex)
                {
                    _minimumWaitTimeInMs = _minimumWaitTimeInMs * 2;
                    using (var sw = File.AppendText("err.log"))
                    {
                        sw.WriteLine($"Message: {ex.Message}\nStackTrace{ex.StackTrace}");
                    }
                }

                if (DateTime.Now.Hour < 7)
                    Thread.Sleep(OneHourInMs);

                else
                {
                    var waitTimeInMs =
                        _minimumWaitTimeInMs +
                        RandomGenerator.Next(0, _minimumWaitTimeInMs);
                    Thread.Sleep(waitTimeInMs);
                }
            }
        }

        private static HtmlNodeCollection GetFinnAdsHtmlNodes()
        {
            var htmlWeb = new HtmlWeb();
            var htmlDoc = htmlWeb.Load(FinnFreeGamesUrl);
            var finnAdsHtmlNodes = htmlDoc.DocumentNode.SelectNodes(FinnAdXpathFilter);
            return finnAdsHtmlNodes;
        }

        private static string CreateEmailBody(IEnumerable<(string Id, string Title, string Description, string Location)> newFinnAds)
        {
            return newFinnAds.Aggregate(
                string.Empty,
                (current, finnAd) =>
                    current +
                    $"Tittel: {finnAd.Title}" +
                    "<br />" +
                    $"Lokasjon: {finnAd.Location}" +
                    "<br />" +
                    $"{FinnAdBaseUrl}{finnAd.Id}" +
                    "<br />" +
                    $"Annonsetekst: {finnAd.Description}" +
                    "<br />" +
                    "<br />");
        }

        private static List<(string Id, string Title, string Description, string Location)> ExtractNewFinnAdsInfo(HtmlNodeCollection htmlAds)
        {
            var newFinnAds = new List<(string Id, string Title, string Description, string Location)>();

            foreach (var htmlAd in htmlAds)
            {
                var adId = htmlAd.ChildNodes[1].ChildNodes[1].Id;

                if (_oldFinnCodes.ContainsKey(adId))
                    continue;

                _oldFinnCodes.Add(adId, DateTime.Now);

                var adTitle = htmlAd.ChildNodes[1].ChildNodes[1].InnerText.Trim();
                var adLocation = htmlAd.ChildNodes[3].ChildNodes[3].InnerText.Trim();
                var adDescription = GetAdDescription(adId);
                newFinnAds.Add((Id: adId, Title: adTitle, Description: adDescription, Location: adLocation));
            }

            return newFinnAds;
        }

        private static string GetAdDescription(string adId)
        {
            var htmlWeb = new HtmlWeb();
            var htmlDoc = htmlWeb.Load(FinnAdBaseUrl+adId);

            var descriptionNode = htmlDoc.DocumentNode.SelectSingleNode(FinnAdDescriptionXpathFilter);
            return descriptionNode.Attributes[1].Value;
        }

        private static void SendEmail(string body, string subject)
        {
            using (var smtpClient = new SmtpClient(GmailSmtpHost))
            {
                smtpClient.Credentials = _gmailCredentials;
                smtpClient.Port = 587;
                smtpClient.EnableSsl = true;

                var message = new MailMessage(_fromEmail, _toEmail)
                {
                    IsBodyHtml = true,
                    Body = body,
                    BodyEncoding = System.Text.Encoding.UTF8,
                    Subject = subject,
                    SubjectEncoding = System.Text.Encoding.UTF8
                };

                smtpClient.Send(message);
            }
        }

        private static void Setup()
        {
            _oldFinnCodes = new Dictionary<string, DateTime>();
            if (File.Exists(OldFinnCodesFileName))
            {
                _oldFinnCodes = JsonConvert
                    .DeserializeObject<Dictionary<string, DateTime>>
                        (File.ReadAllText(OldFinnCodesFileName));
            }

            _gmailConfig = JsonConvert.DeserializeObject<GmailConfig>(File.ReadAllText(GmailConfigFileName));
            _gmailCredentials = new NetworkCredential(_gmailConfig.SenderEmail, _gmailConfig.Password);
            _fromEmail = new MailAddress(_gmailConfig.SenderEmail);
            _toEmail = new MailAddress(_gmailConfig.TargetEmail);
        }
    }
}
