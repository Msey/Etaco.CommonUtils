using System;
using System.Data.Common;

namespace ETACO.CommonUtils
{
    /// <summary> Доступ к базе данных PostgreSQL </summary>
    /// <remarks> используются следующие параметры конфигурационного файла (модуль=db)
    ///     login - имя пользователя (если login и password пусты то IntegratedSecurity=true)
    ///     password - пароль (если login и password пусты то IntegratedSecurity=true)
    ///     encryptpassword - шифрованный пароль (password имеет больший приоритет)
    ///     server - сервер БД
    ///     connectionstring - строка соединения (параметры login, password, server переопределяют соответствующие параметры в connectionstring)
    ///     connect_caching - сохранять соединение
    ///     requestpasscount - количество запросов параметров соединения
    /// </remarks>
    public class PGSQLDataAccess : DataAccess //db.OpenConnection("Server=127.0.0.1;Port=5432;Database=db;User Id=etm;Password=etm;", "test")
    {
        private static DbProviderFactory dbFactory = DbProvider.GetProvider("Npgsql", "NpgsqlFactory");
        //-------------------------------------
        /// <summary> Создание класса на основе секции c именем "pgsqldb" в конфигурационном файле </summary>
        public PGSQLDataAccess() : this("pgsqldb") { }
        public PGSQLDataAccess(string configSection) : base(configSection) { }
        public PGSQLDataAccess(string connectionString, string schema, bool connectCaching = true) : base(connectionString, schema, connectCaching) { }


        protected override DbConnection CreateConnection() { return dbFactory.CreateConnection();}
        public override IDataCommand CreateCommand(string sql, params object[] param) { return new PGSQLCommand(sql, param); }
        protected override void SetSchema(DbConnection connection, string Schema)
        {
            new PGSQLCommand("SET search_path TO {0}, public;".FormatStr(Schema)).ExecuteNonQuery(new DataConnection(connection));   
        }

        protected override bool IsConnectionBroken(DbException ex) { return false; }
        public static string GetPGSQLConnectionString(string server, string database, string login, string password, string connectionString = "")
        {
            var builder = dbFactory.CreateConnectionStringBuilder();
            builder.ConnectionString = connectionString;
            var srv = server.Split(':');
            builder["Server"] = srv[0];
            builder["Port"] = srv.Length > 1 ? srv[1] : "5432";
            builder["UserID"] = login;
            builder["Password"] = password;
            builder["IntegratedSecurity"] = login.IsEmpty() && password.IsEmpty();
            if (!database.IsEmpty()) builder["Database"] = database;

            return builder.ConnectionString;
        }

        public override string GetConnectionString(string server, string database, string login, string password, string schema)
        {
            return GetPGSQLConnectionString(server, database, login, password, ConnectionString);
        }

        public override DbConnectionStringBuilder CreateConnectionStringBuilder(string connectionString)
        {
            var builder = dbFactory.CreateConnectionStringBuilder();
            builder.ConnectionString = ConnectionString;
            return builder;
        }

        public override string Server { get { return ConnectionString.IsEmpty() ? "" : CreateConnectionStringBuilder(ConnectionString)["Server"]+""; } }
        public int Port { get { return ConnectionString.IsEmpty() ? -1 : (int)CreateConnectionStringBuilder(ConnectionString)["Port"]; } }
        public override string GetErrorCode(Exception ex) { return ex.GetType().Name == "PostgresException" ? ex._GetProperty("SqlState") + "" : base.GetErrorCode(ex); }
    }
}

