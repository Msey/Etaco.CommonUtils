using System.Data.Common;

namespace ETACO.CommonUtils
{
    public class MySQLBulkCommand : DataBulkCommand<MySQLCommand> { }
    /// <summary> Обёртка для MySqlCommand </summary>
    public class MySQLCommand : DataCommand<MySQLCommand>
    {
        private static DbProviderFactory dbFactory = DbProvider.GetProvider("MySql.Data.MySqlClient", "MySqlClientFactory", "MySql.Data.dll");
        public MySQLCommand(string sql, params object[] param) { CreateCommand(dbFactory, sql, param); } 
    }
}