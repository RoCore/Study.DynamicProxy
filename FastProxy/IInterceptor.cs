using System;
using System.Collections.Generic;
using System.Text;

namespace FastProxy
{
    public interface IInterceptor
    {
        object Invoke(InterceptorValues values);
    }
}
