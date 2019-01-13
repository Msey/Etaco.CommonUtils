using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ETACO.CommonUtils
{
    public class TNSNameInfo
    {
        public string Name;
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();
        public override string ToString() { return Name; }
    }
    
    
    /// <summary> Работа с файлом tnsnames.ora </summary>
    public class TNSNamesReader 
    {
        /// <summary> Получение пути к файлу tnsnames.ora </summary>
        public string TNSNamesPath
        {
            get
            {
                // 1. check TNS_ADMIN environment variable (%TNS_ADMIN%\tnsnames.ora)  8 версия или ранее
                var path = Path.Combine(Environment.GetEnvironmentVariable("TNS_ADMIN") + "", "tnsnames.ora");
                if(File.Exists(path)) return path;

                // 2. check ORACLE_HOME environment variable (%ORACLE_HOME%\network\admin\tnsnames.ora) 9 или ранее
                path = Path.Combine(Environment.GetEnvironmentVariable("ORACLE_HOME") + "", "network\\ADMIN\\tnsnames.ora");
                if (File.Exists(path)) return path;

                // 3. search in registry                                                                начиная с 10
                try
                {
                    var ora = Registry.LocalMachine.OpenSubKey("Software\\Oracle");
                    if (ora != null)
                    {
                        foreach (var key in ora.GetSubKeyNames())
                        {
                            if (key.IsMatch("HOME*") || key.IsMatch("KEY_*"))
                            {
                                path = Path.Combine(ora.OpenSubKey(key).GetValue("ORACLE_HOME") + "", "network\\ADMIN\\tnsnames.ora");
                                if (File.Exists(path)) return path;
                            }
                        }
                    }
                }
                catch { }
                
                // 4. search in environment variable path to 'oracle bin' (last chance) 
                foreach (Match match in new Regex("[a-zA-Z]:\\\\[a-zA-Z0-9\\\\]*(oracle|app)[a-zA-Z0-9_.\\\\]*(?=bin)").Matches(Environment.GetEnvironmentVariable("Path")))
                {
                    path = match + "network\\ADMIN\\tnsnames.ora";
                    if (File.Exists(path)) return path;
                }
                return "";
            }
        }

        /// <summary> Получение списка tnsnames </summary>
        public List<TNSNameInfo> GetTNSNamesInfo(string tnsNamesPath = null)
        {
            tnsNamesPath = tnsNamesPath.IfEmpty(TNSNamesPath);
            var result = new List<TNSNameInfo>();
      
            if (File.Exists(tnsNamesPath))
            {
                var text = File.ReadAllText(tnsNamesPath);  
                int index = -1;
                TNSNameInfo info = null;
                
                foreach (Match m in Regex.Matches("\n" + text, @"[\n][\s]*[^\(][a-zA-Z0-9_.]+[\s]*"))
                {
                    if (index != -1)
                    {
                        foreach (Match pm in Regex.Matches(text.Substring(index, m.Index - index), @"\((?>[^()]*|\{(?<DEPTH>)|\)(?<-DEPTH>))*(?(DEPTH)(!?))\)"))
                        {
                            var param = pm.Value.Split("=");
                            if (param.Length == 2)
                            {
                                info.Parameters[param[0].Substring(1).Trim()] = param[1].Substring(0, param[1].Length - 1).Trim();
                            }
                        }
                    }
                    var tns = m.Value.Trim();
					if (tns.StartsWith("#", StringComparison.Ordinal))
						index = -1;
					else {
						index = m.Index;
						info = new TNSNameInfo() { Name = tns };
						result.Add(info);
					}
                }
            }
            return result;
        }
    }
}