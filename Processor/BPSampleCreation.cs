using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Configuration;

namespace ServiceLayerTesting.Processor
{
    public static class BPSampleCreation
    {
        public static void CreateBusinessPartner(string sessionId)
        {
            string baseUrl = ConfigurationManager.AppSettings["ServiceLayerBaseUrl"];
            string businessPartnerUrl = $"{baseUrl}/BusinessPartners";

            JObject businessPartnerData = new JObject
            {
                { "CardCode", "SLTest2" },
                { "CardName", "ServiceLayerTestName2" },
                { "CardType", "C" },
                { "GroupCode", 100 }
            };

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(businessPartnerUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(businessPartnerData.ToString());
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        Console.WriteLine("Business Partner created successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Failed to create Business Partner. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (WebException ex)
            {
                using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                {
                    string errorResponse = reader.ReadToEnd();
                    Console.WriteLine("Business Partner creation failed. Detailed error: " + errorResponse);
                }
            }
        }
    }
}
