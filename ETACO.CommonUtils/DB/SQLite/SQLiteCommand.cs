using System.Data.Common;

namespace ETACO.CommonUtils
{
    /*var sda = new SQLiteDataAccess("Data Source = test.db3;Password=42;", ""); 
      sda.ExecuteNonQuery("create table highscores(name varchar(20), score int)");
      sda.ExecuteNonQuery("insert into highscores (name, score) values ('Me', 9001)");
      sda.GetQueryResult("select * from highscores where name = @p0","Me")*/
    public class SQLiteBulkCommand : DataBulkCommand<SQLiteCommand> { }
    /// <summary> Обёртка для MySqlCommand </summary>
    public class SQLiteCommand : DataCommand<SQLiteCommand>
    {
        private static DbProviderFactory dbFactory = DbProvider.GetProvider("System.Data.SQLite", "SQLiteFactory");
        public SQLiteCommand(string sql, params object[] param) { CreateCommand(dbFactory, sql, param); }
        protected override char CommandPrefix { get { return ':'; } }//для минимизации изменений в командах sql (sqlite поддержкивает оба префикса @/:, но используем :)
    }
}