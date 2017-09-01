using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FastProxy.Definitions;
using FastProxy.ExceptionHandling;

namespace FastProxy
{
    public class ProxyInfo<T> : IProxyInfo
    {
        /// <summary>
        /// Allow to create new Instance from Constructor Type
        /// https://rogerjohansson.blog/2008/02/28/linq-expressions-creating-objects/
        /// </summary>
        /// <param name="args">Parameters</param>
        /// <returns></returns>
        private delegate T ObjectActivator(params object[] args);
        public Type DefineType { get; }
        public Type ReturnType { get; }
        public TypeInfo ProxyType { get; }
        public int UniqueId { get; }
        private Dictionary<int, List<ObjectActivator>> _generators;
        private readonly ConstructorInfo[] _constructors;

        public ProxyInfo(Type defineType, Type returnType, TypeInfo proxyType, int uniqueId, ConstructorInfo[] constructors)
        {
            DefineType = defineType;
            ReturnType = returnType;
            ProxyType = proxyType;
            UniqueId = uniqueId;
            _constructors = constructors;
        }

        public T CreateInstance(params object[] parameters)
        {
            InitializeInstanceActivators();
            var instance = default(T);
            if (_generators.TryGetValue(parameters.Length, out var items))
            {
                foreach (var value in items)
                {
                    try
                    {
                        instance = value(parameters);
                        break;
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            if (EqualityComparer<T>.Default.Equals(instance, default(T)))
            {
                throw new NoConstructorFoundException("No constructor availbel for the given Type", typeof(T), parameters);
            }
            return instance;
        }

        private void InitializeInstanceActivators()
        {
            if (_generators == null)
            {
                _generators = new Dictionary<int, List<ObjectActivator>>();
                foreach (var item in _constructors)
                {
                    var length = item.GetParameters().Length;
                    if (_generators.TryGetValue(length, out var activators) == false)
                    {
                        _generators.Add(length, activators = new List<ObjectActivator>());
                    }
                    activators.Add(GetActivator(item));
                }
            }
        }

        private static ObjectActivator GetActivator(ConstructorInfo ctor)
        {
            var paramsInfo = ctor.GetParameters();

            //create a single param of type object[]
            var param = Expression.Parameter(typeof(object[]), "args");

            var argsExp = new Expression[paramsInfo.Length];

            //pick each arg from the params array 
            //and create a typed expression of them
            for (int i = 0; i < paramsInfo.Length; i++)
            {
                var index = Expression.Constant(i);
                var paramType = paramsInfo[i].ParameterType;
                var paramAccessorExp = Expression.ArrayIndex(param, index);
                var paramCastExp = Expression.Convert(paramAccessorExp, paramType);
                argsExp[i] = paramCastExp;
            }

            //make a NewExpression that calls the
            //ctor with the args we just created
            var newExp = Expression.New(ctor, argsExp);

            //create a lambda with the New
            //Expression as body and our param object[] as arg
            var lambda = Expression.Lambda(typeof(ObjectActivator), newExp, param);

            //compile it
            return (ObjectActivator)lambda.Compile();
        }
    }
}
