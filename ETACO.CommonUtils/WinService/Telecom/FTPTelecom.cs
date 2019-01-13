using System;
using System.Collections.Generic;
using System.IO;
using ETACO.CommonUtils.Plugin;
using ETACO.CommonUtils.Telecom;

namespace ETACO.CommonUtils.WinService.Telecom
{

    [Plugin("FTPWriteTelecom")]
    public class FTPWriteTelecom : WriteTelecom
    {
        public FTPWriteTelecom(TaskInfo info) : base(info) { }

        public override void WriteMessage(MessageInfo info, List<MessageData> data)
        {
            using (var telecom = new FTPSClient(TaskInfo["Host"], TaskInfo["Login"], TaskInfo.GetPassword(), TaskInfo.Get("Port", 21), TaskInfo.Get("UseSSL", false), TaskInfo.Get("Timeout", 60000)) { ActiveMode = TaskInfo.Get("ActiveMode", false)})
            {
                if (!TaskInfo["Path"].IsEmpty()) telecom.ChangeDir(TaskInfo["Path"]);
                foreach (var msg in data) 
                {
                    msg.Name = msg.Name.Trim().Replace(' ', '_');
                    telecom.UploadFile(msg.Name.Trim(), msg.Stream); 
                }
            }
        }
    }
    [Plugin("FTPReadTelecom")]
    public class FTPReadTelecom : ReadTelecom
    {
        private FTPSClient telecom = null;
        private string ftpTemp = null;

        public FTPReadTelecom(TaskInfo info) : base(info)
        {
            telecom = new FTPSClient(TaskInfo["Host"], TaskInfo["Login"], TaskInfo.GetPassword(), TaskInfo.Get("Port", 21), TaskInfo.Get("UseSSL", false), TaskInfo.Get("Timeout", 60000)) { ActiveMode = TaskInfo.Get("ActiveMode", false) };
            ftpTemp = Path.Combine(AppContext.AppDir, "FtpTemp");
        }

        public override IEnumerable<MessageInfo> GetMessageList()
        {
            var result = new List<MessageInfo>();
            Array.ForEach(telecom.GetList(TaskInfo["Path"]), (f) => { if (f.IsMatch(TaskInfo.Get("Name","*"))) result.Add(new MessageInfo() { Name = f, CreationTime = telecom.GetFileDate(TaskInfo["Path"].IsEmpty() ? f : FTPSClient.GetPath(TaskInfo["Path"] + "/" + f)) }); });
            return result;
        }

        //возможное неудобство с постоянным переходом из корневой папки в целевую, но не все ftp поддерживают работу с относительными путями
        public override List<MessageData> GetMessageData(MessageInfo info)
        {
            var name = TaskInfo["Path"].IsEmpty() ? info.Name : FTPSClient.GetPath(TaskInfo["Path"] + "/" + info.Name);
            var tempFile = Path.Combine(ftpTemp, GetTempFileName(info));
            telecom.DownloadFile(name, tempFile); //можно сделать докачку
            return new List<MessageData>() { new MessageData(info.Name, new FileInfo(tempFile).OpenRead()) };
        }

        public override void DeleteMessage(MessageInfo info)
        {
            var name = TaskInfo["Path"].IsEmpty() ? info.Name : FTPSClient.GetPath(TaskInfo["Path"] + "/" + info.Name);
            telecom.RemoveFile(name);
            //
            try { new FileInfo(Path.Combine(ftpTemp, GetTempFileName(info))).Delete(); }
            catch { }
        }

        public override void Close()
        {
            if (telecom != null)
            {
                telecom.Disconnect();
                telecom = null;
            }
        }

        private string GetTempFileName(MessageInfo info)
        {
            return TaskInfo["Host"] + "_" + TaskInfo["Port"] + "_" + TaskInfo["Path"].Replace("/", "_") + "_" + info.Name + "_" + info.CreationTime.ToString("yyyyMMddHHmmss");
        }
    }
}
