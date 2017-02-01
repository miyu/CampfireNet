using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.AsyncPrimatives {
   // https://github.com/the-dargon-project/commons/blob/master/src/Dargon.Commons/AsyncPrimitives/AsyncSemaphore.cs
   public class AsyncSemaphore {
      private readonly ConcurrentQueue<WaitContext> waitContexts = new ConcurrentQueue<WaitContext>();
      private readonly object undoLock = new object();
      private int counter; // positive = signals waiting, negative = queued waiting

      public AsyncSemaphore(int count = 0) {
         if (count < 0) {
            throw new ArgumentOutOfRangeException();
         }
         counter = count;
      }

      public int Count => counter;

      public bool TryTake() {
         var spinner = new SpinWait();
         while (true) {
            var capturedCounter = Interlocked.CompareExchange(ref counter, 0, 0);
            if (capturedCounter > 0) {
               var nextCounter = capturedCounter - 1;
               if (Interlocked.CompareExchange(ref counter, nextCounter, capturedCounter) == capturedCounter) {
                  return true;
               }
            } else {
               return false;
            }
            spinner.SpinOnce();
         }
      }

      public async Task WaitAsync(CancellationToken cancellationToken = default(CancellationToken)) {
         var spinner = new SpinWait();
         while (true) {
            var capturedCounter = Interlocked.CompareExchange(ref counter, 0, 0);
            var nextCounter = capturedCounter - 1;
            if (Interlocked.CompareExchange(ref counter, nextCounter, capturedCounter) == capturedCounter) {
               if (capturedCounter > 0) {
                  return;
               } else {
                  var latch = new AsyncLatch();
                  var waitContext = new WaitContext { Latch = latch };
                  try {
                     waitContexts.Enqueue(waitContext);
                     await latch.WaitAsync(cancellationToken).ConfigureAwait(false);
                     return;
                  } catch (OperationCanceledException e) {
                     if (ResolveWaitContextUndoAsync(waitContext)) {
                        throw;
                     } else {
                        await waitContext.Latch.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                        return;
                     }
                     //                     return ResolveWaitContextUndoAsync(waitContext, e);
                  }
               }
            }
            spinner.SpinOnce();
         }
      }

      private bool ResolveWaitContextUndoAsync(WaitContext waitContext) {
         var spinner = new SpinWait();
         bool dequeuedSelf = false;
         lock (undoLock) {
            WaitContext takenWaitContext;
            var maxIterations = waitContexts.Count;
            for (var i = 0; i < maxIterations && waitContexts.TryDequeue(out takenWaitContext); i++) {
               if (takenWaitContext == waitContext) {
                  dequeuedSelf = true;
                  break;
               } else {
                  waitContexts.Enqueue(takenWaitContext);
               }
            }
         }
         if (dequeuedSelf) {
            while (true) {
               var capturedCounter = Interlocked.CompareExchange(ref counter, 0, 0);
               if (capturedCounter >= 0) {
                  waitContexts.Enqueue(waitContext);
                  break;
               } else {
                  var nextCounter = capturedCounter + 1;
                  if (Interlocked.CompareExchange(ref counter, nextCounter, capturedCounter) == capturedCounter) {
                     return true;
                     //                     var tcs = new TaskCompletionSource<bool>();
                     //                     tcs.SetException(e);
                     //                     return tcs.Task;
                     //                     ExceptionDispatchInfo.Capture(e).Throw();
                  }
                  spinner.SpinOnce();
               }
            }
         }
         //         t = waitContext.Latch.WaitAsync();
         return false;
         //         return waitContext.Latch.WaitAsync();
      }

      public void Release(int c) {
         for (var i = 0; i < c; i++) {
            Release();
         }
      }

      public void Release() {
         var spinner = new SpinWait();
         while (true) {
            var capturedCounter = Interlocked.CompareExchange(ref counter, 0, 0);
            var nextCounter = capturedCounter + 1;
            if (Interlocked.CompareExchange(ref counter, nextCounter, capturedCounter) == capturedCounter) {
               if (capturedCounter >= 0) {
                  return;
               } else {
                  while (true) {
                     WaitContext waitContext;
                     while (!waitContexts.TryDequeue(out waitContext)) {
                        spinner.SpinOnce();
                     }
                     waitContext.Latch.Set();
                     return;
                  }
               }
            }
            spinner.SpinOnce();
         }
      }

      public class WaitContext {
         public const int kStatePending = 0;
         public const int kStateReleased = 1;
         public const int kStateCancelled = 2;

         public AsyncLatch Latch { get; set; }
         public int state = kStatePending;
      }
   }
}