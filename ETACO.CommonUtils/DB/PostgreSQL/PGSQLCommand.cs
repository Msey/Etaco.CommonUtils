using System.Data.Common;

namespace ETACO.CommonUtils
{
    public class PGSQLBulkCommand : DataBulkCommand<PGSQLCommand> { }
    /// <summary> Обёртка для MySqlCommand </summary>
    public class PGSQLCommand : DataCommand<PGSQLCommand>
    {
        private static DbProviderFactory dbFactory = DbProvider.GetProvider("Npgsql", "NpgsqlFactory");
        public PGSQLCommand(string sql, params object[] param) { CreateCommand(dbFactory, sql, param); }
    }
}