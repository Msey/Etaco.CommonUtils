using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ETACO.CommonUtils.Json
{
    public abstract class AbstractJsonSerializer
    {
        public abstract T ReadObject<T>(Stream input);
        public abstract T ReadObject<T>(Stream input, IEnumerable<Type> knownTypes);
        public abstract void WriteObject<T>(Stream output, T obj);
        //new JsonSerializer().ReadObject<JsonTest>(@"{""time"": ""03:53:25 AM"",""milliseconds_since_epoch"": 1362196405309,""date"": ""2013-02-23""}");//DateTimeFormat!!
        public T ReadObject<T>(string json) { return ReadObject<T>(new MemoryStream(Encoding.UTF8.GetBytes(json ?? ""))); }
        public MemoryStream ToStream<T>(T obj) { var v = new MemoryStream(); WriteObject(v, obj); v.Position = 0; return v;}
        public String ToJson<T>(T obj) { return ToStream(obj).ReadToEnd().GetString(); }
    }

    public class JsonSerializer : AbstractJsonSerializer
    {
        public readonly DataContractJsonSerializerSettings Settings;
        public JsonSerializer(string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffzzz") { Settings = new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true, DateTimeFormat = new DateTimeFormat(dateTimeFormat) };}
        public override T ReadObject<T>(Stream input) { return (T)new DataContractJsonSerializer(typeof(T), Settings).ReadObject(input); }
        public override T ReadObject<T>(Stream input, IEnumerable<Type> knownTypes) { return (T)new DataContractJsonSerializer(typeof(T), knownTypes).ReadObject(input); }
        public override void WriteObject<T>(Stream output, T obj) { new DataContractJsonSerializer(typeof(T), Settings).WriteObject(output, obj); }
    }

    public class JsonSerializer<T> 
    {
        public readonly DataContractJsonSerializer serializer;
        public JsonSerializer(string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffzzz") { serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true, DateTimeFormat = new DateTimeFormat(dateTimeFormat) }); }
        public  T ReadObject(Stream input) { return (T)serializer.ReadObject(input); }
        public void WriteObject(Stream output, T obj) { serializer.WriteObject(output, obj); }
        public MemoryStream ToStream(T obj) { var v = new MemoryStream(); WriteObject(v, obj); v.Position = 0; return v; }
    }
}
//object: var user={"name":"Tom","gender":"Male","birthday":"1983-8-8"}
//array: var userlist=[{"user":{"name":"Tom","gender":"Male","birthday":"1983-8-8"}},{"user":{"name":"Lucy","gender":"Female","birthday":"1984-7-7"}}]
//date: \/Date(1319266795390+0800)\/"
//dict: "[{\"Key\":\"Name\",\"Value\":\"Tom\"},{\"Key\":\"Age\",\"Value\":\"28\"}]"
//DateTimeToString: jsonString = new Regex(@"\\/Date\((\d+)\+\d+\)\\/").Replace(Encoding.UTF8.GetString(ms.ToArray()), new MatchEvaluator(ConvertJsonDateToDateString));
//Convert "yyyy-MM-dd HH:mm:ss" String as "\/Date(1319266795390+0800)\/":    (T)new DataContractJsonSerializer(typeof(T)).ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(new Regex(@"\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}").Replace(jsonString, new MatchEvaluator(ConvertDateStringToJsonDate)))));
/// Convert Serialization Time /Date(1319266795390+0800) as String
//private static string ConvertJsonDateToDateString(Match m) {return new DateTime(1970, 1, 1).AddMilliseconds(long.Parse(m.Groups[1].Value)).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");}
//private static string ConvertDateStringToJsonDate(Match m){ return string.Format("\\/Date({0}+0800)\\/", (DateTime.Parse(m.Groups[0].Value).ToUniversalTime() - new DateTime(1970, 1, 1)).TotalMilliseconds);}

