using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Packets;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet {
   public class ClientMerkleTreeFactory {
      private readonly BroadcastMessageSerializer broadcastMessageSerializer;
      private readonly ICampfireNetObjectStore objectStore;

      public ClientMerkleTreeFactory(BroadcastMessageSerializer broadcastMessageSerializer, ICampfireNetObjectStore objectStore) {
         this.broadcastMessageSerializer = broadcastMessageSerializer;
         this.objectStore = objectStore;
      }

      public MerkleTree<BroadcastMessageDto> CreateForLocal() {
         return new MerkleTree<BroadcastMessageDto>("local", broadcastMessageSerializer, objectStore);
      }

      public MerkleTree<BroadcastMessageDto> CreateForNeighbor(string id) {
         return new MerkleTree<BroadcastMessageDto>(id, broadcastMessageSerializer, objectStore);
      }
   }
}