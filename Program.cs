using System;
using ServiceLayerTesting.Core;
using ServiceLayerTesting.Processor;
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace SAPB1ServiceLayerTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            var sessionId = Utilities.Login();

            if (!string.IsNullOrEmpty(sessionId))
            {
                Logger.WriteLog("Program Started.");
                Logger.WriteLog("Program Started.HiHe.");
                //BPSampleCreation.CreateMultipleBusinessPartners(sessionId);
                //JESampleCreation.CreateSampleJE(sessionId);
                Utilities.Logout(sessionId);
                Logger.WriteLog("Program finished successfully.");
            }
            else
            {
                Logger.WriteError("Login failed.");
            }

            //Console.ReadLine();
        }
    }
}
