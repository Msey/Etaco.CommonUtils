using System.Collections.Generic;
using System.IO;
using System.Linq;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService.Telecom
{
    [Plugin("filewritetelecom")]
    public class FileWriteTelecom : WriteTelecom
    {
        private readonly WindowsUserImpersonation telecom = null;
        
        public FileWriteTelecom(TaskInfo info) : base(info) 
        {
            telecom = new WindowsUserImpersonation(info["login"], info.GetPassword());
        }

        public override void WriteMessage(MessageInfo info, List<MessageData> data)
        {
            telecom.DoAction(() => {foreach (var msg in data) msg.Stream.WriteToFile(Path.Combine(TaskInfo["path"], msg.Name.Trim()).GetValidFilePath());});
        }
    }

    [Plugin("filereadtelecom")]
    public class FileReadTelecom : ReadTelecom
    {
        private readonly WindowsUserImpersonation telecom = null;

        public FileReadTelecom(TaskInfo info) : base(info) 
        {
            telecom = new WindowsUserImpersonation(info["login"], info.GetPassword());
        }

        public override IEnumerable<MessageInfo> GetMessageList()
        {
            var result = new List<MessageInfo>();
            telecom.DoAction(() =>
            {
                var di = new DirectoryInfo(TaskInfo["path"]);
				if (di.Exists) result  = (from f in di.GetFiles() where f.Name.IsMatch(TaskInfo.Get("name","*")) orderby f.CreationTime select new MessageInfo(){Name =  f.Name, CreationTime = f.CreationTime}).ToList();
            });
            return result;
        }

        public override List<MessageData> GetMessageData(MessageInfo info)
        {
            return telecom.DoAction(() => new List<MessageData> { new MessageData(info.Name, new FileInfo(Path.Combine(TaskInfo["path"], info.Name)).OpenRead()) });
        }

        public override void DeleteMessage(MessageInfo info)
        {
            telecom.DoAction(() => new FileInfo(Path.Combine(TaskInfo["path"], info.Name)).Delete());
        }
    }
}
