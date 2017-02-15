using System.IO;
using System.Linq;
using System.Text;
using CampfireNet.Identities;
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

      public byte[] ToByteArray(WhoisPacket p) {
         if (p.IdHash.Length != CryptoUtil.HASH_SIZE) {
            throw new InvalidStateException();
         }
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms, Encoding.UTF8, true)) {
            writer.Write((uint)PacketType.Whois);
            writer.Write(p.IdHash);
            return ms.ToArray();
         }
      }

      public byte[] ToByteArray(IdentPacket p) {
         if (p.Id.Length != CryptoUtil.ASYM_KEY_SIZE_BYTES) {
            throw new InvalidStateException();
         }
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms, Encoding.UTF8, true)) {
            writer.Write((uint)PacketType.Ident);
            writer.Write(p.Id);

            writer.Write((int)p.TrustChain.Length);
            p.TrustChain.ForEach(n => writer.Write(n.FullData));

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
               case PacketType.Whois:
                  return new WhoisPacket {
                     IdHash = reader.ReadBytes(CryptoUtil.HASH_SIZE)
                  };
               case PacketType.Ident:
                  return new IdentPacket {
                     Id = reader.ReadBytes(CryptoUtil.ASYM_KEY_SIZE_BYTES),
                     TrustChain = TrustChainUtil.SegmentChain(reader.ReadBytes(reader.ReadInt32() * TrustChainNode.NODE_BLOCK_SIZE))
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
