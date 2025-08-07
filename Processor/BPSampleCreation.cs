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
    public static class BPSampleCreation
    {
        // Batch method to handle multiple BP creations
        public static void CreateMultipleBusinessPartners(string sessionId)
        {
            // This data could come from a file or zip for Htachi.
            var bpList = GetBusinessPartners();

            foreach (var bp in bpList)
            {
                CreateBusinessPartner(sessionId, bp);
            }
        }

        // Encapsulate BP sample data here (simulate file/DB/etc.)
        private static List<BusinessPartner> GetBusinessPartners()
        {
            return new List<BusinessPartner>
            {
                new BusinessPartner { CardCode = "SLTest3", CardName = "ServiceLayerTestName3", CardType = "C", GroupCode = 100 },
                new BusinessPartner { CardCode = "SLTest4", CardName = "ServiceLayerTestName4", CardType = "C", GroupCode = 100 }
            };
        }

        public static void CreateBusinessPartner(string sessionId, BusinessPartner bp)
        {
            string baseUrl = ConfigurationManager.AppSettings["ServiceLayerBaseUrl"];
            string businessPartnerUrl = $"{baseUrl}/BusinessPartners";

            string bpJson = JsonConvert.SerializeObject(bp);

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(businessPartnerUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(bpJson);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        Logger.WriteLog($"Business Partner {bp.CardCode} created successfully.");
                    }
                    else
                    {
                        Logger.WriteError($"Failed to create Business Partner {bp.CardCode}. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (WebException ex)
            {
                using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                {
                    string errorResponse = reader.ReadToEnd();
                    Logger.WriteError($"Business Partner {bp.CardCode} creation failed. Detailed error: {errorResponse}");
                }
            }
        }
    }
}
