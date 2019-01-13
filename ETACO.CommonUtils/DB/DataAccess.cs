using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace ETACO.CommonUtils
{
    /// <summary> Доступ к базе данных</summary>
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
    public abstract class DataAccess: IDataProvider, IDisposable
    {
        //-------------------------------------
        private readonly Log _log = AppContext.Log;
        private object lockObj = new object();
        protected abstract DbConnection CreateConnection();
        protected abstract void SetSchema(DbConnection connection, string Schema);
        protected abstract bool IsConnectionBroken(DbException ex);
        public abstract IDataCommand CreateCommand(string sql, params object[] param);
        public abstract string GetConnectionString(string server, string database, string login, string password, string schema);
        public abstract DbConnectionStringBuilder CreateConnectionStringBuilder(string connectionString);
        public virtual object[] GetDBList() { return new[] { "" }; }  

        private DbConnection connection;
        public string ConnectionString  {get; protected set;}
        public string Schema { get; protected set; }
        
        public bool ConnectCaching {get; set;}
        public string ConfigSection { get; protected set; }
        public delegate bool GetConnectionInfo(ref string username, ref string password, ref string server, ref string database, string sectionname);
        public GetConnectionInfo OnGetConnectionInfo;
        public Func<Exception, bool> OnException;


        public event Action<DataAccess> OnConnectionOpened;
        public bool IsConnected { get { return connection != null && connection.State != ConnectionState.Closed && connection.State != ConnectionState.Broken; } }

        /// <summary> Создание класса на основе секции в конфигурационном файле </summary>
        /// <param name="configSection">имя секции в конфигурационном файле</param>
        protected DataAccess(string connectionString, string schema, bool connectCaching = true)
        {
            ConfigSection = "";
            ConnectionString = connectionString;
            Schema = schema;
            ConnectCaching = connectCaching;
        }

        /// <summary> Создание класса на основе секции в конфигурационном файле </summary>
        /// <param name="configSection">имя секции в конфигурационном файле</param>
        protected DataAccess(string configSection)
        {
            ConfigSection = configSection;
            var cfg = AppContext.Config;
            Schema = cfg[ConfigSection, "schema"];
            ConnectionString = cfg[ConfigSection, "connectionstring"];
            var server = cfg[ConfigSection, "server", Server];
            var database = cfg[ConfigSection, "database", Database];
            var login = cfg[ConfigSection, "login", User];
            var password = cfg[ConfigSection, "password", Password];

            if (password.IsEmpty())
            {
                try
                {
                    password = cfg[ConfigSection, "encryptpassword", "", true];
                }
                catch (Exception ex)
                {
                    password = "";
                    _log.HandleException(ex);
                }
            }

            ConnectionString = GetConnectionString(server, database, login, password, Schema);
            ConnectCaching = cfg.GetParameter(ConfigSection, "connect_caching", true);
        }
        
        /// <summary> Открытие соединения с учётом передаваемых параметров или уже существующих настроек </summary>
        /// <param name="connectionString">строка соединения</param>
        /// <param name="schema">схема БД</param>
        /// <returns>true - удалось установить соединение, false - иначе</returns>
        public bool OpenConnection(string connectionString = null, string schema = null)
        {
            lock (lockObj)
            {
                //
                ConnectionString = connectionString.IfEmpty(ConnectionString);
                Schema = schema.IfEmpty(Schema);
                //
                int requestPassCount = AppContext.Config.GetParameter(ConfigSection, "requestpasscount", 0);
                var dbLabel = AppContext.Config[ConfigSection, "label"];
                var server = Server;
                var database = Database;
                var login = User;
                var password = Password;

                int startRequestPassCount = requestPassCount;
                do
                {
                    try
                    {
                        CloseConnection();
                        if (AppContext.UserInteractive && (server.IsEmpty() || (login.IsEmpty() && !password.IsEmpty()) || (!login.IsEmpty() && password.IsEmpty()) || requestPassCount != startRequestPassCount))
                        {
                            if (OnGetConnectionInfo!= null?!OnGetConnectionInfo(ref login, ref password, ref server, ref database, ConfigSection):!LoginForm.GetLogin(ref login, ref password, ref server, dbLabel, ConfigSection, GetDBList())) return false;
                        }
                        ConnectionString = GetConnectionString(server, database, login, password, schema);
                        OpenConnection();
                        return true;
                    }
                    catch (DbException oe)
                    {
                        if(OnException == null || !OnException(oe)) _log.HandleException(oe);
                        CloseConnection();
                        server = Server;
                        login = User;
                        password = "";
                        if (!AppContext.UserInteractive || (--requestPassCount < 0)) return false;
                    }
                }
                while (true);
            }
        }

        public bool OpenConnection(Dictionary<string, string> parameters)
        {
            var v = CreateConnectionStringBuilder(ConnectionString);
            foreach (var p in parameters) v[p.Key] = p.Value;
            return OpenConnection(v.ConnectionString, parameters.GetValue("schema"));
        }

        private void OpenConnection()
        {
            if (connection == null) connection = CreateConnection();
            if (connection.State != ConnectionState.Open)
            {
                connection.ConnectionString = ConnectionString;
                connection.Open();
                if (!Schema.IsEmpty()) SetSchema(connection, Schema);
                OnConnectionOpened?.Invoke(this);
            }
        }


        /// <summary> Закрытие соединения </summary>
        public void CloseConnection()
        {
            if (connection != null)
            {
                try { if (connection.State != ConnectionState.Closed) connection.Close(); }
                catch (Exception ex) { if (OnException == null || !OnException(ex)) _log.HandleException(ex); }
                connection = null;
            }
        }

        private T DoAction<T>(Func<DataConnection, T> action)
        {
            lock (lockObj)
            {
                try
                {           
                    OpenConnection();
                    return action(new DataConnection(connection));
                }
                catch (DbException dbe)
                {
                    CloseConnection();
                    if (!IsConnectionBroken(dbe)) throw;
                    //если произошёл разрыв соединения, пытаемся его восстановить
                    try
                    {
                        OpenConnection();
                        return action(new DataConnection(connection));
                    }
                    catch (Exception)
                    {
                        CloseConnection();
                        throw;
                    }
                }
                catch (Exception)
                {
                    CloseConnection();
                    throw;
                }
                finally
                {
                    if (!ConnectCaching)
                    {
                        CloseConnection();
                    }
                }
            }
        }

        /// <summary> Получение результата выполнения запроса </summary>
        public DataTable GetQueryResult(string sql, params object[] param)
        {
            return GetQueryResult((DataCommand)CreateCommand(sql, param));
        }
        
        /// <summary> Получение результата выполнения запроса </summary>
        public DataTable GetQueryResult(DataCommand command)
        {
            return DoAction(command.GetQueryResult);
        }

        /// <summary> Получение списка объектов заполненных результатами выполнения запроса </summary>
        public List<T> GetObjectList<T>(string sql, params object[] param)// where T: new()
        {
            return GetObjectList<T>((DataCommand)CreateCommand(sql, param));
        }

        public List<T> GetObjectList<T>(DataCommand command)// where T : new()
        {
            return DoAction(command.GetObjectList<T>);
        }

        public List<T> GetObjectList<T>(string sql, Func<DataProvider.IDataRow, T> onRow, params object[] param)// where T: new()
        {
            return GetObjectList((DataCommand)CreateCommand(sql, param), onRow);
        }

        public List<T> GetObjectList<T>(DataCommand command, Func<DataProvider.IDataRow, T> onRow)
        {
            return DoAction(dc=>command.GetObjectList(dc, onRow));
        }

        public void JoinVerticalTable(string sql, Action<List<object[]>> onGroup, params object[] param)
        {
            JoinVerticalTable((DataCommand)CreateCommand(sql, param), onGroup);
        }

        public void JoinVerticalTable(DataCommand command, Action<List<object[]>> onGroup)
        {
            DoAction(dc => command.JoinVerticalTable(dc, onGroup));
        }

        public DataCommand ReadObjectList(string sql, Action<DataProvider.IDataRow> onRow, params object[] param)// where T: new()
        {
            return ReadObjectList((DataCommand)CreateCommand(sql, param), onRow);
        }

        public DataCommand ReadObjectList(DataCommand command, Action<DataProvider.IDataRow> onRow)
        {
            return DoAction(dc => command.ReadObjectList(dc, null, onRow));
        }

        public void Read(bool newConn, IDataCommand command, Action<DataProvider.IDataRow> onInit, Action<DataProvider.IDataRow> onRow)
        {
            DoAction(dc => ((DataCommand)command).ReadObjectList(dc, onInit, onRow));
        }

        /// <summary> Получение результата выполнения запроса (одно значение)</summary>
        public object ExecuteScalar(string sql, params object[] param)
        {
            return ExecuteScalar((DataCommand)CreateCommand(sql, param));
        }

        /// <summary> Получение результата выполнения запроса (одно значение)</summary>
        public object ExecuteScalar(DataCommand command, DataConnection dc = null)
        {
            return dc == null ? DoAction(command.ExecuteScalar) : command.ExecuteScalar(dc);
        }

        public object Execute(DataTransaction dt, IDataCommand command)
        {
            var dc = (dt ?? CurrentTransaction)?.Transaction as DataConnection;
            var cmd = (DataCommand)command;
            return dc == null ? DoAction(cmd.ExecuteScalar) : cmd.ExecuteScalar(dc);
        }

        /// <summary> Получение результата выполнения запроса в виде массива (одна строка)</summary>
        public object[] ExecuteRow(string sql, params object[] param)
        {
            return ExecuteRow((DataCommand)CreateCommand(sql, param));
        }

        /// <summary> Получение результата выполнения запроса в виде массива (одна строка)</summary>
        public object[] ExecuteRow(DataCommand command)
        {
            return DoAction(command.ExecuteRow);
        }

        /// <summary> Выполнение команды </summary>
        /// <returns>Список выходных параметров (в данном случае списко пуст)</returns>
        public DataCommand ExecuteNonQuery(string sql, params object[] param)
        {
            return ExecuteNonQuery((DataCommand)CreateCommand(sql, param));
        }

        /// <summary> Выполнение команды </summary>
        /// <returns>Список выходных параметров </returns>
        public DataCommand ExecuteNonQuery(DataCommand command, IDataBulkCommand bulk = null, DataConnection dc = null, bool useTransaction = true)
        {
            if (dc != null) command.ExecuteNonQuery(dc); //вызвано из useTransaction
            else if (bulk != null) bulk.Add(command);
            else if(useTransaction) UseTransaction(ot => command.ExecuteNonQuery(ot));//транзакция нужна для работы с lob
            else command.ExecuteNonQuery(new DataConnection(connection));
            return command;
        }

        public string GetConnectionStringValue(string connectionString, string key, string defaultValue = "")
        {
            if (ConnectionString.IsEmpty()) return defaultValue;
            var v = CreateConnectionStringBuilder(connectionString);
            return v.ContainsKey(key) ? v[key] + "" : defaultValue;
        }

        public virtual string Server { get { return GetConnectionStringValue(ConnectionString, "Data Source"); } }
        public virtual string Database { get { return GetConnectionStringValue(ConnectionString, "Database"); } }
        public virtual string User { get { return GetConnectionStringValue(ConnectionString, "User ID"); } }
        public virtual string Password { get { return GetConnectionStringValue(ConnectionString, "Password"); } }
        public virtual bool IntegratedSecurity { get { return Convert.ToBoolean(GetConnectionStringValue(ConnectionString, "Integrated Security", "false"));}}
        public virtual string ConnectionInfo { get { return User +"@" +Server+"/" + Database + (Schema.IsEmpty()||Schema==User ? "" : (" [" +Schema + "]")); } }
        /// <summary> Выполнение команд в рамках одной транзакции </summary>
        /// <remarks> Используйте OraCommand.ExecuteNonQuery, OraCommand.ExecuteScalar, OraCommand.GetObjectList</remarks>
        public void UseTransaction(Action<DataConnection> action)
        {
            DoAction((oc) =>
            {
                DbTransaction transaction = null;
                try
                {
                    transaction = oc.Connection.BeginTransaction();
                    action(new DataConnection(transaction));
                    if (_log.UseTrace) _log.Trace("Commit");
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    if (_log.UseTrace) _log.Trace("Rollback: " + ex.Message);
                    if (transaction != null) transaction.Rollback();
                    throw;
                }
                return "";
            });
        }
        public virtual string GetErrorCode(Exception ex) { return ex is DbException ? (ex as DbException).ErrorCode + "" : "0"; }

        public void Dispose() { CloseConnection();}
        public DataTransaction CurrentTransaction { get; private set; }
        public DataTransaction BeginTransaction() { return CurrentTransaction = new DataTransaction() { Transaction = new DataConnection(connection.BeginTransaction()) };}
        public void CommitTransaction(DataTransaction dt) { (dt?.Transaction as DataConnection)?.Transaction?.Commit(); CurrentTransaction = null; }
        public void RollbackTransaction(DataTransaction dt) { (dt?.Transaction as DataConnection)?.Transaction?.Rollback(); CurrentTransaction = null; }
    }
}

