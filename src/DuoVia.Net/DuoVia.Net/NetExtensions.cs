using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DuoVia.Net
{
    public static class NetExtensions
    {
        public static byte[] ToSerializedBytes(this object obj)
        {
            if (null == obj) return null;
            var ms = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, obj);
            var bytes = ms.ToArray();
            return bytes;
        }

        public static object ToDeserializedObject(this byte[] bytes)
        {
            if (null == bytes || bytes.Length == 0) return null;
            MemoryStream ms = new MemoryStream(bytes);
            BinaryFormatter formatter = new BinaryFormatter();
            var obj = formatter.Deserialize(ms);
            return obj;
        }
    }
}
