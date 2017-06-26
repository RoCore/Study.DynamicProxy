using System.Reflection;
using System.Reflection.Emit;
namespace FastProxy.Helpers
{
    public class ProxyTypeBuilderTransientParameters
    {
        public FieldBuilder Decorator { get; set; }
        public PropertyInfo[] Properties { get; set; }
        public MethodInfo[] Methods { get; set; }
        public FieldBuilder InterceptorInvoker { get; set; }
        public bool IsInterfaceType { get; set; }
        public TypeBuilder ProxyType { get; set; }
    }
}
