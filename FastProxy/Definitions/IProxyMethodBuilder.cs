using System.Reflection;
using FastProxy.Helpers;

namespace FastProxy.Definitions
{
    /// <summary>
    /// Allow to create Method proxies for a specific type
    /// </summary>
    public interface IProxyMethodBuilder
    {
        /// <summary>
        /// Allow to create new proxy method
        /// </summary>
        /// <param name="current">Information about the method</param>
        /// <param name="transient">Transient information that should help to create new method</param>
        void Create(MethodInfo current, ProxyMethodBuilderTransientParameters transient);
    }
}
