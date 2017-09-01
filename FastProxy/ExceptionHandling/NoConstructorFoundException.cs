using System;
using System.Runtime.Serialization;

namespace FastProxy.ExceptionHandling
{
    public class NoConstructorFoundException : Exception
    {
        public Type DeclaredType { get; }
        public object[] Parameters { get; }
        public NoConstructorFoundException()
        {
        }

        public NoConstructorFoundException(string message) : base(message)
        {
        }

        public NoConstructorFoundException(string message, Type declaredType, object[] parameters) : this(message)
        {
            DeclaredType = declaredType;
            Parameters = parameters;
        }

        public NoConstructorFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NoConstructorFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
