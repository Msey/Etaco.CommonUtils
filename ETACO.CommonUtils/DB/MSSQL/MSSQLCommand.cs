using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace ETACO.CommonUtils
{
    public class MSSQLBulkCommand : DataBulkCommand<MSSQLCommand> { }
    /// <summary> Обёртка для SqlCommand </summary>
    public class MSSQLCommand : DataCommand<MSSQLCommand>
    { 
        public MSSQLCommand(string sql, params object[] param) 
        {
            Command = new SqlCommand(sql + "");
            if (param != null) { for (int i = 0; i < param.Length; i++) { AddIn(param[i]); } }
        }

        /// <summary> Добавление входного параметра </summary>
        /// <param name="val">значение параметра </param>
        /// <param name="key">имя параметра (по умолчанию: 'p'+число входных параметров)</param>
        /// <remarks>если val=null - вывод типа невозможен и ставится AnsiString со значением DBNull.Value</remarks>
        public override MSSQLCommand AddIn(object val, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p") : key; //IfEmpty не use т.к. параметр сразу приводится к строке и (++) вычисляется
            if (val is string)
            {
                var v = val + "";
                if (v.Length > 7999) AddInClob(v, key); else Command.AddParameterWithValue(CommandPrefix + key, v);   //RPCFUNC+Oracle => 32512 байтов, для теста use string.Join("*", new string[32513])
            }
            else if (val is byte[])
            {
                AddInBlob((byte[])val, key);
            }
            else
            {
                Command.AddParameterWithValue(CommandPrefix + key, val ?? DBNull.Value);
            }
            return this;
        }

        /// <summary> Добавление входного параметра типа Blob</summary>
        /// <param name="val">значение параметра </param>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_blob'+число входных параметров типа blob)</param>
        public MSSQLCommand AddInBlob(byte[] val, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p_blob") : key;
            var p = (SqlParameter)Command.AddParameter(CommandPrefix + key, DbType.Binary, val.Length);
            p.SqlDbType = SqlDbType.Image;
            p.Value = val;
            return this;
        }

        /// <summary> Добавление входного параметра типа Clob</summary>
        /// <param name="val">значение параметра </param>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_clob'+число входных параметров типа clob)</param>
        public MSSQLCommand AddInClob(string val, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p_clob") : key;
            var p = (SqlParameter)Command.AddParameter(CommandPrefix + key, DbType.AnsiString);
            p.SqlDbType = SqlDbType.Text;
            p.Value = val;
            return this;
        }

        /// <summary> Добавление выходного параметра </summary>
        /// <param name="t">тип выходного параметра </param>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_out'+число выходных параметров) </param>
        /// <remarks> Для blob используется тип byte[]</remarks>
        public override MSSQLCommand AddOut(Type t, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p_out") : key;
            if (t == typeof(string))
            {
                Command.AddParameter(CommandPrefix + key, DbType.AnsiString, -1).Direction = ParameterDirection.Output;
            }
            else if (t == typeof(byte[]))
            {
                var p = (SqlParameter)Command.AddParameter(CommandPrefix + key, DbType.Binary);
                p.SqlDbType = SqlDbType.Image;
                p.Direction = ParameterDirection.Output;
            }
            else
            {
                Command.AddParameterWithValue(CommandPrefix + key, t.GetDefault()).Direction = ParameterDirection.Output;
            }
            return this;
        }

        protected DbParameter CreateOutParameter(Type t, string key)
        {
            key = key.IsEmpty() ? CreateParamName("p_out") : key;
            return Command.AddParameterWithValue(key, t.GetDefault());
        }
    }

    //для вызова процедуры, а не функции T = DBNull
    public class MSSQLCommand<T> : MSSQLCommand
    {
        public MSSQLCommand(string sql, params object[] param) : base(sql, param) { }
        //v = AppContext.ORA.ExecuteScalar(new OraCommand<decimal>("RBS_SERV.Create_GSV").AddIn("xxx", "p_name"));
        public override object ExecuteScalar(DataConnection dc)
        {
            var v = typeof(T) == typeof(DBNull) ? null : CreateOutParameter(typeof(T), "v_v");
            if (v != null) v.Direction = ParameterDirection.ReturnValue;
            try
            {
                Command.CommandType = CommandType.StoredProcedure;
                ExecuteNonQuery(dc);
                return v == null ? null : v.Value;
            }
            finally
            {
                Command.CommandType = CommandType.Text;
                if (v != null) Command.Parameters.Remove(v);
            }
        }
    }
}