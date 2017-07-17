using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using FastProxy.Definitions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FastProxy.Test
{
    [TestClass]
    public class UnitTest1
    {
        public interface Test1
        {
            string Test(string t, object value, object va, object sd, object sdsd, object asda);
            int TestX();
            decimal TestY();
            void X();
        }

        public abstract class Test2 : Test1
        {
            protected readonly IInterceptor ProxyInterceptor;

            public Test2()
            {
                ProxyInterceptor = new Interceptor();
            }

            public abstract int TestX();
            public virtual decimal TestY()
            {
                return default(decimal);
            }

            public virtual void X()
            {
                return;
            }

            public virtual string Test(string t, object value, object va, object sd, object sdsd, object asda)
            {
                return t;
            }
        }

        public sealed class Test3 : Test2
        {
            private object executeTest(object a)
            {
                var lst = (object[])a;
                return base.Test((string)lst[0],
                                 (object)lst[1],
                                 (object)lst[2],
                                 (object)lst[3],
                                 (object)lst[4],
                                 (object)lst[5]);
            }

            //public Test3()
            //{
            //    Value = new Test3();
            //}

            //public Test3(Test2 value) : this()
            //{
            //    Value = value;
            //}

            public override string Test(string t, object value, object va, object sd, object sdsd, object asda)
            {
                //var items = new[] { t, value, va, sd, sdsd, asda };
                var items = new object[6];
                items[0] = t;
                items[1] = value;
                items[2] = va;
                items[3] = sd;
                items[4] = sdsd;
                items[5] = asda;

                var task = new Task<object>(executeTest, items);
                var interceptorValues = new InterceptionInformation(this, null, "Test", items, task);
                return (string)ProxyInterceptor.Invoke(interceptorValues);
            }

            public override int TestX()
            {
                var items = new object[0];
                var task = Task.FromResult<object>(default(int));
                var interceptorValues = new InterceptionInformation(this, null, "Test", items, task);
                var result = (int?)ProxyInterceptor.Invoke(interceptorValues);
                return result.GetValueOrDefault();
            }
            public override decimal TestY()
            {
                var x = new Task<decimal>(base.TestY);
                var date = new DateTime();
                return date.Ticks;
            }
            public override void X()
            {
                return;
            }
        }

        public class Interceptor : IInterceptor
        {
            public object Invoke(InterceptionInformation callDescription)
            {
                return callDescription.Next().Result;
            }
        }

        public class TestXY : Test1
        {
            public string Test(string t, object value, object va, object sd, object sdsd, object asda)
            {
                return t;
            }

            public int TestX()
            {
                return 1;
            }

            public decimal TestY()
            {
                throw new NotImplementedException();
            }

            public void X()
            {
                throw new NotImplementedException();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CreateProxyFailed()
        {
            var result = ProxyFactory.Default.CreateProxy<Test3, Interceptor>();
        }


#if (!NETCOREAPP1_1)
        private class NewProxy : NProxy.Core.IInvocationHandler
        {
            public object Invoke(object target, MethodInfo methodInfo, object[] parameters)
            {
                return 1;
            }
        }
#endif

        [DataRow(1)]
        [DataRow(1000)]
        [DataRow(100000)]
        [DataRow(10000000)]
        [TestMethod]
        public void CreateProxy(int value)
        {
            var watch = new Stopwatch();
            var watchNativeImplementedInterceptorWatch = new Stopwatch();
            var watchNative = new Stopwatch();
            var nproxy = new Stopwatch();
            var result = ProxyFactory.Default.CreateProxy<Test1, Interceptor>();
            var native = new TestXY();
            var nativeImplementedInterceptor = new Test3();
            int total = value;
#if (!NETCOREAPP1_1)
            var proxy = (Test1)new NProxy.Core.ProxyFactory().GetProxyTemplate(typeof(Test1), new[] { typeof(Test1) }).CreateProxy(new NewProxy());
#endif
            while (total-- > 0)
            {
                watch.Start();
                result.X();//Test("1", null, 1, 1L, 122, .0d);
                watch.Stop();

                watchNative.Start();
                native.Test("1", null, 1, 1L, 122, .0d);
                watchNative.Stop();

                watchNativeImplementedInterceptorWatch.Start();
                nativeImplementedInterceptor.Test("1", null, 1, 1L, 122, .0d);
                watchNativeImplementedInterceptorWatch.Stop();

#if (!NETCOREAPP1_1)
                nproxy.Start();
                proxy.X();
                nproxy.Stop();
#endif
            }
            Console.WriteLine($"{watch.Elapsed} Proxy takes for {value} calls");
            Console.WriteLine($"{watchNative.Elapsed} Native takes for {value} calls");
            Console.WriteLine($"{watchNativeImplementedInterceptorWatch.Elapsed} Native Use Proxy Call takes for {value} calls");
#if (!NETCOREAPP1_1)
            Console.WriteLine($"{nproxy.Elapsed} nproxy takes for {value} calls");
#endif
        }
    }
}
