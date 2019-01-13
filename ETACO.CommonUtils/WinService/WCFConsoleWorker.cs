using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Threading;
using System.Xml;
using ETACO.CommonUtils.Plugin;
using System.Collections.Generic;

namespace ETACO.CommonUtils.WinService
{
    [Plugin("wcfconsoleworker")]
    internal class WCFConsoleWorker : ServiceWorker
    {
        private ServiceHost host;
        protected override int PingTimeout { get { return -1; } }
        protected override int WaitTimeout { get { return 0; } }
        protected override bool DoWork()
        {
            var uri = new Uri(GetParameter("url", "http://localhost:8764/console"));
            //webmode - true => json (WebServiceHost), false => soap (ServiceHost)
            host = GetParameter("webmode", false) ? new WebServiceHost(typeof(EvalConsoleService), uri) : new ServiceHost(typeof(EvalConsoleService), uri);
            //host.Description.Behaviors.Add(new ServiceMetadataBehavior() { HttpGetEnabled = true });
            try
            {
                host.Open();
            }
            catch (Exception ex)
            {
                _LOG.HandleException(ex, "WCFConsole not started at: " + uri);
                DoStop();
                return true;
            }
            _LOG.Info("WCFConsole start listening: " + uri);
            while (Status == ServiceWorkerStatus.Running) Thread.Sleep(1000);

            return false;
        }
        protected override void DoStop()
        {
            base.DoStop();
            Deinit();
        }

        protected override void Deinit()
        {
            if (host == null) return;
            if (host.State == CommunicationState.Opened || host.State == CommunicationState.Opening) host.Close();
            host = null;
        }

        public static void Test(string code = "DateTime.Now")
        {
            try
            {
                var res = "oops";
                Thread.Sleep(1000);
                if (AppContext.Config.GetParameter("wcfconsoleworker", "webmode", false))
                {
                    var jss = new Json.JsonSerializer();
                    Func<string, string> get = x => {if (!x.StartsWith("<")) return jss.ReadObject<string>(x); else { var r = new XmlTextReader(new StringReader(x)); while (r.NodeType != XmlNodeType.Text) r.Read(); return r.Value; }};

                    var v = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://localhost:8764/console/Eval");
                    v.Method = "POST";
                    v.ContentType = "application/json";
                    using (var s = v.GetRequestStream()) jss.ToStream(code).CopyTo(s);
                    res = get(v.GetResponse().GetResponseStream().ReadToEnd().GetString());

                    res = get(new CommonUtils.Telecom.Http.HttpClientEx().PostRaw("http://localhost:8764/console/Eval", jss.ToStream(code)).ReadToEnd().GetString());
                    /*если ResponseFormat = WebMessageFormat.Json) то можно использовать типизированные вариант*/
                    //res = new CommonUtils.Telecom.Http.HttpClientEx().Post("http://localhost:8764/console/Eval", code);
                }
                else
                {
                    var factory = new ChannelFactory<IEvalConsoleService>(new BasicHttpBinding(), new EndpointAddress("http://localhost:8764/console"));
                    var proxy = factory.CreateChannel();
                    res = proxy.Eval(code);
                    ((IClientChannel)proxy).Close();
                    factory.Close();
                }
                Console.WriteLine(res);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    [ServiceContract]
    public interface IEvalConsoleService
    {
        [OperationContract]
        //[WebInvoke(ResponseFormat = WebMessageFormat.Json)]//for JSON (WCF понимает по Content-Type в каком формате приходят данные, но нужно указать в каком отсылать)
        string Eval(string code);
        [OperationContract]
        [WebGet(/*UriTemplate = "getlist", */ResponseFormat = WebMessageFormat.Json)]
        List<string> GetList();
    }
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class EvalConsoleService : IEvalConsoleService
    {
        public string Eval(string code)
        {
            if (code.IsEmpty()) return "";
            try
            {
                var x = AppContext.JSEval.Eval(File.Exists(code) ? new FileInfo(code).ReadToEnd().GetString() : code);
                return x == null ? "" : AppContext.JSEval.Engine.desc(x);
            }
            catch (Exception ex)
            {
                return ex.Message + AppContext.onCmdEvalErrorText;
            }
        }

        public List<string> GetList()
        {
            return new List<string> { "1", "2", "3" };
        }
    }
}
/*//XML
var req = new XMLHttpRequest();
req.open('POST', 'http://localhost:8764/console', false);
req.setRequestHeader('Content-Type', 'text/xml; charset=utf-8');
req.setRequestHeader('soapAction', 'http://tempuri.org/IEvalConsoleService/Eval');
var body="<s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'><s:Body><Eval xmlns='http://tempuri.org/'><code>123</code></Eval></s:Body></s:Envelope>";
req.send(body);
req.responseXML.getElementsByTagName("EvalResult")[0].firstChild.nodeValue
//JSON - use webmode = true
var req = new XMLHttpRequest();
req.open('POST', 'http://localhost:8764/console/Eval', false);
req.setRequestHeader('Content-Type', 'application/json');
req.send(JSON.stringify("DateTime.Now"));
req.responseXML == null? JSON.parse(req.responseText):req.responseXML.firstChild.textContent;
 */
