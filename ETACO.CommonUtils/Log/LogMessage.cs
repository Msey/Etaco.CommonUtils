using System;

namespace ETACO.CommonUtils
{
    /// <summary> Тип сообщения журнала </summary>
    [FlagsAttribute]
    public enum LogMessageType { None = 0, Trace = 1, Info = 2, Warning = 4, Error = 8, All = Int32.MaxValue }//Trace | Info | Warning | Error

    /// <summary> Сообщение журнала </summary>
    [Serializable]
    public class LogMessage
    {
        public readonly DateTime Time = DateTime.Now;

        public LogMessageType Type { get; set; }
        public string Text { get; set; }
        public string Detail { get; set; }
        public string Source { get; set; }

        public override string ToString()
        {
            return Time.ToString("yyyy.MM.dd HH:mm:ss.fff") + "  " + Type + "(" + Source.IfEmpty("*") + ") " + Text + " " + Detail;
        }
    }
}