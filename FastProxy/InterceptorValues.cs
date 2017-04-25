using System.Collections;
using System.Threading.Tasks;

namespace FastProxy
{
    public class InterceptorValues
    {
        public object Instance { get; }
        public object SealedInstance { get; }
        public string InvokedMethod { get; }
        public IEnumerable Parameters { get; }
        public Task Next { get; }

        private static readonly object EmptyCall = null;

        public InterceptorValues(object instance, string invokedMethod, IEnumerable parameters) : this(instance, invokedMethod, parameters, Task.FromResult(EmptyCall))
        {

        }
        public InterceptorValues(object instance, object sealedInstance, string invokedMethod, IEnumerable parameters) : this(instance, invokedMethod, parameters, Task.FromResult(EmptyCall))
        {
            SealedInstance = sealedInstance;
        }

        public InterceptorValues(object instance, string invokedMethod, IEnumerable parameters, Task next)
        {
            Instance = instance;
            InvokedMethod = invokedMethod;
            Parameters = parameters;
            Next = next;
        }
    }
}
