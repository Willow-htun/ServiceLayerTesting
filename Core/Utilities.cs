using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using ServiceLayerTesting.Model;
using System.Configuration;
using ServiceLayerTesting.Core;

namespace ServiceLayerTesting.Core
{
    public static class Utilities
    {
        private static readonly string baseUrl = ConfigurationManager.AppSettings["ServiceLayerBaseUrl"];

        public static string Login()
        {
            // Read credentials from config file
            string username = ConfigurationManager.AppSettings["SAPUsername"];
            string password = ConfigurationManager.AppSettings["SAPPassword"];
            string companyDB = ConfigurationManager.AppSettings["SAPCompanyDB"];
            string url = $"{baseUrl}/Login";

            var loginRequest = new LoginRequest
            {
                UserName = username,
                Password = password,
                CompanyDB = companyDB
            };
            string jsonRequestBody = JsonConvert.SerializeObject(loginRequest);

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.KeepAlive = true;
            httpWebRequest.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
            httpWebRequest.ServicePoint.Expect100Continue = false;

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(jsonRequestBody);
            }

            try
            {
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    var responseInstance = JsonConvert.DeserializeObject<LoginResponse>(result);
                    Logger.WriteLog("Logged in successfully.");
                    return responseInstance.SessionId;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError("Login failed: " + ex.Message);
            }

            return null;
        }

        public static void Logout(string sessionId)
        {
            string logoutUrl = $"{baseUrl}/Logout";

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(logoutUrl);
                request.Method = "POST";
                request.Accept = "application/json";
                request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.NoContent
                        || response.StatusCode == HttpStatusCode.OK)
                    {
                        Logger.WriteLog("Logged out successfully.");
                    }
                    else
                    {
                        Logger.WriteError("Logout failed. Status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError("An error occurred during logout: " + ex.Message);
            }
        }
    }
}
