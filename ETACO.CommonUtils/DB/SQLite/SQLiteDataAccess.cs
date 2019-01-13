using System;
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
    public class SQLiteDataAccess : DataAccess //db.OpenConnection("Server=127.0.0.1;Uid=edi4;Pwd=edi4;", "test");
    {
        //if (Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Classes\\Installer\\Dependencies\\{33d1fd90-4274-48a1-9bc1-97e33d9c2d6f}") == null)
        //AppContext.Log.Error("Microsoft Visual C++ 2012 Redistributable (x86) must be installed!");
        private static DbProviderFactory dbFactory = DbProvider.GetProvider("System.Data.SQLite", "SQLiteFactory");//using msvcr100.dll : Detect if Visual C++ Redistributable for Visual Studio 2012 is installed
        //-------------------------------------
        /// <summary> Создание класса на основе секции c именем "msdb" в конфигурационном файле </summary>
        // нужен конструктор по умолчанию т.к. OraDataAccess используется как сервис 
        // конструктор с параметрами по умолчанию не подходит (( 
        public SQLiteDataAccess() : this("sqlite") { }
        public SQLiteDataAccess(string configSection) : base(configSection) { }
        public SQLiteDataAccess(string connectionString, string schema, bool connectCaching = true) : base(connectionString, schema, connectCaching) { }


        protected override DbConnection CreateConnection() { return dbFactory.CreateConnection(); }
        public override IDataCommand CreateCommand(string sql, params object[] param) { return new SQLiteCommand(sql, param); }
        protected override void SetSchema(DbConnection connection, string Schema) { }

        protected override bool IsConnectionBroken(DbException ex) { return false; }
        public override string GetConnectionString(string server, string database, string login, string password, string schema)
        {
            var builder = CreateConnectionStringBuilder(ConnectionString);
            builder["Data Source"] = database;
            builder["UserID"] = login;
            builder["Password"] = password;
            builder["IntegratedSecurity"] = login.IsEmpty() && password.IsEmpty();
            return builder.ConnectionString;
        }

        public override DbConnectionStringBuilder CreateConnectionStringBuilder(string connectionString)
        {
            var builder = dbFactory.CreateConnectionStringBuilder();
            builder.ConnectionString = connectionString;
            return builder;
        }

        public override string Server { get { return "localhost"; } }
        public override string Database { get { return ConnectionString.IsEmpty() ? "" : GetConnectionStringValue(ConnectionString, "Data Source"); } }
        public override string User { get { return Password.IsEmpty() ? "" : Environment.UserName; } }
        public override string ConnectionInfo { get { return Database; } }
        public override string GetErrorCode(Exception ex) { return ex.GetType().Name == "SQLiteException" ? ex._GetProperty("ErrorCode") + "" : base.GetErrorCode(ex); }

        public void ChangePassword(string connectionString, string newPassword, string oldPassword = null)
        {
            var v = CreateConnectionStringBuilder(connectionString);
            v["Password"] = oldPassword;
            using (var conn = CreateConnection())
            {
                conn.ConnectionString = v.ConnectionString;
                conn.Open();
                try
                {
                    conn._Invoke("ChangePassword($)", newPassword);
                }
                catch(Exception ex)
                {
                    if (ex.InnerException?.InnerException!= null&& GetErrorCode(ex.InnerException?.InnerException) == "26") throw new Exception("Old password is incorrect!");
                    throw;
                }
                conn.Close();
            }
        }
    }
}

