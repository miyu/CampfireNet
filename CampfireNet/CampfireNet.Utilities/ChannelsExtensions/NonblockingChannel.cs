using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities.AsyncPrimatives;

namespace CampfireNet.Utilities.ChannelsExtensions {
   public class NonblockingChannel<T> : Channel<T> {
      private readonly ConcurrentQueue<T> writeQueue;
      private readonly AsyncSemaphore readSemaphore;
      private readonly AsyncSemaphore writeSemaphore;

      public NonblockingChannel(ConcurrentQueue<T> writeQueue, AsyncSemaphore readSemaphore, AsyncSemaphore writeSemaphore) {
         this.writeQueue = writeQueue;
         this.readSemaphore = readSemaphore;
         this.writeSemaphore = writeSemaphore;
      }

      public int Count => writeQueue.Count;

      public async Task WriteAsync(T message, CancellationToken cancellationToken) {
         if (writeSemaphore != null) {
            await writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
         }
         writeQueue.Enqueue(message);
         readSemaphore.Release();
      }

      public bool TryRead(out T message) {
         if (!readSemaphore.TryTake()) {
            message = default(T);
            return false;
         }
         if (!writeQueue.TryDequeue(out message)) {
            throw new InvalidStateException();
         }
         return true;
      }

      public async Task<T> ReadAsync(CancellationToken cancellationToken, Func<T, bool> acceptanceTest) {
         while (!cancellationToken.IsCancellationRequested) {
            await readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            T message;
            if (!writeQueue.TryDequeue(out message)) {
               throw new InvalidStateException();
            }
            if (acceptanceTest(message)) {
               writeSemaphore?.Release();
               return message;
            } else {
               writeQueue.Enqueue(message);
               readSemaphore.Release();
            }
         }
         // throw is guaranteed
         cancellationToken.ThrowIfCancellationRequested();
         return default(T);
      }
   }
}