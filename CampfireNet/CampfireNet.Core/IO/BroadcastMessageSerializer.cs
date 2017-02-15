using System.IO;
using CampfireNet.Identities;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet.IO {
   public class BroadcastMessageSerializer : IItemOperations<BroadcastMessageDto> {
      public byte[] Serialize(BroadcastMessageDto item) {
         var len = CryptoUtil.HASH_SIZE * 2 + item.Payload.Length + CryptoUtil.ASYM_KEY_SIZE_BYTES;
         using (var ms = new MemoryStream(len)) {
            ms.Write(item.SourceIdHash, 0, item.SourceIdHash.Length);
            ms.Write(item.DestinationIdHash, 0, item.DestinationIdHash.Length);
            ms.Write(item.Payload, 0, item.Payload.Length);
            ms.Write(item.Signature, 0, item.Signature.Length);

            var buffer = ms.GetBuffer();
            if (ms.Length != len || ms.Position != len || buffer.Length != len) {
               throw new InvalidStateException();
            }
            return buffer;
         }
      }

      public BroadcastMessageDto Deserialize(byte[] data) {
         using (var ms = new MemoryStream(data))
         using (var reader = new BinaryReader(ms)) {
            return new BroadcastMessageDto {
               SourceIdHash = reader.ReadBytes(CryptoUtil.HASH_SIZE),
               DestinationIdHash = reader.ReadBytes(CryptoUtil.HASH_SIZE),
               Payload = reader.ReadBytes(data.Length - 2 * CryptoUtil.HASH_SIZE - CryptoUtil.ASYM_KEY_SIZE_BYTES),
               Signature = reader.ReadBytes(CryptoUtil.ASYM_KEY_SIZE_BYTES),
            };
         }
      }
   }
}
