using System;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace CampfireNet.Utilities {
   public static partial class DargonCommonsExtensions {
      public static Task Forgettable(this Task task) {
         return task.ContinueWith(
            (t, _) => {
               if (t.IsFaulted) {
                  Console.WriteLine("Forgotten task threw: " + t.Exception);
               }
            }, null);
      }

      public static void Forget(this Task task) {
         var throwaway = task.Forgettable();
      }

      public static byte[] GetBuffer(this MemoryStream ms) {
         ArraySegment<byte> segment;
         if (!ms.TryGetBuffer(out segment)) {
            throw new InvalidStateException();
         }
         return segment.Array;
      }

      public static string ReadLengthPrefixedUtf8String(this BinaryReader reader) {
         return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
      }

      public static void WriteLengthPrefixedUtf8String(this BinaryWriter writer, string s) {
         var bytes = Encoding.UTF8.GetBytes(s);
         writer.Write((int)bytes.Length);
         writer.Write(bytes, 0, bytes.Length);
      }
   }
}