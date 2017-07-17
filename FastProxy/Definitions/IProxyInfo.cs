using System;
using System.Reflection;

namespace FastProxy.Definitions
{
    internal interface IProxyInfo
    {
        Type DefineType { get; }
        TypeInfo ProxyType { get; }
        Type ReturnType { get; }
        int UniqueId { get; }
    }
}