using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;

namespace ETACO.CommonUtils.Telecom.Mail
{

    #region Example
    /*using (var pop3 = new Pop3Client("cas.etaco.ru","user@etaco.ru", "test")) //port=995 for SSL
{
    var v1 = pop3.GetMessageCount();
    var v2 = pop3.GetMessageList();
    v2.Reverse();
    var v3 = pop3.GetMessage(v2[10]);
}*/
    #endregion

    /// <summary> POP3 клиент </summary>
    /// <remarks> Работа с pop3 подразумевает наличие сессии (соединения), если происходит IOException или SocketException, то сессия была разорвана и id сообщений нужно переначитывать заново</remarks>
    public sealed class Pop3Client : TelecomClient
    {
        public string Login { get; private set; }
        public string Password { get; private set; }

        public Pop3Client(string host, string login, string password, int port = 110, bool useSsl = false, int timeout = 60000, RemoteCertificateValidationCallback certificateValidator = null)
            : base(host, port, useSsl, timeout, certificateValidator)
        {
            if (login.IsEmpty()) throw new ArgumentNullException("login");
            if (password.IsEmpty()) throw new ArgumentNullException("password");

            Login = login;
            Password = password;
            AfterConnect += () =>
            { //GetResponse("AUTH NTLM");
                GetResponse();//read welcome message
                GetResponse("USER {0}".FormatStr(Login));
                GetResponse("PASS {0}".FormatStr(Password));
            };
            BeforeDisconnect += () => GetResponse("QUIT"); //GetResponse("QUIT", null); - чтобы не ждать ответа от сервера
        }

        public MailResponse GetResponse(string command = null, bool isMultiline = false)//public только для dump
        {
            var result = new MailResponse();

            Send(command, (s) =>
            {
                if (s == ".") return false; //end of message
                if (result.Header == null) //begin of message
                {
                    result.Header = s;
                    if (!s.StartsWith("+OK", StringComparison.Ordinal)) throw new Exception("Pop3({0}:{1}) Error: {2}".FormatStr(Host, Port, result.Header)); //"-ERR"
                    return isMultiline;
                }
                result.Data.Add(!s.IsEmpty() && s[0] == '.' ? s.Substring(1) : s);
                return true;
            });

            if (Log.UseTrace) Log.Trace("Pop3({0}:{1}) Receive: {2}".FormatStr(Host, Port, result.Header));
            return result;
        }

        /// <summary>Ничего не делает (ping) </summary>
        public void Noop()
        {
            GetResponse("NOOP");
        }

        /// <summary> Откат транзакций внутри сессии </summary>
        /// <remarks> Если пользователь случайно пометил на удаление какие-либо сообщения, он может убрать эти пометки, отправив эту команду</remarks>
        public void Reset()
        {
            GetResponse("RSET");
        }

        /// <summary> Возвращает количество сообщений в почтовом ящике </summary>
        public int GetMessageCount()
        {
            var response = GetResponse("STAT");
            var v = response.Header.Split(' '); //should consist of '+OK', 'messagecount', 'octets'
            if (v.Length < 3) throw new Exception("Invalid response message: " + response.Header);
            return Convert.ToInt32(v[1]);
        }

        /// <summary> Возвращает суммарный размер сообщений в почтовом ящике </summary>
        public long GetTotalSize()
        {
            var response = GetResponse("STAT");
            var v = response.Header.Split(' '); //should consist of '+OK', 'messagecount', 'octets'
            if (v.Length < 3) throw new Exception("Invalid response message: " + response.Header);
            return Convert.ToInt64(v[2]);
        }

        /// <summary> Возвращает список id для всех сообщений в почтовом ящике </summary>
        /// <remarks> Сообщения, помеченные для удаления, не перечисляются.</remarks>
        public List<int> GetMessageList()
        {
            var response = GetResponse("LIST ", true);
            var result = new List<int>();
            foreach (var line in response.Data)
            {
                var v = line.Split(' ');
                if (v.Length < 2) throw new Exception("Invalid line in multiline response:  " + line);
                result.Add(Convert.ToInt32(v[0]));
            }
            return result;
        }

        /// <summary>Помечает указанное сообщение для удаления </summary>
        /// <remarks>Удаляются только после закрытия транзакции (закрытие транзакций происходит обычно после посыла команды QUIT)</remarks>
        public void Delete(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException("pop3 delMsgId");
            GetResponse("DELE {0}".FormatStr(id));
        }

        /// <summary> Возвращает размер сообщения </summary>
        public long GetMessageSize(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException("pop3 getMsgSizeId");
            var response = GetResponse("LIST {0}".FormatStr(id));
            var v = response.Header.Split(' '); //should consist of '+OK messageNumber octets'
            if (v.Length < 3) throw new Exception("Invalid response message: " + response.Header);
            return Convert.ToInt64(v[2]);
        }

        /// <summary> Возвращает сообщение или его заголовок и указанное количество строк из тела письма (lineCount >=0) </summary>
        /// <param name="encoding">Кодировка для чтения ответа с сервера (по умолчанию utf-8), если = null, то используется текущая кодировка</param>
        /// <remarks> В случае lineCount >=0 Attachment не возвращается (только Subject и Body)</remarks>
        public MailMimeMessage GetMessage(int id, Encoding encoding = null, int lineCount = -1)
        {
            if (id < 0) throw new ArgumentOutOfRangeException("pop3 getMsgId");
            var command = (lineCount < 0) ? "RETR {0}".FormatStr(id) : "TOP {0} {1}".FormatStr(id, lineCount);
            var oldEncoding = Encoding;
            try
            {
                Encoding = encoding ?? Encoding;
                var response = GetResponse(command, true);
                return new MailMimeMessage(new MimeReader(response.Data), Encoding);
            }
            finally
            {
                Encoding = oldEncoding;
            }
        }

        /// <summary> Возвращает список id для всех сообщений в почтовом ящике удовлетворяющих критерию поиска </summary>
        /// <returns> criteria => "from=*s.belo* subject=FW:*"</returns>
        /// <remarks> Сообщения, помеченные для удаления, не перечисляются.</remarks>
        public List<int> GetMessageList(string criteria, Encoding encoding = null)
        {
            var result = new List<int>();
            var filters = ParseCriteria(criteria);

            var oldEncoding = Encoding;
            Encoding = encoding ?? Encoding;
            try
            {
                foreach (var v in GetMessageList())
                {
                    var m = new MimeReader(GetResponse("TOP {0} 0".FormatStr(v), true).Data);
                    foreach (var f in filters)
                    {
                        if (!MimeReader.DecodeWord(m.Headers[f.Key] + "").Trim().IsMatch(f.Value)) { m = null; break; }
                    }
                    if (m != null) result.Add(v);
                }
            }
            finally
            {
                Encoding = oldEncoding;
            }
            return result;
        }

        private static Dictionary<string, string> ParseCriteria(string criteria)
        {
            var result = new Dictionary<string, string>();
            var s = criteria.TrimStart();
            while (s.Length > 0)
            {
                var i = s.IndexOf('=');
                if (i < 0) throw new Exception("Incorrect criteria format: " + criteria);
                var key = s.Substring(0, i);

                s = s.Substring(i + 1).TrimStart();
                if (s.Length == 0) throw new Exception("Incorrect criteria format: " + criteria);

                if (s[0] == '"')
                {
                    i = 0;
                    while ((i = s.IndexOf('"', i + 1)) > 0) if (s[i - 1] != '\\') break;
                    result.Add(key, i < 0 ? s.Substring(1) : s.Substring(1, i - 1));
                    s = i < 0 ? "" : s.Substring(i + 1).TrimStart();
                }
                else
                {
                    i = s.IndexOf(' ');
                    result.Add(key, i < 0 ? s : s.Substring(0, i));
                    s = i < 0 ? "" : s.Substring(i).TrimStart();
                }
            }
            return result;
        }
    }
}