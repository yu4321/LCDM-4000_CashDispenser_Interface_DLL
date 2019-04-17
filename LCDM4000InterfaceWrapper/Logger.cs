using System;
using System.IO;

namespace LCDM4000InterfaceWrapper
{
    public static class Logger
    {
        private static string filePath = null;
        public static DateTime lastLogTime;
        static Logger()
        {
            filePath = ResetFileNamebyDate();
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }
        }
        internal static void Info(string v)
        {
            try
            {
                if (lastLogTime.Date != DateTime.Now.Date)
                {
                    filePath = ResetFileNamebyDate();
                }
                lastLogTime = DateTime.Now;
                using (StreamWriter fs = new StreamWriter(filePath, true))
                {
                    fs.WriteLine($"{lastLogTime} INFO : {v}");
                }
            }
            catch
            {

            }
        }

        internal static void Error(string v)
        {
            try
            {
                if (lastLogTime.Date != DateTime.Now.Date)
                {
                    filePath = ResetFileNamebyDate();
                }
                lastLogTime = DateTime.Now;
                using (StreamWriter fs = new StreamWriter(filePath, true))
                {
                    fs.WriteLine($"{lastLogTime} Error : {v}");
                }
            }
            catch
            {

            }
        }

        internal static void Warn(string v)
        {
            try
            {
                if (lastLogTime.Date != DateTime.Now.Date)
                {
                    filePath = ResetFileNamebyDate();
                }
                lastLogTime = DateTime.Now;
                using (StreamWriter fs = new StreamWriter(filePath, true))
                {
                    fs.WriteLine($"{lastLogTime} Warn : {v}");
                }
            }
            catch
            {

            }
        }

        public static string ResetFileNamebyDate()
        {
            return $"logs/log{DateTime.Now.ToShortDateString()}_{System.Reflection.Assembly.GetEntryAssembly().GetName().Name}_BillDispenser.log";
        }

        internal static void Info(object obj)
        {
            Info(obj.ToString());
        }

        internal static void Error(object obj)
        {
            Error(obj.ToString());
        }

        internal static void Warn(object obj)
        {
            Warn(obj.ToString());
        }
    }
}
