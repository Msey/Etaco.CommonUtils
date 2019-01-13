using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ETACO.CommonUtils.Plugin;
using ETACO.CommonUtils.Telecom.Mail;

namespace ETACO.CommonUtils.WinService.Telecom
{
    [Plugin("POP3ReadTelecom")]
    public class POP3ReadTelecom : ReadTelecom
    {
        private Pop3Client telecom = null;

        public POP3ReadTelecom(TaskInfo info) : base(info)
        {
            telecom = new Pop3Client(TaskInfo["Host"], TaskInfo["Login"], TaskInfo.GetPassword(), TaskInfo.Get("Port", 21), TaskInfo.Get("UseSSL", false), TaskInfo.Get("Timeout", 60000));
        }

        private bool CheckMail(MailMimeMessage mail, TaskInfo filter)
        {
            return mail.Subject.IsMatch(filter.Get("Subject","*")) && (mail.From + "").IsMatch(filter.Get("From","*")) && mail.To.ToString().IsMatch(filter.Get("To","*")) && mail.CC.ToString().IsMatch(filter.Get("CC","*"));
        }

        public override IEnumerable<MessageInfo> GetMessageList()
        {
            foreach (var id in telecom.GetMessageList())
            {
                var mail = telecom.GetMessage(id, null, 0);
                if (CheckMail(mail, TaskInfo))    yield return (MessageInfo)new MessageInfo(){CreationTime = mail.DeliveryDate}.Set("Id", id + "").Set("Subject", mail.Subject)
                         .Set("From", mail.From + "").Set("To", mail.To + "").Set("CC", mail.CC + "")
                        .Set("UseAttach", TaskInfo["UseAttach"]).Set("AttachFilter", TaskInfo["AttachFilter"]);
            }
        }

        public override List<MessageData> GetMessageData(MessageInfo info)
        {
            var mail = telecom.GetMessage(Convert.ToInt32(info["Id"]));
            if (!CheckMail(mail, info)) return null; // для случая разрыва сессии и 'устаревания' msgId

            if (!info.Get("UseAttach",true)) return new List<MessageData>() { new MessageData(info["Subject"].ReplaceAny("_", "\\", "/", ":", "?", "\"", "<", ">", "|", "*", "\t"),
                                                                                    new MemoryStream(Encoding.UTF8.GetBytes(mail.Body + ""), false)) };
            
            var result = new List<MessageData>();
            foreach(var a in mail.Attachments)
            {
                if(a.Name.IsMatch(info.Get("AttachFilter","*"))) result.Add(new MessageData(a.Name, a.ContentStream));
            }
            return result;
        }

        public override void DeleteMessage(MessageInfo info)
        {
            telecom.Delete(Convert.ToInt32(info["Id"]));
        }

        public override void Close()
        {
            if (telecom != null)
            {
                telecom.Disconnect();
                telecom = null;
            }
        }
    }
}
