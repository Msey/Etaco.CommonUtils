using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OracleClient;

//Oracle client deprecated in .NET 4.0
#pragma warning disable 618

namespace ETACO.CommonUtils
{
    /// <summary> Доступ к базе данных Oracle </summary>
    /// <remarks> используются следующие параметры конфигурационного файла (модуль=db)
    ///     login - имя пользователя (если login и password пусты то IntegratedSecurity=true)
    ///     password - пароль (если login и password пусты то IntegratedSecurity=true)
    ///     encryptpassword - шифрованный пароль (password имеет больший приоритет)
    ///     server - сервер БД
    ///     connectionstring - строка соединения (параметры login, password, server переопределяют соответствующие параметры в connectionstring)
    ///     schema - схема Oracle
    ///     connect_caching - сохранять соединение
    ///     requestpasscount - количество запросов параметров соединения
    ///     label - метка сервера БД
    ///     next_id - запрос возвращающий следующий id
    /// </remarks>
    [System.Diagnostics.DebuggerDisplay("{User}@{Server}")]
    public class OraDataAccess: DataAccess
    {
        public string NLSLang { get; private set; }
        //-------------------------------------
        public OraDataAccess() : this("db") {}
        public OraDataAccess(string configSection) : base(configSection) {}
        public OraDataAccess(string connectionString, string schema, bool connectCaching = true) : base(connectionString, schema, connectCaching){}
        
        protected override DbConnection CreateConnection() { return new OracleConnection(); }
        public override IDataCommand CreateCommand(string sql, params object[] param) { return new OraCommand(sql, param); }
        protected override void SetSchema(DbConnection connection, string Schema) //ALTER SESSION SET NLS_LANGUAGE= 'AMERICAN_AMERICA.CL8MSWIN1251'
        {
            using (var q = new OracleCommand("alter session set current_schema=" + Schema, (OracleConnection)connection))
            {
                try
                {
                    q.ExecuteNonQuery();
                }
                catch (OracleException oe)
                {
                    if (oe.Code == 1435) throw new Exception("Schema = {0} not exist".FormatStr(Schema));
                    throw;
                }
            }
        }

        protected override bool IsConnectionBroken(DbException ex) 
        { 
            var oe = (OracleException)ex;
            return oe.Code == 28 || oe.Code == 3113 || oe.Code == 12170; 
        }

        public static string GetORAConnectionString(string server, string login, string password, string shema, string connectionString="")
        {
            return new OracleConnectionStringBuilder(connectionString) { DataSource = server, UserID = login, Password = password,
                IntegratedSecurity = login.IsEmpty() && password.IsEmpty() }.ConnectionString;
        }

        /// <summary> Получение строки соединения на основе передаваемых параметров и уже имеющихся настроек </summary>
        public override string GetConnectionString(string server, string database, string login, string password, string shema)
        {
            return GetORAConnectionString(server, login, password, shema, ConnectionString);
        }

        public override DbConnectionStringBuilder CreateConnectionStringBuilder(string connectionString)
        {
            return new OracleConnectionStringBuilder(connectionString);
        }

        public static object[] GetORADBList()
        {
            return new TNSNamesReader().GetTNSNamesInfo().ToArray();
        }

        public override object[] GetDBList()
        {
            return GetORADBList();
        }

        private const int MAGIC_BLOB_LEN = 32000;
        public static List<byte[]> SplitBlob(byte[] blob)
        {
            if (blob == null || blob.Length <= MAGIC_BLOB_LEN) return new List<byte[]>() { blob };
            var result = new List<byte[]>();
            for (int pos = 0; pos < blob.Length; pos += MAGIC_BLOB_LEN)
            {
                int len = Math.Min(MAGIC_BLOB_LEN, blob.Length - pos);
                var buff = new byte[len];
                Array.Copy(blob, pos, buff, 0, buff.Length);
                result.Add(buff);
            }
            return result;
        }

        public override string GetErrorCode(Exception ex) { return ex is OracleException ? (ex as OracleException).Code + "" : base.GetErrorCode(ex); }
    }
}

