using System;
using System.Collections.Generic;

namespace ETACO.CommonUtils
{
    public interface IDataProvider
    {
        bool OpenConnection(Dictionary<string, string> parameters);
        bool IsConnected { get; }
        string ConnectionInfo { get; }
        void CloseConnection();
        DataTransaction BeginTransaction();
        DataTransaction CurrentTransaction { get; }
        void CommitTransaction(DataTransaction dt);
        void RollbackTransaction(DataTransaction dt);
        IDataCommand CreateCommand(string command, params object[] prms);//эффективно при большом обновлении, не пересоздавать команду, а переустанавливать её параметры
        void Read(bool newConn, IDataCommand command, Action<DataProvider.IDataRow> onInit, Action<DataProvider.IDataRow> onRow);
        object Execute(DataTransaction dt, IDataCommand command);
        string GetErrorCode(Exception ex);
    }

    public interface IDataCommand
    {
        string Sql { get; }
        IDataCommand AddIn(object val, string key = null);
        IDataCommand AddOut(Type t, string key = null);
        IDataCommand ClearParams();
    }

    public class BaseDataCommand : IDataCommand
    {
        public string Sql { get; set; }
        public readonly Dictionary<string, object> Parameters = new Dictionary<string, object>();
        private int pIndex = 0;
        public virtual IDataCommand AddIn(object val, string key = null)
        {
            Parameters.Add(key.IsEmpty() ? "{p" + (pIndex++) + "}" : key, val);
            return this;
        }
        public virtual IDataCommand AddOut(Type t, string key = null) { return this; }
        public virtual IDataCommand ClearParams() { Parameters.Clear(); pIndex = 0; return this; }
    }

    public class DataTransaction//сугубо для типизации параметров, иначемогут быть ошибки в процессе написания кода
    {
        public object Transaction;
    }

    public abstract class DataProvider : IDataProvider
    {
        public virtual bool OpenConnection(Dictionary<string, string> parameters) { return true; }
        public virtual bool IsConnected { get { return true; } }
        public virtual void CloseConnection() { }
        public virtual DataTransaction BeginTransaction() { return null; }
        public virtual DataTransaction CurrentTransaction { get { return null; } }
        public virtual void CommitTransaction(DataTransaction dt) { }
        public virtual void RollbackTransaction(DataTransaction dt) { }
        public virtual IDataCommand CreateCommand(string command, params object[] prms)
        {
            var v = new BaseDataCommand() { Sql = command };
            foreach (var p in prms) v.AddIn(p);
            return v;
        }
        public abstract void Read(bool newConn, IDataCommand command, Action<IDataRow> onInit, Action<IDataRow> onRow);
        public abstract object Execute(DataTransaction dt, IDataCommand command);
        public abstract string ConnectionInfo { get; }
        public string GetErrorCode(Exception ex) { return ex.HResult.ToString(); }

        public interface IDataRow
        {
            object Get(int index);//boxing!!!
            bool GetBool(int index);
            int GetInt(int index);
            long GetLong(int index);//for nullable use r.IsNull(i) ? null : (long?)r.GetLong(i)
            decimal GetDecimal(int index);
            string GetString(int index);
            DateTime GetDateTime(int index);
            byte[] GetBytes(int index);
            string GetFieldName(int index);
            int GetFieldIndex(string name);
            Type GetFieldType(int index);
            int FieldCount { get; }
            int RowIndex { get; }//for columns  is -1
            string Name { get; }
            bool IsNull(int index);
        }
    }

    public static class DataProviderHelper
    {
        public static List<T> Read<T>(this IDataProvider dp, bool newConn, IDataCommand command, Func<DataProvider.IDataRow, T> onRow)
        {
            var v = new List<T>(); dp.Read(newConn, command, null, x => v.Add(onRow(x))); return v;
        }

        public static List<T> Read<T>(this IDataProvider dp, bool newConn, string command, Func<DataProvider.IDataRow, T> onRow, params object[] prms)
        {
            var v = new List<T>(); dp.Read(newConn, dp.CreateCommand(command, prms), null, x => v.Add(onRow(x))); return v;
        }

        public static object Execute(this IDataProvider dp, DataTransaction dt, string command, params object[] prms)
        {
            return dp.Execute(dt, dp.CreateCommand(command, prms));
        }

        public static void Execute<T>(this IDataProvider dp, DataTransaction dt, string command, List<T> prms, Action<IDataCommand,T> prepare, Action<object, T> onResult)
        {
            var cmd = dp.CreateCommand(command);
            foreach(var p in prms)
            {
                cmd.ClearParams();
                prepare?.Invoke(cmd, p);
                var v = dp.Execute(dt, cmd);
                onResult?.Invoke(v, p);
            }
        }

        public static TableList<T> GetTableList<T>(this IDataProvider dp, bool newConn, IDataCommand command, int capacity = 0)
        {
            TableList<T> result = null;
            dp.Read(newConn, command, x => { var tlc = new TLColumnInfo[x.FieldCount]; for (var i = 0; i < tlc.Length; i++) tlc[i] = new TLColumnInfo(x.GetFieldName(i), x.GetFieldType(i)); result = new TableList<T>(tlc) { Capacity = capacity}; }
           , x => { var v = result.Load((T)x.Get(0)); for (var i = 1; i < v.Length; i++) v[i] = x.Get(i); });
            return result;
        }

        public static TableList<T> GetTableList<T>(this IDataProvider dp, int capacity, bool newConn, string command, params object[] prms)
        {
            return dp.GetTableList<T>(newConn, dp.CreateCommand(command, prms), capacity);
        }

        public static void UseTransaction(this IDataProvider dp, Action<DataTransaction> action, DataTransaction dt = null)
        {
            DataTransaction _dt = null;
            try
            {
                _dt = dt ?? dp.BeginTransaction();
                action(_dt);
                if (dt == null)
                {
                    if (AppContext.Log.UseTrace) AppContext.Log.Trace("Commit");
                    dp.CommitTransaction(_dt);
                }
            }
            catch (Exception ex)
            {
                if (dt == null)
                {
                    if (AppContext.Log.UseTrace) AppContext.Log.Trace("Rollback: " + ex.Message);
                    if (_dt != null) dp.RollbackTransaction(_dt);
                }
                throw;
            }
        }
    }

    public static class DataRowHelper
    {
        public static bool? GetBoolOrNull(this DataProvider.IDataRow row, int index) { return row.IsNull(index) ? null : (bool?)row.GetBool(index); }
        public static int? GetIntOrNull(this DataProvider.IDataRow row, int index) { return row.IsNull(index) ? null : (int?)row.GetInt(index); }
        public static long? GetLongOrNull(this DataProvider.IDataRow row, int index) { return row.IsNull(index) ? null : (long?)row.GetLong(index); }
        public static decimal? GetDecimalOrNull(this DataProvider.IDataRow row, int index) { return row.IsNull(index) ? null : (decimal?)row.GetDecimal(index); }
        public static DateTime? GetDateTimeOrNull(this DataProvider.IDataRow row, int index) { return row.IsNull(index) ? null : (DateTime?)row.GetDateTime(index); }
    }

    public static class DataCommandHelper
    {
        public static IDataCommand AddParams(this IDataCommand cmd, params object[] prms) { foreach (var p in prms) cmd.AddIn(p); return cmd; }
    }
}
