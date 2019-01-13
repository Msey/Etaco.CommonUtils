using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ETACO.CommonUtils.Telecom.Http;

namespace ETACO.CommonUtils.Json
{
    [DataContract]//(Name = "entry", Namespace = "http://www.w3.org/2005/Atom")]  
    public class JsonTestObject
    {
        [DataMember(Name = "time")]
        public string Time { get; set; }

        [DataMember(Name = "date")]
        public DateTime Date { get; set; }

        [DataMember(Name = "milliseconds_since_epoch")]
        public long mse { get; set; }

        [DataMember(Name = "properties"/*, IsRequired = false*/)]//if value not exist in json = > set null
        public Dictionary<string, object> Properties { get { return prop??(prop = new Dictionary<string, object>()); } set { prop = value; } }
        private Dictionary<string, object> prop;

        [DataMember(Name = "data")]
        public List<object> Data { get { return data??(data = new List<object>()); } set { data = value; } }
        private List<object> data;
        public static void Test()
       {
            var s = new JsonSerializer("yyyy-MM-dd");
            var v = s.ReadObject<JsonTestObject>(@"{""time"": ""03:53:25 AM"",""milliseconds_since_epoch"": 1362196405309,""date"": ""2013-02-23""}");
            v.Properties.Add("x", DateTime.Now);
            v.Data.Add(42);
            v.Data.Add("oops");
            var m = new System.IO.MemoryStream();
            s.WriteObject(m, v);//{ "data":[42,"oops"],"date":"2013-02-23","milliseconds_since_epoch":1362196405309,"properties":{"x":"2016-12-28"},"time":"03:53:25 AM"}
            m.Position = 0;
            var sss = m.GetBuffer().GetString();
            v = s.ReadObject<JsonTestObject>(m);
            v = new HttpClientEx() { JsonSerializer = new JsonSerializer("MM-dd-yyyy") }.Get<JsonTestObject>("http://date.jsontest.com/");
            v = new HttpWebRequestClient() { JsonSerializer = new JsonSerializer("MM-dd-yyyy") }.Get<JsonTestObject>("http://date.jsontest.com/");
        }
    }
}
