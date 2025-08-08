using System;
using log4net.Config;
using ServiceLayerTesting.Core;
using ServiceLayerTesting.Processor;

namespace SAPB1ServiceLayerTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            Logger.WriteLog("--------------------------------------------------------------------------------------------------.");
            Logger.WriteLog("Program Started.");
            Logger.WriteLog("--------------------------------------------------------------------------------------------------.");

            var sessionId = Utilities.Login();

            if (!string.IsNullOrEmpty(sessionId))
            {
                JEReadAndInsert.ReadJEAndInsert(sessionId);
                //BPSampleCreation.CreateMultipleBusinessPartners(sessionId);
                //JESampleCreation.CreateSampleJE(sessionId);
                Utilities.Logout(sessionId);
                Logger.WriteLog("--------------------------------------------------------------------------------------------------.");
                Logger.WriteLog("Program finished successfully.");
                Logger.WriteLog("--------------------------------------------------------------------------------------------------.");
            }
            else
            {
                Logger.WriteError("Login failed.");
            }

            Console.ReadLine();
        }
    }
}
