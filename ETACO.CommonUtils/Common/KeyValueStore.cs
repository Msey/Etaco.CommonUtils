using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ETACO.CommonUtils
{
    public class KeyValueStore : IEnumerable<KeyValuePair<string, string>>//key, value - '=' и '\n'
    {
        private Dictionary<string, string> Params { get; set; }
        private void check(string str)
        {
            if (!str.IsEmpty()) for (int i = 0, l = str.Length, c = str[i]; i < l; c = str[i++]) if (c == '=' || c == '\n') throw new Exception("KeyValueStore=> contains '\n' or '='");
        }

        public string this[string key]
        {
            get { return Params?.GetValue(key); }
            set { if (value == null) Params?.Remove(key); else { check(key); check(value); if (Params == null) Params = new Dictionary<string, string>(StringComparer.Ordinal); Params[key] = value; } }
        }

        public KeyValueStore Set(KeyValueStore info)
        {
            Params = info.Params == null ? null : new Dictionary<string, string>(info.Params, StringComparer.Ordinal);
            return this;
        }

        public KeyValueStore(string str = null)
        {
            if (!str.IsEmpty())
            {
                Params = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var v in str.Split('\n'))
                {
                    if (v.IsEmpty()) continue;
                    var ind = v.IndexOf('=');
                    if (ind > 0) Params[v.Substring(0, ind)] = v.Substring(ind + 1); else Params[v] = "";
                }
            }
        }

        public override string ToString()
        {
            return Params == null ? string.Empty : string.Join("\n", Params.Select(p => p.Key + "=" + p.Value));
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator(){ return Params.GetEnumerator();}
        IEnumerator IEnumerable.GetEnumerator(){ return Params.GetEnumerator();}
    }
}
