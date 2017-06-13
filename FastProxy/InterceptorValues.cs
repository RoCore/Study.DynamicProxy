using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy
{
    public class InterceptorValues
    {
        /// <summary>
        /// actual instance of intercepted object
        /// </summary>
        public object Instance { get; }
        /// <summary>
        /// Marked as decorator. Null if 
        /// </summary>
        public object SealedInstance { get; }
        /// <summary>
        /// Method Name
        /// </summary>
        public string InvokedMethod { get; }
        /// <summary>
        /// parameter that have been intercepted
        /// </summary>
        public IEnumerable Parameters { get; }

        /// <summary>
        /// Executes the base instuction
        /// </summary>
        private Task<object> _next;

        private static readonly object EmptyCall = null;

        public InterceptorValues(object instance, string invokedMethod, IEnumerable parameters) : 
            this(instance, invokedMethod, parameters, Task.FromResult(EmptyCall))
        {

        }
        public InterceptorValues(object instance, object sealedInstance, string invokedMethod, IEnumerable parameters) : 
            this(instance, invokedMethod, parameters, Task.FromResult(EmptyCall))
        {
            SealedInstance = sealedInstance;
        }

        public InterceptorValues(object instance, string invokedMethod, IEnumerable parameters, Task<object> next)
        {
            Instance = instance;
            InvokedMethod = invokedMethod;
            Parameters = parameters;
            _next = next;
        }

        public Task<object> Next()
        {
            if (_next.IsCompleted == false && _next.Status == TaskStatus.Created)
            {
                _next.Start();
            }
            return _next;
        }
    }
}
