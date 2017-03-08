using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using CampfireNet.Utilities;

namespace CampfireNet.Identities {
   public class Identity {
      public readonly static byte[] BROADCAST_ID = new byte[CryptoUtil.HASH_SIZE];

      private RSAParameters privateKey;
      private byte[] identityHash;
      private IdentityManager identityManager;

      public Identity(IdentityManager identityManager, string name) : this(new RSACryptoServiceProvider(CryptoUtil.ASYM_KEY_SIZE_BITS), identityManager, name) { }

      public Identity(RSACryptoServiceProvider rsa, IdentityManager identityManager, string name) {
         privateKey = rsa.ExportParameters(true);
         identityHash = CryptoUtil.GetHash(privateKey.Modulus);

         this.identityManager = identityManager;

         TrustChain = null;
         PermissionsHeld = Permission.None;
         PermissionsGrantable = Permission.None;
         Name = name;
      }

      public Identity(IdentityManager identityManager, RSAParameters privateKey, string name) {
         this.privateKey = privateKey;
         identityHash = CryptoUtil.GetHash(privateKey.Modulus);

         this.identityManager = identityManager;

         TrustChain = null;
         PermissionsHeld = Permission.None;
         PermissionsGrantable = Permission.None;
         Name = name;
      }

      public TrustChainNode[] TrustChain { get; private set; }

      public Permission PermissionsHeld { get; private set; }
      public Permission PermissionsGrantable { get; private set; }

      public string Name { get; set; }

      public byte[] PublicIdentity => privateKey.Modulus;
      public byte[] PublicIdentityHash => identityHash;
      // TODO remove
      public RSAParameters PrivateKeyDebug => privateKey;
      public IdentityManager IdentityManager => identityManager;

      // gives this identity a trust chain to use
      public void AddTrustChain(byte[] trustChain) {
         TrustChainNode[] nodes = TrustChainUtil.SegmentChain(trustChain);
         bool isValid = TrustChainUtil.ValidateTrustChain(nodes);
         bool endsWithThis = nodes[nodes.Length - 1].ThisId.SequenceEqual(PublicIdentity);

         if (isValid && endsWithThis) {
            TrustChain = nodes;
            PermissionsHeld = nodes[nodes.Length - 1].HeldPermissions;
            PermissionsGrantable = nodes[nodes.Length - 1].GrantablePermissions;

            identityManager.AddIdentities(nodes);
         } else {
            throw new BadTrustChainException("Could not validate trust chain ending with this");
         }
      }

      // generates a new trust chain with this as the root node
      public void GenerateRootChain() {
         if (Name.Length > TrustChainUtil.UNASSIGNED_DATA_SIZE - 1) {
            throw new ArgumentException("Name too long");
         }

         var nameBytes = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
         using (var ms = new MemoryStream(nameBytes))
         using (var writer = new BinaryWriter(ms)) {
            writer.Write((byte)Name.Length);
            writer.Write(Encoding.UTF8.GetBytes(Name));
         }

         byte[] rootChain = TrustChainUtil.GenerateNewChain(new TrustChainNode[0], PublicIdentity, PublicIdentity, Permission.All,
                                       Permission.All, nameBytes, privateKey);
         PermissionsHeld = Permission.All;
         PermissionsGrantable = Permission.All;
         AddTrustChain(rootChain);
      }

      // generates a trust chain to pass to another client
      public byte[] GenerateNewChain(byte[] childId, Permission heldPermissions, Permission grantablePermissions,
                        string name) {
         bool canGrant = CanGrantPermissions(heldPermissions, grantablePermissions);

         if (canGrant) {
            if (name.Length > TrustChainUtil.UNASSIGNED_DATA_SIZE - 1) {
               throw new ArgumentException("Name too long");

            }
            byte[] nameBytes = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
            nameBytes[0] = (byte)name.Length;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(name), 0, nameBytes, 1, name.Length);

            return TrustChainUtil.GenerateNewChain(TrustChain, PublicIdentity, childId, heldPermissions,
                                   grantablePermissions, nameBytes, privateKey);
         } else {
            throw new InvalidPermissionException($"Insufficient authorization to grant permissions");
         }
      }

      // validates the given trust chain and adds the nodes to the list of known nodes, or returns false
      public bool ValidateAndAdd(byte[] trustChain) {
         TrustChainNode[] trustChainNodes = TrustChainUtil.SegmentChain(trustChain);
         return ValidateAndAdd(trustChainNodes);
      }

      public bool ValidateAndAdd(TrustChainNode[] trustChainNodes) {
         bool validChain = TrustChainUtil.ValidateTrustChain(trustChainNodes);
         if (!validChain) {
            return false;
         }

         bool sameRoot = TrustChain[0].ParentId.SequenceEqual(trustChainNodes[0].ParentId);
         if (!sameRoot) {
            return false;
         }

         for (int i = 0; i < trustChainNodes.Length; i++) {
            identityManager.AddIdentity(trustChainNodes[i], Name);
         }

         return true;
      }

      // unicast/broadcast
      // () asymmetric encrypt
      // <[sender hash][recipient hash]([sender hash][message])[signature]>
      //  [32         ][32            ] [32         ][msg len] [256      ]
      // 
      // multicast
      // () symmetric encrypt
      // <[sender hash][recipient hash][IV](<[message][signature]>)[signature]>
      public BroadcastMessageDto EncodePacket(byte[] message, byte[] remoteKey = null) {
         if (remoteKey != null && remoteKey.Length != CryptoUtil.ASYM_KEY_SIZE_BYTES && remoteKey.Length != CryptoUtil.SYM_KEY_SIZE) {
            throw new CryptographicException("Bad key size");
         }

         byte[] senderHash = CryptoUtil.GetHash(privateKey.Modulus);
         byte[] recipientHash = CryptoUtil.GetHash(remoteKey); ;
         byte[] processedMessage;

         if (remoteKey == null) {
            // broadcast
            recipientHash = BROADCAST_ID;
            processedMessage = message;
         } else if (remoteKey.Length == CryptoUtil.ASYM_KEY_SIZE_BYTES) {
            // unicast
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms)) {
               writer.Write(senderHash);
               writer.Write(message);
               processedMessage = ms.ToArray();
            }

            processedMessage = CryptoUtil.AsymmetricEncrypt(processedMessage, remoteKey);
         } else {
            // multicast
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms)) {
               writer.Write(message);
               writer.Write(CryptoUtil.Sign(message, privateKey));
               processedMessage = ms.ToArray();
            }

            byte[] iv = CryptoUtil.GenerateIV();
            processedMessage = CryptoUtil.SymmetricEncrypt(processedMessage, remoteKey, iv);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms)) {
               writer.Write(iv);
               writer.Write(processedMessage);
               processedMessage = ms.ToArray();
            }
         }

         byte[] payload;
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms)) {
            writer.Write(senderHash);
            writer.Write(recipientHash);
            writer.Write(processedMessage);
            payload = ms.ToArray();
         }

         byte[] signature = CryptoUtil.Sign(payload, privateKey);

         return new BroadcastMessageDto {
            SourceIdHash = senderHash,
            DestinationIdHash = recipientHash,
            Payload = processedMessage,
            Signature = signature
         };
      }

      public bool TryDecodePayload(BroadcastMessageDto broadcastMessage, out byte[] decryptedPayload) {
         var senderHash = broadcastMessage.SourceIdHash;
         var receiverHash = broadcastMessage.DestinationIdHash;
         var payload = broadcastMessage.Payload;
         var signature = broadcastMessage.Signature;

         byte[] totalMessage;
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms)) {
            writer.Write(senderHash);
            writer.Write(receiverHash);
            writer.Write(payload);
            totalMessage = ms.ToArray();
         }

         var senderNode = identityManager.LookupIdentity(senderHash);
         if (senderNode == null) {
            //            throw new InvalidStateException("Sender has not recognized");
            decryptedPayload = null;
            return false;
         }

         if (!CryptoUtil.Verify(totalMessage, senderNode.ThisId, signature)) {
            //            throw new CryptographicException("Could not verify message");
            decryptedPayload = null;
            return false;
         }

         // message is now verified

         byte[] symmetricKey;
         if (receiverHash.SequenceEqual(BROADCAST_ID)) {
            // broadcast
            decryptedPayload = payload;
            return true;
         } else if (receiverHash.SequenceEqual(identityHash)) {
            // unicast to us
            var decryptedSenderAndMessage = CryptoUtil.AsymmetricDecrypt(payload, privateKey);
            if (!decryptedSenderAndMessage.Take(CryptoUtil.HASH_SIZE).SequenceEqual(senderHash)) {
               // BREACH BREACH BREACH DEPLOY SECURITY COUNTER MEASURES
               //               throw new CryptographicException("DATA WAS BAD THERE ARE BAD PEOPLE HERE THEY MUST BE KEPT OUT");
               decryptedPayload = null;
               return false;
            }
            decryptedPayload = decryptedSenderAndMessage.Skip(CryptoUtil.HASH_SIZE).ToArray();
            return true;
         } else if (identityManager.TryLookupMulticastKey(IdentityHash.GetFlyweight(receiverHash), out symmetricKey)) {
            // multicast
            var iv = payload.Take(CryptoUtil.IV_SIZE).ToArray();
            var encryptedMessage = payload.Skip(CryptoUtil.IV_SIZE).ToArray();
            var messageAndInnerSignature = CryptoUtil.SymmetricDecrypt(encryptedMessage, symmetricKey, iv);
            var innerSignature = messageAndInnerSignature.Skip(messageAndInnerSignature.Length - CryptoUtil.ASYM_KEY_SIZE_BYTES).ToArray();
            var message = messageAndInnerSignature.Take(messageAndInnerSignature.Length - CryptoUtil.ASYM_KEY_SIZE_BYTES).ToArray();

            if (!CryptoUtil.Verify(message, senderNode.ThisId, innerSignature)) {
               //               throw new CryptographicException("Could not verify inner signature");
               decryptedPayload = null;
               return false;
            }

            decryptedPayload = message;
            return true;
         } else {
            // unknown multi/unicast
            decryptedPayload = null;
            return false;
         }
      }

      public void SaveKey(string path) {
         byte[] data = CryptoUtil.SerializeKey(privateKey);
         File.WriteAllBytes(path, data);
      }

      // whether the given permissions can be granted by this node
      public bool CanGrantPermissions(Permission heldPermissions, Permission grantablePermissions) {
         return PermissionsHeld.HasFlag(Permission.Invite) &&
              TrustChainUtil.ValidatePermissions(PermissionsGrantable, heldPermissions) &&
              TrustChainUtil.ValidatePermissions(heldPermissions, grantablePermissions);
      }
   }
}
