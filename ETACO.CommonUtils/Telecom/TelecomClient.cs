using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ETACO.CommonUtils.Telecom
{
    /// <summary> Базовый клиент для telecom </summary>
    public class TelecomClient : IDisposable
    {
        protected readonly string EndOfCommand = "\r\n";
        private TcpClient _client;
        private StreamReader _clientStream;

        protected readonly Log Log = AppContext.Log;
        public string Host { get; private set; }
        public int Port { get; private set; }
        public int Timeout { get; private set; }
        public bool UseSsl { get; private set; }
        public RemoteCertificateValidationCallback CertificateValidator { get; private set; }
        private Encoding _encoding = Encoding.UTF8;
        public Encoding Encoding
        {
            get { return _encoding; }
            protected set
            {
                value = value ?? Encoding.UTF8;
                if (value != _encoding)
                {
                    _encoding = value;
                    if (_clientStream != null) _clientStream = new StreamReader(_clientStream.BaseStream, _encoding, false);
                }
            }
        }

        /// <summary>Дополнительная инициализация соединения</summary>
        protected event Action AfterConnect;
        /// <summary>Дополнительная действия перед разрывом соединения</summary>
        protected event Action BeforeDisconnect;

        protected TelecomClient(string host, int port = 80, bool useSsl = false, int timeout = 60000, RemoteCertificateValidationCallback certValidator = null)
        {
            if (host.IsEmpty()) throw new ArgumentNullException("host");
            if (port > IPEndPoint.MaxPort || port < IPEndPoint.MinPort) throw new ArgumentOutOfRangeException("port");
            Host = host;
            Port = port;
            Timeout = Math.Max(timeout, 0);
            UseSsl = useSsl;
            CertificateValidator = certValidator;
        }

        /// <summary>Соеднинение с сервером</summary>
        public void Connect()
        {
            if (_client == null) _client = new TcpClient() { SendTimeout = Timeout, ReceiveTimeout = Timeout };
            if (!_client.Connected)
            {
                try
                {
                    _client.Connect(Host, Port);
                    _clientStream = new StreamReader(UseSsl ? _client.GetSslStream(Host, CertificateValidator) : _client.GetStream(), Encoding, false); //Encoding.GetEncoding(1251), GetEncoding(20866)
                    AfterConnect?.Invoke();
                }
                catch (SocketException e)
                {
                    Disconnect(true);
                    throw new Exception("Unable to connect to ({0}:{1})  : {2}".FormatStr(Host, Port, e.Message), e);
                }
                catch
                {
                    Disconnect(true);
                    throw;
                }
            }
        }

        /// <summary>Отправка сообщения и обработка ответа</summary>
        /// <param name="message">Отправляемая команда (если == null то чтение ответа не производится)</param>
        /// <param name="waitResponseTimeout">Время ожидания ответа (=0 - сразу пробуем читать, > 0 - ждём, если данных нет - то выходим из цикла ожидания)</param>
        public void Send(string message, Func<string, bool> onResponseLine = null, int waitResponseTimeout = 0)
        {
            Connect();
            try
            {
                if (!message.IsEmpty())
                {
                    _clientStream.BaseStream.Write(Encoding.ASCII.GetBytes(message.Trim() + EndOfCommand));
                    _clientStream.BaseStream.Flush();
                    if (Log.UseTrace) Log.Trace("TelecomClient({0}:{1}) Send: {2}".FormatStr(Host, Port, message));
                }

                if (onResponseLine == null) return;
                for (int i = waitResponseTimeout; i > 0 && _client.Available == 0; i -= 1000) Thread.Sleep(i < 0 ? i + 1000 : 1000);
                if (waitResponseTimeout > 0 && _client.Available == 0) return;

                string line;
                do
                {
                    line = _clientStream.ReadLine();
                    if (Log.UseTrace) Log.Trace("TelecomClient({0}:{1}) Read: {2}".FormatStr(Host, Port, line));
                } while (onResponseLine(line));
            }
            catch // (ThreadAbortException) (SocketException)
            {
                Disconnect(true);
                throw;
            }
        }

        /// <summary>Закрытие соединения с сервером </summary>
        public void Disconnect()
        {
            Disconnect(false);
        }

        private void Disconnect(bool force)
        {
            if (_clientStream != null)
            {
                if (!force && BeforeDisconnect != null) BeforeDisconnect();
                _clientStream.Close();
                _clientStream = null;
            }
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
        }

        /// <summary> Освобождение занятых ресурсов </summary>
        public void Dispose()
        {
            Disconnect(false);
        }
    }
}