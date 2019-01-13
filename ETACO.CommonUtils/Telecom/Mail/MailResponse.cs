using System.Collections.Generic;

namespace ETACO.CommonUtils.Telecom.Mail
{
    public sealed class MailResponse
    {
        public string Header { get; set; }
        public readonly List<string> Data = new List<string>();

        public override string ToString()
        {
            return Header + "\r\n" + string.Join("\r\n", Data.ToArray());
        }
    }
}
