using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace ETACO.CommonUtils.Telecom
{
    //var v = new Telecom.FTPWebClient("ftp://anthonyus.myftp.org", "", "")  { Encoding = System.Text.Encoding.GetEncoding(1251)};
    //var x = v.GetList();//иногда подвисает на открытии dataSocket
    //x = v.GetList(x[0], true);//Incoming
    //v.DownloadFile("Incoming/Nikon_D7000-Ru.pdf","test.pdf");//var z = v.GetCurrentDir(); //всегда возвращает ''
    //остальное выпадает с ошибкой 550 - видино не поддерживает CWD
    //var x4 = v.GetFileDate("Video/dune_folder.txt");
    //v.MakeDir("Incoming/test1"); v.RemoveDir("Incoming/test1");
    //v.UploadFile("Incoming/_Nikon_D7000-Ru.pdf", "test.pdf"); v.RenameFile("Incoming/_Nikon_D7000-Ru.pdf", "Incoming/__Nikon_D7000-Ru.pdf");

    /// <summary> FtpWebRequest клиент </summary>
    public sealed class FTPWebClient //OR USE WebClient Upload Download file
    {
        private int timeout = 60000;
        private Encoding _encoding = Encoding.UTF8;
        public bool BinaryMode { get; set; }
        public bool ActiveMode { get; set; }
        public bool UseSsl { get; private set; }
        public Encoding Encoding { get { return _encoding; } set { _encoding = value ?? Encoding.UTF8; } }

        public string Host { get; set; }
        public string Login { get; private set; }
        public string Password { get; private set; }

        //если используем ssl то и для данных и для команд одновременно - получаем ftps
        public FTPWebClient(string host, string login = null, string password = null, bool useSsl = false, int timeout = 60000)
        {
            Host = host;
            Login = login;
            Password = password;
            ActiveMode = false;
            BinaryMode = true;
            UseSsl = useSsl;
            this.timeout = timeout;
            //SetMethodRequiresCWD();
            //ServerCertificateValidationCallback = useSsl && allowInvalidCertificate ? ServicePointManager_ServerCertificateValidationCallback : null;
        }

        private static void SetMethodRequiresCWD()
        {
            var t = typeof(FtpWebRequest).GetField("m_MethodInfo", BindingFlags.NonPublic | BindingFlags.Instance).FieldType;

            var knownMethodsField = t.GetField("KnownMethodInfo", BindingFlags.Static | BindingFlags.NonPublic);
            var flagsField = t.GetField("Flags", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var knownMethod in (Array)knownMethodsField.GetValue(null))
            {
                flagsField.SetValue(knownMethod, (int)flagsField.GetValue(knownMethod) | 0x100);
            }
        }

        private FtpWebRequest GetRequest(string method, string path = "", long offset = 0, long length = 0)
        {
            var result = (FtpWebRequest)FtpWebRequest.Create(Host + "/" + path);
            result.Credentials = new NetworkCredential(Login, Password);
            result.EnableSsl = UseSsl;
            result.ReadWriteTimeout = timeout;
            result.Timeout = timeout;
            result.UseBinary = BinaryMode;
            result.UsePassive = !ActiveMode;
            result.KeepAlive = true;
            result.Method = method;
            result.ContentOffset = offset;
            result.ContentLength = length;
            result.Proxy = null;
            return result;
        }

        private string ReadResponse(string method, string path = "")
        {
            using (var v = new StreamReader(GetRequest(method, path).GetResponse().GetResponseStream(), Encoding))
            {
                return v.ReadToEnd();
            }
        }

        /// <summary>  Получение текущего каталога </summary>
        public string GetCurrentDir()
        {
            return ReadResponse(WebRequestMethods.Ftp.PrintWorkingDirectory);
        }

        /// <summary> Создание каталога (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void MakeDir(string path)
        {
            GetRequest(WebRequestMethods.Ftp.MakeDirectory, path).GetResponse().Close();
        }

        /// <summary> Удаление каталога (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void RemoveDir(string path)
        {
            GetRequest(WebRequestMethods.Ftp.RemoveDirectory, path).GetResponse().Close();
        }

        /// <summary> Получение даты файла (../f1//f2/f3.txt), если начинается с '/' - то путь абсолютный</summary>
        public DateTime GetFileDate(string fileName)
        {
            return DateTime.ParseExact(ReadResponse(WebRequestMethods.Ftp.GetDateTimestamp, fileName), "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary> Получение размера файла (../f1//f2/f3.txt), если начинается с '/' - то путь абсолютный</summary>
        public long GetFileSize(string fileName)
        {
            return long.Parse(ReadResponse(WebRequestMethods.Ftp.GetFileSize, fileName));
        }

        /// <summary> Удаление файла (../f1//f2/f3.txt), если начинается с '/' - то путь абсолютный</summary>
        public void RemoveFile(string fileName)
        {
            GetRequest(WebRequestMethods.Ftp.DeleteFile, fileName).GetResponse().Close();
        }

        /// <summary> Переименование файла (../f1//f2/f3.txt /f4/f5.txt), если начинается с '/' - то путь абсолютный</summary>
        public void RenameFile(string oldName, string newName)
        {
            var v = GetRequest(WebRequestMethods.Ftp.Rename, oldName);
            v.RenameTo = newName;
            v.GetResponse().Close();
        }

        ////////////////////////   USE dataSocket  ////////////////////////

        /// <summary> Получение списка файлов и каталогов (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public string[] GetList(string path = "", bool showDetails = false)//+
        {
            return ReadResponse(showDetails ? WebRequestMethods.Ftp.ListDirectoryDetails : WebRequestMethods.Ftp.ListDirectory, path).Split(Environment.NewLine);
        }

        /// <summary> Получение файла (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void DownloadFile(string fileName, Stream stream, long offset = 0)
        {
            using (var v = GetRequest(WebRequestMethods.Ftp.DownloadFile, fileName, offset).GetResponse().GetResponseStream())
            {
                v.CopyTo(stream, 16384);
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
            using (var v = GetRequest(WebRequestMethods.Ftp.UploadFile, fileName, offset, stream.Length).GetRequestStream())
            {
                //stream.Seek(offset, SeekOrigin.Begin);
                stream.CopyTo(v, 16384);
            }
            /*using (FtpWebResponse response = (FtpWebResponse)request.GetResponse()) { bytesReceived = response.ContentLength;}*/
        }

        /// <summary>Загрузка файла на сервер (../f1//f2/f3), если начинается с '/' - то путь абсолютный</summary>
        public void UploadFile(string fileName, string localFileName, long offset = 0)
        {
            using (Stream stream = new FileInfo(localFileName).OpenRead())
            {
                UploadFile(fileName, stream, offset);
            }
        }
    }
}