using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
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
            protected readonly Interceptor ProxyInterceptor;

            public Test2()
            {
                ProxyInterceptor = new Interceptor();
            }

            public abstract int TestX();
            public virtual decimal TestY()
            {
                throw new NotImplementedException();
            }

            public virtual void X()
            {
                throw new NotImplementedException();
            }

            public virtual string Test(string t, object value, object va, object sd, object sdsd, object asda)
            {
                var list = new List<object>();
                list.Add(t);
                list.Add(value);
                list.Add(va);
                list.Add(sd);
                list.Add(sdsd);
                list.Add(asda);
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
            public override string Test(string t, object value, object va, object sd, object sdsd, object asda)
            {
                var items = new object[6];
                items[0] = t;
                items[1] = value;
                items[2] = va;
                items[3] = sd;
                items[4] = sdsd;
                items[5] = asda;
                var task = new Task<object>(executeTest, items);
                var interceptorValues = new InterceptorValues(this, "Test", items, task);
                return (string)ProxyInterceptor.Invoke(interceptorValues);
            }

            public override int TestX()
            {
                return (int)ProxyInterceptor.Invoke(null);
            }
            public override decimal TestY()
            {

                var x = new Task<decimal>(base.TestY);

                return x.Result;
            }
            public override void X()
            {
                return;
            }
        }

        public class Interceptor : IInterceptor
        {
            public object Invoke(InterceptorValues values)
            {
                return null;
            }
        }

        private class TestXY : Test1
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
            var result = DynamicTypeBuilder.Build<Test3, Interceptor>();
        }

        [DataRow(1)]
        [DataRow(1000)]
        [DataRow(100000)]
        [DataRow(10000000)]
        [TestMethod]
        public void CreateProxy(int value)
        {
            var watch = new Stopwatch();
            watch.Reset();
            var watchNative = new Stopwatch();
            watchNative.Reset();
            var result = DynamicTypeBuilder.Build<Test1, Interceptor>();
            var native = new TestXY();
            while (value-- > 0)
            {
                watch.Start();
                result.TestX();
                watch.Stop();

                watchNative.Start();
                native.TestX();
                watchNative.Stop();
            }
            Console.WriteLine(watch.Elapsed);
            Console.WriteLine(watchNative.Elapsed);
        }
    }
}
