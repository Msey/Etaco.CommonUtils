using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace ETACO.CommonUtils.Telecom
{
    #region Example
    /*using (var ftp = new FTPSClient("testftp.myftp.org", "ftpuser", "ftppass"))
    {
    var v = ftp.GetList("Incoming");
    var l = ftp.GetFileSize("/dir1/dir2/dir3//file.dat");
    ftp.DownloadFile("/dir1/dir2/dir3//file.dat", @"C:\Temp\test.dat");
    ftp.UploadFile("/Incoming/test.dat", @"C:\Temp\test.dat");   
    }*/
    #endregion

    /// <summary> FTP клиент </summary>
    /// <remarks> Есть FTP(OFTP) сервера, которые не поддерживаю некоторые команды, которые используются при работе с FtpWebRequest, для общения с ними предназначен этот класс </remarks>
    public sealed class FTPSClient : TelecomClient
    {
        private const int ConnectionTimeout = 5000;
        private bool binaryMode = false;//true
        public bool ActiveMode { get; set; }
        public string Login { get; private set; }
        public string Password { get; private set; }

        //если используем ssl то и для данных и для команд одновременно - получаем ftps
        public FTPSClient(string host, string login = null, string password = null, int port = 21, bool useSsl = false, int timeout = 60000, RemoteCertificateValidationCallback certValidator = null)
            : base(host, port, useSsl, timeout, certValidator)
        {
            Login = login;
            Password = password;
            //var uri = new UriBuilder(host);
            //Host = (uri.Scheme == "ftp") ? uri.Host : host;
            //Port = (uri.Scheme == "ftp" && port == 21) ? uri.Port : port;
            ActiveMode = false;
            AfterConnect += () =>
            {
                if (!GetResponse().StartsWith("220", StringComparison.Ordinal)) throw new Exception("Осутствует приветствие FTP сервера");
                if (!Login.IsEmpty())
                {
                    var response = GetResponse("USER " + Login);
                    if (response.StartsWith("220", StringComparison.Ordinal)) response = GetResponse("", "220");//всё ещё идёт приведствие

                    if (!(response.StartsWith("230", StringComparison.Ordinal) || response.StartsWith("331", StringComparison.Ordinal))) { Disconnect(); throw new Exception("Ошибка при авторизации пользователя: " + response); }
                    if (response.StartsWith("331", StringComparison.Ordinal))
                    {
                        response = GetResponse("PASS " + Password);
                        if (!(response.StartsWith("230", StringComparison.Ordinal) || response.StartsWith("202", StringComparison.Ordinal))) { Disconnect(); throw new Exception("Ошибка при авторизации пользователя: " + response); }
                        //читаем приветствие до конца
                        GetResponse("NOOP", "230");
                    }
                }
            };
            BeforeDisconnect += () => Send("QUIT");
        }

        private string GetResponse(string command = null, string skipCode = "")
        {
            var result = "";
            Send(command, (s) =>
            {
                if (!skipCode.IsEmpty() && s.StartsWith(skipCode, StringComparison.Ordinal)) return true;
                result = s;
                return false;
            });
            if (result.IsEmpty()) Send("NOOP", (s) => //NOOP - принудительно получаем ответ с сервера
            {
                if (result.IsEmpty()) { result = s; return true; }
                return false;
            });
            return result;
        }

        /// <summary>Двоичный режим передачи файлов </summary>
        public bool BinaryMode
        {
            get { return binaryMode; }
            set
            {
                if (binaryMode != value)
                {
                    var v = GetResponse(value ? "TYPE I" : "TYPE A");
                    if (!v.StartsWith("200", StringComparison.Ordinal)) throw new Exception("Ошибка установки режима передачи файлов: " + v);
                    binaryMode = value;
                }
            }
        }

        /// <summary> Смена каталога (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void ChangeDir(string path)
        {
            var v = GetResponse("CWD " + path);
            if (!v.StartsWith("250", StringComparison.Ordinal)) throw new Exception("Ошибка при смене каталога: " + v);
        }

        /// <summary>  Получение текущего каталога </summary>
        public string GetCurrentDir()
        {
            var v = GetResponse("PWD");
            if (!v.StartsWith("257", StringComparison.Ordinal)) throw new Exception("Ошибка при запросе текущего каталога: " + v);
            return v.Split('"')[1];
        }

        /// <summary> Создание каталога (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void MakeDir(string path)
        {
            var v = GetResponse("MKD " + path);
            if (!(v.StartsWith("257", StringComparison.Ordinal) || v.StartsWith("250", StringComparison.Ordinal))) throw new Exception("Ошибка при создании каталога :" + v);
        }

        /// <summary> Удаление каталога (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void RemoveDir(string path)
        {
            var v = GetResponse("RMD " + path);
            if (!v.StartsWith("250", StringComparison.Ordinal)) throw new Exception("Ошибка при удалении каталога: " + v);
        }

        /// <summary> Получение даты файла (../f1//f2/f3.txt), если начинается с '/' - то путь абсолютный</summary>
        public DateTime GetFileDate(string fileName)
        {
            var v = GetResponse("MDTM " + fileName);
            if (!v.StartsWith("213", StringComparison.Ordinal)) throw new Exception("Ошибка получение даты файла: " + v);
            try
            {
                return DateTime.ParseExact(v.Split(' ')[1].Trim(), "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { throw new Exception("Некорректный формат даты файла: " + v, ex); }
        }

        /// <summary> Получение размера файла (../f1//f2/f3.txt), если начинается с '/' - то путь абсолютный</summary>
        public long GetFileSize(string fileName)
        {
            var v = GetResponse("SIZE " + fileName);
            if (!v.StartsWith("213", StringComparison.Ordinal)) throw new Exception("Ошибка получение размера файла: " + v);
            return long.Parse(v.Split(' ')[1].Trim());
        }

        /// <summary> Удаление файла (../f1//f2/f3.txt), если начинается с '/' - то путь абсолютный</summary>
        public void RemoveFile(string fileName)
        {
            var v = GetResponse("DELE " + fileName);
            if (!v.StartsWith("250", StringComparison.Ordinal)) throw new Exception("Ошибка при удалении файла: " + v);
        }

        /// <summary> Переименование файла (../f1//f2/f3.txt /f4/f5.txt), если начинается с '/' - то путь абсолютный</summary>
        public void RenameFile(string oldName, string newName)
        {
            var v = GetResponse("RNFR " + oldName);
            if (!v.StartsWith("350", StringComparison.Ordinal)) throw new Exception("Ошибка при переименовании файла: " + v);
            v = GetResponse("RNTO " + newName);
            if (!v.StartsWith("250", StringComparison.Ordinal)) throw new Exception("Ошибка при переименовании файла: " + v);
        }

        ////////////////////////   USE dataSocket  ////////////////////////

        /// <summary> Получение списка файлов и каталогов (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public string[] GetList(string path = "")
        {
            string dir = ""; //некоторые ftp не поддерживают path для NLST //NLST [remote-directory]
            if (!path.IsEmpty())
            {
                dir = GetCurrentDir();
                ChangeDir(path);
            }
            try
            {
                using (var ms = new MemoryStream())
                {
                    DoActionWithDataSocket((s) => s.CopyTo(ms, 4096), "NLST", ms, 0);
                    return Encoding.GetString(ms.ToArray()).Split("\r\n");
                }
            }
            finally
            {
                if (!dir.IsEmpty()) ChangeDir(dir);
            }
        }

        /// <summary> Получение списка файлов (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public FTPFileInfo[] GetFileList(string path = "")
        {
            var result = new List<FTPFileInfo>();
            Array.ForEach(GetList(path), (v) => { var f = path.IfEmpty(v, "{0}/" + v); try { result.Add(new FTPFileInfo(v, GetFileSize(f), GetFileDate(f))); } catch { } });
            return result.ToArray();
        }

        /// <summary> Получение списка каталогов (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public string[] GetDirList(string path = "")
        {
            var result = new List<string>();
            Array.ForEach(GetList(path), (v) => { if (!GetResponse("SIZE " + (path.IfEmpty(v, "{0}/" + v))).StartsWith("213", StringComparison.Ordinal)) result.Add(v); });
            return result.ToArray();
        }

        /// <summary> Получение файла (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void DownloadFile(string fileName, Stream stream, long offset = 0)
        {
            var oldDir = "";  //некоторые ftp не поддерживают относительные пути для RETR 
            var dir = GetDirectoryName(fileName);
            if (!dir.IsEmpty() && !dir.StartsWith("/", StringComparison.Ordinal))
            {
                oldDir = GetCurrentDir();
                fileName = GetFileName(fileName);
                ChangeDir(dir);
            }
            try
            {
                DoActionWithDataSocket((s) => s.CopyTo(stream, 16384), "RETR " + fileName, stream, offset);
            }
            finally
            {
                if (!oldDir.IsEmpty()) ChangeDir(oldDir);
            }
        }

        /// <summary> Получение файла (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void DownloadFile(string fileName, string localFileName, long offset = 0)
        {
            var fi = new FileInfo(localFileName);
            fi.Directory.Create();
            using (Stream stream = fi.Open(FileMode.OpenOrCreate, FileAccess.Write))
            {
                DownloadFile(fileName, stream, offset);
            }
        }

        /// <summary>Загрузка файла на сервер (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void UploadFile(string fileName, Stream stream, long offset = 0)
        {
            //некоторые ftp не поддерживают относительные пути для STOR 
            var oldDir = "";
            var dir = GetDirectoryName(fileName);
            if (!dir.IsEmpty() && !dir.StartsWith("/", StringComparison.Ordinal))
            {
                oldDir = GetCurrentDir();
                fileName = GetFileName(fileName);
                ChangeDir(dir);
            }

            try
            {
                DoActionWithDataSocket((s) => stream.CopyTo(s, 16384), "STOR " + fileName, stream, offset);
            }
            finally
            {
                if (!oldDir.IsEmpty()) ChangeDir(oldDir);
            }
        }

        /// <summary>Загрузка файла на сервер (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void UploadFile(string fileName, string localFileName, long offset = 0)
        {
            using (Stream stream = new FileInfo(localFileName).OpenRead())
            {
                UploadFile(fileName, stream, offset);
            }
        }

        private void DoActionWithDataSocket(Action<Stream> action, string command, Stream stream, long offset)
        {
            try
            {
                var response = "";
                if (ActiveMode)
                {
                    var tcpListener = GetConnectedListener(command, stream, offset);
                    try
                    {
                        var tcpClient = tcpListener.AcceptTcpClient();
                        try
                        {
                            response = GetResponse();//тут может подвиснуть
                            if (!(response.StartsWith("150", StringComparison.Ordinal) || response.StartsWith("125", StringComparison.Ordinal))) throw new Exception("Ошибка при открытии dataSocket: " + response);
                            action(UseSsl ? tcpClient.GetSslStream(Host, CertificateValidator) : tcpClient.GetStream());
                        }
                        finally
                        {
                            try
                            {
                                if (tcpClient.Connected) tcpClient.GetStream().Close();
                            }
                            finally
                            {
                                tcpClient.Close();
                                tcpClient = null;
                            }
                        }
                    }
                    finally
                    {
                        tcpListener.Stop();
                        tcpListener = null;
                    }
                }
                else //passive
                {
                    var tcpClient = GetConnectedClient();
                    try
                    {
                        if ((offset > 0 && (BinaryMode = true) && GetResponse("REST " + offset).StartsWith("350", StringComparison.Ordinal))) stream.Seek(offset, SeekOrigin.Begin);
                        response = GetResponse(command);
                        if (!(response.StartsWith("150", StringComparison.Ordinal) || response.StartsWith("125", StringComparison.Ordinal))) throw new Exception("Ошибка при открытии dataSocket: " + response);
                        action(UseSsl ? tcpClient.GetSslStream(Host, CertificateValidator) : tcpClient.GetStream());
                    }
                    finally
                    {
                        try
                        {
                            if (tcpClient.Connected) tcpClient.GetStream().Close();
                        }
                        finally
                        {
                            tcpClient.Close();
                            tcpClient = null;
                        }
                    }

                }
                //!!!!!!!!!!!Важно, что получения ответа должно быть после dataSocket.Close()
                response = GetResponse();
                if (!(response.StartsWith("226", StringComparison.Ordinal) || response.StartsWith("250", StringComparison.Ordinal))) throw new Exception("Ошибка при загрузке данных: " + response);
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        private TcpListener GetConnectedListener(string command, Stream stream, long offset, int attempt = 5)
        {
            attempt = Math.Max(attempt, 1);

            Func<TcpListener> getConnectedListner = () =>
            {
                var adr = Array.Find(Dns.GetHostEntry(Dns.GetHostName()).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
                if (adr == null) throw new Exception("Can't get local IPv4 address");

                var tcpListener = new TcpListener(new IPEndPoint(adr, 0));
                try
                {
                    tcpListener.Start();

                    int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;// 50000-65538;

                    var response = GetResponse("PORT {0},{1},{2}".FormatStr((adr + "").Replace('.', ','), Math.Floor(port / 256d), port % 256d));

                    if (!response.StartsWith("200", StringComparison.Ordinal)) throw new Exception("Ошибка при соединении с сервером в активном режиме: " + response);

                    if ((offset > 0 && (BinaryMode = true) && GetResponse("REST " + offset).StartsWith("350", StringComparison.Ordinal))) stream.Seek(offset, SeekOrigin.Begin);
                    Send(command);
                    for (int i = 0; i < ConnectionTimeout && !tcpListener.Pending(); i += 100, System.Threading.Thread.Sleep(100)) ;

                    if (!tcpListener.Pending()) throw new Exception("Ошибка при соединении с сервером в активном режиме: "/* + GetResponse()*/); //GetResponse() - blocked
                }
                catch
                {
                    tcpListener.Stop();
                    tcpListener = null;
                    throw;
                }
                return tcpListener;
            };

            var msg = "";
            for (int i = 0; i < attempt; i++)
            {
                try
                {
                    return getConnectedListner();
                }
                catch (Exception ex)
                {
                    if (msg.IsEmpty()) msg = ex.Message;
                }
            }
            throw new Exception("Не удалось выполнить подключение для передачи данных: " + msg);
        }

        private TcpClient GetConnectedClient(int attempt = 5)
        {
            attempt = Math.Max(attempt, 1);
            Func<TcpClient> getConnectedClient = () =>
            {
                var response = GetResponse("PASV");
                if (!response.StartsWith("227", StringComparison.Ordinal)) throw new Exception("Ошибка при соединении с сервером в пассивном режиме");
                string server = "";
                int port = 0;

                try
                {
                    int i1 = response.IndexOf('(') + 1;
                    int i2 = response.IndexOf(')', i1) - i1;
                    string[] param = response.Substring(i1, i2).Split(",");
                    server = "{0}.{1}.{2}.{3}".FormatStr(param[0], param[1], param[2], param[3]);
                    port = (int.Parse(param[4]) << 8) + int.Parse(param[5]);
                }
                catch (Exception ex) { throw new Exception("Неправильный ответ PASV: " + response, ex); }

                var tcpClient = new TcpClient();
                tcpClient.SendTimeout = Timeout;
                tcpClient.ReceiveTimeout = Timeout;
                try
                {
                    //connection timeout - 23 сек - нужно use begin connection
                    tcpClient.Connect(server, port, ConnectionTimeout); //проблемма в номере порта - A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond 95.84.170.14:55551
                }
                catch
                {
                    tcpClient.Close();
                    tcpClient = null;
                    throw;
                }
                return tcpClient;
            };

            var msg = "";
            for (int i = 0; i < attempt; i++)
            {
                try
                {
                    return getConnectedClient();
                }
                catch (Exception ex)
                {
                    if (msg.IsEmpty()) msg = ex.Message;
                }
            }
            throw new Exception("Не удалось выполнить подключение для передачи данных: " + msg);
        }

        /// <summary>Удаление лишних символов из пути</summary>
        public static string GetPath(string path)
        {
            if (path.IsEmpty()) return path;
            var result = string.Join("/", path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            result = (path[0] == '/' ? "/" : "") + result + (path.Length > 1 && path[path.Length - 1] == '/' ? "/" : "");
            return result == "//" ? "/" : result;
        }

        /// <summary>Получение имени файла</summary>
        public static string GetFileName(string path)
        {
            path = GetPath(path + "");
            int i = path.LastIndexOf('/');
            return i < 0 ? path : path.Substring(i + 1);
        }

        /// <summary>Получение имени директории</summary>
        public static string GetDirectoryName(string path)
        {
            path = GetPath(path + "");
            int i = path.LastIndexOf('/');
            return i < 0 ? "" : i == 0 ? "/" : path.Substring(0, i);
        }
    }

    /// <summary>Описатель удалённого файла</summary>
    public class FTPFileInfo
    {
        public readonly string Name;
        public readonly long Size;
        public readonly DateTime LastModified;

        public FTPFileInfo(string name, long size, DateTime lastModified)
        {
            Name = name;
            Size = size;
            LastModified = lastModified;
        }
    }
}