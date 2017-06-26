using System;
using System.Reflection;
using System.Reflection.Emit;
using FastProxy.Helpers;

namespace FastProxy.Definitions
{
    public interface IProxyTypeBuilder
    {
        ProxyTypeBuilderTransientParameters Create(Type abstractType, Type concreteType, Type interceptorType, ModuleBuilder moduleBuilder, string postfix);
    }
}
