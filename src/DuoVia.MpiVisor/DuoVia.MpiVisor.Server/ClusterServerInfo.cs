using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace DuoVia.MpiVisor.Server
{
    [Serializable]
    [XmlRoot("Server")]
    public class ClusterServerInfo
    {
        private IPEndPoint _endPoint;

        [XmlIgnore]
        public IPEndPoint EndPoint {
            get
            {
                if (null == _endPoint && null != this.IPAddress && this.Port > 0)
                {
                    _endPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.IPAddress), this.Port);
                }
                return _endPoint;
            }
            set 
            {
                _endPoint = value;
                if (null != _endPoint)
                {
                    this.IPAddress = _endPoint.Address.ToString();
                    this.Port = _endPoint.Port;
                }
            }
        }

        [XmlElement("IPAddress")]
        public string IPAddress { get; set; }

        [XmlElement("Port")]
        public int Port { get; set; }

        [XmlElement("ProcessorCount")]
        public ushort ProcessorCount { get; set; }

        [XmlElement("MachineName")]
        public string MachineName { get; set; }

        [XmlElement("IsActive")]
        public bool IsActive { get; set; }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var comparedTo = obj as ClusterServerInfo;
            if (comparedTo == null) return false;
            return IPEndPoint.Equals(this.EndPoint, comparedTo.EndPoint);
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}", EndPoint, ProcessorCount);
        }
    }

    [Serializable]
    [XmlRoot("ClusterServerConfig", Namespace = "http://mpivisor.duovia.com")]
    public class ClusterServerConfig
    {
        public ClusterServerConfig()
        {
            //never null
            this.ClusterServers = new List<ClusterServerInfo>(); 
        }

        [XmlArray("ClusterServers"), XmlArrayItem("Server", Type = typeof(ClusterServerInfo))]
        public List<ClusterServerInfo> ClusterServers { get; set; }

        public static ClusterServerConfig Load()
        {
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cluster.config");
            if (File.Exists(configFile))
            {
                var xml = File.ReadAllText(configFile);
                var config = ObjectSerializer.Deserialize<ClusterServerConfig>(xml);
                return config;
            }
            return null;
        }

        public void Save()
        {
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cluster.config");
            var xml = ObjectSerializer.Serialize<ClusterServerConfig>(this);
            File.WriteAllText(configFile, xml);
        }

        public string ToXmlString()
        {
            var xml = ObjectSerializer.Serialize<ClusterServerConfig>(this);
            return xml;
        }

        public static ClusterServerConfig Parse(string xml)
        {
            var config = ObjectSerializer.Deserialize<ClusterServerConfig>(xml);
            return config;
        }
    }

    internal static class ObjectSerializer
    {
        public static string Serialize<T>(T value)
        {
            if (null == value) return null;
            var serializer = new XmlSerializer(typeof(T));
            var settings = new XmlWriterSettings()
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false,
                NamespaceHandling = NamespaceHandling.OmitDuplicates                
            };
            using (StringWriter sw = new StringWriter())
            using (XmlWriter xw = XmlWriter.Create(sw, settings))
            {
                serializer.Serialize(xw, value);
                return sw.ToString();
            }
        }

        public static T Deserialize<T>(string xml)
        {
            if (null == xml) return default(T);
            var serializer = new XmlSerializer(typeof(T));
            var settings = new XmlReaderSettings();
            using (StringReader sr = new StringReader(xml))
            using (XmlReader xr = XmlReader.Create(sr, settings))
            {
                return (T)serializer.Deserialize(xr);
            }
        }
    }


}
