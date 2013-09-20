using AsyncBridge;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncBridge.Tests
{
    [TestFixture]
    public class AsyncHelperTests
    {
        [TestFixtureSetUp]
        public void SetUp()
        {
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

        public async Task<string> AsyncString(string s)
        {
            await Task.Yield();

            return s;
        }
    }
}
