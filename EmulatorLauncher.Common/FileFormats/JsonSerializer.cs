using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Json;

namespace EmulatorLauncher.Common.FileFormats
{
    public static class JsonSerializer
    {
        public static T DeserializeFile<T>(string fileName) where T : new()
        {
            T deserializedObj = new T();

            using (Stream ms = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
                deserializedObj = (T)ser.ReadObject(ms);                
                ms.Close();
            }

            return deserializedObj;
        }

        public static T DeserializeString<T>(string json) where T : new()
        {
            T deserializedObj = new T();

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
                deserializedObj = (T)ser.ReadObject(ms);
                ms.Close();
            }

            return deserializedObj;
        }
    }
}
