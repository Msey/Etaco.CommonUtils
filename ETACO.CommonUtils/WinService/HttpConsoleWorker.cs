using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using ETACO.CommonUtils.Plugin;

namespace ETACO.CommonUtils.WinService
{
    [Plugin("httpconsoleworker")]
    internal class HttpConsoleWorker : ServiceWorker
    {
        private HttpListener listener;
        protected override int PingTimeout { get { return -1; } }
        protected override int WaitTimeout { get { return 0; } }
        protected override bool DoWork()
        {
            listener = new HttpListener();
            var prefix = "http://*:{0}/".FormatStr(GetParameter("port", 8080));
            listener.Prefixes.Add(prefix);
            try
            {
                //find cmd + popup menu run as Administrator and execute -> netsh http add urlacl url=http://+:8765/ user=Everyone listen=yes
                //netsh http delete urlacl url=http://+:8765/
                listener.Start();//run as Administrator
            }
            catch(HttpListenerException ex)
            {
                _LOG.HandleException(ex, "HttpConsole not started at: " + prefix);
                DoStop();
                return true;
            }
            _LOG.Info("HttpConsole start listening: " + prefix);//new Worker(listener.GetContext()).ProcessRequest
            while (Status == ServiceWorkerStatus.Running)
            {
                ShowActivity();
                try
                {
                    new Thread(x => 
                    {
                        var ctx = (HttpListenerContext)x;
                        _LOG.Info("HttpConsole: " + ctx.Request.HttpMethod + " " + ctx.Request.Url);
                        var v = "";
                        try { v = GetResponce(ctx.Request); }
                        catch (Exception ex) { v = "<html><body><h3>Error: " + ex.Message + "<br>" + ex.StackTrace + "</h3></body></html>"; }
                        //"HTTP/1.1 200 OK\r\nServer: mysrv\r\nDate:2016-12-01\r\nContent-Type: text/html\r\nConnection: close\r\nContent-Length: " + res.Length + "\r\n\r\n";
                        var res = Encoding.UTF8.GetBytes(v);
                        ctx.Response.ContentLength64 = res.Length;
                        ctx.Response.OutputStream.Write(res);
                        ctx.Response.OutputStream.Close();
                    }).Start(listener.GetContext());
                }//check on stop
                catch (HttpListenerException) { if (Status == ServiceWorkerStatus.Running && listener != null) throw; }
            }
            return false;
        }
        protected override void DoStop()
        {
            base.DoStop();
            Deinit();
        }

        protected override void Deinit()
        {
            if (listener == null) return;
            try
            {
                if(listener.IsListening) listener.Stop();
                listener.Close();
            }
            catch (ObjectDisposedException) { }
            listener = null;
        }

        protected virtual string GetResponce(HttpListenerRequest request)
        {
            var param = new Dictionary<string, string>();
            if (request.HttpMethod == "POST" && request.HasEntityBody)
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    foreach (var v in reader.ReadToEnd().Split('&'))
                    {
                        var x = v.Split('=');
                        param.Add(x[0], HttpUtility.UrlDecode(x[1]));
                    }
                }
            }
            var res = param.GetValue("script");
            if(res!= null) res = new EvalConsoleService().Eval(param.GetValue("script")); //res!= null => POST

            var wcfUseJson = _CFG.GetParameter("wcfconsoleworker", "webmode", false);
            var result =  
          @"<html>
            <head>
                <meta charset = 'utf-8'>
                <title> CommonUtilsWebConsole </title>
                <style type = 'text/css'>
                    textarea {width : 100%; box-sizing : border-box; -moz-box-sizing: border-box; border : 1px solid #000;background : #FFF; resize:vertical;}
                    div { overflow: auto; width:100%; height:auto; color: #0000; background:#99CCFF; border: 3px #CCCCCC solid; border-radius: 10px; -moz-border-radius: 10px; -webkit-border-radius: 10px; -khtml-border-radius:10px;}
                </style>
            </head>
            <body>" + //строку разбили на 2 т.к. в css есть теги ->{<-, что приводит к ошибке при форматировании строки ////'name' нужен для submit, id для js 
                @"<form method = 'post'>
                    <div>
                        <textarea id = 'script' name = 'script' rows='15'>{0}</textarea> 
                        <input type = 'submit'>
                        <a href=""javascript: out(eval, document.getElementById('script').value)"">&nbsp;&#9658;&nbsp;Run JS&nbsp;</a>
                        <a href=""javascript: out(execWCF, document.getElementById('script').value)"">&nbsp;&#9658;&nbsp;Run WCF&nbsp;</a>
                        </div>
                    <div id='divout' style='visibility:{1}'>
                        <textarea id = 'out' rows='25'>{2}</textarea>
                    </div>
                    <div id='data' style='display:none'><![CDATA[PHM6RW52ZWxvcGUgeG1sbnM6cz0naHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvc29hcC9lbnZlbG9wZS8nPjxzOkJvZHk+PEV2YWwgeG1sbnM9J2h0dHA6Ly90ZW1wdXJpLm9yZy8nPjxjb2RlPiM8L2NvZGU+PC9FdmFsPjwvczpCb2R5PjwvczpFbnZlbG9wZT4=]]></div>
                </form>
            </body>".FormatStr(param.GetValue("script").IfEmpty("sc.Info()"), res.IsEmpty() ? "hidden" : "visible", res) +
            @"<script type ='text/javascript'>
                String.prototype.replaceAll = function (find, replace) {return this.replace(new RegExp(find, 'g'), replace);};
                function out(f, cmd) {var v =document.getElementById('out'); try{v.value = f(cmd);} catch(e) {v.value = e;} document.getElementById('divout').style.visibility = 'visible';}
                function execWCF(cmd){var req = new XMLHttpRequest();";
            if (_CFG.GetParameter("wcfconsoleworker", "webmode", false)) //use json
            {
                result += @"req.open('POST', '" + _CFG.GetParameter("wcfconsoleworker", "url", "") + @"/Eval', false);
                    req.setRequestHeader('Content-Type', 'application/json');
                    req.send(JSON.stringify(cmd));
                    return req.responseXML == null? JSON.parse(req.responseText):req.responseXML.firstChild.textContent;}";
                }
                else
                {
                    result += @"req.open('POST', '" + _CFG.GetParameter("wcfconsoleworker", "url", "") + @"', false);
                    req.setRequestHeader('Content-Type', 'text/xml; charset=utf-8');
                    req.setRequestHeader('soapAction', 'http://tempuri.org/IEvalConsoleService/Eval');
                    var xml = document.getElementById('data').innerHTML.substring(11);
                    req.send(atob(xml.substring(0, xml.length - 6)).replaceAll('#',cmd));
                    return req.responseXML.getElementsByTagName('EvalResult')[0].firstChild.nodeValue;}";
                }
          return result+  "</script></html>";
        }/*btoa("<s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'><s:Body><Eval xmlns='http://tempuri.org/'><code>123</code></Eval></s:Body></s:Envelope>")*/
    }
}
/*использовали base64, чтобы запихнуть xml в xml + получить его через js innerHTML без изменения
btoa("<s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'><s:Body><Eval xmlns='http://tempuri.org/'><code>123</code></Eval></s:Body></s:Envelope>")
==> PHM6RW52ZWxvcGUgeG1sbnM6cz0naHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvc29hcC9lbnZlbG9wZS8nPjxzOkJvZHk+PEV2YWwgeG1sbnM9J2h0dHA6Ly90ZW1wdXJpLm9yZy8nPjxjb2RlPiM8L2NvZGU+PC9FdmFsPjwvczpCb2R5PjwvczpFbnZlbG9wZT4=
*/
/*telnet localhost 8765
GET / HTTP/1.1
Host: localhost:8765
User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:50.0) Gecko/20100101 Firefox/50.0
Accept: text/html,application/xhtml+xml,application/xml;
Accept-Language: ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3
Accept-Encoding: gzip, deflate
DNT: 1
Connection: keep-alive
Upgrade-Insecure-Requests: 1

HTTP/1.1 400 Bad Request
Content-Type: text/html; charset=us-ascii
Server: Microsoft-HTTPAPI/2.0
Date: Sun, 18 Dec 2016 13:44:17 GMT
Connection: close
Content-Length: 326

<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01//EN""http://www.w3.org/TR/html4/strict.dtd">
<HTML><HEAD><TITLE>Bad Request</TITLE><META HTTP-EQUIV="Content-Type" Content="text/html; charset=us-ascii"></HEAD>
<BODY><h2>Bad Request - Invalid Verb</h2><hr><p>HTTP Error 400. The request verb is invalid.</p></BODY>
</HTML>*/
