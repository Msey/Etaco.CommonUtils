using System.Collections.Generic;
using System.IO;
using System.Text;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService.Telecom
{
    [Plugin("dbwritetelecom")]
    public class DbWriteTelecom : WriteTelecom
    {
        private readonly DataAccess telecom = null;
        private readonly string dbTypeName = null;

        public DbWriteTelecom(TaskInfo info) : base(info) 
        {
            dbTypeName = info.Get("dbtype", "OraDataAccess");
            var dbType = AppContext.GetType(dbTypeName);
            var telecom = (DataAccess)System.Activator.CreateInstance(dbType, info["db"]);
        }

        public override void WriteMessage(MessageInfo info, List<MessageData> data)
        {
            foreach (var msg in data) telecom.ExecuteNonQuery(msg.Stream.ReadToEnd().GetString(Encoding.UTF8));   
        }
    }

    [Plugin("dbreadtelecom")]
    public class DbReadTelecom : ReadTelecom
    {
        private readonly DataAccess telecom = null;
        private readonly string dbTypeName = null;

        public DbReadTelecom(TaskInfo info) : base(info) 
        {
            dbTypeName = info.Get("dbtype", "OraDataAccess");
            telecom = (DataAccess)System.Activator.CreateInstance(AppContext.GetType(dbTypeName), info["db"]);
        }

        public override List<MessageData> GetMessageData(MessageInfo info)
        {
            var v = TaskInfo["initsql"].Trim();
            if (!v.IsEmpty()) telecom.ExecuteNonQuery(v.FormatStrEx());

            var dt = telecom.GetObjectList<object[]>(TaskInfo["sql"].FormatStrEx());

            v = TaskInfo["deinit"].Trim();
            if (!v.IsEmpty()) telecom.ExecuteNonQuery(v.FormatStrEx());

            var res = new StringBuilder();

            for (var i = 1; i < dt.Count; i++)
            {
                res.Append("<ROW>");
                for (var j = 0; j < dt[0].Length; j++) res.AppendFormat("<{0}>{1}</{0}>", dt[0][j], dt[i][j]);
                res.Append("</ROW>");
            }
            return new List<MessageData>() { new MessageData(info.Name, new MemoryStream(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><" + dbTypeName + ">" + res + "</" + dbTypeName + ">"), false))};
        }
    }
}
