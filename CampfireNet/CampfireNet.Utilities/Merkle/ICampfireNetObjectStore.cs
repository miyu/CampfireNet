using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.Merkle {
   public interface ICampfireNetObjectStore {
      Task<Tuple<bool, byte[]>> TryReadAsync(string ns, string hash);
      Task WriteAsync(string ns, string hash, byte[] contents);
      Task<bool> TryWriteUniqueAsync(string ns, string hash, byte[] contents);
   }

   public class InMemoryCampfireNetObjectStore : ICampfireNetObjectStore {
      private readonly ConcurrentDictionary<string, byte[]> store = new ConcurrentDictionary<string, byte[]>();

      public Task<Tuple<bool, byte[]>> TryReadAsync(string ns, string hash) {
         byte[] contents;
         var exists = store.TryGetValue(BuildKey(ns, hash), out contents);
         return Task.FromResult(Tuple.Create(exists, contents));
      }

      public Task WriteAsync(string ns, string hash, byte[] contents) {
         store[BuildKey(ns, hash)] = contents;
         return Task.CompletedTask;
      }

      public Task<bool> TryWriteUniqueAsync(string ns, string hash, byte[] contents) {
         return Task.FromResult(store.TryAdd(BuildKey(ns, hash), contents));
      }

      private string BuildKey(string ns, string hash) => $"{ns}/{hash}";
   }
}