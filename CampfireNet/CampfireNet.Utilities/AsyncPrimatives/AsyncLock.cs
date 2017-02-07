using System;
using System.Threading;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.AsyncPrimatives {
   // https://github.com/the-dargon-project/commons/blob/master/src/Dargon.Commons/AsyncPrimitives/AsyncLock.cs
   public class AsyncLock {
      private readonly AsyncSemaphore semaphore = new AsyncSemaphore(1);

      public async Task<Guard> LockAsync(CancellationToken cancellationToken = default(CancellationToken)) {
         await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
         return new Guard(semaphore);
      }

      /// <summary>
      /// Releases semaphore on disposal or request
      /// </summary>
      public class Guard : IDisposable {
         private readonly AsyncCountdownLatch duplicateFreeCheckLatch = new AsyncCountdownLatch(1);
         private readonly AsyncSemaphore semaphore;
         private const int STATE_TAKEN = 0;
         private const int STATE_FREED = 1;
         private const int STATE_DISPOSED = 2;
         private int state = STATE_TAKEN;

         public Guard(AsyncSemaphore semaphore) {
            this.semaphore = semaphore;
         }

         public void Free() {
            if (state != STATE_TAKEN) throw new InvalidOperationException();
            FreeInternal();
            state = STATE_FREED;
         }

         public void Dispose() {
            if (state == STATE_DISPOSED) throw new InvalidOperationException();
            if (state == STATE_TAKEN) {
               FreeInternal();
            }
            state = STATE_DISPOSED;
         }

         private void FreeInternal() {
            duplicateFreeCheckLatch.Signal();
            semaphore.Release();
         }
      }
   }
}