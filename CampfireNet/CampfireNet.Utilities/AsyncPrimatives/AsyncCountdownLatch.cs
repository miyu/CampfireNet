using System;
using System.Threading;
using System.Threading.Tasks;

// https://github.com/the-dargon-project/commons/blob/master/src/Dargon.Commons/AsyncPrimitives/AsyncCountdownLatch.cs
namespace CampfireNet.Utilities.AsyncPrimatives {
   public class AsyncCountdownLatch {
      private readonly AsyncLatch latch = new AsyncLatch();
      private int count;

      public AsyncCountdownLatch(int count) {
         this.count = count;
      }

      public Task WaitAsync(CancellationToken cancellationToken = default(CancellationToken)) {
         return latch.WaitAsync(cancellationToken);
      }

      public bool Signal() {
         var decrementResult = Interlocked.Decrement(ref count);
         if (decrementResult == 0) {
            latch.Set();
            return true;
         }
         if (decrementResult < 0) {
            throw new InvalidOperationException("Attempted to decrement latch beyond zero count.");
         }
         return false;
      }
   }
}