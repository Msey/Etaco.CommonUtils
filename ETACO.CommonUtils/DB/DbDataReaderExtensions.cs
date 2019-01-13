using System;
using System.Data.Common;

namespace ETACO.CommonUtils
{
    public static class DbDataReaderExtensions
    {
        private static readonly System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.GetCultureInfo("ru");
        internal static object GetDbValue(this DbDataReader dr, int colIndex, int rowIndex, Type type = null, bool isPrimitive = true)
        {
            object v = null;
            try
            {
                try
                {
                    v = dr.GetValue(colIndex);
                }
                catch
                {
                    var sv = dr.GetProviderSpecificValue(colIndex);
                    var t = dr.GetFieldType(colIndex);
                    try
                    {
                        v = Convert.ChangeType(Convert.ToString(sv, ci), t, ci);
                    }
                    catch (FormatException)//если формат возвращаемый базой не совпадает с локальными установками
                    {
                        v = Convert.ChangeType(sv + "", t, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                if (type != null)
                {
                    if (v == DBNull.Value) v = type.GetDefault();//DBNull.Value может вернуться, только из dr.GetValue(colIndex) - не должно быть проблем с возвратом значения
                    else if(!type.IsInstanceOfType(v)) v = isPrimitive ? Convert.ChangeType(v, Nullable.GetUnderlyingType(type) ?? type) : Activator.CreateInstance(type, v);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Can't convert value: {0} to type: {1}\r\nRow: {2} Field: {3}".FormatStr(dr.GetProviderSpecificValue(colIndex),
                            dr.GetFieldType(colIndex), rowIndex, dr.GetName(colIndex)), ex);
            }
            return v;
        }
    }
}
