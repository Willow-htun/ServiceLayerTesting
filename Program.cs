using System;
using ServiceLayerTesting.Core;
using ServiceLayerTesting.Processor;

namespace SAPB1ServiceLayerTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var sessionId = Utilities.Login();

            if (!string.IsNullOrEmpty(sessionId))
            {
                BPSampleCreation.CreateBusinessPartner(sessionId);
                Utilities.Logout(sessionId);
            }
            else
            {
                Console.WriteLine("Login failed.");
            }

            Console.ReadLine();
        }
    }
}
