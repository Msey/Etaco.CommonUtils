using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using ETACO.CommonUtils.Json;

namespace ETACO.CommonUtils.Telecom.Http
{
    public abstract class BaseHttpClient
    {
        public enum HttpMethod {GET, POST, PUT, DELETE};
        protected readonly string authHeader;
        public string UserName { get; protected set; }
        public string Accept { get; protected set; }
        public string ContentType { get; protected set; }

        public AbstractJsonSerializer JsonSerializer { get; set; } = new JsonSerializer();//"application/vnd.emc.documentum+json"

        public BaseHttpClient(string userName = "", string password = "", string accept = "application/*+json", string contentType = "application/json")
        {
            UserName = userName;
            authHeader = UserName.IsEmpty() ? "" : "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(userName + ":" + password));//DomainAuth or Basic
            Accept = accept;
            ContentType = contentType;
        }
        public Uri BuildUri(string uri, GenericOptions option)
        {
            var ub = new UriBuilder(uri);
            if (option != null)
            {
                var query = HttpUtility.ParseQueryString(ub.Query);
                foreach (var pair in option.ToQueryList()) if (pair.Value != null) query[pair.Key] = pair.Value + "";
                ub.Query = query.ToString();
            }
            return ub.Uri;
        }
        public abstract Stream SendRaw(HttpMethod method, Uri uri, object content = null, string contentType = null);

        public abstract Stream Post(Uri uri, Stream content, IDictionary<Stream, string> other, string contentType = null);

        public Stream GetRaw(string uri, GenericOptions options = null)
        {
            return SendRaw(HttpMethod.GET, BuildUri(uri, options));
        }

        public string GetString(string uri, GenericOptions options = null)
        {
            return SendRaw(HttpMethod.GET, BuildUri(uri, options)).ReadToEnd().GetString();
        }

        public void Delete(string uri, GenericOptions options = null)
        {
            SendRaw(HttpMethod.DELETE, BuildUri(uri, options));
        }

        public Stream PostRaw(string uri, Stream content, GenericOptions options = null, string contentType = null)
        {
            using (content) return SendRaw(HttpMethod.POST, BuildUri(uri, options), content, contentType);
        }

        private R Send<T, R>(HttpMethod method, string uri, GenericOptions options, T obj, string contentType)
        {
            using (var ms = new MemoryStream())
            {
                JsonSerializer.WriteObject(ms, obj);
                ms.Position = 0;
                using (var v = SendRaw(method, BuildUri(uri, options), ms, contentType))
                {
                    return JsonSerializer.ReadObject<R>(v);
                }
            }
        }

        public T Get<T>(string uri, GenericOptions options = null)
        {
            using (var s = GetRaw(uri, options)) return JsonSerializer.ReadObject<T>(s);
        }

        public T Get<T>(string uri, IEnumerable<Type> knownTypes, GenericOptions options = null)
        {
            using (var s = GetRaw(uri, options)) return JsonSerializer.ReadObject<T>(s, knownTypes);
        }

        public R Put<T, R>(string uri, T obj, GenericOptions options = null, string contentType = null)
        {
            return Send<T, R>(HttpMethod.PUT, uri, options, obj, contentType);
        }

        public R Post<T, R>(string uri, T obj, IDictionary<Stream, string> otherParts = null, GenericOptions options = null, string contentType = null)
        {
            if(otherParts == null) return Send<T, R>(HttpMethod.POST, uri, options, obj, contentType);
            using (var ms = new MemoryStream())
            {
                JsonSerializer.WriteObject(ms, obj);
                ms.Position = 0;
                return JsonSerializer.ReadObject<R>(Post(BuildUri(uri, options), ms, otherParts, contentType));
            }
        }

        public T Post<T>(string uri, T obj, IDictionary<Stream, string> otherParts = null, GenericOptions options = null, string contentType = null)
        {
            return Post<T, T>(uri, obj, otherParts, options, contentType);
        }

        public T Post<T>(string uri, Stream stream, string mimeType, GenericOptions options = null)
        {
            return JsonSerializer.ReadObject<T>(PostRaw(uri, stream, options, mimeType));
        }
    }
}
