using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities.AsyncPrimatives;

namespace CampfireNet.Utilities.Merkle {
   public class FileSystemCampfireNetObjectStore : ICampfireNetObjectStore {
      private readonly string basePath;

      public FileSystemCampfireNetObjectStore(string basePath) {
         this.basePath = basePath;
      }

      public async Task<Tuple<bool, byte[]>> TryReadAsync(string ns, string hash) {
         var filePath = BuildPath(ns, hash);
         Directory.CreateDirectory(Path.GetDirectoryName(filePath));
         try {
            using (var fs = File.OpenRead(filePath)) {
               var length = (int)fs.Length;
               var buffer = new byte[length];
               int offset = 0;
               while (offset < length) {
                  var bytesRemaining = length - offset;
                  offset += await fs.ReadAsync(buffer, offset, bytesRemaining);
               }
               return Tuple.Create(true, buffer);
            }
         } catch (FileNotFoundException) {
            return Tuple.Create(false, (byte[])null);
         }
      }

      public async Task WriteAsync(string ns, string hash, byte[] contents) {
         var filePath = BuildPath(ns, hash);
         Directory.CreateDirectory(Path.GetDirectoryName(filePath));

         using (var fs = File.OpenWrite(filePath)) {
            await fs.WriteAsync(contents, 0, contents.Length);
         }
      }

      public async Task<bool> TryWriteUniqueAsync(string ns, string hash, byte[] contents) {
         var filePath = BuildPath(ns, hash);
         Directory.CreateDirectory(Path.GetDirectoryName(filePath));

         // todo: validate file not corrupted e.g. due to app crash
         if (File.Exists(filePath)) {
            return false;
         }

         // todo: handle race
         using (var fs = File.Open(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            await fs.WriteAsync(contents, 0, contents.Length);
            return true;
         }
      }

      // .net base64 uses + / =, also expect = for padding
      private string BuildPath(string ns, string hash) => Path.Combine(basePath, ns, hash.Replace('/', '_'));
   }
}