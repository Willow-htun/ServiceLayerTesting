using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using System.Reflection;


namespace ServiceLayerTesting.Core
{
    public class Logger
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Logger));

        public static void WriteLog(string message)
        {
            if (log.IsInfoEnabled)
            {
                log.Info(message);
            }
        }

        public static void WriteError(string message)
        {
            if (log.IsErrorEnabled)
            {
                log.Error(message);
            }
        }
    }
}