using System;
namespace ETACO.CommonUtils
{
    /// <summary> Регистратор сообщения журнала с помощью Console </summary> 
    [Serializable]
    public class CommonConsoleLogger
    {
        public LogMessageType Mode { get; set; }
        public string Source { get; set; }

        public CommonConsoleLogger() { Mode = LogMessageType.Error; }

        /// <summary> Регистрация сообщения журнала </summary>
        public void WriteMessage(LogMessage message)
        {
            if (AppContext.UserInteractive && Mode.Includes(message.Type) && (Source.IsEmpty() || message.Source == Source))
            {
                if (Console.WindowWidth < 131) Console.WindowWidth = 131;
                Console.Write(message.Time.ToString("yyyy.MM.dd HH:mm:ss.fff") + " ");
                var c = Console.ForegroundColor;
                if (message.Type == LogMessageType.Error) Console.ForegroundColor = ConsoleColor.Red;
                else if (message.Type == LogMessageType.Warning) Console.ForegroundColor = ConsoleColor.Yellow;
                else if (message.Type == LogMessageType.Info) Console.ForegroundColor = ConsoleColor.Green;
                else Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write("{0,-7}".FormatStr(message.Type));
                Console.ForegroundColor = c;
                Console.WriteLine(" " + message.Text);
            }
        }
    }
}