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

      public MerkleTree<BroadcastMessage> CreateForLocal() {
         return new MerkleTree<BroadcastMessage>("local", broadcastMessageSerializer, objectStore);
      }

      public MerkleTree<BroadcastMessage> CreateForNeighbor(string id) {
         return new MerkleTree<BroadcastMessage>(id, broadcastMessageSerializer, objectStore);
      }
   }
}