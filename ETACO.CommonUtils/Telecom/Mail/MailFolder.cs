namespace ETACO.CommonUtils.Telecom.Mail
{
    public class MailFolder
    {
        public MailFolder(string name = "")
        {
            Name = name;
            Flags = new string[0];
        }

        public string Name { get; internal set; }
        public int New { get; internal set; }
        public int Total { get; internal set; }
        public int UnSeen { get; internal set; }
        public string[] Flags { get; internal set; }
        public bool Read { get; internal set; }
        public bool Write { get; internal set; }
    }
}
