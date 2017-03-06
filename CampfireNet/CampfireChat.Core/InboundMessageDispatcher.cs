using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampfireNet;
using CampfireNet.Identities;
using CampfireNet.Utilities;

namespace CampfireChat {
   public static class CampfireChatSerializer {
      public static object Deserialize(MessageReceivedEventArgs e) {
         using (var ms = new MemoryStream(e.Message.DecryptedPayload))
         using (var reader = new BinaryReader(ms)) {
            var type = reader.ReadUInt32();
            switch ((ChatDtoTypeId)type) {
               case ChatDtoTypeId.Message:
                  return new ChatMessageDto {
                     LocalTimestamp = DateTime.Now,
                     BroadcastMessage = e.Message,
                     SequenceNumber = reader.ReadInt32(),
                     LogicalClock = ReadLogicalClock(reader),
                     ContentType = (ChatMessageContentType)reader.ReadUInt32(),
                     ContentRaw = reader.ReadBytes(reader.ReadInt32()),
                     FriendlySenderName = reader.ReadLengthPrefixedUtf8String(),
                  };
               default:
                  throw new InvalidStateException($"Unknown message type: {type}");
            }
         }
      }

      private static Dictionary<IdentityHash, int> ReadLogicalClock(BinaryReader reader) {
         var dict = new Dictionary<IdentityHash, int>();
         var count = reader.ReadInt32();
         for (var i = 0; i < count; i++) {
            var identityHashBytes = reader.ReadBytes(CryptoUtil.HASH_SIZE);
            var clock = reader.ReadInt32();

            var identityHash = IdentityHash.GetFlyweight(identityHashBytes);
            dict.Add(identityHash, clock);
         }
         return dict;
      }

      public static byte[] GetBytes(ChatMessageDto data) {
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms)) {
            writer.Write((uint)ChatDtoTypeId.Message);
            writer.Write((int)data.SequenceNumber);
            writer.Write((int)data.LogicalClock.Count);
            foreach (var kvp in data.LogicalClock) {
               writer.Write(kvp.Key.Bytes.ToArray());
               writer.Write((int)kvp.Value);
            }
            writer.Write((uint)data.ContentType);
            writer.Write((int)data.ContentRaw.Length);
            writer.Write(data.ContentRaw, 0, data.ContentRaw.Length);
            writer.WriteLengthPrefixedUtf8String(data.FriendlySenderName);
            return ms.ToArray();
         }
      }
   }

   public enum ChatDtoTypeId : uint {
      Message,
      Invite
   }

   public enum ChatMessageContentType : uint {
      Text
   }

   public class ChatMessageDto {
      public BroadcastMessage BroadcastMessage { get; set; }
      
      public DateTime LocalTimestamp { get; set; }
      public int SequenceNumber { get; set; }
      public Dictionary<IdentityHash, int> LogicalClock { get; set; }
      public string FriendlySenderName { get; set; }
      public ChatMessageContentType ContentType { get; set; }
      public byte[] ContentRaw { get; set; }
   }
}
