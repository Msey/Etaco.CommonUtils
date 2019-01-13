using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ETACO.CommonUtils.MetatextParser
{
    /// <summary> Описатель макроса </summary>
    public class MacrosInfo : IEquatable<MacrosInfo>
    {
        private static readonly Regex regex = new Regex(@"(?<field>(\w(\w|\d|_)*\.)?\w(\w|\d|_)*)\s*((?<operator>=)\s*((?<value>\{#(?>[^{}]+|\{(?<DEPTH>)|\}(?<-DEPTH>))*(?(DEPTH)(!?))\}\S*)|(?<value>[^']\S*)|(?<value>'([^']|'')*')))", RegexOptions.Compiled);
        public string Name { get; private set; }
        public readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();

        public MacrosInfo(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            var result = Name;
            if (Parameters.Count != 0)
            {
                result += "(";
                foreach (var kvp in Parameters) result += kvp.Key + "=" + kvp.Value + " "; ;
                result = result.Trim() + ")";
            }
            return result;
        }

        /// <summary> Получить описатель макроса из строки </summary>
        public static MacrosInfo GetMacrosInfo(string macrosString, Func<string, string> parse)
        {
            if (macrosString.IsEmpty()) throw new ArgumentException("Macros string is empty");
            macrosString = macrosString.Trim();
            int startIndex = macrosString.IndexOf('(');

            if (macrosString[macrosString.Length - 1] == ')' && startIndex > 0)
            {
                try
                {
                    var mi = new MacrosInfo(macrosString.Substring(0, startIndex));
                    var paramString = macrosString.Substring(startIndex + 1, macrosString.Length - startIndex - 2);
                    foreach (Match match in regex.Matches(paramString))
                    {
                        var value = match.Groups["value"] + "";
                        if (value.StartsWith("'", StringComparison.Ordinal)) value = value.Substring(1);
                        if (value.EndsWith("'", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 1);
                        mi.Parameters.Add(match.Groups["field"] + "", parse(value));
                    }
                    return mi;
                }
                catch (Exception ex)
                {
                    throw new Exception("Incorrect macros string format: '{0}' (error='{1}')".FormatStr(macrosString, ex.Message));
                }
            }
            else
            {
                return new MacrosInfo(macrosString);
            }
        }

        public bool Equals(MacrosInfo mi)
        {
            if (mi == null || !Name.Equals(mi.Name) || Parameters.Count != mi.Parameters.Count) return false;
            foreach (var pair in Parameters)
            {
                if (mi.Parameters[pair.Key] != pair.Value) return false;
            }
            return true;
        }
    }
}