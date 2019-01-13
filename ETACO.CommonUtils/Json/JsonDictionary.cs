using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ETACO.CommonUtils.Json
{
    [Serializable]
    public class JsonDictionary<T> : ISerializable where T : class//для примера как работает сериализация объекта
    {
        public Dictionary<string, T> Dictionary { get; set; }
        public JsonDictionary() { Dictionary = new Dictionary<string, T>(); }
        protected JsonDictionary(SerializationInfo info, StreamingContext context) : this()
        {
            foreach (var entry in info) Dictionary.Add(entry.Name, entry.Value as T);
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            foreach (var v in Dictionary.Keys) info.AddValue(v, Dictionary[v]);
        }
    }
}
