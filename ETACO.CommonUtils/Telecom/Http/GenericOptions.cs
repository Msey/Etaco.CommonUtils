using System.Collections.Generic;

namespace ETACO.CommonUtils.Telecom.Http
{
    /// <summary>key value pairs of URI query parameters</summary>
    public class GenericOptions
    {
        protected List<KeyValuePair<string, object>> pa = new List<KeyValuePair<string, object>>();
        public GenericOptions SetQuery(string name, object value) { pa.Add(new KeyValuePair<string, object>(name, value)); return this; }
        public virtual List<KeyValuePair<string, object>> ToQueryList() { return pa;}
    }
}
