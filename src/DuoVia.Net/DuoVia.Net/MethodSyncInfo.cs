using System;

namespace DuoVia.Net
{
    [Serializable]
    internal class MethodSyncInfo
    {
        public int MethodIdent { get; set; }
        public string MethodName { get; set; }
        public Type[] ParameterTypes { get; set; }
    }
}
