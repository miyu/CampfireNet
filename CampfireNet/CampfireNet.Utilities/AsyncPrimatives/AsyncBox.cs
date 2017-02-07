using System;
using System.Threading;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.AsyncPrimatives {
   public class AsyncBox<T> {
      private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      private readonly object synchronization = new object();
      private T result;

      public void SetResult(T value) {
         lock (synchronization) {
            result = value;
         }
         tcs.TrySetResult(true);
      }

      public void SetException(Exception ex) {
         tcs.SetException(ex);
      }

      public async Task<T> GetResultAsync(CancellationToken cancellationToken = default(CancellationToken)) {
         await tcs.Task;
         lock (synchronization) {
            return result;
         }
      }
   }
}