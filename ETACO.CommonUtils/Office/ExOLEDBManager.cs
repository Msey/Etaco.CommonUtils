using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ETACO.CommonUtils
{
    // <exch url="http://localhost/Public/ЭСДД"/>
    /// <summary>Класс обёртка для работы с Exchange ("ExOLEDB.DataSource")</summary>
    public class ExOLEDBManager : AbstractExchangeManager, IDisposable
    {
        private static Type ADODBConnection = Type.GetTypeFromProgID("ADODB.Connection"); //ADODB.Command, ADODB.Error, ADODB.Parameter, ADODB.Recordset   
        private static Type ADODBRecord = Type.GetTypeFromProgID("ADODB.Record");

        private bool _connectCaching = true;
        private object _lock = new object();
        private Object _connection = null;
        
        public string URL { get; private set; }

        //Если мпользуем connectCaching, то ExOLEDBManager - становится однопоточным, т.е. к открытой коннекции нельзя обращаться из разных потоков
        public ExOLEDBManager(string url, bool connectCaching = false) // connect только с правами пользователя под кем запущена программа
        {
            URL = url + "";
            _connectCaching = connectCaching;
        }

        public void Connect()
        {
            if (_connection == null)
            {
                _connection = Activator.CreateInstance(ADODBConnection);
                _connection._SetProperty("Provider", "ExOLEDB.DataSource");
                //_connection.SetProperty("CursorLocation", 3); //adUseClient
                try
                {
                    _connection._InvokeMethod("Open", URL, "", "", -1);
                }
                catch (Exception ex)
                {
                    Disconnect();
                    throw new Exception("Connection error {0}".FormatStr(ex.Message), ex);
                }
            }
        }

        public void Disconnect()
        {
            if (_connection != null)
            {
                try
                {
                    if ((int)_connection._GetProperty("State") != 0)
                    {
                        _connection._InvokeMethod("Close");
                    }
                }
                catch (Exception ex)
                {
                    AppContext.Log.HandleException(ex);
                }
                Marshal.ReleaseComObject(_connection);
                _connection = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        protected override void LoadItemList(string url, Action<object[]> onItem, string sql, string[] fields)
        {  
            DoActionWithConnection(() =>
            {
                object res = _connection._InvokeMethod("Execute", sql);
                try
                {
                    if ((int)res._GetProperty("RecordCount") > 0)
                    {
                        if (onItem != null)
                        {
                            res._InvokeMethod("MoveFirst");
                            while (!(bool)res._GetProperty("EOF"))
                            {
                                var row = new object[fields.Length];
                                for (int i = 0; i < row.Length; i++)
                                {
                                    row[i] = res._GetProperty("Fields", i)._GetProperty("Value");
                                }
                                onItem(row);
                                res._InvokeMethod("MoveNext");
                            }
                        }
                    }
                }
                finally
                {
                    res._InvokeMethod("Close");
                    res = null;
                }
            });
        }

        private void ReadProperties(string url, Action<object> onItem, params string[] fields)
        {
            var result = new List<ItemProperty>();
            DoActionWithRecord((r) =>
            {
                r._InvokeMethod("Open", url, _connection);
                if (fields == null || fields.Length == 0)
                {
                    foreach (var f in r._GetProperty("Fields") as IEnumerable) //или r.GetProperty("Fields", name).GetProperty("Value");
                    {
                        onItem(f);
                    }
                }
                else
                {
                    foreach (var v in fields)
                    {
                        onItem(r._GetProperty("Fields", v));
                    }
                }
            });
        }
        
        public override List<ItemProperty> GetProperties(string url, params string[] fields)
        {
            var result = new List<ItemProperty>();
            ReadProperties(url, (f)=>result.Add(new ItemProperty(f._GetProperty("Name") + "", f._GetProperty("Value"), GetType(Int32.Parse(f._GetProperty("Type") + "")))), fields);
            return result;
        }

        public override Dictionary<string, object> GetPropertiesDict(string url, params string[] fields)
        {
            var result = new Dictionary<string, object>();
            ReadProperties(url, (f) => result.Add(f._GetProperty("Name") + "", f._GetProperty("Value")), fields);
            return result;
        }

        protected override void CreateItem(string url, List<ItemProperty> forCreate, bool isFolder)
        {
            DoActionWithRecord((r) =>
            {
                r._InvokeMethod("Open", url, _connection, 3, isFolder ? 0x2000 | 0x2000000 : 0);
                var fields = r._GetProperty("Fields");
                if (forCreate != null)
                {
                    foreach (var v in forCreate)
                    {
                        fields._SetProperty("Item", v.Name, v.Value);
                    }
                }
                fields._InvokeMethod("Update");
            }, true);
        }


        public override void UpdateItem(string url, List<ItemProperty> forUpdate = null, List<string> forDelete = null)
        {
            DoActionWithRecord((r) =>
            {
                r._InvokeMethod("Open", url, _connection, 3); //3=rw
                var fields = r._GetProperty("Fields");
                if (forUpdate != null)
                {
                    foreach (var v in forUpdate)
                    {
                        fields._SetProperty("Item", v.Name, v.Value);
                    }
                }
                if (forDelete != null)
                {
                    foreach (var v in forDelete)
                    {
                        fields._InvokeMethod("Delete", v);
                    }
                }
                fields._InvokeMethod("Update");
            }, true);
        }

        public override void DeleteItem(string url)
        {
            DoActionWithRecord((r) =>
            {
                r._InvokeMethod("Open", url, _connection, 3);
                r._InvokeMethod("DeleteRecord");
            });
        }

        private void DoActionWithRecord(Action<object> action, bool useTransaction = false)
        {
            DoActionWithConnection(() =>
            {
                var record = Activator.CreateInstance(ADODBRecord);
                try
                {
                    if (useTransaction) _connection._InvokeMethod("BeginTrans");
                    action(record);
                    if (useTransaction) _connection._InvokeMethod("CommitTrans");
                }
                catch
                {
                    if (useTransaction) _connection._InvokeMethod("RollbackTrans");
                    throw;
                }
                finally
                {
                    if ((int)record._GetProperty("State") != 0)
                    {
                        record._InvokeMethod("Close");
                    }
                    Marshal.ReleaseComObject(record);
                    record = null;
                }
            });
        }

        private void DoActionWithConnection(Action action)
        {
            lock (_lock)
            {
                try
                {
                    Connect();
                    action();
                }
                catch
                {
                    Disconnect();
                    throw;
                }
                if (!_connectCaching) Disconnect();
            }
        }

        private static string GetType(int code)
        {
            switch (code)
            {
                case 0x2000: return "adArray";//   Combine with another data type to indicate that the other data type is an array";
                case 20: return "adBigInt";//  8-byte signed integer";
                case 128: return "adBinary";//  Binary";
                case 11: return "adBoolean";//  True or false Boolean";
                case 8: return "adBSTR";//  Null-terminated character string";
                case 136: return "adChapter";//     4-byte chapter value for a child recordset";
                case 129: return "adChar";// String";
                case 6: return "adCurrency";// Currency format";
                case 7: return "adDate";// 	Number of days since 12/30/1899";
                case 133: return "adDBDate";// 		YYYYMMDD date format";
                case 137: return "adDBFileTime";// 		Database file time";
                case 134: return "adDBTime";// 		HHMMSS time format";
                case 135: return "adDBTimeStamp";// 	 	YYYYMMDDHHMMSS date/time format";
                case 14: return "adDecimal";// 		Number with fixed precision and scale";
                case 5: return "adDouble";// 	Double precision floating-point";
                case 10: return "adError";// 	 	32-bit error code";
                case 64: return "adFileTime";//  	Number of 100-nanosecond intervals since 1/1/1601";
                case 72: return "adGUID";// 		Globally Unique identifier";
                case 9: return "adIDispatch";// 	 	Currently not supported by ADO";
                case 3: return "adInteger";// 		4-byte signed integer";
                case 205: return "adLongVarBinary";// 	 	Long binary value";
                case 201: return "adLongVarChar";// 	 	Long string value";
                case 203: return "adLongVarWChar";// 		Long Null-terminates string value";
                case 131: return "adNumeric";// 	 	Number with fixed precision and scale";
                case 138: return "adPropVariant";// 		PROPVARIANT automation";
                case 4: return "adSingle";// 		Single-precision floating-point value";
                case 2: return "adSmallInt";// 	 	2-byte signed integer";
                case 16: return "adTinyInt";// 	 	1-byte signed integer";
                case 21: return "adUnsignedBigInt";// 	 	8-byte unsigned integer";
                case 19: return "adUnsignedInt";// 	 	4-byte unsigned integer";
                case 18: return "adUnsignedSmallInt";// 	 	2-byte unsigned integer";
                case 17: return "adUnsignedTinyInt";// 	 	1-byte unsigned integer";
                case 132: return "adUserDefined";// 	 	User-defined variable";
                case 204: return "adVarBinary";// 	 	Binary value";
                case 200: return "adVarChar";// 	 	String";
                case 12: return "adVariant";// 	 	Automation variant";
                case 139: return "adVarNumeric";// 	 	Variable width exact numeric with signed scale";
                case 202: return "adVarWChar";// 	 	Null-terminated Unicode character string";
                case 130: return "adWChar";// 	 Null-terminated Unicode character string";
                case 0: return "adEmpty";// 	no value";
                default: return "";
            }
        }
    }
}