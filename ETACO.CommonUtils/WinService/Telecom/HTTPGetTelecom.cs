using System;
using System.Collections.Generic;
using System.Net;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService.Telecom
{
    [Plugin("HttpGetReadTelecom")]
    public class HttpGetReadTelecom : ReadTelecom
    {
        public HttpGetReadTelecom(TaskInfo info) : base(info) { }

        public override List<MessageData> GetMessageData(MessageInfo info)
        {
            var get = WebRequest.Create(TaskInfo["path"] + info.Name);
            get.Timeout = TaskInfo.Get("timeout",60000);
            get.Credentials = new NetworkCredential(TaskInfo["login"], TaskInfo.GetPassword());
            info.CreationTime = DateTime.Now;
            return new List<MessageData>() { new MessageData(info.Name, get.GetResponse().GetResponseStream()) };
        }
    }
}
