using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CampfireNet.Utilities;

namespace CampfireNet.Identities
{
	public class Identity
	{
		public readonly static byte[] BROADCAST_ID = new byte[CryptoUtil.HASH_SIZE];

		private RSAParameters privateKey;
		private byte[] identityHash;
		private IdentityManager identityManager;

		public Identity(IdentityManager identityManager, string name)
		{
			// generate new public and private keys
			var rsa = new RSACryptoServiceProvider(CryptoUtil.ASYM_KEY_SIZE_BITS);
			privateKey = rsa.ExportParameters(true);
			identityHash = CryptoUtil.GetHash(privateKey.Modulus);

			this.identityManager = identityManager;

			TrustChain = null;
			HeldPermissions = Permission.None;
			GrantablePermissions = Permission.None;
			Name = name;
		}

		public TrustChainNode[] TrustChain { get; private set; }
		public Permission HeldPermissions { get; private set; }
		public Permission GrantablePermissions { get; private set; }

		public string Name { get; set; }

		public byte[] PublicIdentity => privateKey.Modulus;
		public byte[] PublicIdentityHash => identityHash;
		// TODO remove
		public RSAParameters privateKeyDebug => privateKey;
		public IdentityManager IdentityManager => identityManager;

		// gives this identity a trust chain to use
		public void AddTrustChain(byte[] trustChain)
		{
			TrustChainNode[] nodes = TrustChainUtil.SegmentChain(trustChain);
			bool isValid = TrustChainUtil.ValidateTrustChain(nodes);
			bool endsWithThis = nodes[nodes.Length - 1].ThisId.SequenceEqual(PublicIdentity);

			if (isValid && endsWithThis)
			{
				TrustChain = nodes;
				HeldPermissions = nodes[nodes.Length - 1].HeldPermissions;
				GrantablePermissions = nodes[nodes.Length - 1].GrantablePermissions;

				identityManager.AddIdentities(nodes);
			}
			else
			{
				throw new BadTrustChainException("Could not validate trust chain ending with this");
			}
		}

		// generates a new trust chain with this as the root node
		public void GenerateRootChain()
		{
			if (Name.Length > TrustChainUtil.UNASSIGNED_DATA_SIZE - 1)
			{
				throw new ArgumentException("Name too long");
			}

			var nameBytes = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
			using (var ms = new MemoryStream(nameBytes))
			using (var writer = new BinaryWriter(ms))
			{
				writer.Write((byte)Name.Length);
				writer.Write(Encoding.UTF8.GetBytes(Name));
			}

			byte[] rootChain = TrustChainUtil.GenerateNewChain(new TrustChainNode[0], PublicIdentity, PublicIdentity, Permission.All,
															   Permission.All, nameBytes, privateKey);
			HeldPermissions = Permission.All;
			GrantablePermissions = Permission.All;
			AddTrustChain(rootChain);
		}

		// generates a trust chain to pass to another client
		public byte[] GenerateNewChain(byte[] childId, Permission heldPermissions, Permission grantablePermissions,
									   string name)
		{
			bool canGrant = CanGrantPermissions(heldPermissions, grantablePermissions);

			if (canGrant)
			{
				if (name.Length > TrustChainUtil.UNASSIGNED_DATA_SIZE - 1)
				{
					throw new ArgumentException("Name too long");

				}
				byte[] nameBytes = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
				nameBytes[0] = (byte)name.Length;
				Buffer.BlockCopy(Encoding.UTF8.GetBytes(name), 0, nameBytes, 1, name.Length);

				return TrustChainUtil.GenerateNewChain(TrustChain, PublicIdentity, childId, heldPermissions,
													   grantablePermissions, nameBytes, privateKey);
			}
			else
			{
				throw new InvalidPermissionException($"Insufficient authorization to grant permissions");
			}
		}

		// validates the given trust chain and adds the nodes to the list of known nodes, or returns false
		public bool ValidateAndAdd(byte[] trustChain)
		{
			TrustChainNode[] trustChainNodes = TrustChainUtil.SegmentChain(trustChain);
			return ValidateAndAdd(trustChainNodes);
		}

		public bool ValidateAndAdd(TrustChainNode[] trustChainNodes)
		{
			bool validChain = TrustChainUtil.ValidateTrustChain(trustChainNodes);
			if (!validChain)
			{
				return false;
			}

			bool sameRoot = TrustChain[0].ParentId.SequenceEqual(trustChainNodes[0].ParentId);
			if (!sameRoot)
			{
				return false;
			}

			for (int i = trustChainNodes.Length - 1; i >= 0; i--)
			{
				if (!identityManager.AddIdentity(trustChainNodes[i], Name))
				{
					break;
				}
			}

			return true;
		}

		// () asymmetric encrypt
		// <[sender hash][recipient hash]([message])[signature]>
		//  [32         ][32            ] [msg len] [256      ]
		public BroadcastMessageDto EncodePacket(byte[] message, byte[] remoteModulus = null)
		{
			if (remoteModulus != null && remoteModulus.Length != CryptoUtil.ASYM_KEY_SIZE_BYTES)
			{
				throw new CryptographicException("Bad key size");
			}

			byte[] senderHash = CryptoUtil.GetHash(privateKey.Modulus);
			byte[] recipientHash;
			byte[] processedMessage;

			if (remoteModulus == null)
			{
				recipientHash = BROADCAST_ID;
				processedMessage = message;
			}
			else
			{
				recipientHash = CryptoUtil.GetHash(remoteModulus);
				processedMessage = CryptoUtil.AsymmetricEncrypt(message, remoteModulus);
			}

			byte[] payload = new byte[2 * CryptoUtil.HASH_SIZE + processedMessage.Length];
			Buffer.BlockCopy(senderHash, 0, payload, 0, CryptoUtil.HASH_SIZE);
			Buffer.BlockCopy(recipientHash, 0, payload, CryptoUtil.HASH_SIZE, CryptoUtil.HASH_SIZE);
			Buffer.BlockCopy(processedMessage, 0, payload, 2 * CryptoUtil.HASH_SIZE, processedMessage.Length);

			byte[] signature = CryptoUtil.Sign(payload, privateKey);

			//byte[] finalPacket = new byte[payload.Length + CryptoUtil.SIGNATURE_SIZE];
			//Buffer.BlockCopy(payload, 0, finalPacket, 0, payload.Length);
			//Buffer.BlockCopy(signature, 0, finalPacket, payload.Length, CryptoUtil.SIGNATURE_SIZE);

			return new BroadcastMessageDto { 
				SourceId = senderHash,
				DestinationId = recipientHash,
				Payload = processedMessage,
				Signature = signature
			};
		}

		public bool IsSenderKnown(BroadcastMessageDto broadcastMessage) {
			return identityManager.LookupIdentity(broadcastMessage.SourceId) != null;
		}

		public bool TryDecodePayload(BroadcastMessageDto broadcastMessage, out byte[] decryptedPayload)
		{
			var senderHash = broadcastMessage.SourceId;
			var receiverHash = broadcastMessage.DestinationId;
			var payload = broadcastMessage.Payload;
			var signature = broadcastMessage.Signature;

			bool unicast = receiverHash.SequenceEqual(identityHash);
			bool broadcast = receiverHash.SequenceEqual(BROADCAST_ID);

			if (!unicast && !broadcast) {
				decryptedPayload = null;
				return false;
			}

			byte[] modulus;
			TrustChainNode senderNode = identityManager.LookupIdentity(senderHash);
			if (senderNode == null)
			{
				throw new InvalidStateException("Sender hash not recognized");
			}
			else
			{
				modulus = senderNode.ThisId;
			}

			if (!CryptoUtil.Verify(broadcastMessage, modulus, signature))
			{
				throw new CryptographicException("Could not verify message");
			}

			decryptedPayload = broadcast ? payload : CryptoUtil.AsymmetricDecrypt(payload, privateKey);
			return true;
		}

		// whether the given permissions can be granted by this node
		public bool CanGrantPermissions(Permission heldPermissions, Permission grantablePermissions)
		{
			return HeldPermissions.HasFlag(Permission.Invite) &&
				   TrustChainUtil.ValidatePermissions(GrantablePermissions, heldPermissions) &&
				   TrustChainUtil.ValidatePermissions(heldPermissions, grantablePermissions);
		}
	}
}
