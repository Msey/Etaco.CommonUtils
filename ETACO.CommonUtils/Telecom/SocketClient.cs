using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ETACO.CommonUtils.Telecom
{
    /// <summary> Socket клиент </summary>
    public class SocketClient : IDisposable
    {
        private Log _log = AppContext.Log;
        private Socket socket;
        public string Host { get; protected set; }
        public int Port { get; protected set; }
        public int Timeout { get; private set; }

        /// <summary>Дополнительная инициализация соединения</summary>
        public event Action AfterConnect;
        /// <summary>Дополнительная действия перед разрывом соединения</summary>
        public event Action BeforeDisconnect;
        
        public SocketClient(string host, int port = 80, int timeout = 60000)
        {
            Host = host;
            Port = port;
            Timeout = Math.Max(timeout, 0);
        }

        /// <summary>Соеднинение с сервером</summary>
        public void Connect()
        {
            if (socket == null)
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveTimeout = Timeout;
                socket.SendTimeout = Timeout;
            }
            if (!socket.Connected)
            {
                try
                {
                    socket.Connect(Dns.GetHostEntry(Host).AddressList[0] + "", Port);
                    AfterConnect?.Invoke();
                }
                catch (Exception ex)
                {
                    if (socket != null) socket.Close(); //проверка нужна для случая, если в AfterConnect вызвали Disconnect
                    socket = null;
                    throw new Exception("Ошибка при попытке соединения с сервером ({0}:{1}) : {2}".FormatStr(Host, Port, ex.Message), ex);
                }
            }
        }

        /// <summary>Отправка сообщения</summary>
        public byte[] Send(byte[] message, bool getResponse = false, Func<MemoryStream, int, bool> isTheEndOfMessage = null, int buffSize = 256, int attempt = 0, bool useOffset = false)
        {
            Connect();
            try
            {
                if (message != null)
                {
                    int count = 0;
                    do
                    {
                        count += socket.Send(message, count, message.Length - count, SocketFlags.None);
                    } while (count < message.Length);
                    if(_log.UseTrace) _log.Trace("SocketClient send {0} bytes.".FormatStr(message.Length));
                }
                if (getResponse)
                {
                    if (attempt > 0)
                    {
                        while ((attempt-- > 0) && (socket.Available == 0))
                        {
                            Thread.Sleep(1000);
                        }
                        if (socket.Available == 0) return new byte[0];
                    }

                    using (var result = new MemoryStream())
                    {
                        if (isTheEndOfMessage == null) isTheEndOfMessage = (m, c) => socket.Available == 0;

                        var buffer = new byte[buffSize < 1 ? 256 : buffSize];
                        int count = 0;
                        int offset = 0;
                        do
                        {
                            count = socket.Receive(buffer, offset, buffSize - offset, SocketFlags.None);
                            result.Write(buffer, offset, count);
                            offset += count;
                            if(!useOffset || offset == buffSize) offset = 0;
                        } while (!isTheEndOfMessage(result, count));
                        if (_log.UseTrace) _log.Trace("SocketClient receive {0} bytes.".FormatStr(result.Position));
                        return result.ToArray();
                    }
                }
            }
            catch (ThreadAbortException)
            {
                socket.Close();
                socket = null;
                throw;
            }
            catch (SocketException)
            {
                socket.Close();
                socket = null;
                throw;
            }
            return new byte[0];
        }

        /// <summary>Закрытие соединения с сервером </summary>
        public void Disconnect()
        {
            if (socket != null)
            {
                try
                {
                    if (socket.Connected)
                    {
                        BeforeDisconnect?.Invoke();
                        socket.Shutdown(SocketShutdown.Both); //добиваемся, чтобы все отправляемые/принимаемые данные были обработанны
                    }
                }
                finally
                {
                    socket.Close();
                    socket = null;
                }
            }
        }

        /// <summary> Освобождение занятых ресурсов </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}
