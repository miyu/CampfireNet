using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet.Utilities.AsyncPrimatives;

namespace CampfireNet.Utilities.Merkle {
   public class MerkleTree<T> {
      private readonly AsyncLock treeSync = new AsyncLock();
      private readonly string rootKey;
      private readonly IItemOperations<T> itemOperations;
      private readonly ICampfireNetObjectStore objectStore;

      public MerkleTree(string rootKey, IItemOperations<T> itemOperations, ICampfireNetObjectStore objectStore) {
         this.rootKey = rootKey;
         this.itemOperations = itemOperations;
         this.objectStore = objectStore;
      }

      private string RootsNamespace => $"net/roots";
      private string NetworkDataNamespace => $"net/data";

      public async Task<string> GetRootHashAsync() {
         using (await treeSync.LockAsync()) {
            return await GetRootHashAsyncUnderLock();
         }
      }

      private async Task<string> GetRootHashAsyncUnderLock() {
         var tryReadResult = await objectStore.TryReadAsync(RootsNamespace, rootKey);
         var rootExists = tryReadResult.Item1;
         return rootExists
            ? CampfireNetHash.ConvertBase64BufferToSha256Base64String(tryReadResult.Item2)
            : null;
      }

      private async Task SetRootHashAsyncUnderLock(string nextRootHash) {
         var nextRootHashBytes = new byte[CampfireNetHash.BASE64_BYTE_COUNT];
         using (var ms = new MemoryStream(nextRootHashBytes))
         using (var writer = new BinaryWriter(ms)) {
            writer.WriteSha256Base64(nextRootHash);
            await objectStore.WriteAsync(RootsNamespace, rootKey, nextRootHashBytes);
         }
      }

      public async Task<MerkleNode> GetNodeAsync(string hash) {
         return await objectStore.ReadMerkleNodeAsync(NetworkDataNamespace, hash);
      }

      public async Task InsertAsync(T item) {
         // Persist object contents
         var contents = itemOperations.Serialize(item);
         var itemNode = new MerkleNode {
            TypeTag = MerkleNodeTypeTag.Data,
            LeftHash = CampfireNetHash.ZERO_HASH_BASE64,
            RightHash = CampfireNetHash.ZERO_HASH_BASE64,
            Descendents = 0,
            Contents = contents
         };
         await InsertAsyncInternal(itemNode);
      }

      public Task InsertAsync(MerkleNode merkleNode) {
         if (merkleNode.LeftHash != CampfireNetHash.ZERO_HASH_BASE64 ||
             merkleNode.RightHash != CampfireNetHash.ZERO_HASH_BASE64) {
            throw new ArgumentException();
         }
         return InsertAsyncInternal(merkleNode);
      }

      private async Task InsertAsyncInternal(MerkleNode itemNode) {
         var itemHash = await objectStore.WriteMerkleNodeAsync(NetworkDataNamespace, itemNode);

         // Persist pointer to object in merkle structure.
         using (await treeSync.LockAsync()) {
            var nextRootHash = itemHash;
            var rootHash = await GetRootHashAsyncUnderLock();
            if (rootHash != null) {
               var rootNode = await GetNodeAsync(rootHash);
               if (rootNode == null) throw new InvalidStateException();

               nextRootHash = await InsertHelperAsyncUnderTreeLock(rootHash, rootNode, itemHash);
               Console.WriteLine("ROOT HASH " + rootHash + " => " + nextRootHash);
            }

            await SetRootHashAsyncUnderLock(nextRootHash);
         }
      }

      private async Task<string> InsertHelperAsyncUnderTreeLock(string replaceeHash, MerkleNode replaceeNode, string inserteeHash) {
         var rightHash = replaceeNode.RightHash;
         var rightNode = await objectStore.ReadMerkleNodeAsync(NetworkDataNamespace, rightHash);
         var rightwardDescendents = rightNode == null ? 0 : (1 + rightNode.Descendents);

         var isReplaceePerfect = replaceeNode.Descendents == rightwardDescendents * 2;
         if (isReplaceePerfect) {
            var internalNode = new MerkleNode {
               TypeTag = MerkleNodeTypeTag.Node,
               LeftHash = replaceeHash,
               RightHash = inserteeHash,
               Descendents = 2 + replaceeNode.Descendents
            };
            return await objectStore.WriteMerkleNodeAsync(NetworkDataNamespace, internalNode);
         }

         var rightReplacementHash = await InsertHelperAsyncUnderTreeLock(rightHash, rightNode, inserteeHash);
         replaceeNode.Descendents += 2; // leaf and inner node
         replaceeNode.RightHash = rightReplacementHash;
         Console.WriteLine("Write: " + replaceeNode.Descendents);
         return await objectStore.WriteMerkleNodeAsync(NetworkDataNamespace, replaceeNode);
      }

      public async Task ImportAsync(string upstreamRoot, List<Tuple<string, MerkleNode>> nodesToImport) {
         foreach (var job in nodesToImport) {
            var merkleHash = job.Item1;
            var merkleNode = job.Item2;
            var insertHash = await objectStore.WriteMerkleNodeAsync(NetworkDataNamespace, merkleNode);
            if (merkleHash != insertHash) {
               throw new InvalidStateException($"Hash Mismatch! {merkleHash} {insertHash}");
            }
         }
         using (await treeSync.LockAsync()) {
            await SetRootHashAsyncUnderLock(upstreamRoot);
         }
      }
   }
}
