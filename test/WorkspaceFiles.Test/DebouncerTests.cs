using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspaceFiles.Test
{
    [TestClass]
    public class DebouncerTests
    {
        [TestMethod]
        public async Task WhenDebounceIsCalledRepeatedlyThenOnlyLastActionRuns()
        {
            var key = Guid.NewGuid().ToString("N");
            var invocationCount = 0;

            Debouncer.Debounce(key, () => Interlocked.Increment(ref invocationCount), 100);
            Debouncer.Debounce(key, () => Interlocked.Increment(ref invocationCount), 100);

            await Task.Delay(400);

            Assert.AreEqual(1, invocationCount);
        }
    }
}
