using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ETACO.CommonUtils
{
    /// <summary> Обёртка для DbCommand </summary>
    public abstract class DataCommand<T> : DataCommand where T : DataCommand 
    {
        public static DataCommand<T> CreateCommand(string sql, object[] param = null) 
        { 
            var v = (DataCommand<T>)Activator.CreateInstance(typeof(T), sql);
            if (param != null) { for (int i = 0; i < param.Length; i++) { v.AddIn(param[i]); } }
            return v;
        }
        protected void CreateCommand(DbProviderFactory dbFactory, string sql, object[] param = null)
        {
            Command = dbFactory.CreateCommand();
            Command.CommandText = sql + "";
            if (param != null) { for (int i = 0; i < param.Length; i++) { AddIn(param[i]); } }
        }
        /// <summary> Добавление входного параметра </summary>
        /// <param name="val">значение параметра </param>
        /// <param name="key">имя параметра (по умолчанию: 'p'+число входных параметров)</param>
        /// <remarks>если val=null - вывод типа невозможен и ставится AnsiString со значением DBNull.Value</remarks>
        public new virtual T AddIn(object val, string key = null)
        {
            return base.AddIn(val,key) as T;
        }
        /// <summary> Добавление выходного параметра </summary>
        /// <param name="t">тип выходного параметра </param>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_out'+число выходных параметров) </param>
        /// <remarks> Для blob используется тип byte[]</remarks>
        public new virtual T AddOut(Type t, string key = null)
        {
            return base.AddOut(t, key) as T;
        }

        /// <summary> Добавление выходного параметра </summary>
        /// <typeparam name="S">тип выходного параметра </typeparam>
        /// <param name="key">имя параметра (по умолчанию используется имя вида 'p_out'+число выходных параметров) </param>
        public T AddOut<S>(string key = null)
        {
            return AddOut(typeof(S), key);
        }

        //проблема с params object[] param - S==Object, а это не так
        /*public virtual T AddIn<S>(S val, string key = null, DbType dbType = DbType.Object, int size = 0)
        {
            key = key.IsEmpty() ? CreateParamName("p") : key; //IfEmpty не use т.к. параметр сразу приводится к строке и (++) вычисляется
            Command.AddParameter(CommandPrefix + key, dbType == DbType.Object ? ToDbType(typeof(S)) : dbType, size).Value = ((object)val) ?? DBNull.Value;
            return this as T;
        }

        private DbType ToDbType(Type t)
        {
            if (t.IsArray)
            {
                if (t.GetElementType() == typeof(byte)) return DbType.Binary;
                if (t.GetElementType() == typeof(char)) return DbType.AnsiString;
                return DbType.Object;
            }
            try
            {
                return (DbType)Enum.Parse(typeof(DbType), t.Name);
            }
            catch
            {
                return DbType.Object;
            }
        }*/
    }

    public abstract class DataCommand :IDataCommand
    {
        private Log _log = AppContext.Log;
        protected DbCommand Command { get; set; }
        protected virtual void BeforeExecuteNonQuery(DataConnection dc) { }
        protected virtual void AfterExecuteNonQuery(DataConnection dc) { }
        protected virtual char CommandPrefix { get { return '@'; } }
        protected virtual string GetParamName(string key) { return CommandPrefix + key; }

        private Dictionary<string, int> paramsIndex = new Dictionary<string, int>();
        protected string CreateParamName(string type)
        {
            var v = paramsIndex.GetValue(type, 0, true);
            paramsIndex[type] = v + 1;
            return type + v;
        }

        /// <summary> Вызывается после чтения новой строки из результирующего набора  </summary>
        public event Action<DataRow> OnDataRead;

        /// <summary> Возвращает описание текста команды и списка параметров  </summary>
        public string Description
        {
            get
            {
                var result = new System.Text.StringBuilder(Command.CommandText);
                foreach (DbParameter param in Command.Parameters)
                {
                    result.AppendFormat(" [{0} {1}='{2}']", param.Direction, param.ParameterName, param.Value);
                }
                return result.ToString();
            }
        }

        /// <summary> Возвращает и устанавливает текст команды  </summary>
        public virtual string Sql
        {
            get { return Command.CommandText; }
            set { Command.CommandText = value + "";}
        }

        public virtual IDataCommand AddIn(object val, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p") : key; //IfEmpty не use т.к. параметр сразу приводится к строке и (++) вычисляется
            Command.AddParameterWithValue(CommandPrefix + key, val is Enum ? val.ToString() : (val ?? DBNull.Value));
            return this;
        }

        public virtual IDataCommand AddOut(Type t, string key = null)
        {
            key = key.IsEmpty() ? CreateParamName("p_out") : key;
            Command.AddParameterWithValue(CommandPrefix + key, t.GetDefault()).Direction = ParameterDirection.Output;
            return this;
        }

        /// <summary> Возвращает список имён параметров указанных в текста команды </summary>
        public string[] DeriveParameters()
        {
            var set = new HashSet<string>();
            if (!Command.CommandText.IsEmpty())
            {
                var cmd = new Regex(@"'([^']|''|[\\'])*'").Replace(Command.CommandText, "''");
                foreach (Match match in new Regex(@"{0}(\w+)".FormatStr(CommandPrefix)).Matches(cmd))
                {
                    set.Add(match.Groups[1] + "");
                }
            }
            var result = new string[set.Count];
            set.CopyTo(result);
            return result;
        }

        /// <summary> Возвращает список параметров переданных в команду с указанием является ли данный параметр входящим или нет </summary>
        public Dictionary<string, bool> GetParametersInfo()
        {
            var result = new Dictionary<string, bool>();
            foreach (DbParameter p in Command.Parameters)
            {
                result.Add(p.ParameterName[0] == CommandPrefix ? p.ParameterName.Substring(1) : p.ParameterName, p.Direction == ParameterDirection.Input);
            }
            return result;
        }

        /// <summary> Очистить список параметров </summary>
        public IDataCommand ClearParams() { Command.Parameters.Clear(); paramsIndex.Clear(); return this; }

        /// <summary> Доступ к параметрам </summary>
        /// <param name="key">имя параметра</param>
        public object this[string key]
        {
            get { return Command.Parameters[GetParamName(key)].Value; }
            set { Command.Parameters[GetParamName(key)].Value = value; }
        }

        public virtual DataCommand ExecuteNonQuery(DataConnection dc)
        {
            if (dc == null) throw new Exception("DataConnection for Command.ExecuteNonQuery is null");
            if (_log.UseTrace) _log.Trace("ExecuteNonQuery : " + Description);
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                Command.Connection = dc.Connection;
                Command.Transaction = dc.Transaction;

                BeforeExecuteNonQuery(dc);
                Command.ExecuteNonQuery();
                AfterExecuteNonQuery(dc);
            }
            finally
            {
                Command.Transaction = null;
                Command.Connection = null;
            }

            sw.Stop();
            if (_log.UseTrace) _log.Trace("ExecuteNonQuery : time = " + sw.ElapsedMilliseconds + " ms");

            return this;
        }

        // DBReader 
        private void UseDBDataReader(Action<DbDataReader,int> onInit, Action<DbDataReader,int, int> onRow, DataConnection dc, string name, CommandBehavior behavior = CommandBehavior.SingleResult)
        {
            if (dc == null) throw new Exception("DataConnection for Command." + name + " is null");
            if (onRow == null) throw new Exception("OnRow for Command." + name + " is null");
            var rowIndex = 0;
            if (_log.UseTrace) _log.Trace(name + " : " + Description);
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                Command.Connection = dc.Connection;
                Command.Transaction = dc.Transaction;
                using (var dr = Command.ExecuteReader(behavior))//(CommandBehavior.SequentialAccess)
                {
                    var fieldCount = dr.FieldCount;
                    onInit?.Invoke(dr, fieldCount);
                    if (dr.Read()) do { onRow(dr, rowIndex++, fieldCount);} while(behavior != CommandBehavior.SingleRow && dr.Read());
                }
            }
            finally
            {
                Command.Transaction = null;
                Command.Connection = null;
            }

            sw.Stop();
            if (_log.UseTrace) _log.Trace(name + " : time = " + sw.ElapsedMilliseconds + " ms " + rowIndex + " row(s) fetched");
        }

        public virtual object[] ExecuteRow(DataConnection dc)
        {
            object[] v = null;
            UseDBDataReader(null, (dr, rowIndex, fCount) => { v = new object[fCount]; for (int i = 0; i < fCount; i++) v[i] = dr.GetDbValue(i, rowIndex); }, dc, "ExecuteRow", CommandBehavior.SingleRow);
            return v;
        }

        public virtual object ExecuteScalar(DataConnection dc)
        {
            object v = null;
            UseDBDataReader(null, (dr, rowIndex, fCount) => v = dr.GetDbValue(0, rowIndex), dc, "ExecuteScalar", CommandBehavior.SingleRow);
            return v;
        }

        public virtual DataTable GetQueryResult(DataConnection dc)
        {
            var v = new DataTable();
            UseDBDataReader((dr, fCount) => {
                    for (int col = 0; col < fCount; col++)
                    {
                        var colName = dr.GetName(col).ToUpper();
                        var cn = colName;
                        for (int i = 1; v.Columns.Contains(cn); i++) { cn = colName + "_" + i; }
                        v.Columns.Add(cn, dr.GetFieldType(col));
                    }},
                (dr, rowIndex, fCount) => { var row = v.NewRow(); for (int i = 0; i < fCount; i++) {row[i] = dr.GetDbValue(i, rowIndex);} v.Rows.Add(row); OnDataRead?.Invoke(row); },
                dc, "GetQueryResult");    
            v.AcceptChanges();
            return v;
        }

        /// <summary>
        /// Получение списка объектов заполненных результатами выполнения запроса
        /// </summary>
        /// <typeparam name="T">Тип объектов для заполнения данными(Common,Tuple,object[],dynamic, class with attr ObjectsMapping on constructor, or set object property)</typeparam>
        /// <param name="dc"></param>
        /// <returns>Список объектов T </returns>
        public virtual List<T> GetObjectList<T>(DataConnection dc)//where T : new() 
        {
            var result = new List<T>();
            var type = typeof(T);
            bool isExpando = type == typeof(Expando);
            if (type == typeof(object)||isExpando) //dynamic
            {
                string[] cols = null;

                UseDBDataReader((dr, fCount) => { if (fCount > 1 || isExpando) { cols = new string[fCount]; for (int i = 0; i < fCount; i++) cols[i] = dr.GetName(i); } },
                      (dr, ri, fc) => { if (cols == null) result.Add((T)dr.GetDbValue(0, ri)); else { var e = new Expando(); for (int i = 0; i < fc; i++) e.TrySetMember(cols[i], dr.GetDbValue(i, ri)); result.Add((T)(object)e); } },
                    dc, "GetObjectList<dynamic>");
            }
            else if(type == typeof(object[]))
            {
                UseDBDataReader((dr, fCount) => { var a = new object[fCount]; for (int i = 0; i < fCount; i++) a[i] = dr.GetName(i).ToUpper(); result.Add((T)(object)a); },
                  (dr, rowIndex, fCount) => { var a = new object[fCount]; for (int i = 0; i < fCount; i++) a[i] = dr.GetDbValue(i, rowIndex); result.Add((T)(object)a); },
                dc, "GetObjectList<object[]>");
            }
            else if (type.IsCommon()) UseDBDataReader((dr, fCount) => { }, (dr, rowIndex, fCount) => result.Add((T)dr.GetDbValue(0, rowIndex, type, true)), dc, "GetObjectList<T> where T is Common type");
            else if (type.IsGenericType && type.FullName.StartsWith("System.Tuple`"))
            {
                var len = TupleHolder<T>.Length;
                var buff = new object[len];
                var ctFlags = new Type[len];
                UseDBDataReader((dr, fCount) =>
                {
                    if (len > fCount) throw new Exception("Tuple.Items={0} > FieldCount={1}".FormatStr(len, fCount));
                    //for (var i = 0; i < len; i++) if (TupleHolder<T>.Items[i] != dr.GetFieldType(i)) throw new Exception("Tuple.Items[{0}]={1} <> ResultSet.Field[{0}]={2}".FormatStr(i, TupleHolder<T>.Items[i], dr.GetFieldType(i)));
                    for (var i = 0; i < len; i++) ctFlags[i] = TupleHolder<T>.Items[i] != dr.GetFieldType(i)? TupleHolder<T>.Items[i]:null; 
                },
                (dr, rowIndex, fCount) =>
                {
                    for (var i = 0; i < len; i++) buff[i] = dr.IsDBNull(i) ? TupleHolder<T>.Items[i].GetDefault() : dr.GetDbValue(i, rowIndex, ctFlags[i]);
                    result.Add(TupleHolder<T>.Activator(buff));
                }, dc, "GetTupleList");
            }
            else
            {
                var ctorParams = TypeInfo<T>.GetCtorParams();
                if (ctorParams.Length == 1)//для оптимизации (конструктор с одним параметром, ищем только его в columns)
                {
                    int index = 0;
                    var t = ctorParams[0].ParameterType;
                    UseDBDataReader(
                    (dr, fCount) => { for (int col = 0; col < fCount; col++) if (ctorParams[0].Name.Equals(dr.GetName(col), StringComparison.OrdinalIgnoreCase)) { index = col; break; } },
                    (dr, rowIndex, fCount) => result.Add(TypeInfo<T>.CreateWithValues(new [] { dr.GetDbValue(index, rowIndex, t) })),
                    dc, "GetObjectList<T>");
                }
                else if (ctorParams.Length > 1)
                {
                    int[] ctorParamIndex = Enumerable.Repeat(-1, ctorParams.Length).ToArray();
                    UseDBDataReader(
                    (dr, fCount) =>
                    {
                        for (int col = 0; col < fCount; col++) { var p = ctorParams.FirstOrDefault(x => x.Name.Equals(dr.GetName(col), StringComparison.OrdinalIgnoreCase)); if (p != null) ctorParamIndex[p.Position] = col; }
                    },
                    (dr, rowIndex, fCount) =>
                    {
                        var buff = new object[ctorParamIndex.Length];
                        for (var i = 0; i < buff.Length; i++)
                        {
                            var ind = ctorParamIndex[i];
                            var t = ctorParams[i].ParameterType;
                            buff[i] = (ind == -1 || dr.IsDBNull(ind)) ? t.GetDefault() : dr.GetDbValue(ind, rowIndex, t);

                        }
                        result.Add(TypeInfo<T>.CreateWithValues(buff));
                    },
                    dc, "GetObjectList<T>");
                }
                else //Set object property
                {
                    var mapping = new SortedDictionary<int, MappingInfo>();
                    var ctor = TypeInfo<T>.CreateDefaultConstructor();
                    UseDBDataReader(
                    (dr, fCount) =>
                    {
                        var props = TypeInfo<T>.PropInfo;
                        MappingInfo mi;
                        for (int col = 0; col < fCount; col++) if (props.TryGetValue(dr.GetName(col), out mi)) mapping.Add(col, mi);
                    },
                    (dr, rowIndex, fCount) =>
                    {
                        var o = ctor();
                        foreach (var m in mapping)
                        {
                            if (dr.IsDBNull(m.Key)) m.Value.SetValue(o, m.Value.Mapping.Default);
                            else if (m.Value.IsBoolean)
                            {
                                var v = dr.GetString(m.Key);
                                bool? val = v == m.Value.Mapping.BoolTrue;
                                if (val == false && m.Value.Mapping.BoolFalse != null)
                                    val = v == m.Value.Mapping.BoolFalse ? false : (bool?)null;
                                m.Value.SetValue(o, val);
                            }
                            else if (m.Value.UseConvertToLocalTime)
                            {
                                var obj = dr.GetValue(m.Key) as DateTime?;
                                if (obj != null)
                                    obj = obj.Value.ToLocalTime();
                                m.Value.SetValue(o, obj.Value);
                            }
                            else
                                m.Value.SetValue(o, dr.GetDbValue(m.Key, rowIndex, m.Value.Type, m.Value.IsPrimitive));
                        }
                        result.Add(o);
                    },
                    dc, "GetObjectList<T>");
                }
            }
            return result;
        }

        public virtual List<T> GetObjectList<T>(DataConnection dc, Func<DataProvider.IDataRow, T> onRow)
        {
            var result = new List<T>();
            var row = new DbRow();
            UseDBDataReader(null, (dr, ri, fc) => result.Add(onRow(row.SetCurrent(dr, ri))), dc, "GetObjectList<?,Func<?,?>>");
            return result;
        }

        public virtual DataCommand ReadObjectList(DataConnection dc, Action<DataProvider.IDataRow> onInit, Action<DataProvider.IDataRow> onRow)
        {
            var row = new DbRow();
            UseDBDataReader((dr, ri) => onInit?.Invoke(row.SetCurrent(dr, ri)), (dr, ri, fc) => onRow(row.SetCurrent(dr, ri)), dc, "ReadObjectList<?,Action<?>>");
            return this;
        }

        public class DbRow : DataProvider.IDataRow
        {
            private DbDataReader reader;
            internal DbRow() {}
            internal DbRow SetCurrent(DbDataReader reader, int ri) { this.reader = reader; RowIndex = ri;  return this; }
            public string Name { get { return string.Empty; } }
            public int RowIndex { get; private set; }
            public object Get(int index) { return reader.GetDbValue(index, RowIndex); }
            public bool GetBool(int index) { return reader.GetBoolean(index); }
            public int GetInt(int index) { return reader.GetInt32(index); }
            public long GetLong(int index) { return reader.GetInt64(index); }
            public decimal GetDecimal(int index) { return reader.GetDecimal(index); }
            public string GetString(int index) { return reader.IsDBNull(index) ? string.Empty : reader.GetString(index); }
            public DateTime GetDateTime(int index) { return reader.GetDateTime(index); }
            public byte[] GetBytes(int index) {
                if (reader.IsDBNull(index)) return null; //(byte[])Get(index);
                var v = new byte[reader.GetBytes(index, 0, null, 0, 0)];
                int pos = 0;
                while (pos < v.Length) pos += (int)reader.GetBytes(index, pos, v, pos, v.Length);//можно реализовать докачку или сохранение в file
                return v;
            }
            public int GetFieldIndex(string name) { return reader.GetOrdinal(name); }
            public string GetFieldName(int index) { return reader.GetName(index); }
            public Type GetFieldType(int index) { return reader.GetFieldType(index); }
            public int FieldCount { get { return reader.FieldCount; }}
            public bool IsNull(int index) { return reader.IsDBNull(index); }
        }

        //тут передайтея в Action не DbRow, а List<object[]>, т.к. нужно обрабатывать не 1 запись, а сразу группу
        public virtual bool JoinVerticalTable(DataConnection dc, Action<List<object[]>> onGroup, int groupKeyCount = 1)//в качестве ключей используются первые column resultSet (ключи должны быть отсортированы)
        {
            var rows = new List<object[]>();
            UseDBDataReader(null, (dr, ri, fc) =>
            {
                var v = new object[fc]; for (int i = 0; i < fc; i++) v[i] = dr.GetDbValue(i, ri);
                var eqFlag = true;//ref key not null
                if (rows.Count != 0) { eqFlag = rows[0][0].Equals(v[0]); if (groupKeyCount > 1) for (var i = 1; i < groupKeyCount && eqFlag; i++) eqFlag &= rows[0][i].Equals(v[i]); }
                if (eqFlag) rows.Add(v);
                else { onGroup(rows); rows = new List<object[]>() { v };}
            }, dc, "JoinVerticalTable");
            if (rows.Count != 0) onGroup(rows);
            return rows != null;
        }

        private static Type[] tupleTypes = { typeof(Tuple<>), typeof(Tuple<,>), typeof(Tuple<,,>), typeof(Tuple<,,,>), typeof(Tuple<,,,,>), typeof(Tuple<,,,,,>), typeof(Tuple<,,,,,,>), typeof(Tuple<,,,,,,,>) };
        private static class TupleHolder<T>
        {
            public static readonly Func<object[], T> Activator;//для tuple object[] - boxing и скорость падает в 2 раза
            public static readonly int Length;
            public static readonly Type[] Items;
            static TupleHolder()
            {
                var t = typeof(T);
                Items = t.GetGenericArguments();
                Length = Items.Length;
                if (t.Name != "Tuple" && Length > 7) throw new Exception("use tuple length < 8");
                Activator = tupleTypes[Length - 1].MakeGenericType(Items).GetConstructors()[0].GetActivator<T>();
            }
        }
    }

    

    public class ObjectsMapping : Attribute
    {
        public string FieldName;
        public string BoolTrue = "Y";
        public string BoolFalse;
        public object Default;
        public bool ConvertToLocalTime = false;
    }

    public class ObjectsMappingConstructor : Attribute{}

    internal class MappingInfo
    {
        public PropertyInfo PropertyInfo {set;private get;}
        public ObjectsMapping Mapping;
        public Type Type;
        public bool IsBoolean;
        public bool UseConvertToLocalTime;
        public bool IsPrimitive = true;
        public void SetValue(object target, object value) { PropertyInfo.SetValue(target, value, null); }
    }
    
    internal class TypeInfo<T>
    {
        private static Dictionary<string, MappingInfo> _propInfo;
        private static ParameterInfo[] _ctorParams;
        private static Func<object[], T> _createdActivator; 
        protected TypeInfo() { }

        public static Dictionary<string, MappingInfo> PropInfo
        {
            get
            {
                if (_propInfo == null)
                {
                    _propInfo = new Dictionary<string, MappingInfo>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in typeof(T).GetProperties())
                    {
                        var attr = (ObjectsMapping)Attribute.GetCustomAttribute(p, typeof(ObjectsMapping)) ?? new ObjectsMapping() { FieldName = p.Name };
                        var mi = new MappingInfo() { PropertyInfo = p, Mapping = attr, Type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType };
                        mi.IsBoolean = mi.Type == typeof(bool);
                        mi.UseConvertToLocalTime = attr.ConvertToLocalTime && mi.Type == typeof(DateTime);
                        mi.IsPrimitive = mi.Type.IsPrimitive || mi.Type.IsAssignableFrom(typeof(string)) || mi.Type.IsAssignableFrom(typeof(DateTime)) || mi.Type.IsAssignableFrom(typeof(decimal));
                        _propInfo.Add(attr.FieldName ?? p.Name, mi);
                    }
                }
                return _propInfo;
            }
        }

        public static ParameterInfo[] GetCtorParams()
        {
            if (_ctorParams == null)
            {
                _ctorParams = new ParameterInfo[0];
                foreach (var c in typeof(T).GetConstructors())
                {
                    if(Attribute.IsDefined(c, typeof(ObjectsMappingConstructor)))
                    {
                        _ctorParams = c.GetParameters().OrderBy(x => x.Position).ToArray();
                        _createdActivator = c.GetActivator<T>();
                        break;
                    }
                }
            }
            return _ctorParams;
        }

        public static Func<T> CreateDefaultConstructor() { return Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();}
        public static T CreateWithValues(object[] values) { return _createdActivator == null ? default(T) : /*(T)_ctor.Invoke(values)*/_createdActivator(values); }
    }
}