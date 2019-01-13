using System.Collections.Generic;

namespace ETACO.CommonUtils.MetatextParser
{
    /// <summary> Результат работы парсера </summary>
    public class MetatextParseResult
    {
        public readonly List<string> ErrorLog = new List<string>();
        public string Text = "";

        public bool HasError { get { return ErrorLog.Count > 0; } }
        public string LogToString(string separator = "") { return string.Join(separator.IfEmpty(""), ErrorLog); }
    }
}