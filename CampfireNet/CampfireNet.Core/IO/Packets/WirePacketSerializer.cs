using System.IO;
using System.Text;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet.IO.Packets {
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
