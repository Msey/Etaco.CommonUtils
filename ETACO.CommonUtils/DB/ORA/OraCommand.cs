using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OracleClient;

//Oracle client deprecated in .NET 4.0
#pragma warning disable 618

namespace ETACO.CommonUtils
{
    public class OraBulkCommand : DataBulkCommand<OraCommand> { }
    /// <summary> Обёртка для OracleCommand (OracleType.Blob и OracleType.Clob - требует особой обработки)</summary>
    [System.Diagnostics.DebuggerDisplay("{Description}")]
    public class OraCommand: DataCommand<OraCommand>
    {
        protected override char CommandPrefix { get { return ':'; } }
        protected override string GetParamName(string key) { return key; }

        private Dictionary<string, byte[]> inBlobs = new Dictionary<string, byte[]>();
        protected override void BeforeExecuteNonQuery(DataConnection dc)
        {
            foreach (OracleParameter op in Command.Parameters)
                if (op.OracleType == OracleType.Blob && op.Direction == ParameterDirection.Input)
                    op.Value = CreateBlob(dc, inBlobs.GetValue(op.ParameterName, null));
        }

        protected override void AfterExecuteNonQuery(DataConnection dc)
        {
            foreach (OracleParameter op in Command.Parameters)
            {
                if (op.Direction == ParameterDirection.Output)
                {
                    if (op.OracleType == OracleType.Blob)
                    {
                        var buff = inBlobs.GetValue(op.ParameterName, null);
                        var lob = (OracleLob)op.Value;
                        if (buff != null) lob.Write(buff, 0, buff.Length); //является входным
                        else op.Value = lob.IsNull ? new byte[0] : lob.ReadToEnd();
                    }
                    else if (op.OracleType == OracleType.Clob)
                    {
                        if (op.Value == DBNull.Value) op.Value = "";
                        else
                        {
                            var lob = ((OracleLob)op.Value);
                            op.Value = lob.IsNull ? "" : System.Text.Encoding.Unicode.GetString(lob.ReadToEnd());
                        }
                    }
                    else if (op.OracleType == OracleType.Cursor)
                    {
                        var dr = (OracleDataReader)op.Value;
                        var dt = new DataTable();
                        op.Value = dt;
                        try
                        {
                            var fCount = dr.FieldCount;
                            for (int col = 0; col < fCount; col++)
                            {
                                var colName = dr.GetName(col).ToUpper();
                                var cn = colName;
                                for (int i = 1; dt.Columns.Contains(cn); i++) { cn = colName + "_" + i; }
                                dt.Columns.Add(cn, dr.GetFieldType(col));
                            }
                            var rowIndex = 0;
                            while (dr.Read())
                            {
                                var row = dt.NewRow();
                                for (int i = 0; i < fCount; i++) { row[i] = dr.GetDbValue(i, rowIndex++); }
                                dt.Rows.Add(row);
                            }
                            dt.AcceptChanges();
                        }
                        finally
                        {
                            dr.Close();
                        }
                    }
                }
            }
        }

        public OraCommand(string sql, params object[] param)
        {
            Command = new OracleCommand(PrepareSQL(sql));
            if (param != null) { for (int i = 0; i < param.Length; i++) { AddIn(param[i]); } }
        }

        public override string Sql
        {
            set { Command.CommandText = PrepareSQL(value); }
        }

        private string PrepareSQL(string sql)
        {
            return (sql + "").Replace("\r\n", "\n").Replace("\r", "\n"); //проблеммы в pl/sql с '\r', если '\r' нужен - используются параметры (select :p0 from dual)
        }

        /// <summary> Добавление входного параметра </summary>
        /// <param name="val">значение параметра </param>
        /// <param name="key">имя параметра (по умолчанию: 'p'+число входных параметров)</param>
        /// <remarks>если val=null - вывод типа невозможен и ставится AnsiString со значением DBNull.Value</remarks>
        public override OraCommand AddIn(object val, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p") : key; //IfEmpty не use т.к. параметр сразу приводится к строке и (++) вычисляется
            if (val is string)
            {
                var v = val + "";
                if (v.Length > 32512) AddInClob(v, key); else Command.AddParameterWithValue(key, v);   //RPCFUNC+Oracle => 32512 байтов, для теста use string.Join("*", new string[32513])
            }
            else if (val is byte[])
            {
                AddInBlob((byte[])val, true, key);
            }
            else
                Command.AddParameterWithValue(key, val ?? DBNull.Value);
            return this;
        }

        /// <summary> Добавление входного параметра типа Blob</summary>
        /// <param name="val">значение параметра </param>
        /// <param name="useTempLob">использовать ли dbms_lob.createtemporary (см. example)</param>
        /// <param name="key">имя параметра (по умолчанию:'p_blob'+число входных параметров типа Blob)</param>
        /// <remarks> !!! Для выходного параметра используется AddOut c типом byte[], либо используется результат GetQueryResult!!!</remarks>
        /// <example> useTempLob=false  =>  new OraCommand("begin insert into t (id, text) values (:p0, empty_blob()) RETURNING text INTO :p_blob0; end;", id).AddInBlob(blob))</example>
        /// <example> useTempLob=true   =>  new OraCommand("begin insert into t (id, text) values (:p0, empty_blob()); update t set text = :p_blob0 where id = :p0; end;", id).AddInBlob(blob, true))</example>
        public OraCommand AddInBlob(byte[] val, bool useTempLob = false, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p_blob") : key;
            ((OracleCommand)Command).Parameters.Add(key, OracleType.Blob).Direction = useTempLob ? ParameterDirection.Input : ParameterDirection.Output;//Command.AddParameter(key, (DbType)OracleType.Blob);
            inBlobs.Add(key, val);
            return this;
        }

        /// <summary> Добавление входного параметра типа Clob</summary>
        /// <param name="val">значение параметра </param>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_clob'+число входных параметров типа clob)</param>
        public OraCommand AddInClob(string val, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p_clob") : key;
            ((OracleCommand)Command).Parameters.Add(key, OracleType.Clob).Value = val;// Command.AddParameterWithValue(key, (DbType)OracleType.Clob).Value = val;
            return this;
        }

        /// <summary> Добавление выходного параметра </summary>
        /// <param name="t">тип выходного параметра </param>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_out'+число выходных параметров) </param>
        /// <remarks> Для blob используется тип byte[]</remarks>
        public override OraCommand AddOut(Type t, string key = null)
        {
            CreateOutParameter(t, key).Direction = ParameterDirection.Output;
            return this;
        }

        /// <summary> Добавление выходного параметра типа OracleType.Cursor</summary>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_out'+число выходных параметров) </param>
        /// <remarks> Результат будет храниться в переменной типа DataTable</remarks>
        public OraCommand AddOutCursor(string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p_out") : key;
            ((OracleCommand)Command).Parameters.Add(key, OracleType.Cursor).Direction = ParameterDirection.Output;
            return this;
        }

        protected DbParameter CreateOutParameter(Type t, string key)
        {
            key = key.IsEmpty() ? CreateParamName("p_out") : key;
            if (t == typeof(string))        return ((OracleCommand)Command).Parameters.Add(key, OracleType.Clob);//Command.AddParameter(key, (DbType)OracleType.Clob);
            else if (t == typeof(byte[]))   return ((OracleCommand)Command).Parameters.Add(key, OracleType.Blob);//Command.AddParameter(key, (DbType)OracleType.Blob);
            return Command.AddParameterWithValue(key, t.GetDefault());
        }

        private OracleLob CreateBlob(DataConnection dc, byte[] buff)
        {
            var cmd = new OracleCommand("declare xx blob; begin dbms_lob.createtemporary(xx, false, 0); :x := xx; end;", (OracleConnection)dc.Connection, (OracleTransaction)dc.Transaction);
            cmd.Parameters.Add("x", OracleType.Blob).Direction = ParameterDirection.Output;
            cmd.ExecuteNonQuery();
            var result = (OracleLob)cmd.Parameters[0].Value;
            result.BeginBatch(OracleLobOpenMode.ReadWrite);
            if (buff != null) result.Write(buff, 0, buff.Length);
            result.EndBatch();
            return result;
        }

        private T FixOra_4068<T>(Func<DataConnection, T> func, DataConnection dc)
        {
            try
            {
                return func(dc);
            }
            catch (OracleException oe) //existing state of packages has been discarded
            {
                if (oe.Code == 4068) return func(dc);
                else throw;
            }
        }

        public override DataCommand ExecuteNonQuery(DataConnection dc) 
        {
            return FixOra_4068(base.ExecuteNonQuery, dc);
        }

        ///  <remarks> Для plsql функции c dml нельзя использоват select func()</remarks>
        /// var v = AppContext.ORA.ExecuteNonQuery(new OraCommand("begin :v:=RBS_SERV.Create_GSV(:p1); end;","xxx").AddOut<decimal>("v"))["v"];
        public override object ExecuteScalar(DataConnection dc)
        {
            return FixOra_4068(base.ExecuteScalar, dc);
        }

        public override DataTable GetQueryResult(DataConnection dc)
        {
            return FixOra_4068(base.GetQueryResult, dc);
        }
    }

    //для вызова процедуры, а не функции T = DBNull
    public class OraCommand<T> : OraCommand
    {
        public OraCommand(string sql, params object[] param) : base(sql, param) { }
        //v = AppContext.ORA.ExecuteScalar(new OraCommand<decimal>("RBS_SERV.Create_GSV").AddIn("xxx", "p_name"));
        public override object ExecuteScalar(DataConnection dc)
        {
            var v = typeof(T) == typeof(DBNull) ? null : CreateOutParameter(typeof(T), "v_v");
            if (v != null) v.Direction = ParameterDirection.ReturnValue;
            try
            {
                Command.CommandType = CommandType.StoredProcedure;
                ExecuteNonQuery(dc);
                return v == null ? null: v.Value;
            }
            finally
            {
                Command.CommandType = CommandType.Text;
                if (v != null) Command.Parameters.Remove(v);
            }
        }
    }

}
/*
    var v = AppContext.ORA.ExecuteNonQuery(new OraCommand("begin select text into :p_out0 from file_storages where id = 9564862210; end;").AddOut(new byte[0]))["p_out0"];
    AppContext.ORA.ExecuteNonQuery(new OraCommand("begin update file_storages set text = empty_blob() where id = :p0; select text into :p_blob0 from file_storages where id = :p0 FOR UPDATE; end;").AddIn(9564862210).AddBlob(System.Text.Encoding.ASCII.GetBytes(" ".Repeat(72000))));
    причём вызов empty_blob() нужен только для первого раза
    sql = "declare lBlob blob; begin update file_storages set text= empty_blob() where id = :p0;"+ 
    "select text into lBlob from file_storages where id = :p0 for update; "+
    "DBMS_LOB.writeappend(lBlob, dbms_lob.getlength(:p1), :p1); end;";
*/