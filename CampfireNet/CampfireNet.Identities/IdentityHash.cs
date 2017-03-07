using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CampfireNet.Utilities;

namespace CampfireNet.Identities {
   public class IdentityHash {
      private static readonly ConcurrentDictionary<string, IdentityHash> flyweights = new ConcurrentDictionary<string, IdentityHash>();
      private readonly byte[] data;

      private IdentityHash(byte[] data) {
         this.data = data;
      }

      public IReadOnlyList<byte> Bytes => data;

      public override string ToString() => data.ToHexString();

      public static IdentityHash GetFlyweight(byte[] data) {
         if (data.Length != CryptoUtil.HASH_SIZE) {
            Console.WriteLine(BitConverter.ToString(data));
            Console.WriteLine(data.Length);
            throw new ArgumentException($"{nameof(data)} wasn't of correct hash length.");
         }

         return flyweights.GetOrAdd(
            data.ToHexString(),
            new IdentityHash(data));
      }
   }
}