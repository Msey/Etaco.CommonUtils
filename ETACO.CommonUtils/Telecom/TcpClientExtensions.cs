using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace ETACO.CommonUtils.Telecom
{
    public static class TcpClientExtensions
    {
        public static void Connect(this TcpClient tcpClient, string host, int port, int connectTimeout)
        {
            var result = tcpClient.BeginConnect(host, port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(connectTimeout, false))
            {
                tcpClient.Close();
                throw new Exception("TcpClient({0}:{1}) connection timeout.".FormatStr(host, port));
            }
            tcpClient.EndConnect(result);
        }

        public static Stream GetSslStream(this TcpClient tcpClient, string host, RemoteCertificateValidationCallback certValidator = null)
        {
            Stream stream = tcpClient.GetStream();
            try
            {
                stream = (certValidator == null) ? new SslStream(stream, true) : new SslStream(stream, false, certValidator);
                ((SslStream)stream).AuthenticateAsClient(host);
            }
            catch (ArgumentException e) { throw new IOException("Unable to create Ssl Stream for hostname: " + host, e); }
            catch (AuthenticationException e) { throw new IOException("Unable to authenticate ssl stream for hostname: " + host, e); }
            catch (InvalidOperationException e) { throw new IOException("There was a problem  attempting to authenticate this SSL stream for hostname: " + host, e); }
            return stream;
        }
    }
}