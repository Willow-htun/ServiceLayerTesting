using System;
using System.Configuration;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using ServiceLayerTesting.Model;
using ServiceLayerTesting.Core;
using ServiceLayerTesting.HelperMethod;

namespace ServiceLayerTesting.Processor
{
    public static class JEPoster
    {
        public static bool PostJE(string sessionId, JE je)
        {
            string baseUrl = ConfigurationManager.AppSettings["ServiceLayerBaseUrl"];
            string journalEntryUrl = $"{baseUrl}/JournalEntries";
            string jeJson = JsonConvert.SerializeObject(je);

            // Read flag once
            bool emailEnabled = string.Equals(
                (ConfigurationManager.AppSettings["EmailSend"] ?? "Y").Trim(),
                "Y",
                StringComparison.OrdinalIgnoreCase
            );

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(journalEntryUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(jeJson);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        Logger.WriteLog($"{today} - Journal Entry from object created successfully.");

                        if (emailEnabled)
                        {
                            EmailSender.Send(
                                "JE Created Successfully(Thu Rein Htun)",
                                $"Date: {today}\nMemo: {je?.Memo}\nLines: {je?.JournalEntryLines?.Count}"
                            );
                        }
                        else
                        {
                            Logger.WriteLog("EmailSend = N in config — skipping success email.");
                        }

                        return true;
                    }
                    else
                    {
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        var msg = $"{today} - Failed to create Journal Entry. Status code: {response.StatusCode}";
                        Logger.WriteError(msg);

                        if (emailEnabled)
                        {
                            EmailSender.Send("JE Creation Failed", msg);
                        }
                        else
                        {
                            Logger.WriteLog("EmailSend = N in config — skipping failure email.");
                        }

                        return false;
                    }
                }
            }
            catch (WebException ex)
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                string errorResponse = "(no response)";
                try
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                        errorResponse = reader.ReadToEnd();
                }
                catch { /* ignore if no response stream */ }

                var msg = $"{today} - Journal Entry creation failed. Detailed error: {errorResponse}";
                Logger.WriteError(msg);

                if (emailEnabled)
                {
                    EmailSender.Send("JE Creation Failed (WebException)", msg);
                }
                else
                {
                    Logger.WriteLog("EmailSend = N in config — skipping WebException email.");
                }

                return false;
            }
            catch (Exception ex)
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                var msg = $"{today} - Unexpected error posting Journal Entry: {ex.Message}";
                Logger.WriteError(msg);

                if (emailEnabled)
                {
                    EmailSender.Send("JE Creation Failed (Unexpected)", msg);
                }
                else
                {
                    Logger.WriteLog("EmailSend = N in config — skipping unexpected-error email.");
                }

                return false;
            }
        }
    }
}
