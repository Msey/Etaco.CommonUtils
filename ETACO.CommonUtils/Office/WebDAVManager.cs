using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ETACO.CommonUtils
{
    // <webdav url="http://s-exch-pf01.docflow.test/Public/ЭСДД" login="docflowsystem@docflow.test" password="P@ssw0rd"/>
    /// <summary>Класс обёртка для работы с Exchange ("WebDAV")</summary>
    public class WebDAVManager : AbstractExchangeManager
    {
        private readonly Regex _tagNameFinder = new Regex(@"(?<=\</?\w+\:)\w+", RegexOptions.Compiled);
        private string _login;
        private string _password;

        public WebDAVManager(string login, string password)
        {
            _login = login;
            _password = password;
        }

        private string Request(string method, string url, string query, Dictionary<string, string> headers = null)
        {
            if (headers == null) headers = new Dictionary<string, string>();
            headers.Add("Content-Type", "text/xml");
            return GetResponse(method, url, query.IsEmpty() ? null : Encoding.UTF8.GetBytes(query), headers);
        }

        private string Request(string url, byte[] content, Dictionary<string, string> headers = null)
        {
            if (headers == null) headers = new Dictionary<string, string>();
            headers.Add("Content-Type", "octet/stream");
            return GetResponse("PUT", url, content, headers);
        }

        private string GetResponse(string method, string url, byte[] query, Dictionary<string, string> headers, int timeout = 360000)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Timeout = timeout;
            req.Credentials = new NetworkCredential(_login, _password);
            req.Headers.Add(HttpRequestHeader.ContentEncoding, "utf-8");

            if (headers != null)
            {
                foreach (var h in headers)
                {
                    if (h.Key == "Content-Type")    req.ContentType = h.Value;
                    else                            req.Headers.Add(h.Key, h.Value);
                }
            }

            if (query != null)
            {
                req.ContentLength = query.Length;
                using (var stream = req.GetRequestStream())
                {
                    stream.Write(query, 0, query.Length);
                }
            }

            using (var resp = req.GetResponse())
            {
                var status = ((HttpWebResponse)resp).StatusCode + "";
                if (status == "OK" || (status.StartsWith("2", StringComparison.Ordinal) && status.Length == 3))
                {
                    using (var sr = new StreamReader(resp.GetResponseStream()))
                    {
                        return sr.ReadToEnd();
                    }
                }
                else throw new Exception("Не удалось получить данные с сервера! Статус ответа: {0}".FormatStr(status));
            }
        }

        private void ReadResponse<T>(string xml, Action<T, XmlNode> onNode, Action<T> onItem) where T :new()
        {
            xml = _tagNameFinder.Replace(xml, (m) => XmlConvert.EncodeLocalName(m.Value));
            var xmlResp = new XmlDocument();
            xmlResp.LoadXml(xml);

            var nsMan = new XmlNamespaceManager(xmlResp.NameTable);
            foreach (XmlAttribute attr in xmlResp.SelectSingleNode("/*").Attributes)
            {
                if (attr.Prefix == "xmlns") nsMan.AddNamespace(attr.LocalName, attr.Value);
            }

            foreach (XmlNode node in xmlResp.SelectNodes("//a:response/a:propstat", nsMan))
            {
                if (node.SelectSingleNode("a:status", nsMan).InnerXml == "HTTP/1.1 200 OK")
                {
                    var result = new T();
                    foreach (XmlNode n in node.SelectNodes("a:prop/*", nsMan))
                    {
                        onNode(result, n);
                    }
                    onItem(result);
                }
            }
        }

        private void ReadList(List<ItemProperty> list, XmlNode node)
        {
            var attr = node.Attributes["b:dt"];
            list.Add(new ItemProperty(node.NamespaceURI + XmlConvert.DecodeName(node.LocalName), node.InnerXml, attr == null ? "string" : attr.Value)); // string sNamespace = node.GetNamespaceOfPrefix(node.Prefix);
        }

        private void ReadList(Dictionary<string,object> dict, XmlNode node)
        {
            dict.Add(node.NamespaceURI + XmlConvert.DecodeName(node.LocalName), node.InnerXml);
        }
        
        protected override void LoadItemList(string url, Action<object[]> onItem, string sql, string[] fields)
        {
            ReadResponse<Dictionary<string, object>>(Request("SEARCH", url, "<?xml version='1.0'?><g:searchrequest xmlns:g='DAV:'> <g:sql>{0}</g:sql> </g:searchrequest>".FormatStr(sql)), ReadList, (v) =>
            {
                var row = new object[fields.Length];
                foreach(var i in v) row[Array.IndexOf(fields, i.Key)] = i.Value;
                onItem(row);
            });
        }

        private string GetPropertiesString(string url, params string[] fields)
        {
            var sql = (fields == null || fields.Length == 0) ? "<?xml version='1.0'?><a:propfind xmlns:a='DAV:'><a:allprop/></a:propfind>" : GetSQL(null, new List<string>(fields), "propfind", "<d:prop>", "</d:prop>");
            return Request("PROPFIND", url, sql, new Dictionary<string, string>() { { "Depth", "0" } });
        }

        public override List<ItemProperty> GetProperties(string url, params string[] fields)
        {       
            var result = new List<ItemProperty>();
            ReadResponse<List<ItemProperty>>(GetPropertiesString(url, fields), ReadList,(n) => result = n);
            return result;
        }

        public override Dictionary<string, object> GetPropertiesDict(string url, params string[] fields)
        {
            var result = new Dictionary<string, object>();
            ReadResponse<Dictionary<string, object>>(GetPropertiesString(url, fields), ReadList, (n) => result = n);
            return result;
        }

        protected override void CreateItem(string url, List<ItemProperty> forCreate, bool isFolder)
        {
            var query = GetSQL(forCreate, null, "propertyupdate", null, null);
            Request(isFolder ? "MKCOL" : "PROPPATCH", url, query, new Dictionary<string, string>() { { "Depth", "0" }, { "Translate", "F" } });  //return (response.IndexOf("HTTP/1.1 200 OK") > 0) ? "" : response;
        }

        public override void UpdateItem(string url, List<ItemProperty> forUpdate = null, List<string> forDelete = null)
        {
            var query = GetSQL(forUpdate, forDelete, "propertyupdate", "<d:remove><d:prop>", "</d:prop></d:remove>");
            Request("PROPPATCH", url, query, new Dictionary<string, string>() { { "Depth", "0" }, { "Translate", "F" } }); //return (response.IndexOf("HTTP/1.1 200 OK") > 0) ? "" : response;
        }

        public override void DeleteItem(string url)
        {
            try
            {
                Request("DELETE", url, null, new Dictionary<string, string>() { { "Overwrite", "T" } });
            }
            catch (WebException ex)
            {
                var httpResp = (HttpWebResponse)ex.Response;
                if (httpResp.StatusCode == HttpStatusCode.NotFound)
                {
                    using (var sr = new StreamReader(httpResp.GetResponseStream())) {/*return sr.ReadToEnd();*/}
                }
                else throw;
            }
        }

        /// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private string GetSQL(List<ItemProperty> forUpdate, List<string> forDelete, string mainPrefix, string slavePrefix, string slave2Prefix)
        {
            var query = new StringBuilder("<?xml version='1.0' encoding='utf-8'?><d:{0} ".FormatStr(mainPrefix)); ;
            var body = new StringBuilder();
            var namespaces = new Dictionary<string, string>() { { "DAV:", "d" }, { "urn:uuid:c2f41010-65b3-11d1-a29f-00aa00c14882/", "b" } };


            if (forUpdate != null && forUpdate.Count > 0)
            {
                body.Append("<d:set><d:prop>");
                foreach (var val in forUpdate)
                {
                    var v = GetFieldNameParts(val.Name);    
                    var v1 = namespaces.GetValue(v[0], "n" + namespaces.Keys.Count, true);    
                    var fType = val.Type.IsEmpty() || val.Type == "string" ? "" : " b:dt='{0}'".FormatStr(val.Type);
                    body.AppendFormat("<{0}:{1} {2}>{3}</{0}:{1}>", v1, GetValidName(v[1]), fType, val.Value);
                }
                body.Append("</d:prop></d:set>");
            }

            if (forDelete != null && forDelete.Count > 0)
            {
                body.Append(slavePrefix);
                foreach (var val in forDelete)
                {
                    var v = GetFieldNameParts(val);
                    var v1 = namespaces.GetValue(v[0], "n" + namespaces.Keys.Count, true);
                    body.AppendFormat("<{0}:{1}/>", v1, GetValidName(v[1]));
                }
                body.Append(slave2Prefix);
            }

            foreach (var v in namespaces)
            {
                query.AppendFormat(" xmlns:{0}='{1}'", v.Value, v.Key);
            }

            return query +">" + body + "</d:{0}>".FormatStr(mainPrefix);
        }

        private string GetValidName(string name)
        {
            return name.Replace(" ", @"_x0020_").Replace("(", @"_x0028_").Replace(")", @"_x0029_").Replace("№", @"_x2116_");
        }

        public static string[] GetFieldNameParts(string fieldName)
        {
            int i = fieldName.LastIndexOf('/');
            if (i < 0) i = fieldName.LastIndexOf(':');
			return i > 0 ? new string[] { fieldName.Substring(0, i + 1), fieldName.Substring(i + 1) } : new string[] { "", fieldName };
        }

        /*
        //запрос для получения !расширенного! списка полей со значениями
        @"<?xml version='1.0'?>
        <a:propfind 
        xmlns:a='DAV:'
        xmlns:b='urn:schemas:mailheader:'
        xmlns:c='urn:schemas:httpmail:'
        xmlns:d='urn:schemas:contacts:'
        xmlns:e='urn:schemas:calendar:'
        xmlns:f='http://schemas.microsoft.com/exchange/'
        xmlns:g='http://schemas.microsoft.com/mapi/'
        xmlns:h='http://schemas.microsoft.com/mapi/id/'
        xmlns:i='http://schemas.microsoft.com/mapi/proptag/'
        xmlns:j='xml:'
        xmlns:ee='http://schemas.microsoft.com/exchange/events/'
        xmlns:ecga='http://schemas.microsoft.com/mapi/id/{00062008-0000-C0000-000000000046}/'
        xmlns:es='http://schemas.microsoft.com/exchange/security/'
        xmlns:ft='urn:schemas.microsoft.com:fulltextqueryinfo:'
        xmlns:repl='http://schemas.microsoft.com/exchange/events/'
        xmlns:ed='urn:schemas-microsoft-com:exch-data:'
        xmlns:uua='urn:uuid:c2f41010-65b3-11d1-a29f-00aa00c14882/'
        xmlns:of='urn:schemas-microsoft-com:office:forms'
        xmlns:oo='urn:schemas-microsoft-com:office:office'
        xmlns:xd='urn:schemas-microsoft-com:xml-data' >
        <a:allprop><a:allprop />
        <b:allprop /><c:allprop /><d:allprop /><e:allprop />
        <f:allprop /><g:allprop /><h:allprop /><i:allprop /><j:allprop />
        <ee:allprop /><ecga:allprop /> <es:allprop /><ft:allprop />
        <ed:allprop /><repl:allprop /><of:allprop /><oo:allprop />
        <uua:allprop /><xd:allprop /></a:allprop>
        </a:propfind>"
         */
    }
}
