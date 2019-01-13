using System.Collections.Generic;
using System.IO;
using System.Text;
using ETACO.CommonUtils.Plugin;
using ETACO.CommonUtils.Telecom.Mail;

namespace ETACO.CommonUtils.WinService.Telecom
{
    [Plugin("EWSWriteTelecom")]
    public class EWSWriteTelecom : WriteTelecom
    {
        private readonly EWSTelecom telecom = null;

        public EWSWriteTelecom(TaskInfo info) : base(info) 
        {
            telecom = new EWSTelecom(info["Host"], info["Login"], info.GetPassword(), AppContext.Config["EWS2filecfgtelecomWorker", "ExchangeVersion", "Exchange2010"]);
        }

        public override void WriteMessage(MessageInfo info, List<MessageData> data)
        {
            //if (!info.From.IsEmpty()) mail.From = new EmailAddress(info.From);
            var attach = new Dictionary<string, byte[]>();
            for (int i = info.Get("UseAttach",true) ? 0 : 1; i < data.Count; i++) attach.Add(data[i].Name, data[i].Stream.ReadToEnd());
            
            telecom.SendMessage(info["To"], info.Get("Subject","{0}").FormatStr(info.Name.IfEmpty(data.Count > 0 ? data[0].Name : ""), info.CreationTime),
                info.Get("UseAttach", true) || data.Count == 0 ? "" : info.Encoding.GetString(data[0].Stream.ReadToEnd()), attach,info["CC"], "", false);
        }
    }

    [Plugin("EWSReadTelecom")]
    public class EWSReadTelecom : ReadTelecom
    {
        private readonly EWSTelecom telecom = null;

        public EWSReadTelecom(TaskInfo info) : base(info) 
        {
            telecom = new EWSTelecom(info["Host"], info["Login"], info.GetPassword(), AppContext.Config["EWS2filecfgtelecomWorker", "ExchangeVersion", "Exchange2010"]);
        }

        public override IEnumerable<MessageInfo> GetMessageList()
        {
            var result = new List<MessageInfo>();
            foreach (var item in telecom.FindMessages(telecom.getFolderByPath(TaskInfo["Path"]).Id, TaskInfo["From"].IfEmpty("", "from:({0})"))) //a OR b + см getFolderId 
            {
                if (item.Subject.IsMatch(TaskInfo.Get("Subject", " * ")) && (!TaskInfo.Get("UseAttach", true) || item.HasAttachments)) //тут from пустой - поэтому фильтруем в FindItems
                    result.Add((MessageInfo)new MessageInfo() { CreationTime = item.DateTimeReceived }.Set("Id", item.Id).Set("Subject", item.Subject)
                        .Set("From", item.From).Set("To", string.Join(";", item.To)).Set("CC", string.Join(";", item.Cc))
                        .Set("UseAttach", TaskInfo["UseAttach"]).Set("AttachFilter", TaskInfo["AttachFilter"]).Set("DeleteFolder", TaskInfo["DeleteFolder"]));
            }
            return result;
        }

        private string EAC2String(List<string> eac)
        {
            var v = "";
            foreach (var a in eac) v += a + ";";
            return v;
        }

        public override List<MessageData> GetMessageData(MessageInfo info)
        {
            var mail = telecom.GetMessage(info["Id"]);
            //if (!EAC2String(mail.To).IsMatch(info.Get("To","*")) || !EAC2String(mail.Cc).IsMatch(info.Get("CC","*"))) return null;//To вместо m.burdin@etaco.ru содержит Бурдин Михаил Вадимович
            if (!info.Get("UseAttach",true)) return new List<MessageData>() { new MessageData(info["Subject"].ReplaceAny("_", "\\", "/", ":", "?", "\"", "<", ">", "|", "*", "\t"),
                                                                                    new MemoryStream(Encoding.UTF8.GetBytes(mail.Body + ""), false)) };
            var result = new List<MessageData>();
            foreach (var a in mail.FileAttachments)
            {
                if(a.Name.IsMatch(info.Get("AttachFilter","*")))
                {
                    result.Add(new MessageData(a.Name, new MemoryStream(telecom.GetAttachment(a.Id), false)));
                }
            }
           
            return result;
        }

        public override void DeleteMessage(MessageInfo info)
        {
            telecom.DeleteMessage(info["Id"],TaskInfo.Get("harddelete",false));
            /*MarkAsRead
            var msg = EmailMessage.Bind(telecom, info.Id);
            msg.IsRead = true;
            msg.Update(ConflictResolutionMode.AutoResolve);*/
        }
    }
}
