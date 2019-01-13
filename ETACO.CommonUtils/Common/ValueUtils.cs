using System;
using System.Globalization;

namespace ETACO.CommonUtils
{
    public static class ValueUtils
    {
        private static NumberFormatInfo nfi = new NumberFormatInfo() { NumberDecimalSeparator = ".", NumberGroupSeparator = "" };
        public static string GetString(object val, bool trimEmptyTime = true)
        {
            if (val == null || val == DBNull.Value || string.Empty.Equals(val)) return string.Empty;
            switch (Type.GetTypeCode(val.GetType()))
            {
                case TypeCode.String: return (string)val;
                case TypeCode.Decimal: return ((decimal)val).ToString("G29", nfi);//fast + убераем лишние нули 123,400 => 123,4
                case TypeCode.DateTime: return ((DateTime)val).GetStringFast(false, trimEmptyTime);//fast в 4 раза
            }
            return (val as IFormattable)?.ToString(null, nfi) ?? val.ToString();//fast  - тут G17 не нужен, т.к. 123,400 => 123,4 автоматически для double
        }

        public static object GetValue<T>(string str)//object => boxing!!!
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.SByte:    return (sbyte)str.GetIntFast();//ибо boxing
                case TypeCode.Byte:     return (byte)str.GetIntFast();
                case TypeCode.Int16:    return (short)str.GetIntFast();
                case TypeCode.UInt16:   return (ushort)str.GetIntFast();
                case TypeCode.Int32:    return str.GetIntFast();
                case TypeCode.Int64:    return str.GetLongFast();
                case TypeCode.UInt64:   return (ulong)str.GetLongFast();//!!! может не влезть
                case TypeCode.UInt32:   return (uint)str.GetLongFast();
                case TypeCode.Decimal:  return str.GetDecimalFast();
                case TypeCode.Single:   return (float)str.GetDoubleFast();
                case TypeCode.Double:   return str.GetDoubleFast();
                case TypeCode.DateTime: return str.GetDateTimeFast();//20170101212121
            }
            return str;
        }

        public static object ParseInputValue(string str)//"12.12.2017 21:21:21", "123,12" // только для ввода данных пользователем, для хранения используем число с точкой и даты yyyyMMddHHmmss
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            var x1 = str.IndexOf(':');

            decimal num;//2017.01 - число, а 2017.01.01 - дата
            if (x1 < 0 && str.TryGetDecimalFast(out num)) return num;

            var x2 = str.IndexOf('.');
            var x3 = x2 > 0 ? str.IndexOf('.', x2 + 1) : -1;
            if (str.Length > 4 && x3 > 0 && str.IndexOf('.', x3 + 1) < 0)
            {
                DateTime dt;
                if (str.TryGetDateTimeFast(out dt)) return dt;//sortable format yyyy.MM.dd HH:mm:ss or dd.MM.yyyy HH:mm:ss
                if((x1 < 0 || (x1 > 0 && (str.IndexOf(':', x1 + 1) > x1 + 1))) && DateTime.TryParse(str, out dt)) return dt;//local format G
            }
            return str;
        }
    }
}
