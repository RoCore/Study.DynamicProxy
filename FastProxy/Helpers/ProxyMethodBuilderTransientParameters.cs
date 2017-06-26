using System.Collections.Generic;
using System.Reflection.Emit;
using FastProxy.Definitions;

namespace FastProxy.Helpers
{
    public class ProxyMethodBuilderTransientParameters
    {
        public List<IBeforeCollectInterceptorInformation> PreInit { get; set; }
        public List<IBeforeInvokeInterceptor> PreInvoke { get; set; }
        public List<IAfterInvokeInterceptor> PostInvoke { get; set; }

        public TypeBuilder TypeBuilder { get; set; }
        public FieldBuilder SealedTypeDecorator { get; set; }
        public FieldBuilder InterceptorDecorator { get; set; }
        public int MethodCreationCounter { get; set; }
    }
}
