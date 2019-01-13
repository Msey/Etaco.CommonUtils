using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ETACO.CommonUtils
{
    /// <summary> Журнал </summary>
    [Serializable]
    public class Log
    {
        private readonly object _lock = new object();
        private readonly Queue<LogMessage> _buffer = new Queue<LogMessage>();
        /// <summary> Нужно ли генерировать trace сообщения (для оптимизации) </summary>
        public bool UseTrace { get; set; }
        public event Action<LogMessage> OnLog;

        public Delegate[] LoggerList
        {
            get { return OnLog == null ? new Delegate[0] : OnLog.GetInvocationList(); }
        }

        public void Trace(string message, string source = "") { Write(new LogMessage() { Type = LogMessageType.Trace, Text = message, Source = source.IfEmpty(UseTrace ? GetTypeName(AppContext.GetCallingType()) : "") }); }
        public void Info(string message, string source = "") { Write(new LogMessage() { Type = LogMessageType.Info, Text = message, Source = source.IfEmpty(UseTrace ? GetTypeName(AppContext.GetCallingType()) : "") }); }
        public void Warning(string message, string source = "") { Write(new LogMessage() { Type = LogMessageType.Warning, Text = message, Source = source.IfEmpty(UseTrace ? GetTypeName(AppContext.GetCallingType()) : "") }); }
        public void Error(string message, string source = "") { Write(new LogMessage() { Type = LogMessageType.Error, Text = message, Source = source.IfEmpty(UseTrace ? GetTypeName(AppContext.GetCallingType()) : "") }); }

        public void Write(ushort level, string message, string source = "")
        {
            Write(new LogMessage() { Type = (LogMessageType)level, Text = message, Source = source.IfEmpty(UseTrace ? GetTypeName(AppContext.GetCallingType()) : "") });
        }

        public void HandleException(Exception ex, string message = "", string source = "")
        {
            Write(new LogMessage()
            {
                Type = LogMessageType.Error,
                Source = source.IfEmpty(UseTrace ? GetTypeName(AppContext.GetCallingType()) : ""),
                Text = (message.IsEmpty() ? "" : message + Environment.NewLine) + ex.GetFullExceptionText(),
                Detail = ex.GetType() + ":    " + GetShortStackTrace(ex)
            });
        }

        internal void Write(LogMessage message)
        {
            lock (_lock)
            {
                _buffer.Enqueue(message);
                if (_buffer.Count > 100) _buffer.Dequeue();
                if (OnLog == null) { if (message.Type == LogMessageType.Error) DumpAppError("LogWriter not registred.", message); }
                else
                {
                    try
                    {//можно использовать и асинхронный вызов регистраторов сообщений BeginInvoke (но иногда полезно и блокирующий вызов MessageBox)
                        OnLog(message);
                    }
                    catch (Exception ex)
                    {
                        DumpAppError("Error in logging process: " + ex.Message, message);
                    }
                }
            }
        }

        private void DumpAppError(string dumpComment, LogMessage errorMessage)
        {
            var dumpText = "{0}{2}{1}{2}".FormatStr(dumpComment, errorMessage, Environment.NewLine);
            try
            {
                3.TryInvoke(() => dumpText.WriteToFile(Path.Combine(AppContext.AppDir, DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_DUMP.log")));
            }
            finally
            {
                if (AppContext.UserInteractive) MessageBox.Show(dumpText, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LoadFromConfig(Config cfg, Func<string, Action<LogMessage>> OnLoadFromConfig = null)
        {
            UseTrace = cfg.GetParameter("log", "usetrace", false);
            foreach (var v in cfg.GetModules("log", 1))
            {
                if (v.StartsWith("log/file", StringComparison.Ordinal)) OnLog += new CommonFileLogger() { Mode = cfg.GetParameter(v, "mode", LogMessageType.Error), Dir = cfg[v, "dir", "Log"], FileName = cfg[v, "filename"], FileNameMask = cfg[v, "filenamemask"], TimeFormat = cfg[v, "timeformat"], Source = cfg[v, "source"], ErrorCaching = cfg.GetParameter(v, "error_caching", true) }.WriteMessage;
                else if (v.StartsWith("log/eventlog", StringComparison.Ordinal)) OnLog += new CommonEventLogLogger(cfg[v, "eventlogsource"]) { Mode = cfg.GetParameter(v, "mode", LogMessageType.Error), Source = cfg[v, "source"], ErrorCaching = cfg.GetParameter(v, "error_caching", true) }.WriteMessage;
                else if (v.StartsWith("log/console", StringComparison.Ordinal)) OnLog += new CommonConsoleLogger() { Mode = cfg.GetParameter(v, "mode", LogMessageType.Error | LogMessageType.Info | LogMessageType.Warning), Source = cfg[v, "source"] }.WriteMessage;
                else if (v.StartsWith("log/msgbox", StringComparison.Ordinal)) OnLog += new CommonMessageBoxLogger() { Mode = cfg.GetParameter(v, "mode", LogMessageType.Error), Source = cfg[v, "source"] }.WriteMessage;
                else if (OnLoadFromConfig != null) { var action = OnLoadFromConfig(v); if (action != null) OnLog += action; }
            }
        }

        private static string GetTypeName(Type t)
        {
            if (t.IsGenericType)
            {
                var result = "";
                foreach (var argType in t.GetGenericArguments()) result += ", " + GetTypeName(argType);
                return t.Name.Substring(0, t.Name.IndexOf('`')) + "<" + result.Substring(1).Trim() + ">";
            }
            else if (t.IsNested) return t.ReflectedType.FullName;
            return t.FullName;
        }

        public LogMessage[] GetBuffer(LogMessageType type = LogMessageType.All, string source = null)
        {
            var result = _buffer.ToArray();
            if (type == LogMessageType.All && source.IsEmpty()) return result;

            var buff = new List<LogMessage>();
            Array.ForEach(result, r => { if (type.Includes(r.Type) && (source.IsEmpty() || r.Source == source)) buff.Add(r); });
            return buff.ToArray();
        }

        private string GetShortStackTrace(Exception ex)
        {
            try
            {
                var result = "";
                var nameSpace = "";
                var className = "";
                var prevLine = "";

                foreach (var v in ex.StackTrace.Split("\r\n"))
                {
                    var line = v.Trim();
                    var start = line.IndexOf(' ') + 1;//'at ' либо 'в '
                    var l = line.IndexOf('(') - start;
                    if (l < 0) { result += "\r\n      " + line; continue; }
                    line = line.Substring(start, l);
                    if (line.StartsWith("System.", StringComparison.Ordinal) || line.StartsWith("Microsoft.", StringComparison.Ordinal))
                    {
                        prevLine = v;
                        continue;
                    }

                    var nameParts = line.Split(".");
                    var cName = nameParts[nameParts.Length - 2];
                    var sName = string.Join(".", nameParts, 0, nameParts.Length - 2);

                    if (nameSpace != sName)
                    {
                        nameSpace = sName;
                        className = cName;
                    }
                    else
                    {
                        var mName = nameParts[nameParts.Length - 1];
                        if (className != cName)
                        {
                            className = cName;
                            line = className + "." + mName;
                        }
                        else line = " <= " + mName;
                    }
                    result += prevLine + "\r\n      " + line + "(" + GetParamList(v) + ")";
                    prevLine = "";
                }
                return result.TrimStart();
            }
            catch
            {
                return ex.StackTrace;
            }
        }

        private string GetParamList(string traceLine)
        {
            var result = "";
            int start = traceLine.IndexOf('(');
            foreach (var v in traceLine.Substring(start + 1, traceLine.IndexOf(')') - start - 1).Split(","))
            {
                var s = v.Trim();
                if (s.Length > 0) result += (result.Length > 0 ? ", " : "") + s.Substring(0, s.IndexOf(' '));
            }
            return result;
        }
    }
}