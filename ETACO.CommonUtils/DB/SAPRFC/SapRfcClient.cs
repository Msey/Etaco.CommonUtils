using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ETACO.CommonUtils
{
    public class SAPRFCClient : DataProvider, IDisposable
    {
        private static Type RfcConfigParameters = null;
        private static MethodInfo GetDestination = null;
        private static MethodInfo BeginContext = null;
        private static MethodInfo EndContext = null;
        private static MethodInfo SetValue = null;
        private static MethodInfo GetTable = null;
        private static MethodInfo GetElementMetadata = null;
        private static MethodInfo _GetObject = null;
        private static MethodInfo _GetInt = null;
        private static MethodInfo _GetLong = null;
        private static MethodInfo _GetDecimal = null;
        private static MethodInfo _GetString = null;
        private static MethodInfo _GetBytes = null;

        private object destination;//RfcDestination

        public override string ConnectionInfo { get { return destination?._GetProperty("User") + "@" + destination?._GetProperty("SAPRouter"); } }

        public SAPRFCClient()
        {
            if (RfcConfigParameters == null)
            {
                var sapnco = Assembly.LoadFrom("sapnco.dll");//+sapnco_utils.dll
                RfcConfigParameters = sapnco.GetType("SAP.Middleware.Connector.RfcConfigParameters");
                GetDestination = sapnco.GetType("SAP.Middleware.Connector.RfcDestinationManager").GetMethod("GetDestination", new[] { RfcConfigParameters });

                var t = sapnco.GetType("SAP.Middleware.Connector.RfcSessionManager");
                BeginContext = t.GetMethod("BeginContext");
                EndContext = t.GetMethod("EndContext");

                t = sapnco.GetType("SAP.Middleware.Connector.IRfcDataContainer");
                SetValue = t.GetMethod("SetValue", new[] { typeof(string), typeof(string) });
                GetTable = t.GetMethod("GetTable", new[] { typeof(string) });
                var arg = new[] { typeof(int) };
                GetElementMetadata = t.GetMethod("GetElementMetadata", arg);
                _GetObject = t.GetMethod("GetObject", arg);
                _GetInt = t.GetMethod("GetInt", arg);
                _GetLong = t.GetMethod("GetLong", arg);
                _GetDecimal = t.GetMethod("GetDecimal", arg);
                _GetString = t.GetMethod("GetString", arg);
                _GetBytes = t.GetMethod("GetByteArray", arg);
            }
        }

        public SAPRFCClient Connect(string host, int port, string user, string passwd, string systemNumber, string systemId, string client, string trace = "0")
        {
            var prms = new Dictionary<string, string>();
            prms["USER"] = user;
            prms["PASSWD"] = passwd;
            prms["SAPROUTER"] = "/H/" + host + (port < 1 ? "" : ("/S/" + port));
            if (!systemNumber.IsEmpty()) prms["SYSNR"] = systemNumber;
            if (!client.IsEmpty()) prms["CLIENT"] = client;
            if (!systemId.IsEmpty()) prms["SYSID"] = systemId;
            prms["TRACE"] = trace;//0-off, 1,2 .. more detail
            OpenConnection(prms);
            return this;
        }

        public override bool OpenConnection(Dictionary<string, string> _params)
        {
            try
            {
                CloseConnection();
                var prms = (Dictionary<string, string>)Activator.CreateInstance(RfcConfigParameters);
                foreach (var v in _params) prms[v.Key.ToUpper()] = v.Value;
                prms["NAME"] = "saprfc";
                destination = GetDestination.Invoke(null, new[] { prms });
                BeginContext.Invoke(null, new[] { destination });
                destination._InvokeMethod("Ping");
                return true;
            }
            catch
            {
                destination = null;
                throw;
            }
        }
        public override bool IsConnected { get { return destination != null; } }
        public override void CloseConnection()
        {
            if (destination != null) EndContext.Invoke(null, new[] {destination});
            destination = null;
        }

        public override void Read(bool newConn, IDataCommand command, Action<IDataRow> onInit, Action<IDataRow> onRow)//funcName:tableName
        {
            if (destination == null) return;

            var v = (BaseDataCommand)command;
            var cmd = v.Sql.Split(':');

            var func = destination._GetProperty("Repository")._InvokeMethod("CreateFunction", cmd[0]) as ICollection;
            foreach (var x in v.Parameters) SetValue.Invoke(func, new[] { x.Key, x.Value });
            func._InvokeMethod("Invoke", destination);

            if (cmd.Length > 1) ReadTable(func, cmd[1], onInit, onRow);
            else for (var i = 0; i < func.Count; i++)
            {
                var mtd = GetElementMetadata.Invoke(func, new object[] { i });
                if((int)mtd._GetProperty("DataType") == 25/*TABLE*/) ReadTable(func, (string)mtd._GetProperty("Name"), onInit, onRow);
            }
        }

        private void ReadTable(ICollection func, string name, Action<IDataRow> onInit, Action<IDataRow> onRow)
        {
            var tbl = GetTable.Invoke(func, new object[] { name }) as ICollection;

            var fCount = (int)tbl._GetProperty("Metadata.LineType.FieldCount");
            var colNames = new string[fCount];
            var colTypes = new int[fCount];
            for (var f = 0; f < fCount; f++)
            {
                var m = GetElementMetadata.Invoke(tbl, new object[] { f });
                colNames[f] = (string)m._GetProperty("Name");
                colTypes[f] = (int)m._GetProperty("DataType");

            }
            int r = 0;
            var dr = new SAPRFCDataRow(name, colNames, colTypes);
            onInit?.Invoke(dr);
            foreach (var row in tbl) onRow(dr.SetCurrent(row, r++));
        }

        public override object Execute(DataTransaction dt, IDataCommand command)//funcName:tableName
        {
            if (destination == null) return null;
            var v = (BaseDataCommand)command;
            var cmd = v.Sql.Split(':');
            var func = destination._GetProperty("Repository")._InvokeMethod("CreateFunction", cmd[0]);
            var obj = func;
            if (cmd.Length > 1)
            {
                var tbl = GetTable.Invoke(func, new[] { cmd[1] }) as ICollection;
                tbl._InvokeMethod("Clear");
                tbl._InvokeMethod("Append");
                var row = tbl.GetEnumerator();
                row.MoveNext();
                obj = row.Current;
            }
            foreach (var x in v.Parameters) SetValue.Invoke(obj, new[] { x.Key, x.Value });
            func._InvokeMethod("Invoke", destination);
            return null;
        }

        public void Dispose()
        {
            CloseConnection();
        }

        private class SAPRFCDataRow : IDataRow //для доступа к приватным полям SAPRFCClient
        {
            private static readonly Type S = typeof(string);
            private static readonly Type I = typeof(int);
            private static readonly Type L = typeof(long);
            private static readonly Type D = typeof(decimal);
            private string[] columnsName;
            private Type[] columnsType;
            private int columnsCount;
            private object row;
            private object[] param = new object[1];

            internal SAPRFCDataRow(string name, string[] ColumnsName, int[] ColumnsType)
            {
                Name = name;
                columnsCount = ColumnsName.Length;
                columnsName = ColumnsName;
                columnsType = new Type[columnsCount];
                for (var i = 0; i < columnsCount; i++)
                {
                    switch (ColumnsType[i])
                    {
                        case 18: columnsType[i] = I; break;
                        case 19: columnsType[i] = L; break;
                        case 2:
                        case 3: columnsType[i] = D; break;
                        default: columnsType[i] = S; break;
                    }
                }
            }
            internal IDataRow SetCurrent(object row, int ri) { this.row = row; RowIndex = ri; return this; }
            public string Name { get; private set; }
            public int RowIndex { get; private set; }
            public object Get(int index) { param[0] = index; return _GetObject.Invoke(row, param); }
            public bool GetBool(int index) { return bool.Parse(GetString(index)); }
            public int GetInt(int index) { param[0] = index; return (int)_GetInt.Invoke(row, param); }
            public long GetLong(int index) { param[0] = index; return (long)_GetLong.Invoke(row, param); }
            public decimal GetDecimal(int index) { param[0] = index; return (decimal)_GetDecimal.Invoke(row, param); }
            public string GetString(int index) { param[0] = index; return (string)_GetString.Invoke(row, param); }
            public DateTime GetDateTime(int index) { return DateTime.Parse(GetString(index)); }
            public byte[] GetBytes(int index) { param[0] = index; return (byte[])_GetBytes.Invoke(row, param); }
            public int GetFieldIndex(string name) { return Array.IndexOf(columnsName, name); }
            public string GetFieldName(int index) { return columnsName[index]; }
            public Type GetFieldType(int index) { return columnsType[index]; }
            public int FieldCount { get { return columnsCount; } }
            public bool IsNull(int index) { return Get(index) == null; }
        }
    }

    /*var cfg = AppContext.Config;
     var sap = new SAPRFCClient();
     sap.Connect(cfg["SAP", "Host"], cfg.GetParameter("SAP", "Port", 0), cfg["SAP", "User"], cfg["SAP", "Password", "", true], cfg["SAP", "SystemNumber"], cfg["SAP", "SystemID"], cfg["SAP", "Client"]);
     sap.Read(false, "YGE_GETCATALOG:CATALOG", 
                dr => { Console.WriteLine(dr.Name); for (var i = 0; i < dr.FieldCount; i++) Console.WriteLine(dr.GetFieldName(i) + ":" + dr.GetFieldType(i)); Console.WriteLine(); }
                ,dr => { for (var i = 0; i < dr.FieldCount; i++) Console.WriteLine(dr.GetFieldName(i) + ":" + dr.Get(i)); },
                new Dictionary<string, object>() { { "CatalogType", "COUNTERPARTY" } });
     sap.CloseConnection();*/
}