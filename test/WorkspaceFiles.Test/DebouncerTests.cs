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

        [TestMethod]
        public async Task WhenDebounceUsesDifferentKeysThenBothActionsRun()
        {
            var firstKey = Guid.NewGuid().ToString("N");
            var secondKey = Guid.NewGuid().ToString("N");
            var invocationCount = 0;

            Debouncer.Debounce(firstKey, () => Interlocked.Increment(ref invocationCount), 75);
            Debouncer.Debounce(secondKey, () => Interlocked.Increment(ref invocationCount), 75);

            await Task.Delay(300);

            Assert.AreEqual(2, invocationCount);
        }

        [TestMethod]
        public async Task WhenDebounceIsCalledAfterExecutionThenActionRunsAgain()
        {
            var key = Guid.NewGuid().ToString("N");
            var invocationCount = 0;

            Debouncer.Debounce(key, () => Interlocked.Increment(ref invocationCount), 50);
            await Task.Delay(250);

            Debouncer.Debounce(key, () => Interlocked.Increment(ref invocationCount), 50);
            await Task.Delay(250);

            Assert.AreEqual(2, invocationCount);
        }
    }
}
