using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CampfireNet.Utilities.AsyncPrimatives;

namespace CampfireNet.Utilities.Merkle {
   public class MerkleTree<T> {
      private readonly AsyncLock treeSync = new AsyncLock();
      private readonly string treeKey;
      private readonly IItemOperations<T> itemOperations;
      private readonly ICampfireNetObjectStore objectStore;

      public MerkleTree(string treeKey, IItemOperations<T> itemOperations, ICampfireNetObjectStore objectStore) {
         this.treeKey = treeKey;
         this.itemOperations = itemOperations;
         this.objectStore = objectStore;

         TreeNamespace = $"net/trees/{treeKey}";
         TreeContainmentNamespace = $"{TreeNamespace}/contains";
      }


      private string TreeNamespace { get; }
      private string TreeContainmentNamespace { get; }
      private string NetworkDataNamespace => $"net/data";

      public async Task<string> GetRootHashAsync() {
         using (await treeSync.LockAsync().ConfigureAwait(false)) {
            return await GetRootHashAsyncUnderLock().ConfigureAwait(false);
         }
      }

      private async Task<string> GetRootHashAsyncUnderLock() {
         var tryReadResult = await objectStore.TryReadAsync(TreeNamespace, "root").ConfigureAwait(false);
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
            await objectStore.WriteAsync(TreeNamespace, "root", nextRootHashBytes).ConfigureAwait(false);
         }
      }

      public async Task<MerkleNode> GetNodeAsync(string hash) {
         // We use one large shared store for merkle nodes, so we must check the 
         // tree's containment set to ensure the node is marked as held there.
         var containedResult = await objectStore.TryReadAsync(TreeContainmentNamespace, hash).ConfigureAwait(false);
         var isHashContained = containedResult.Item1;
         if (!isHashContained) {
            return null;
         }
         return await objectStore.ReadMerkleNodeAsync(NetworkDataNamespace, hash).ConfigureAwait(false);
      }

      public Task<Tuple<bool, string>> TryInsertAsync(T item) {
         // Persist object contents
         var contents = itemOperations.Serialize(item);
         var itemNode = new MerkleNode {
            TypeTag = MerkleNodeTypeTag.Data,
            LeftHash = CampfireNetHash.ZERO_HASH_BASE64,
            RightHash = CampfireNetHash.ZERO_HASH_BASE64,
            Descendents = 0,
            Contents = contents
         };
         return InsertAsyncInternal(itemNode);
      }

      public Task<Tuple<bool, string>> TryInsertAsync(MerkleNode merkleNode) {
         if (merkleNode.LeftHash != CampfireNetHash.ZERO_HASH_BASE64 ||
             merkleNode.RightHash != CampfireNetHash.ZERO_HASH_BASE64) {
            throw new ArgumentException();
         }
         return InsertAsyncInternal(merkleNode);
      }

      private async Task<Tuple<bool, string>> InsertAsyncInternal(MerkleNode itemNode) {
         // ignore whether we're writing a new object to the store (item1)
         var writeMerkleNodeResult = await objectStore.TryWriteMerkleNodeAsync(NetworkDataNamespace, itemNode).ConfigureAwait(false);
         var itemHash = writeMerkleNodeResult.Item2;

         // if the tree does not contain the merkle node, insert.
         var isNodeContained = (await objectStore.TryReadAsync(TreeContainmentNamespace, itemHash).ConfigureAwait(false)).Item1;

         if (!isNodeContained) {
            // Persist pointer to object in merkle structure.
            using (await treeSync.LockAsync().ConfigureAwait(false)) {
               isNodeContained = (await objectStore.TryReadAsync(TreeContainmentNamespace, itemHash).ConfigureAwait(false)).Item1;
               if (!isNodeContained) {
                  var nextRootHash = itemHash;
                  var rootHash = await GetRootHashAsyncUnderLock().ConfigureAwait(false);
                  if (rootHash != null) {
                     var rootNode = await GetNodeAsync(rootHash).ConfigureAwait(false);
                     if (rootNode == null) throw new InvalidStateException();

                     nextRootHash = await InsertHelperAsyncUnderTreeLock(rootHash, rootNode, itemHash).ConfigureAwait(false);
                  }

                  await SetRootHashAsyncUnderLock(nextRootHash).ConfigureAwait(false);
                  await objectStore.TryWriteUniqueAsync(TreeContainmentNamespace, itemHash, new byte[0]).ConfigureAwait(false);
//                  Console.WriteLine($"LOC UPD ROOT {rootHash:n} => {nextRootHash:n}");
               }
            }
         }

         return Tuple.Create(!isNodeContained, itemHash);
      }

      private async Task<string> InsertHelperAsyncUnderTreeLock(string replaceeHash, MerkleNode replaceeNode, string inserteeHash) {
         var rightHash = replaceeNode.RightHash;
         var rightNode = await objectStore.ReadMerkleNodeAsync(NetworkDataNamespace, rightHash).ConfigureAwait(false);
         var rightwardDescendents = rightNode == null ? 0 : (1 + rightNode.Descendents);

         var isReplaceePerfect = replaceeNode.Descendents == rightwardDescendents * 2;
         if (isReplaceePerfect) {
            var internalNode = new MerkleNode {
               TypeTag = MerkleNodeTypeTag.Node,
               LeftHash = replaceeHash,
               RightHash = inserteeHash,
               Descendents = 2 + replaceeNode.Descendents
            };
            var internalNodeHash = (await objectStore.TryWriteMerkleNodeAsync(NetworkDataNamespace, internalNode).ConfigureAwait(false)).Item2;
            await objectStore.TryWriteUniqueAsync(TreeContainmentNamespace, internalNodeHash, new byte[0]).ConfigureAwait(false);
            return internalNodeHash;
         }

         var rightReplacementHash = await InsertHelperAsyncUnderTreeLock(rightHash, rightNode, inserteeHash).ConfigureAwait(false);
         replaceeNode.Descendents += 2; // leaf and inner node
         replaceeNode.RightHash = rightReplacementHash;

         var newReplaceeNodeHash = (await objectStore.TryWriteMerkleNodeAsync(NetworkDataNamespace, replaceeNode).ConfigureAwait(false)).Item2;
         await objectStore.TryWriteUniqueAsync(TreeContainmentNamespace, newReplaceeNodeHash, new byte[0]).ConfigureAwait(false);
         return newReplaceeNodeHash;
      }

      public async Task ImportAsync(string upstreamRoot, List<Tuple<string, MerkleNode>> nodesToImport) {
         foreach (var job in nodesToImport) {
            var merkleHash = job.Item1;
            var merkleNode = job.Item2;
            var insertHash = (await objectStore.TryWriteMerkleNodeAsync(NetworkDataNamespace, merkleNode).ConfigureAwait(false)).Item2;
            await objectStore.TryWriteUniqueAsync(TreeContainmentNamespace, insertHash, new byte[0]).ConfigureAwait(false);
            if (merkleHash != insertHash) {
               throw new InvalidStateException($"Hash Mismatch! {merkleHash} {insertHash}");
            }
         }
         using (await treeSync.LockAsync().ConfigureAwait(false)) {
            await SetRootHashAsyncUnderLock(upstreamRoot).ConfigureAwait(false);
         }
      }
   }
}
