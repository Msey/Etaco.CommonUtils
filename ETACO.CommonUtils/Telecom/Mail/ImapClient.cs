using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace ETACO.CommonUtils.Telecom.Mail
{

    #region Example
    /*using (var imap = new ImapClient("mail.ETACO.RU","user@etaco.ru", "test"))//port=993 for ssl
{
    var v = imap.GetFolders("INBOX", "*мо*");
    var v2 = imap.GetFolderMessageCount(v[0]);
    v = imap.GetFolders("INBOX");
    var v3 = imap.SelectFolder(v[0]);
    var v4 = imap.SearchMessage("SUBJECT FW:");//!!!
    var v5 = imap.GetMessages(v4[0]);
    //imap.DeleteMessage(v3[0]);
    //imap.CommitDeletingMessages();
}*/
    #endregion

    /// <summary> IMAP клиент </summary>
    public sealed class ImapClient : TelecomClient //APPEND - не используем
    {
        private const string TAG = "001";
        public string Login { get; private set; }
        public string Password { get; private set; }

        public ImapClient(string host, string login, string password, int port = 143, bool useSsl = false, int timeout = 0, RemoteCertificateValidationCallback certValidator = null)
            : base(host, port, useSsl, timeout, certValidator)
        {
            if (login.IsEmpty()) throw new ArgumentNullException("login");
            if (password.IsEmpty()) throw new ArgumentNullException("password");

            Login = login;
            Password = password;
            AfterConnect += () =>
            {
                Send("", (v) => false);//read welcome message
                GetResponse("LOGIN {0} {1}".FormatStr(Login, Password)); //GetResponse("AUTH NTLM\r\n");
            };
            BeforeDisconnect += () => GetResponse("LOGOUT"); //ждём пока пройдёт Commit на сервере, иначе можно было бы GetResponse("LOGOUT", null);
        }

        public MailResponse GetResponse(string command = null, bool checkResult = true)
        {
            var result = new MailResponse();

            Send(command.IfEmpty("", TAG + " {0}"), (s) =>
            {
                if (s.StartsWith(TAG, StringComparison.Ordinal)) //end of message
                {
                    result.Header = s.Substring(s.IndexOf(' ')).Trim();
                    if (!result.Header.StartsWith("OK", StringComparison.Ordinal)) throw new Exception("Imap({0}:{1}) Error: {2}".FormatStr(Host, Port, result.Header));
                    return false;
                }
                result.Data.Add(s);
                return true;
            });

            if (Log.UseTrace) Log.Trace("Imap({0}:{1}) Receive: {2}".FormatStr(Host, Port, result.Header));
            return result;
        }

        private static string Decode(string line)
        {
            return Encoding.UTF7.GetString(Encoding.UTF8.GetBytes(line.Replace("&", "+").Replace(",", "/"))).ToUpper();
        }

        private static string Encode(string line)
        {
            string newLine = Encoding.ASCII.GetString(Encoding.UTF7.GetBytes(line.ToUpper()));
            return newLine == line ? line : newLine.Replace("+", "&")/*.Replace("/", ",")*/;
        }

        private static string Encode(string line, string separator)
        {
            var v = line.Split(separator, false);
            for (int i = 0; i < v.Length; i++) v[i] = Encode(v[i]);
            return string.Join(separator, v);
        }

        private static List<string> GetListResult(MailResponse response)
        {
            var result = new List<string>();
            foreach (var v in response.Data)
            {
                if (v.StartsWith("*", StringComparison.Ordinal))
                {
                    var line = v.Substring(v.IndexOf(')') + 1).Trim();   // Remove * LIST(..) // LSUB
                    line = line.Substring(line.IndexOf(' ')).Trim();     // Remove Folder separator
                    result.Add(Decode(line.Trim('"')));
                }
            }
            return result;
        }

        private static MailFolder GetSelectResult(MailResponse response, string folder)
        {
            MailFolder x = null;
            if (response.Data.Count > 0)
            {
                x = new MailFolder(folder);
                foreach (var v in response.Data)
                {
                    Match m;
                    m = Regex.Match(v, @"(\d+) EXISTS");
                    if (m.Groups.Count > 1) { x.Total = Convert.ToInt32(m.Groups[1].ToString()); }
                    m = Regex.Match(v, @"(\d+) RECENT");
                    if (m.Groups.Count > 1) x.New = Convert.ToInt32(m.Groups[1].ToString());
                    m = Regex.Match(v, @"UNSEEN (\d+)");
                    if (m.Groups.Count > 1) x.UnSeen = Convert.ToInt32(m.Groups[1].ToString());
                    m = Regex.Match(v, @" FLAGS \((.*?)\)");
                    if (m.Groups.Count > 1) x.Flags = m.Groups[1].ToString().Split(' ');
                }
                x.Read = response.Header.ToUpper().IndexOf("READ", StringComparison.Ordinal) > -1;
                x.Write = response.Header.ToUpper().IndexOf("WRITE", StringComparison.Ordinal) > -1;
            }
            return x;
        }

        /// <summary> Возвращает список папок в почтовом ящике </summary>
        /// <remarks> var v = imap.GetFolders("INBOX", "*te*");</remarks>
        public List<string> GetFolders(string reference = "", string pattern = "*")
        {
            return GetListResult(GetResponse("LIST \"{0}\" \"{1}\"".FormatStr(Encode(reference), Encode(pattern, "*")), false));
        }

        /// <summary> Возвращает список подписанных папок в почтовом ящике </summary>
        public List<string> GetSuscribeFolders(string reference = "", string pattern = "*")
        {
            return GetListResult(GetResponse("LSUB \"{0}\" \"{1}\"".FormatStr(Encode(reference), Encode(pattern, "*")), false));
        }

        /// <summary> Добавляем папку в список подписаных</summary>
        public void SubscribeFolder(string folder)
        {
            GetResponse("SUBSCRIBE \"{0}\"".FormatStr(Encode(folder)));
        }

        /// <summary> Удаляем папку из списка подписаных</summary>
        public void UnsubscribeFolder(string folder)
        {
            GetResponse("UNSUBSCRIBE \"{0}\"".FormatStr(Encode(folder)));
        }

        /// <summary> Выбор папки в почтовом ящике для последующей работы с ней </summary>
        public MailFolder SelectFolder(string folder)
        {
            return GetSelectResult(GetResponse("SELECT \"{0}\"".FormatStr(Encode(folder)), false), folder);
        }

        /// <summary> Выбор папки в почтовом ящике для последующей работы с ней (read-only)</summary>
        public MailFolder ExamineFolder(string folder)
        {
            return GetSelectResult(GetResponse("EXAMINE \"{0}\"".FormatStr(Encode(folder)), false), folder);
        }

        /// <summary> Возвращает количество сообщений в папке</summary>////REFACTOR (пока проблемма с русскими буквами)
        public int GetFolderMessageCount(string folder)
        {
            var response = GetResponse("STATUS " + Encode(folder) + " (MESSAGES)");
            int result = 0;
            if (response.Data.Count > 0)
            {
                var m = Regex.Match(response.Data[0], @"\* STATUS.*MESSAGES (\d+)");
                if (m.Groups.Count > 1) result = Convert.ToInt32(m.Groups[1].ToString());
            }
            return result;
        }

        /// <summary> Создаём папку </summary>
        public void CreateFolder(string folder)
        {
            GetResponse("CREATE \"{0}\"".FormatStr(Encode(folder)));
        }

        /// <summary> Удаляем папку </summary>
        public void DeleteFolder(string folder)
        {
            GetResponse("DELETE \"{0}\"".FormatStr(Encode(folder)));
        }

        /// <summary> Переименовываем папку </summary>
        public void RenameFolder(string from, string to)
        {
            GetResponse("RENAME \"{0}\" \"{1}\"".FormatStr(Encode(from), Encode(to)));
        }

        /// <summary> Возвращает UID писем из текущей папки удовлетворяющих критерию поиска (сначала нужно вызвать Select)</summary> 
        /// <remarks>imap.Search("FROM s.belo SUBJECT \"How to improve\"")</remarks>
        /// <remarks>Не все сервара поддерживают русские буквы (charset=UTF-8 или другие)</remarks>
        public List<string> SearchMessage(string criteria = "ALL", string charset = "US-ASCII")
        {
            var response = GetResponse("UID SEARCH CHARSET " + charset + " " + criteria);

            var x = new List<string>();
            foreach (var v in response.Data)
            {
                var m = Regex.Match(v, @"^\* SEARCH (.*)");
                if (m.Success) Array.ForEach(m.Groups[1].ToString().Trim().Split(' '), x.Add);
            }
            return x;
        }

        /// <summary> Возвращает сообщения или их заголовоки </summary>
        public List<MailMimeMessage> GetMessages(string startUID, bool headersonly = false, bool setseen = false, string endUID = "")
        {

            string HEADERS = headersonly ? "HEADER" : "";
            string SETSEEN = setseen ? ".PEEK" : "";
            var response = GetResponse("UID FETCH " + startUID + ":" + endUID.IfEmpty(startUID) + " (UID RFC822.SIZE FLAGS BODY" + SETSEEN + "[" + HEADERS + "])");

            var result = new List<MailMimeMessage>();
            if (response.Data.Count > 0)
            {
                var reg = new Regex(@"\* \d+ FETCH.*?BODY.*?\{(\d+)\}");
                var lines = new Queue<string>(response.Data);
                var buffer = new List<string>();

                lines.Dequeue();
                while (lines.Count > 0)
                {
                    var s = lines.Dequeue();
                    while (lines.Count > 0 && !reg.IsMatch(s))
                    {
                        buffer.Add(s);
                        s = lines.Dequeue();
                    }
                    result.Add(new MailMimeMessage(new MimeReader(buffer), Encoding));
                    buffer.Clear();
                }
            }
            return result;
        }

        /// <summary> Возвращает список поддерживаемых сервером опций</summary>
        public string[] Capability()
        {
            var response = GetResponse("CAPABILITY");
            return response.Data[0].StartsWith("* CAPABILITY ", StringComparison.Ordinal) ? response.Data[0].Substring(13).Trim().Split(' ') : response.Data[0].Trim().Split(' ');
        }

        /// <summary> Ничего не делаем (ping) </summary>
        public void Noop()
        {
            GetResponse("NOOP");
        }

        /// <summary> Копируем сообщения в папку </summary>//нужно вернуть новый UID
        public void CopyMessages(string startUID, string folder, string endUID = "")
        {
            GetResponse("UID COPY " + startUID + ":" + endUID.IfEmpty(startUID) + " \"" + Encode(folder) + "\"");
        }

        /// <summary> Помечаем сообщение на удаление (потом нужно вызвать CommitDeletingMessages)</summary>
        public void DeleteMessage(string uid)
        {
            SetMessagesFlags(uid, "\\Seen \\Deleted");
        }

        /// <summary> Перемещаем сообщение (copy + delete)</summary>
        public void MoveMessage(string uid, string folderName)
        {
            CopyMessages(uid, folderName);
            DeleteMessage(uid);
        }

        /// <summary> Удаляем все помеченные на удаления сообщения </summary>
        public void CommitDeletingMessages()
        {
            GetResponse("EXPUNGE");
        }

        /// <summary> Устанавливает флаги для сообщений</summary>
        /// <remarks>   imap.Store("2:4", "\\Seen"); \answered \flagged \deleted \draft</remarks>
        public void SetMessagesFlags(string startUID, string flags, bool replace = true, string endUID = "")
        {
            GetResponse("UID STORE " + startUID + ":" + endUID.IfEmpty(startUID) + " " + (replace ? "+" : "") + "FLAGS.SILENT (" + flags + ")");
        }
    }
}