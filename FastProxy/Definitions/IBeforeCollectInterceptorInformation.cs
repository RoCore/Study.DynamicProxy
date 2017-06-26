using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace FastProxy.Definitions
{
    /// <summary>
    /// Builder that allows create new instrumentation before invokation information is about to be collected
    /// </summary>
    public interface IBeforeCollectInterceptorInformation
    {
        /// <summary>
        /// Create new intrumentation execution bofore invokation information is about to be collected
        /// </summary>
        /// <param name="typeBuilder">Type builder for proxy instance</param>
        /// <param name="sealedTypeDecorator">Sealed types cannot be inherited. Thats why we create new decorator that allow us to access the implemenation</param>
        /// <param name="interceptorDecorator">Instance of Type IInterceptor that we access</param>
        /// <param name="methodInfo">Current Method Information</param>
        /// <param name="preInvoke">could be used to add new pre invoke instrumentation</param>
        /// <param name="postInvoke">could be used to add new post invoke instrumentation, before the value will be returned</param>
        void Execute(
            TypeBuilder typeBuilder,
            FieldBuilder sealedTypeDecorator, 
            FieldBuilder interceptorDecorator, 
            MethodInfo methodInfo,
            IList<IBeforeInvokeInterceptor> preInvoke,
            IList<IAfterInvokeInterceptor> postInvoke);
    }
}
