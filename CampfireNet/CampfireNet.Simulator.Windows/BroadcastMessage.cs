using System.IO;
using System.Text;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet.Simulator {
   public class BroadcastMessage {
      public byte[] Data { get; set; }
   }

   public class CampfireNetPacketMerkleOperations : IItemOperations<BroadcastMessage> {
      public byte[] Serialize(BroadcastMessage item) {
         return item.Data;
      }
   }

   public enum PacketType : uint {
      /// <summary>
      /// "have"
      /// </summary>
      Have = 0x65766168U,

      /// <summary>
      /// "need"
      /// </summary>
      Need = 0x6465656EU,

      /// <summary>
      /// "give"
      /// </summary>
      Give = 0x65766967U,

      /// <summary>
      /// 'done'
      /// </summary>
      Done = 0x656E6F64
   }

   public class HavePacket {
      public string MerkleRootHash { get; set; }
   }

   public class NeedPacket {
      public string MerkleRootHash { get; set; }
   }

   public class GivePacket {
      public string NodeHash { get; set; }
      public MerkleNode Node { get; set; }
   }

   public class DonePacket { }

   public class WirePacketSerializer {
      public byte[] ToByteArray(HavePacket p) {
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms, Encoding.UTF8, true)) {
            writer.Write((uint)PacketType.Have);
            writer.WriteSha256Base64(p.MerkleRootHash);
            return ms.ToArray();
         }
      }

      public byte[] ToByteArray(NeedPacket p) {
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms, Encoding.UTF8, true)) {
            writer.Write((uint)PacketType.Need);
            writer.WriteSha256Base64(p.MerkleRootHash);
            return ms.ToArray();
         }
      }

      public byte[] ToByteArray(GivePacket p) {
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms, Encoding.UTF8, true)) {
            writer.Write((uint)PacketType.Give);
            writer.WriteSha256Base64(p.NodeHash);
            writer.WriteMerkleNode(p.Node);
            return ms.ToArray();
         }
      }

      public byte[] ToByteArray(DonePacket p) {
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms, Encoding.UTF8, true)) {
            writer.Write((uint)PacketType.Done);
            return ms.ToArray();
         }
      }

      public object ToObject(byte[] buffer) {
         using (var ms = new MemoryStream(buffer))
         using (var reader = new BinaryReader(ms)) {
            var packetType = (PacketType)reader.ReadUInt32();
            switch (packetType) {
               case PacketType.Have:
                  return new HavePacket {
                     MerkleRootHash = reader.ReadSha256Base64()
                  };
               case PacketType.Need:
                  return new NeedPacket {
                     MerkleRootHash = reader.ReadSha256Base64()
                  };
               case PacketType.Give:
                  return new GivePacket {
                     NodeHash = reader.ReadSha256Base64(),
                     Node = reader.ReadMerkleNode()
                  };
               case PacketType.Done:
                  return new DonePacket();
               default:
                  throw new InvalidStateException();
            }
         }
      }
   }
}
