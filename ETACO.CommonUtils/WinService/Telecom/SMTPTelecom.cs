using System;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Collections.Generic;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService.Telecom
{

    [Plugin("SMTPWriteTelecom")]
    public class SMTPWriteTelecom : WriteTelecom
    {
        private readonly SmtpClient telecom = null; 
        
        public SMTPWriteTelecom(TaskInfo info) : base(info) 
        {
            telecom = new SmtpClient(info["Host"], info.Get("Port",25));
            telecom.Credentials = new NetworkCredential(info["Login"], info.GetPassword());//!!! если password пустой, то подключение идёт с доменной учётной записью
            telecom.Timeout = info.Get("Timeout",60000);
            telecom.EnableSsl = info.Get("UseSSL",false);
        }

        public override void WriteMessage(MessageInfo info, List<MessageData> data)
        {
            var mail = new MailMessage();
            try
            {
                mail.Subject = info.Get("Subject", "{0}").FormatStr(info.Name.IfEmpty(data.Count > 0 ? data[0].Name : ""), info.CreationTime);
                mail.From = new MailAddress(info.Get("From",info["login"]));
                Array.ForEach(info.Get("To", info["login"]).Split(';'), to => { if (!to.IsEmpty()) mail.To.Add(new MailAddress(to)); });
                Array.ForEach(info["CC"].Split(';'), cc => { if (!cc.IsEmpty()) mail.CC.Add(new MailAddress(cc)); });
                
                mail.BodyEncoding = info.Encoding;
                mail.Body = "";
                bool useBody = !info.Get("UseAttach",true);
                
                foreach (var msg in data)
                {
                    if(useBody) 
                    {
                        mail.Body += mail.BodyEncoding.GetString(msg.Stream.ReadToEnd());
                        useBody = false;
                    }
                    else 
                    {
                        var attachment = new Attachment(msg.Stream, msg.Name.Trim(), MediaTypeNames.Application.Octet);
                        attachment.ContentDisposition.Inline = false;
                        mail.Attachments.Add(attachment);
                    }
                }
                telecom.Send(mail);
            }
            finally
            {
                foreach (var attachment in mail.Attachments) attachment.Dispose();
                mail.Attachments.Dispose();
                mail.Dispose();
                mail = null;
                //telecom.Dispose();//этот метод доступен только в FW4
            }
        }
    }
}
