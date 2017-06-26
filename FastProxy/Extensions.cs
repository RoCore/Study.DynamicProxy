using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FastProxy.Definitions;

namespace FastProxy
{
    public static class Extensions
    {
        /// <summary>
        /// Create new Proxy where we have no differences between concrete and abstract type 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TInterceptor"></typeparam>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static TypeInfo CreateProxy<T, TInterceptor>(this ProxyFactory factory)
            where TInterceptor : IInterceptor, new()
        {
            return factory.CreateProxy<T, T, TInterceptor>();
        }

        private static readonly HashSet<Type> IntegerInitializable = new HashSet<Type>
        {
            typeof(bool),
            typeof(byte),
            typeof(char),
            typeof(int),
            typeof(sbyte),
            typeof(short),
            typeof(uint),
            typeof(ushort)
        };

        private static readonly HashSet<Type> LongInitializable = new HashSet<Type>
        {
            typeof(long),
            typeof(ulong)
        };


        public static void EmitDefaultValue(Type type, ILGenerator generator)
        {
            if (IntegerInitializable.Contains(type) || type.GetTypeInfo().IsEnum)
            {
                generator.Emit(OpCodes.Ldc_I4_0);
            }
            else if (LongInitializable.Contains(type))
            {
                generator.Emit(OpCodes.Ldc_I8, 0L);
            }
            else if (type == typeof(float))
            {
                generator.Emit(OpCodes.Ldc_R4, 0f);
            }
            else if (type == typeof(double))
            {
                generator.Emit(OpCodes.Ldc_R8, 0D);
            }
            else if (type.GetTypeInfo().IsValueType)
            {
                //custom structs and other non nummeric
                generator.Emit(OpCodes.Initobj, type);
            }
            else
            {
                //class
                generator.Emit(OpCodes.Ldnull);
            }
        }
    }
}
