using System;
using System.Threading;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.Channels {
   public class DisconnectableChannel<T, TError> : Channel<T>
      where TError : Exception, new() {
      private readonly ReadableChannel<bool> disconnectedChannel;
      private readonly Channel<T> dataChannel;

      public DisconnectableChannel(ReadableChannel<bool> disconnectedChannel, Channel<T> dataChannel) {
         this.disconnectedChannel = disconnectedChannel;
         this.dataChannel = dataChannel;
      }

      public int Count => dataChannel.Count;

      public bool TryRead(out T message) {
         return dataChannel.TryRead(out message);
      }

      public async Task<T> ReadAsync(CancellationToken cancellationToken, Func<T, bool> acceptanceTest) {
         bool disconnected = false;
         T result = default(T);
         await new Select {
            ChannelsExtensions.Case(disconnectedChannel, () => {
               disconnected = true;
            }),
            ChannelsExtensions.Case(dataChannel, data => {
               result = data;
            }, acceptanceTest)
         }.WaitAsync(cancellationToken);

         if (disconnected) {
            throw new TError();
         }
         return result;
      }

      public Task WriteAsync(T message, CancellationToken cancellationToken) {
         return dataChannel.WriteAsync(message, cancellationToken);
      }
   }
}