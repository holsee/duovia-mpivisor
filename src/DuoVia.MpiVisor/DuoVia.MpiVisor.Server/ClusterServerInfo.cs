using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.MpiVisor.Server
{
    [Serializable]
    public class ClusterServerInfo
    {
        public IPEndPoint EndPoint { get; set; }
        public ushort ProcessorCount { get; set; }

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
}
