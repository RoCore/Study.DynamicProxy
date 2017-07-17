using System;
using System.Reflection;
using FastProxy.Definitions;

namespace FastProxy
{
    public class ProxyInfo<T> : IProxyInfo
    {
        public Type DefineType { get; }
        public Type ReturnType { get; }
        public TypeInfo ProxyType { get; }
        public int UniqueId { get; }
        public Func<T> Generator { get; }

        public ProxyInfo(Type defineType, Type returnType, TypeInfo proxyType, int uniqueId, Func<T> generator)
        {
            DefineType = defineType;
            ReturnType = returnType;
            ProxyType = proxyType;
            UniqueId = uniqueId;
            Generator = generator;
        }
    }
}
