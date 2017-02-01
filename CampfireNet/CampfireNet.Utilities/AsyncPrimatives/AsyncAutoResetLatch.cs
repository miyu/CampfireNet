using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.AsyncPrimatives {
   public class AsyncAutoResetLatch {
      private readonly ConcurrentQueue<AsyncLatch> latches = new ConcurrentQueue<AsyncLatch>();
      private const int kStateSet = -1;
      private const int kStateNeutral = 0;
      private int state = kStateNeutral;

      public Task WaitAsync(CancellationToken token = default(CancellationToken)) {
         var spinner = new SpinWait();
         while (!token.IsCancellationRequested) {
            var previousValue = Interlocked.CompareExchange(ref state, kStateNeutral, kStateSet);
            if (previousValue == kStateSet) {
               return Task.FromResult(false);
            }
            var nextValue = previousValue + 1;
            if (Interlocked.CompareExchange(ref state, nextValue, previousValue) == previousValue) {
               var latch = new AsyncLatch();
               latches.Enqueue(latch);
               return latch.WaitAsync(token);
            }
            spinner.SpinOnce();
         }
         token.ThrowIfCancellationRequested();
         // impossible code
         throw new InvalidStateException();
      }

      public void Set() {
         var spinner = new SpinWait();
         while (true) {
            var previousValue = Interlocked.CompareExchange(ref state, kStateSet, kStateNeutral);
            switch (previousValue) {
               case kStateSet:
               case kStateNeutral:
                  return;
               default:
                  var nextValue = previousValue - 1;
                  if (Interlocked.CompareExchange(ref state, nextValue, previousValue) == previousValue) {
                     AsyncLatch latch;
                     while (!latches.TryDequeue(out latch)) {
                        spinner.SpinOnce();
                     }
                     latch.Set();
                     return;
                  }
                  break;
            }
            spinner.SpinOnce();
         }
      }
   }
}