using System.Data.Common;

namespace ETACO.CommonUtils
{
    public class OleDBBulkCommand : DataBulkCommand<OleDBCommand> { }
    /// <summary> Обёртка для OleDBCommand </summary>
    public class OleDBCommand : DataCommand<OleDBCommand>
    {
        private static DbProviderFactory dbFactory =  DbProviderFactories.GetFactory("System.Data.OleDb");
        public OleDBCommand(string sql, params object[] param) { CreateCommand(dbFactory, sql, param); } 
    }  
}