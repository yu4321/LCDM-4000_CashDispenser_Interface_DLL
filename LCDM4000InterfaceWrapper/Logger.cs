using log4net;
using log4net.Appender;
using log4net.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LCDM4000InterfaceWrapper
{
    public static class Logger
    {
        public static readonly ILog LogWriter = GetAppLogger();

        static Logger()
        {
            log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo("log4net.xml"));
        }

        private static ILog GetAppLogger()
        {
            RollingFileAppender roller = new RollingFileAppender();
            roller.AppendToFile = true;
            roller.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller.MaxSizeRollBackups = 10;
            roller.MaximumFileSize = "1MB";
            roller.File = $@"logs\{System.Reflection.Assembly.GetEntryAssembly().GetName().Name+"_BillDispenser"}.log";

            roller.StaticLogFileName = true;
            roller.Layout = new PatternLayout("%d{yyMMdd HH:mm:ss.fff} %-5p : %m%n");
            roller.LockingModel = new FileAppender.MinimalLock();
            roller.ActivateOptions();


            DummyLogger dummyILogger = new DummyLogger("AppLog");
            dummyILogger.Hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
            dummyILogger.Level = log4net.Core.Level.Info;
            dummyILogger.AddAppender(roller);

            return new VarLog(dummyILogger);
        }
    }

    public sealed class DummyLogger : log4net.Repository.Hierarchy.Logger
    {
        internal DummyLogger(string name)
            : base(name)
        {
        }
    }

    public class VarLog : log4net.Core.LogImpl
    {
        public VarLog(DummyLogger log) : base(log)
        {
        }


        public override void Info(object message)
        {
            base.Info(message);
        }
        public override void Error(object message)
        {
            base.Error(message);
        }
        public override void Warn(object message)
        {
            base.Warn(message);
        }
        public override void Error(object message, Exception exception)
        {
            base.Error(message, exception);
        }
        public override void Warn(object message, Exception exception)
        {
            base.Warn(message, exception);
        }
    }
}
