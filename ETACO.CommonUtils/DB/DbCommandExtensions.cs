using System.Data;
using System.Data.Common;

namespace ETACO.CommonUtils
{
    static class DbCommandExtensions
    {
        public static DbParameter AddParameterWithValue(this DbCommand command, string name, object value)
        {
            var v = command.CreateParameter();
            v.ParameterName = name;
            v.Value = value;
            command.Parameters.Add(v);
            return v;
        }

        public static DbParameter AddParameter(this DbCommand command, string name, DbType type, int size = 0)
        {
            var v = command.CreateParameter();
            v.ParameterName = name;
            v.DbType = type;
            //v.DbType = DbType.Object;
            if(size != 0) v.Size = size;
            command.Parameters.Add(v);
            return v;
        }
    }
}
