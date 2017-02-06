using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Threading;

namespace CampfireNet.Utilities.Merkle {
   public static class CampfireNetHash {
      // Regardless of their documentation SHA256 is not thread safe.
      private static readonly ThreadLocal<SHA256> sha256 = new ThreadLocal<SHA256>(() => new SHA256Managed());

      public const int RAW_BYTE_COUNT = 32;
      public const int BASE64_BYTE_COUNT = 48;
      public static readonly string ZERO_HASH_BASE64 = Convert.ToBase64String(new byte[RAW_BYTE_COUNT]);                                                                                                                    

      public static string ComputeSha256Base64(byte[] contents) {
         return ComputeSha256Base64(contents, 0, contents.Length);
      }

      public static string ComputeSha256Base64(byte[] contents, int offset, int length) {
         return Convert.ToBase64String(sha256.Value.ComputeHash(contents, offset, length));
      }

      public static string ConvertBase64BufferToSha256Base64String(byte[] buffer) {
         return new BinaryReader(new MemoryStream(buffer)).ReadSha256Base64();
      }

      public static string ReadSha256Base64(this BinaryReader reader) {
         var buffer = reader.ReadBytes(BASE64_BYTE_COUNT);
         var length = buffer.ToList().IndexOf(0);
         return Encoding.ASCII.GetString(buffer, 0, length);
      }

      public static void WriteSha256Base64(this BinaryWriter writer, string hashBase64) {
         var ascii = Encoding.ASCII.GetBytes(hashBase64);
         writer.Write(ascii, 0, ascii.Length);
         for (var i = ascii.Length; i < BASE64_BYTE_COUNT; i++) {
            writer.Write((byte)0);
         }
      }
   }
}