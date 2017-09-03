using System.Collections.Generic;
using System.Reflection.Emit;
using FastProxy.Definitions;

namespace FastProxy.Helpers
{
    public class ProxyMethodBuilderTransientParameters
    {
        public ProxyTypeBuilderTransientParameters TypeInfo { get; set; }
        public List<IBeforeCollectInterceptorInformation> PreInit { get; set; }
        public List<IBeforeInvokeInterceptor> PreInvoke { get; set; }
        public List<IAfterInvokeInterceptor> PostInvoke { get; set; }
        public int MethodCreationCounter { get; set; }
    }
}
