using System;
using System.Configuration;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using ServiceLayerTesting.Model;
using ServiceLayerTesting.Core;

namespace ServiceLayerTesting.Processor
{
    public static class JEPoster
    {
        public static bool PostJE(string sessionId, JE je)
        {
            string baseUrl = ConfigurationManager.AppSettings["ServiceLayerBaseUrl"];
            string journalEntryUrl = $"{baseUrl}/JournalEntries";
            string jeJson = JsonConvert.SerializeObject(je);

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
                        return true; // Success
                    }
                    else
                    {
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        Logger.WriteError($"{today} - Failed to create Journal Entry. Status code: " + response.StatusCode);
                        return false;
                    }
                }
            }
            catch (WebException ex)
            {
                using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                {
                    string today = DateTime.Now.ToString("yyyy-MM-dd");
                    string errorResponse = reader.ReadToEnd();
                    Logger.WriteError($"{today} - Journal Entry creation failed. Detailed error: " + errorResponse);
                }
                return false;
            }
            catch (Exception ex)
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                Logger.WriteError($"{today} - Unexpected error posting Journal Entry: " + ex.Message);
                return false;
            }
        }
    }
}
