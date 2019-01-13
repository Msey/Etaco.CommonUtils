using System;
using System.Data;

namespace ETACO.CommonUtils
{
    public static class DataTableExtensions
    {
        public static string ToString(this DataTable dt, int len)
        {
            if (dt == null) return "";
            len = Math.Abs(len);
            var f = "{0,-" + len + "}";
            var v = "";
            foreach (var c in dt.Columns) { var x = c+""; v += x.Length == len ? x : x.Center(len); v += '|'; }
            foreach (DataRow r in dt.Rows) { v += "\r\n"; for (var i = 0; i < dt.Columns.Count; i++) v += string.Format(f,r[i]).Substring(0, len) +'|'; }
            return v;
        }
    }
}
