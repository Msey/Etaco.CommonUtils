using System.Data.Common;

namespace ETACO.CommonUtils
{
    /// <summary> Доступ к базе данных MySQL </summary>
    /// <remarks> используются следующие параметры конфигурационного файла (модуль=db)
    ///     login - имя пользователя (если login и password пусты то IntegratedSecurity=true)
    ///     password - пароль (если login и password пусты то IntegratedSecurity=true)
    ///     encryptpassword - шифрованный пароль (password имеет больший приоритет)
    ///     server - сервер БД
    ///     connectionstring - строка соединения (параметры login, password, server переопределяют соответствующие параметры в connectionstring)
    ///     connect_caching - сохранять соединение
    ///     requestpasscount - количество запросов параметров соединения
    /// </remarks>
    public class MySQLDataAccess : DataAccess //db.OpenConnection("Server=127.0.0.1;Uid=edi4;Pwd=edi4;", "test");
    {
        private static DbProviderFactory dbFactory = DbProvider.GetProvider("MySql.Data.MySqlClient", "MySqlClientFactory", "MySql.Data.dll");
        //-------------------------------------
        /// <summary> Создание класса на основе секции c именем "msdb" в конфигурационном файле </summary>
        // нужен конструктор по умолчанию т.к. OraDataAccess используется как сервис 
        // конструктор с параметрами по умолчанию не подходит (( 
        public MySQLDataAccess() : this("mysqldb") { }
        public MySQLDataAccess(string configSection) : base(configSection) { }
        public MySQLDataAccess(string connectionString, string schema, bool connectCaching = true) : base(connectionString, schema, connectCaching){}


        protected override DbConnection CreateConnection() { return dbFactory.CreateConnection();}
        public override IDataCommand CreateCommand(string sql, params object[] param) { return new MySQLCommand(sql, param); }
        protected override void SetSchema(DbConnection connection, string Schema) { }

        protected override bool IsConnectionBroken(DbException ex) { return false; }
        public override string GetConnectionString(string server, string database, string login, string password, string schema)
        {
            var builder = CreateConnectionStringBuilder(ConnectionString);
            builder["Server"] = server;
            builder["UserID"] = login;
            builder["Password"] = password;
            builder["IntegratedSecurity"] = login.IsEmpty() && password.IsEmpty();
            if(!database.IsEmpty()) builder["Database"]= database;
            return builder.ConnectionString;
        }

        public override DbConnectionStringBuilder CreateConnectionStringBuilder(string connectionString)
        {
            var builder = dbFactory.CreateConnectionStringBuilder();
            builder.ConnectionString = ConnectionString;
            return builder;
        }

        public override string Server { get { return ConnectionString.IsEmpty() ? "" : CreateConnectionStringBuilder(ConnectionString)["Server"]+""; } }
    }
}

