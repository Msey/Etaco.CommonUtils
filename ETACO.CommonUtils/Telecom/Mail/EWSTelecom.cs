using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace ETACO.CommonUtils.Telecom.Mail
{
    //http://msdn.microsoft.com/en-us/library/office/bb409286%28v=exchg.150%29.aspx
    public class EWSTelecom
    {
        private const string URI_MSG = "http://schemas.microsoft.com/exchange/services/2006/messages";
        private const string URI_TYPES = "http://schemas.microsoft.com/exchange/services/2006/types";

        private ICredentials _credential = null;
        private string _url = null;
        private string _version = "";
        private XmlNamespaceManager _namespaceManager = null;

        public EWSTelecom(string url, string login = "", string password = "", string version = "Exchange2010")
        {
            _url = url;//https://cas.etaco.ru/EWS/Exchange.asmx
            _credential = login.IsEmpty() ? CredentialCache.DefaultCredentials : new NetworkCredential(login, password);
            _version = version;
            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallBack;
        }

        private XmlDocument GetResponse(string command)
        {
            var request = (HttpWebRequest)WebRequest.Create(_url);
            request.AllowAutoRedirect = false;
            request.Credentials = _credential;
            request.Method = "POST";
            request.ContentType = "text/xml";

            command = "<?xml version=\"1.0\" encoding=\"utf-8\"?> <soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:m=\"" + URI_MSG + "\"" +
                      " xmlns:t=\"" + URI_TYPES + "\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">" +
                      "<soap:Header><t:RequestServerVersion soap:mustUnderstand=\"0\" Version=\"" + _version + "\" /></soap:Header><soap:Body>" + command + "</soap:Body></soap:Envelope>";

            if (AppContext.Log.UseTrace) AppContext.Log.Trace(command, "EWSTelecom");

            var buff = System.Text.Encoding.UTF8.GetBytes(command);
            using (var v = request.GetRequestStream()) v.Write(buff, 0, buff.Length);

            var response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK) throw new WebException(response.StatusDescription);

            using (var s = new StreamReader(response.GetResponseStream()))
            {
                var v = s.ReadToEnd();
                if (AppContext.Log.UseTrace) AppContext.Log.Trace(v, "EWSTelecom");
                var xml = new XmlDocument();
                xml.LoadXml(v);
                if (_namespaceManager == null)
                {
                    _namespaceManager = new XmlNamespaceManager(xml.NameTable);
                    _namespaceManager.AddNamespace("m", URI_MSG);
                    _namespaceManager.AddNamespace("t", URI_TYPES);
                    _namespaceManager.AddNamespace("h", URI_TYPES);
                }
                if (xml.SelectSingleNode("//m:ResponseCode", _namespaceManager).InnerXml.ToLowerInvariant() != "noerror") throw new Exception(xml.SelectSingleNode("//m:MessageText", _namespaceManager).InnerXml);
                return xml;
            }
        }

        private static bool CertificateValidationCallBack(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true; // If the certificate is a valid, signed certificate, return true.
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0) // If there are errors in the certificate chain, look at each error to determine the cause.
            {
                if (chain != null && chain.ChainStatus != null)
                {
                    foreach (X509ChainStatus status in chain.ChainStatus)
                    {
                        if ((certificate.Subject == certificate.Issuer) && (status.Status == X509ChainStatusFlags.UntrustedRoot)) continue;  // Self-signed certificates with an untrusted root are valid. 
                        if (status.Status != X509ChainStatusFlags.NoError) return false; // If there are any other errors in the certificate chain, the certificate is invalid
                    }
                }
                // When processing reaches this line, the only errors in the certificate chain are untrusted root errors for self-signed certificates. These certificates are valid for default Exchange server installations.
                return true;
            }
            return true;//- ignore sertificate error // false;
        }

        public Dictionary<string, string> GetServiceInfo(string address = "")
        {
            address = address.IsEmpty() ? "" : "<m:ActingAs><t:EmailAddress>" + address + "</t:EmailAddress><t:RoutingType>SMTP</t:RoutingType></m:ActingAs>";
            var xml = GetResponse("<m:GetServiceConfiguration>" + address + "<m:RequestedConfiguration><m:ConfigurationName>" + (address.IsEmpty() ? "UnifiedMessagingConfiguration" : "MailTips") + "</m:ConfigurationName></m:RequestedConfiguration></m:GetServiceConfiguration>");

            var result = new Dictionary<string, string>();
            foreach (XmlAttribute attr in xml.SelectSingleNode("//h:ServerVersionInfo", _namespaceManager).Attributes) result.Add(attr.Name, attr.Value);
            if (address.IsEmpty()) foreach (XmlNode node in xml.SelectSingleNode("//m:UnifiedMessagingConfiguration", _namespaceManager).ChildNodes) result.Add(node.LocalName, node.InnerText);
            else foreach (XmlNode node in xml.SelectSingleNode("//m:MailTipsConfiguration", _namespaceManager).ChildNodes) result.Add(node.LocalName, node.InnerText);
            return result;
        }

        public Dictionary<string, string> ResolveName(string name = "")
        {
            var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (XmlNode v in GetResponse("<ResolveNames xmlns=\"" + URI_MSG + "\" xmlns:t=\"" + URI_TYPES + "\" ReturnFullContactData=\"true\"><UnresolvedEntry>" + (name.IsEmpty() ? Environment.UserName : name) + "</UnresolvedEntry></ResolveNames>").SelectNodes("//t:*", _namespaceManager))
                if (v.InnerText == v.InnerXml && !v.InnerText.IsEmpty()) result[v.Attributes["Key"] == null ? v.LocalName : v.Attributes["Key"].Value] = v.InnerText;
            return result;
        }

        public string GetRootFolderId()
        {
            return GetResponse("<m:GetFolder><m:FolderShape><t:BaseShape>IdOnly</t:BaseShape></m:FolderShape><m:FolderIds><t:DistinguishedFolderId Id=\"msgfolderroot\"/></m:FolderIds></m:GetFolder>")
                .SelectSingleNode("//t:FolderId/@Id", _namespaceManager).Value;
        }

        //"Входящие/Мониторинг/SCOM"
        public Folder getFolderByPath(string path, string parentId = "")
        {
            path = (path + "").Trim();
            if (path.IsEmpty()) throw new ArgumentException("Path for folder is empty");

            Folder parent = null;
            foreach (var f in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Folder result = null;
                foreach (var v in GetFolderList(parent == null ? "" : parent.Id))
                {
                    if (v.Name.Equals(f, StringComparison.InvariantCultureIgnoreCase)) { result = v; break; }
                }
                if (result == null) throw new ArgumentException("Folder not found: " + path);
                parent = result;
            }
            return parent;
        }

        public List<Folder> GetFolderList(string pid = "")
        {
            var xml = GetResponse("<FindFolder Traversal=\"Shallow\" xmlns=\"" + URI_MSG + "\"><FolderShape><t:BaseShape>Default</t:BaseShape><t:AdditionalProperties><t:FieldURI FieldURI=\"folder:FolderClass\"/></t:AdditionalProperties></FolderShape><ParentFolderIds>"
                + (pid.IsEmpty() ? "<t:DistinguishedFolderId Id=\"msgfolderroot\"/>" : ("<t:FolderId Id=\"" + pid + "\"/>")) + "</ParentFolderIds></FindFolder>");//msgfolderroot or inbox
            var result = new List<Folder>();
            foreach (XmlNode f in xml.SelectNodes("//t:Folder", _namespaceManager))
            {
                result.Add(new Folder()
                {
                    Id = f.SelectSingleNode("t:FolderId/@Id", _namespaceManager).Value,
                    Name = f.SelectSingleNode("t:DisplayName", _namespaceManager).InnerXml,
                    TotalCount = int.Parse(f.SelectSingleNode("t:TotalCount", _namespaceManager).InnerXml),
                    ChildFolderCount = int.Parse(f.SelectSingleNode("t:ChildFolderCount", _namespaceManager).InnerXml),
                    UnreadCount = int.Parse(f.SelectSingleNode("t:UnreadCount", _namespaceManager).InnerXml),
                    FolderClass = f.SelectSingleNode("t:FolderClass", _namespaceManager)?.InnerXml,
                    ParentId = pid
                });
            }
            return result;
        }

        //http://msdn.microsoft.com/en-us/library/office/ee693615%28v=exchg.150%29.aspx  //Subject:(product AND development) AND Body:(project OR proposal) AND Body:(NOT proposal)  + use GetMessageList(showTo: true);
        public List<Message> GetMessageList(string pid = "", int offset = 0, string subject = "", string attachment = "", string from = "", string body = "", string to = "", string cc = "", string bcc = "", int maxRowCount = Int32.MaxValue)
        {
            return FindMessages(pid, subject.IfEmpty("", "subject:({0}) ") + attachment.IfEmpty("", "attachment:({0}) ") + from.IfEmpty("", "from:({0}) ") + body.IfEmpty("", "body:({0}) ")
                    + to.IfEmpty("", "to:({0}) ") + cc.IfEmpty("", "cc:({0}) ") + bcc.IfEmpty("", "bcc:({0}) "), maxRowCount, offset);
        }

        private string GetInnerXML(XmlNode node, string path, string defaultValue = "")
        {
            var v = node.SelectSingleNode(path, _namespaceManager);
            return v == null ? defaultValue : v.InnerXml;
        }

        public List<Message> FindMessages(string pid = "", string queryString = "", int maxRowCount = Int32.MaxValue, int offset = 0)
        {
            var xml = GetResponse("<m:FindItem Traversal=\"Shallow\"><m:ItemShape><t:BaseShape>IdOnly</t:BaseShape>" +
                      "<t:AdditionalProperties><t:FieldURI FieldURI=\"item:Subject\" /><t:FieldURI FieldURI=\"item:DateTimeReceived\" /><t:FieldURI FieldURI=\"item:HasAttachments\"/>" +
                      "<t:FieldURI FieldURI=\"item:ItemClass\"/><t:FieldURI FieldURI=\"message:From\"/><t:FieldURI FieldURI=\"message:IsRead\"/><t:FieldURI FieldURI=\"item:DisplayTo\"/></t:AdditionalProperties></m:ItemShape>" +
                      "<m:IndexedPageItemView MaxEntriesReturned=\"" + maxRowCount + "\" Offset=\"" + offset + "\" BasePoint=\"Beginning\" />" +
                      "<m:SortOrder><t:FieldOrder Order=\"Descending\"><t:FieldURI FieldURI=\"item:DateTimeReceived\"/></t:FieldOrder></m:SortOrder>" +
                      "<m:ParentFolderIds>" + (pid.IsEmpty() ? "<t:DistinguishedFolderId Id=\"inbox\"/>" : ("<t:FolderId Id=\"" + pid + "\"/>")) + "</m:ParentFolderIds>" +
                      "<m:QueryString>" + queryString + "</m:QueryString></m:FindItem>");

            var result = new List<Message>();
            foreach (XmlNode f in xml.SelectNodes("//t:Message", _namespaceManager))
            {
                var v = new Message()
                {
                    Id = f.SelectSingleNode("t:ItemId/@Id", _namespaceManager).Value,
                    ItemClass = f.SelectSingleNode("t:ItemClass", _namespaceManager).InnerXml,
                    Subject = f.SelectSingleNode("t:Subject", _namespaceManager).InnerXml,
                    DateTimeReceived = DateTime.Parse(GetInnerXML(f, "t:DateTimeReceived", "1900.01.01")),
                    HasAttachments = bool.Parse(f.SelectSingleNode("t:HasAttachments", _namespaceManager).InnerXml),
                    IsRead = f.SelectSingleNode("t:IsRead", _namespaceManager).InnerXml == "true",//так безопаснее
                    From = GetInnerXML(f, "t:From/t:Mailbox/t:Name"),
                    Body = ""
                };
                v.To.AddRange(GetInnerXML(f, "t:DisplayTo").Split("; "));
                result.Add(v);
            }
            return result;
        }

        public Message GetMessage(string id)
        {
            var xml = GetResponse("<GetItem xmlns=\"" + URI_MSG + "\"><ItemShape><t:BaseShape>Default</t:BaseShape><t:IncludeMimeContent>false</t:IncludeMimeContent>" +
                     "<t:AdditionalProperties><t:FieldURI FieldURI=\"item:Attachments\"/><t:FieldURI FieldURI=\"item:DateTimeReceived\"/><t:FieldURI FieldURI=\"item:ItemClass\"/></t:AdditionalProperties></ItemShape><ItemIds><t:ItemId Id=\"" + id + "\"/></ItemIds></GetItem>");
            var result = new Message()
            {
                Id = id,
                ItemClass = xml.SelectSingleNode("//t:ItemClass", _namespaceManager).InnerXml,
                Subject = xml.SelectSingleNode("//t:Subject", _namespaceManager).InnerXml,
                Body = xml.SelectSingleNode("//t:Body", _namespaceManager).InnerXml,
                BodyType = xml.SelectSingleNode("//t:Body/@BodyType", _namespaceManager).Value,
                DateTimeReceived = DateTime.Parse(GetInnerXML(xml, "//t:DateTimeReceived", "1900.01.01")),
                From = GetInnerXML(xml, "//t:From/t:Mailbox/t:EmailAddress")
            };
            if (result.BodyType == "HTML") result.Body = XMLUtils.Decode(result.Body);
            foreach (XmlNode f in xml.SelectNodes("//t:FileAttachment", _namespaceManager)) result.FileAttachments.Add(new AttachmentInfo()
            {
                Id = f.SelectSingleNode("t:AttachmentId/@Id", _namespaceManager).Value,
                Name = f.SelectSingleNode("t:Name", _namespaceManager).InnerXml,
                ContentId = GetInnerXML(f, "t:ContentId"),
                ContentType = GetInnerXML(f, "t:ContentType"),
                IsInline = bool.Parse(f.SelectSingleNode("t:IsInline", _namespaceManager).InnerXml),
                Size = long.Parse(f.SelectSingleNode("t:Size", _namespaceManager).InnerXml)
            });
            foreach (XmlNode f in xml.SelectNodes("//t:ToRecipients/t:Mailbox/t:EmailAddress", _namespaceManager)) result.To.Add(f.InnerXml);
            foreach (XmlNode f in xml.SelectNodes("//t:CcRecipients/t:Mailbox/t:EmailAddress", _namespaceManager)) result.Cc.Add(f.InnerXml);
            foreach (XmlNode f in xml.SelectNodes("//t:BccRecipients/t:Mailbox/t:EmailAddress", _namespaceManager)) result.Bcc.Add(f.InnerXml);
            result.HasAttachments = result.FileAttachments.Count > 0;
            return result;
        }

        public byte[] GetAttachment(string id)
        {
            var xml = GetResponse("<GetAttachment xmlns=\"" + URI_MSG + "\" xmlns:t=\"" + URI_TYPES + "\"><AttachmentShape/>" +
                      "<AttachmentIds><t:AttachmentId Id=\"" + id + "\"/></AttachmentIds></GetAttachment>");
            return Convert.FromBase64String(xml.SelectSingleNode("//t:FileAttachment/t:Content", _namespaceManager).InnerXml);
        }

        public string DeleteMessage(string id, bool hardDelete = false)
        {
            return GetResponse("<DeleteItem DeleteType=\"" + (hardDelete ? "HardDelete" : "MoveToDeletedItems") + "\" xmlns=\"" + URI_MSG + "\"><ItemIds><t:ItemId Id=\"" + id + "\"/></ItemIds></DeleteItem>").InnerXml;
        }

        public void MarkAsRead(string id, bool asRead = true)
        {   //ChangeKey is required for this operation
            var key = GetResponse("<GetItem xmlns=\"" + URI_MSG + "\"><ItemShape><t:BaseShape>IdOnly</t:BaseShape><t:IncludeMimeContent>false</t:IncludeMimeContent></ItemShape><ItemIds><t:ItemId Id=\"" + id + "\"/></ItemIds></GetItem>").SelectSingleNode("//@ChangeKey", _namespaceManager).Value;
            GetResponse("<UpdateItem MessageDisposition=\"SaveOnly\" ConflictResolution=\"AutoResolve\" xmlns=\"" + URI_MSG + "\"><ItemChanges><t:ItemChange><t:ItemId Id=\"" + id + "\" ChangeKey=\"" + key + "\"/><t:Updates><t:SetItemField>" +
            "<t:FieldURI FieldURI=\"message:IsRead\"/><t:Message><t:IsRead>" + (asRead + "").ToLowerInvariant() + "</t:IsRead></t:Message></t:SetItemField></t:Updates></t:ItemChange></ItemChanges></UpdateItem>");
        }

        public string MoveMessage(string id, string folderID)
        {
            return GetResponse("<MoveItem xmlns=\"" + URI_MSG + "\"><ToFolderId><t:FolderId Id=\"" + folderID + "\"/></ToFolderId><ItemIds><t:ItemId Id=\"" + id + "\"/></ItemIds></MoveItem>").SelectSingleNode("//t:ItemId/@Id", _namespaceManager).Value;
        }

        public string SendMessage(string to, string subject = "", string body = "", Dictionary<string, byte[]> attachments = null, string cc = "", string bcc = "", bool saveItem = true)
        {
            var type = attachments != null && attachments.Count > 0 ? "SaveOnly" : saveItem ? "SendAndSaveCopy" : "SendOnly";
            var _to = ""; foreach (var v in to.Split(";")) _to += "<t:Mailbox><t:EmailAddress>" + v.Trim() + "</t:EmailAddress></t:Mailbox>";
            var _cc = ""; foreach (var v in cc.Split(";")) _cc += "<t:Mailbox><t:EmailAddress>" + v.Trim() + "</t:EmailAddress></t:Mailbox>";
            var _bcc = ""; foreach (var v in bcc.Split(";")) _bcc += "<t:Mailbox><t:EmailAddress>" + v.Trim() + "</t:EmailAddress></t:Mailbox>";


            var xml = GetResponse("<m:CreateItem MessageDisposition=\"" + type + "\"><m:SavedItemFolderId><t:DistinguishedFolderId Id=\"sentitems\" /></m:SavedItemFolderId><m:Items>" +
                      "<t:Message><t:Subject>" + subject + "</t:Subject><t:Body BodyType=\"HTML\">" + XMLUtils.Encode(body) + "</t:Body>" +
                      "<t:ToRecipients>" + _to + "</t:ToRecipients><t:CcRecipients>" + _cc + "</t:CcRecipients><t:BccRecipients>" + _bcc + "</t:BccRecipients></t:Message></m:Items></m:CreateItem>");
            if (type == "SaveOnly")
            {
                var pid = xml.SelectSingleNode("//t:ItemId/@Id", _namespaceManager).Value;
                var s = "<CreateAttachment xmlns=\"" + URI_MSG + "\" xmlns:t=\"" + URI_TYPES + "\"><ParentItemId Id=\"" + pid + "\"/><Attachments>";
                foreach (var att in attachments) s += "<t:FileAttachment><t:Name>" + att.Key + "</t:Name><t:Content>" + Convert.ToBase64String(att.Value) + "</t:Content></t:FileAttachment>";
                xml = GetResponse(s + "</Attachments></CreateAttachment>");
                xml = GetResponse("<SendItem xmlns=\"" + URI_MSG + "\" SaveItemToFolder=\"" + (saveItem + "").ToLower() + "\"><ItemIds><t:ItemId Id=\"" + pid + "\" ChangeKey=\"" +
                    xml.SelectSingleNode("//t:AttachmentId/@RootItemChangeKey", _namespaceManager).Value + "\"/></ItemIds></SendItem>");
            }
            return xml.InnerXml;
        }

        public string CreateTask(string subject = "", string body = "")
        {
            var xml = GetResponse("<m:CreateItem><m:Items>" +
                          "<t:Task><t:Subject>" + subject + "</t:Subject><t:Body BodyType=\"Text\">" + body + "</t:Body>" +
                          "<t:ExtendedProperty><t:ExtendedFieldURI PropertySetId=\"c11ff724-aa03-4555-9952-8fa248a11c3e\" PropertyName=\"TestProp\" PropertyType=\"String\"/><t:Value>секретное свойство</t:Value></t:ExtendedProperty>" +
                          "<t:Recurrence><t:WeeklyRegeneration><t:Interval>1</t:Interval></t:WeeklyRegeneration><t:NoEndRecurrence><t:StartDate>2014-06-15</t:StartDate></t:NoEndRecurrence> </t:Recurrence>" +
                          "<t:StartDate>2014-06-15T14:24:51.3876635-07:00</t:StartDate></t:Task></m:Items></m:CreateItem>");
            return xml.InnerXml;
        }

        public List<Task> GetTaskList(string queryString = "", int maxRowCount = Int32.MaxValue, int offset = 0)
        {
            var xml = GetResponse("<m:FindItem Traversal=\"Shallow\"><m:ItemShape><t:BaseShape>AllProperties</t:BaseShape></m:ItemShape>" +
                    "<m:IndexedPageItemView MaxEntriesReturned=\"" + maxRowCount + "\" Offset=\"" + offset + "\" BasePoint=\"Beginning\" />" +
                    "<m:ParentFolderIds><t:DistinguishedFolderId Id=\"tasks\"/></m:ParentFolderIds>" +
                    "</m:FindItem>");

            var result = new List<Task>();
            foreach (XmlNode f in xml.SelectNodes("//t:Task", _namespaceManager))
            {
                var v = new Task()
                {
                    Id = f.SelectSingleNode("t:ItemId/@Id", _namespaceManager).Value,
                    ItemClass = f.SelectSingleNode("t:ItemClass", _namespaceManager).InnerXml,
                    Subject = f.SelectSingleNode("t:Subject", _namespaceManager).InnerXml,
                    DateTimeReceived = DateTime.Parse(GetInnerXML(f, "t:DateTimeReceived", "1900.01.01")),
                    HasAttachments = bool.Parse(f.SelectSingleNode("t:HasAttachments", _namespaceManager).InnerXml),
                    Status = f.SelectSingleNode("t:Status", _namespaceManager).InnerXml,
                    Body = ""
                };
                result.Add(v);
            }
            return result;
        }

        public Task GetTask(string id)
        {
            var xml = GetResponse("<GetItem xmlns=\"" + URI_MSG + "\"><ItemShape><t:BaseShape>AllProperties</t:BaseShape><t:IncludeMimeContent>false</t:IncludeMimeContent>" +
                     "<t:AdditionalProperties><t:ExtendedFieldURI PropertySetId=\"c11ff724-aa03-4555-9952-8fa248a11c3e\" PropertyName=\"TestProp\" PropertyType=\"String\" /> " +
                     "<t:FieldURI FieldURI=\"item:Attachments\"/><t:FieldURI FieldURI=\"item:DateTimeReceived\"/><t:FieldURI FieldURI=\"item:ItemClass\"/></t:AdditionalProperties></ItemShape><ItemIds><t:ItemId Id=\"" + id + "\"/></ItemIds></GetItem>");
            var result = new Task()
            {
                Id = id,
                ItemClass = xml.SelectSingleNode("//t:ItemClass", _namespaceManager).InnerXml,
                Subject = xml.SelectSingleNode("//t:Subject", _namespaceManager).InnerXml,
                Body = xml.SelectSingleNode("//t:Body", _namespaceManager).InnerXml,
                BodyType = xml.SelectSingleNode("//t:Body/@BodyType", _namespaceManager).Value,
                DateTimeReceived = DateTime.Parse(GetInnerXML(xml, "//t:DateTimeReceived", "1900.01.01")),
                Status = xml.SelectSingleNode("//t:Status", _namespaceManager).InnerXml,
                HasAttachments = bool.Parse(xml.SelectSingleNode("//t:HasAttachments", _namespaceManager).InnerXml)
            };
            if (result.BodyType == "HTML") result.Body = XMLUtils.Decode(result.Body);
            return result;
        }

        public class Folder
        {
            public string Id { get; internal set; }
            public string ParentId { get; internal set; }
            public string Name { get; internal set; }
            public int TotalCount { get; internal set; }
            public int UnreadCount { get; internal set; }
            public int ChildFolderCount { get; internal set; }
            public string FolderClass { get; internal set; }//IPF.Note
        }

        public class Message
        {
            public string Id { get; internal set; }
            public string ItemClass { get; internal set; }//IPM.Note
            public string Subject { get; internal set; }
            public DateTime DateTimeReceived { get; internal set; }
            public bool HasAttachments { get; internal set; }
            public string Body { get; internal set; }
            public string BodyType { get; internal set; }
            public string From { get; internal set; }
            public bool IsRead { get; internal set; }
            public readonly List<string> To = new List<string>();
            public readonly List<string> Cc = new List<string>();
            public readonly List<string> Bcc = new List<string>();
            public readonly List<AttachmentInfo> FileAttachments = new List<AttachmentInfo>();
        }

        public class Task
        {
            public string Id { get; internal set; }
            public string ItemClass { get; internal set; }//IPM.Task
            public string Subject { get; internal set; }
            public DateTime DateTimeReceived { get; internal set; }
            public bool HasAttachments { get; internal set; }
            public string Body { get; internal set; }
            public string BodyType { get; internal set; }
            public string Status { get; internal set; }
        }

        public class AttachmentInfo
        {
            public string Id { get; internal set; }
            public string Name { get; internal set; }
            public string ContentType { get; internal set; }
            public string ContentId { get; internal set; }
            public long Size { get; internal set; }
            public bool IsInline { get; internal set; }

            public AttachmentInfo() { }
            public AttachmentInfo(string id, string name, string contentid, string contenttype = "", long size = 0, bool isinline = false)
            {
                Id = id; Name = name; ContentId = contentid; ContentType = contenttype; Size = size; IsInline = isinline;
            }
        }
    }
}