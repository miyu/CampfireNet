using System;
using System.Threading;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.AsyncPrimatives {
   public class AsyncBox<T> {
      private readonly TaskCompletionSource<T> tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

      public void SetResult(T value) {
         tcs.SetResult(value);
      }

      public void SetException(Exception ex) {
         tcs.SetException(ex);
      }

      public Task<T> GetResultAsync(CancellationToken cancellationToken = default(CancellationToken)) {
         return tcs.Task;
      }
   }
}