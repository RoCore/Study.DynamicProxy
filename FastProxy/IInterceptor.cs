using System;
using System.Collections.Generic;
using System.Text;

namespace FastProxy
{
    public interface IInterceptor
    {
        object InterceptorInvokeAsync(InterceptorValues values);
    }
}
