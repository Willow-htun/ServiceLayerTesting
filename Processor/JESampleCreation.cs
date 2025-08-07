using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using ServiceLayerTesting.Model;
using ServiceLayerTesting.Core;

namespace ServiceLayerTesting.Processor
{
    public static class JESampleCreation
    {
        public static void CreateSampleJE(string sessionId)
        {
            string baseUrl = ConfigurationManager.AppSettings["ServiceLayerBaseUrl"];
            string journalEntryUrl = $"{baseUrl}/JournalEntries";

            var je = new JE
            {
                ReferenceDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Memo = "Test JE via Service Layer",
                JournalEntryLines = new List<JELine>
                {
                    new JELine
                    {
                        AccountCode = "160000", // Replace
                        Debit = 12.00,
                        Credit = 0.00,
                        LineMemo = "Test Debit"
                    },
                    new JELine
                    {
                        AccountCode = "161000", // Replace
                        Debit = 0.00,
                        Credit = 12.00,
                        LineMemo = "Test Credit"
                    }
                }
            };

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
                        Logger.WriteLog("Sample Journal Entry created successfully.");
                    }
                    else
                    {
                        Logger.WriteError("Failed to create Journal Entry. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (WebException ex)
            {
                using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                {
                    string errorResponse = reader.ReadToEnd();
                    Logger.WriteError("Journal Entry creation failed. Detailed error: " + errorResponse);
                }
            }
        }
    }
}
