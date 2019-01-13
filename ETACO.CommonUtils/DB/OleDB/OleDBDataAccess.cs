using System.Data.Common;

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
    public class OleDBDataAccess : DataAccess //db.OpenConnection("Server=127.0.0.1;Uid=edi4;Pwd=edi4;", "test");
    {
        private static DbProviderFactory dbFactory = DbProviderFactories.GetFactory("System.Data.OleDb");
        //-------------------------------------
        /// <summary> Создание класса на основе секции c именем "msdb" в конфигурационном файле </summary>
        // нужен конструктор по умолчанию т.к. OraDataAccess используется как сервис 
        // конструктор с параметрами по умолчанию не подходит (( 
        public OleDBDataAccess() : this("oledb") { }
        public OleDBDataAccess(string configSection) : base(configSection) { }
        public OleDBDataAccess(string connectionString, string schema, bool connectCaching = true) : base(connectionString, schema, connectCaching) { }


        protected override DbConnection CreateConnection() { return dbFactory.CreateConnection();}
        public override IDataCommand CreateCommand(string sql, params object[] param) { return new OleDBCommand(sql, param); }
        protected override void SetSchema(DbConnection connection, string Schema) { }

        protected override bool IsConnectionBroken(DbException ex) { return false; }
        public override string GetConnectionString(string server, string database, string login, string password, string schema)
        {
            var builder = CreateConnectionStringBuilder(ConnectionString);
            builder["Data Source"] = database;
            builder["User ID"] = login;
            builder["Password"] = password;
            //builder["IntegratedSecurity"] = login.IsEmpty() && password.IsEmpty();
            //if(!database.IsEmpty()) builder["Database"]= database;
            return builder.ConnectionString;
        }

        public override DbConnectionStringBuilder CreateConnectionStringBuilder(string connectionString)
        {
            var builder = dbFactory.CreateConnectionStringBuilder();
            builder.ConnectionString = ConnectionString;
            return builder;
        }
    }
}

