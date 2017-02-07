using CampfireNet.Utilities.Merkle;

namespace CampfireNet.IO {
   public class BroadcastMessageSerializer : IItemOperations<BroadcastMessage> {
      public byte[] Serialize(BroadcastMessage item) {
         return item.Data;
      }

      public BroadcastMessage Deserialize(byte[] data) {
         return new BroadcastMessage {
            Data = data
         };
      }
   }
}
