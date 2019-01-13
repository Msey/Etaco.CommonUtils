using System.Data.Common;
using System.Data.SqlClient;

namespace ETACO.CommonUtils
{
    /// <summary> Доступ к базе данных MSSQL </summary>
    /// <remarks> используются следующие параметры конфигурационного файла (модуль=db)
    ///     login - имя пользователя (если login и password пусты то IntegratedSecurity=true)
    ///     password - пароль (если login и password пусты то IntegratedSecurity=true)
    ///     encryptpassword - шифрованный пароль (password имеет больший приоритет)
    ///     server - сервер БД
    ///     connectionstring - строка соединения (параметры login, password, server переопределяют соответствующие параметры в connectionstring)
    ///     connect_caching - сохранять соединение
    ///     requestpasscount - количество запросов параметров соединения
    /// </remarks>
    public class MSSQLDataAccess : DataAccess
    {
        //-------------------------------------
        /// <summary> Создание класса на основе секции c именем "msdb" в конфигурационном файле </summary>
        // нужен конструктор по умолчанию т.к. OraDataAccess используется как сервис 
        // конструктор с параметрами по умолчанию не подходит (( 
        public MSSQLDataAccess() : this("msdb") { }
        public MSSQLDataAccess(string configSection) : base(configSection) { }
        public MSSQLDataAccess(string connectionString, string schema, bool connectCaching = true) : base(connectionString, schema, connectCaching){}
        
        
        protected override DbConnection CreateConnection() { return new SqlConnection(); }
        public override IDataCommand CreateCommand(string sql, params object[] param) { return new MSSQLCommand(sql, param); }
        protected override void SetSchema(DbConnection connection, string Schema) { }

        protected override bool IsConnectionBroken(DbException ex) { return false; }
        public override string GetConnectionString(string server, string database, string login, string password, string schema)
        {
            return GetMSSQLConnectionString(server, database, login, password);
        }

        public static string GetMSSQLConnectionString(string server, string database, string login, string password, string connectionString = "")
        {
            var v = new SqlConnectionStringBuilder(connectionString)
                { DataSource = server, UserID = login, Password = password, IntegratedSecurity = login.IsEmpty() && password.IsEmpty() };
            if (!database.IsEmpty()) v.InitialCatalog = database;
            return v.ConnectionString;
        }

        public override DbConnectionStringBuilder CreateConnectionStringBuilder(string connectionString)
        {
            return new SqlConnectionStringBuilder(connectionString);
        }
    }
}

