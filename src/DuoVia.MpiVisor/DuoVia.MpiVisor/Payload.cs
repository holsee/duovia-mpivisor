using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DuoVia.Net;

namespace DuoVia.MpiVisor
{
    public static class Payload
    {
        public static string GetRaw()
        {
            var p = Process.GetCurrentProcess();
            if (p.StartInfo.EnvironmentVariables.ContainsKey("payload"))
            {
                return p.StartInfo.EnvironmentVariables["payload"];
            }
            return null;
        }

        public static object Deserialize()
        {
            var raw = GetRaw();
            if (null != raw)
            {
                try
                {
                    var data = Convert.FromBase64String(raw);
                    return data.ToDeserializedObject();
                }
                catch { } //if fails deserialization, return null
            }
            return null;
        }
    }
}
