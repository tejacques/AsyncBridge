using AsyncBridge;
using Functional.Option;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncBridge.Tests
{
    [TestFixture]
    public class AsyncBridgeTests
    {
        [TestFixtureSetUp]
        public void SetUp()
        {
        }

        public async Task<string> AsyncString(string s)
        {
            await Task.Yield();

            return s;
        }

        [Test]
        public void TestResult()
        {
            string actual = "";
            string expected = "string";

            using (var A = AsyncHelper.Wait)
            {
                A.Run(AsyncString(expected), (result) => actual = result);
            }

            Assert.AreEqual(expected, actual);
        }

        public async Task<string> AsyncStringOption(string s)
        {
            await Task.Yield();

            return s;
        }

        [Test]
        public void TestResultOption()
        {
            Option<string> actual;
            string expected = "string";

            using (var A = AsyncHelper.Wait)
            {
                A.Run(AsyncStringOption(expected), out actual);
            }

            Assert.True(expected == actual);
        }

        public async Task<string> AsyncStringDelay(string s, int ms)
        {
            await Task.Delay(ms);

            return s;
        }

        [Test]
        public void TestTime()
        {
            int delay = 100;
            string expected = "string";

            string string1 = "";
            string string2 = "";

            Stopwatch s = new Stopwatch();

            s.Start();
            using (var A = AsyncHelper.Wait)
            {
                A.Run(AsyncStringDelay(expected, delay), res => string1 = res);
                A.Run(AsyncStringDelay(expected, delay), res => string2 = res);
            }
            s.Stop();

            // Total Execution time at this point will be ~100ms, not ~200ms
            int EPSILON = 30; // millisecond margin of error
            Assert.Less(s.ElapsedMilliseconds, delay + EPSILON);

            // The value of string1 == expected
            Assert.AreEqual(expected, string1);

            // The value of string2 == expected
            Assert.AreEqual(expected, string2);
        }

        public async Task<string> AsyncStringException()
        {
            await Task.Yield();

            throw new Exception("Test Exception.");
        }

        public async Task<string> AsyncStringException(int msdelay)
        {
            await Task.Delay(msdelay);

            throw new Exception("Test Exception.");
        }

        [Test]
        public void TestException()
        {

            Assert.Throws(typeof(AggregateException), () =>
            {
                using (var A = AsyncHelper.Wait)
                {
                    A.Run(AsyncStringException());
                }
            });

            try
            {
                using (var A = AsyncHelper.Wait)
                {
                    A.Run(AsyncStringException());
                }
            }
            catch (Exception e)
            {
                Assert.AreEqual(
                    "AsyncBridge.Run method threw an exception.",
                    e.Message);
                Assert.NotNull(e.InnerException);
                Assert.AreEqual("Test Exception.", e.InnerException.Message);
            }
        }

        private async Task FAFExample(int ms)
        {
            await Task.Delay(ms);

            throw new Exception("Test exception");
        }

        [Test]
        public void TestFAF()
        {
            int delay = 100;
            Stopwatch s = new Stopwatch();
            bool exceptionThrown = false;
            var waitHandle = new AutoResetEvent(false);

            s.Start();
            AsyncHelper.FireAndForget(
                () => FAFExample(delay),
                e =>
                {
                    exceptionThrown = true;
                    waitHandle.Set();
                });
            s.Stop();

            // These lines will be reached immediately
            int EPSILON = 30; // millisecond margin of error
            Assert.Less(s.ElapsedMilliseconds, EPSILON);

            waitHandle.WaitOne(delay * 2 + EPSILON);
            Assert.AreEqual(true, exceptionThrown);
        }

        [Test]
        public void TestMulti()
        {
            int delay = 100;
            string expected = "string";

            string string1 = "";
            string string2 = "";

            Stopwatch s = new Stopwatch();

            s.Start();
            using (var A = AsyncHelper.Wait)
            {
                A.Run(MultiHelperAsync(expected, delay));
                A.Run(AsyncStringDelay(expected, delay), res => string1 = res);
                A.Run(AsyncStringDelay(expected, delay), res => string2 = res);
            }
            s.Stop();

            // Total Execution time at this point will be ~100ms, not ~200ms
            int EPSILON = 30; // millisecond margin of error
            Assert.Less(s.ElapsedMilliseconds, delay + EPSILON);

            // The value of string1 == expected
            Assert.AreEqual(expected, string1);

            // The value of string2 == expected
            Assert.AreEqual(expected, string2);
        }

        private async Task MultiHelperAsync(string expected, int delay)
        {
            await Task.Yield();

            MultiHelper(expected, delay);

        }
        private void MultiHelper(string expected, int delay)
        {
            string string1 = "";
            string string2 = "";

            using (var A = AsyncHelper.Wait)
            {
                A.Run(AsyncStringDelay(expected, delay), res => string1 = res);
                A.Run(AsyncStringDelay(expected, delay), res => string2 = res);
            }
        }

        [Test]
        public void TestMultiException()
        {
            Assert.Throws(typeof(AggregateException), () =>
            {
                using (var A = AsyncHelper.Wait)
                {
                    A.Run(MultiHelperExceptionAsync());
                    A.Run(AsyncStringException(50));
                }
            });

            try
            {
                using (var A = AsyncHelper.Wait)
                {
                    A.Run(MultiHelperExceptionAsync());
                    A.Run(AsyncStringException(50));
                }
            }
            catch (Exception e)
            {
                Assert.AreEqual(
                    "AsyncBridge.Run method threw an exception.",
                    e.Message);
                Assert.NotNull(e.InnerException);
                Assert.AreEqual("Test Exception.", e.InnerException.Message);
            }
        }

        private async Task MultiHelperExceptionAsync()
        {
            await Task.Yield();

            MultiHelperException();

        }
        private void MultiHelperException()
        {
            string s = "";

            try
            {
                using (var A = AsyncHelper.Wait)
                {
                    A.Run(AsyncStringDelay("s", 100), res => s = res);
                }
            }
            catch
            {
                Assert.Fail("This should not throw!");
            }

            Assert.AreEqual("s", s);
        }

    }
}
