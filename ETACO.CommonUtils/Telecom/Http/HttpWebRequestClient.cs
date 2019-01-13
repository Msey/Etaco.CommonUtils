using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace ETACO.CommonUtils.Telecom.Http
{
    public class HttpWebRequestClient : BaseHttpClient
    {
        public HttpWebRequestClient(string userName = "", string password = "", string accept = "application/*+json", string contentType = "application/json") 
            : base(userName, password, accept, contentType) { }

        public override Stream SendRaw(HttpMethod method, Uri uri, object content = null, string contentType = null)
        {
            var v = (HttpWebRequest)WebRequest.Create(uri);                    
            v.Method = method+"";                                              
            v.UseDefaultCredentials = authHeader.IsEmpty();
            if (!v.UseDefaultCredentials) v.Headers.Add("Authorization", authHeader);
            v.Accept = Accept;                                              
            var stream = content as Stream;
            if (stream != null)
            {
                v.KeepAlive = true;
                v.ContentType = contentType.IfEmpty(ContentType);           
                using (var s = v.GetRequestStream()) stream.CopyTo(s);      
            }
            var resp = (HttpWebResponse)v.GetResponse();                    
            if (resp.StatusCode != HttpStatusCode.OK) { resp.Close(); throw new Exception(resp.Headers + "\r\nStatus:" + resp.StatusDescription); }
            return resp.GetResponseStream();
        }

        public override Stream Post(Uri uri, Stream content, IDictionary<Stream, string> others, string contentType = null)
        {
            using (var ms = new MemoryStream())
            {
                var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");//27 или 29 '-'
                var frm = "\r\n--" + boundary + "\r\nContent-Disposition: form-data; name=\"{0}\"; filename=\"{0}\"\r\nContent-Type: {1}\r\n\r\n";
                ms.Write(Encoding.UTF8.GetBytes(frm.FormatStr("metadata", contentType)));
                content.CopyTo(ms);
                ms.Write(Encoding.ASCII.GetBytes("\r\n"));

                if (others != null) foreach (var other in others)
                    {
                        ms.Write(Encoding.UTF8.GetBytes(frm.FormatStr(other.Value, "binary")));//frm.FormatStr("binary", other.Value)
                        other.Key.CopyTo(ms);
                        ms.Write(Encoding.ASCII.GetBytes("\r\n"));
                    }
                //boundary = "--" + boundary;
                ms.Write(Encoding.ASCII.GetBytes(boundary + "--"));
                ms.Position = 0;
                var v = SendRaw(HttpMethod.POST, uri, ms, "multipart/form-data; boundary=" + boundary);
                foreach (var other in others) other.Key.Close();
                return v;
            }
        }
    }
}
