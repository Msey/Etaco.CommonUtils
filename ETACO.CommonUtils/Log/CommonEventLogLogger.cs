using System;
using System.Diagnostics;

namespace ETACO.CommonUtils
{
    /// <summary> Регистратор сообщений журнала в EventLog </summary>
    [Serializable]
    public class CommonEventLogLogger : IDisposable
    {
        public readonly EventLog EventLog = new EventLog();
        public bool ErrorCaching { get; set; }
        public LogMessageType Mode { get; set; }
        public string Source { get; set; }

        //---------
        private string lastErrorMessage = "";
        private decimal errorMessageCount = 0;

        public CommonEventLogLogger(string eventLogSource)
        {
            EventLog.Source = eventLogSource;
            Mode = LogMessageType.Error;
            //if(!EventLog.SourceExists(source, Environment.MachineName)) EventLog.CreateEventSource(source, "Application");  //требует доп. прав (runAsAdministrator) 
        }

        /// <summary> Регистрация сообщения журнала </summary>
        public void WriteMessage(LogMessage message)
        {
            if (Mode.Includes(message.Type) && (Source.IsEmpty() || message.Source == Source))
            {
                if ((message.Type == LogMessageType.Error) && (ErrorCaching))
                {
                    if (string.Equals(lastErrorMessage, message.Text, StringComparison.InvariantCultureIgnoreCase) && (errorMessageCount < decimal.MaxValue - 7))
                    {
                        if (errorMessageCount == 1) EventLog.WriteEntry(message.Text + " (error_count=1)", LogTypeToEventLogType(message.Type));
                        errorMessageCount++;
                    }
                    else
                    {
                        if (errorMessageCount > 1) EventLog.WriteEntry(lastErrorMessage + " (error_count={0})".FormatStr(errorMessageCount), EventLogEntryType.Error);
                        lastErrorMessage = message.Text;
                        errorMessageCount = 0;
                        EventLog.WriteEntry(message.Text, LogTypeToEventLogType(message.Type));
                    }
                }
                else
                {
                    EventLog.WriteEntry(message.Text, LogTypeToEventLogType(message.Type));
                }
            }
        }

        /// <summary> Перевод типа сообщения журнала в тип сообщения EventLog </summary>
        public static EventLogEntryType LogTypeToEventLogType(LogMessageType type)
        {
            if ((type == LogMessageType.Info) || (type == LogMessageType.Trace)) return EventLogEntryType.Information;
            else if (type == LogMessageType.Warning) return EventLogEntryType.Warning;
            return EventLogEntryType.Error;
        }

        public void Dispose()
        {
            EventLog.Close();
        }
    }
}