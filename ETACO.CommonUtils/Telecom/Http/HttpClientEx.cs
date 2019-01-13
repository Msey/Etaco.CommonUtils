using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ETACO.CommonUtils.Telecom.Http
{
    public class HttpClientEx : BaseHttpClient
    {
        private readonly HttpClient httpClient;
        public HttpClientEx(string userName = "", string password = "", string accept = "application/*+json", string contentType = "application/json")
            : base(userName, password, accept, contentType)
        {
            httpClient = new HttpClient(new HttpClientHandler() { UseDefaultCredentials = authHeader.IsEmpty() });
        }
        public override Stream SendRaw(HttpMethod method, Uri uri, object content = null, string contentType = null)
        {
            var v = new HttpRequestMessage(new System.Net.Http.HttpMethod(method + ""), uri);
            v.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Accept));
            if (!authHeader.IsEmpty()) v.Headers.Add("Authorization", authHeader);
            if (content != null)
            {
                v.Content = content is Stream ? new StreamContent((Stream)content) : content as HttpContent;
                v.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType.IfEmpty(ContentType));
            }
            return httpClient.SendAsync(v, HttpCompletionOption.ResponseContentRead).Result.EnsureSuccessStatusCode().Content.ReadAsStreamAsync().Result;
        }

        public override Stream Post(Uri uri, Stream content, IDictionary<Stream, string> others, string contentType = null)
        {
            using (var multiPartStream = new MultipartFormDataContent())
            {
                var stream = new MemoryStream();
                content.CopyTo(stream);
                var firstPart = new ByteArrayContent(stream.GetBuffer());
                firstPart.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
                firstPart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                multiPartStream.Add(firstPart);
                stream.Close();
                if (others != null) foreach (var other in others)
                    {
                        var otherContent = new StreamContent(other.Key);
                        otherContent.Headers.ContentType = new MediaTypeHeaderValue(other.Value);
                        otherContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "binary" };
                        multiPartStream.Add(otherContent);
                    }

                var v = SendRaw(HttpMethod.POST, uri, multiPartStream);
                if (others != null) foreach (var other in others) other.Key.Close();
                return v;
            }
        }

        public async Task<T> DoRestAsync<T>(System.Net.Http.HttpMethod method, string url) { return await DoRestAsync<object, T>(method, url, null);}
   
        public async Task<Tout> DoRestAsync<Tin,Tout>(System.Net.Http.HttpMethod method, string url, Tin body = default(Tin))
        {
            var client = new HttpClient();
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "Your Oauth token");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            var req = new HttpRequestMessage(method, new Uri(url));
            var jss = new Json.JsonSerializer();
            if (body != null) req.Content = new StringContent(jss.ToJson(body), Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(req);
            return jss.ReadObject<Tout>(await resp.Content.ReadAsStringAsync());
        }

    }
}
