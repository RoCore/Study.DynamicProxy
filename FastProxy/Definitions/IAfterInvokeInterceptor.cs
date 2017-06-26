using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace FastProxy.Definitions
{
    /// <summary>
    /// Builder that allows to create new intrumentation interceptor post invokation
    /// </summary>
    public interface IAfterInvokeInterceptor
    {
        /// <summary>
        /// Create new intrumentation execution interceptor post invokation
        /// </summary>
        /// <param name="typeBuilder">Type builder for proxy instance</param>
        /// <param name="sealedTypeDecorator">Sealed types cannot be inherited. Thats why we create new decorator that allow us to access the implemenation</param>
        /// <param name="interceptorDecorator">Instance of Type IInterceptor that we access</param>
        /// <param name="methodInfo">Current Method Information</param>
        void Execute(
            TypeBuilder typeBuilder,
            FieldBuilder sealedTypeDecorator,
            FieldBuilder interceptorDecorator,
            MethodInfo methodInfo);
    }
}
