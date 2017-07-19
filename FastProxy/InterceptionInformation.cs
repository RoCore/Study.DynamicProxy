using System.Collections;
using System.Threading.Tasks;

namespace FastProxy
{
    public class InterceptionInformation
    {
        /// <summary>
        /// actual instance of intercepted object
        /// </summary>
        public object Instance { get; }
        /// <summary>
        /// Marked as decorator. Null if base type can be inherited
        /// </summary>
        public object SealedInstance { get; }
        /// <summary>
        /// Method Name
        /// </summary>
        public string InvokedMethod { get; }

        /// <summary>
        /// parameter that have been intercepted. add new Item will have no effect on execution.
        /// </summary>
        public IList Parameters => _parameters;

        private readonly object[] _parameters;

        /// <summary>
        /// Executes the base instuction
        /// </summary>
        private readonly Task<object> _next;

        /// <summary>
        /// specified for an class that is sealed and cannot be inherited
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="sealedInstance"></param>
        /// <param name="invokedMethod"></param>
        /// <param name="parameters"></param>
        /// <param name="next">allow async execution of original implementation</param>
        public InterceptionInformation(object instance, object sealedInstance, string invokedMethod, object[] parameters, Task<object> next)
        {
            SealedInstance = sealedInstance;
            Instance = instance;
            InvokedMethod = invokedMethod;
            _parameters = parameters;
            _next = next;
        }

        /// <summary>
        /// Execute base implementation (if exists)
        /// </summary>
        /// <returns></returns>
        public Task<object> Next()
        {
            if (_next.Status == TaskStatus.Created)
            {
                _next.Start();
            }
            return _next;
        }
    }
}
