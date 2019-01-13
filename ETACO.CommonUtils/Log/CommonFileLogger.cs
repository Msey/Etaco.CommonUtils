using System;
using System.IO;

namespace ETACO.CommonUtils
{
    /// <summary> Регистратор сообщения журнала в текстовый файл </summary>
    [Serializable]
    public class CommonFileLogger
    {
        public string Dir { get; set; }
        public string FileName { get; set; }
        public string FileNameMask { get; set; }
        public string TimeFormat { get; set; }
        public bool ErrorCaching { get; set; }
        public LogMessageType Mode { get; set; }
        public string Source { get; set; }

        private string lastErrorMessage = "";
        private decimal errorMessageCount = 0;


        public CommonFileLogger() { Mode = LogMessageType.Error; }

        public string GetFileName(string suffix = "")
        {
            return FileName.IfEmpty(Path.Combine(Path.Combine(AppContext.AppDir, Environment.ExpandEnvironmentVariables(Dir.IfEmpty("Log"))), Path.GetFileNameWithoutExtension(AppContext.AppFileName))) + (FileNameMask.IsEmpty() ? "" : DateTime.Now.ToString(FileNameMask)) + suffix + ".log";
        }

        /// <summary> Возвращает текстовое представление сообщения журнала </summary>
        public string GetLogMessage(LogMessage message)
        {
            return message.Time.ToString(TimeFormat.IfEmpty("HH:mm:ss.fff")) + " {0,-7} ".FormatStr(message.Type) + "(" + message.Source.IfEmpty("*") + ") " + message.Text + (message.Detail.IsEmpty() ? "" : Environment.NewLine + message.Detail);
        }

        /// <summary> Регистрация сообщения журнала </summary>
        public void WriteMessage(LogMessage message)
        {
            if (Mode.Includes(message.Type) && (Source.IsEmpty() || message.Source == Source))
            {
                var logFileName = GetFileName();
                if ((message.Type == LogMessageType.Error) && (ErrorCaching) && File.Exists(logFileName))
                {
                    if (string.Equals(lastErrorMessage, message.Text, StringComparison.InvariantCultureIgnoreCase) && (errorMessageCount < decimal.MaxValue - 7))
                    {
                        if (errorMessageCount == 1) WriteToFile(GetLogMessage(message) + "   (error_count = 1)", logFileName);
                        errorMessageCount++;
                    }
                    else
                    {
                        if (errorMessageCount > 1) WriteToFile(GetLogMessage(new LogMessage() { Type = LogMessageType.Error, Text = lastErrorMessage + "   (error_count = {0})".FormatStr(errorMessageCount) }), logFileName);
                        lastErrorMessage = message.Text;
                        errorMessageCount = 0;
                        WriteToFile(GetLogMessage(message), logFileName);
                    }
                }
                else
                {
                    WriteToFile(GetLogMessage(message), logFileName);
                }
            }
        }

        private void WriteToFile(string text, string logFileName)
        {
            text += Environment.NewLine;
            try
            {
                3.TryInvoke(() => text.WriteToFile(logFileName));
            }
            catch (IOException ioe)
            {
                var copyLog = GetFileName("_copy");
                ("Last error: " + ioe.Message + Environment.NewLine).WriteToFile(copyLog);
                text.WriteToFile(copyLog);
            }
        }
    }
}