using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace CampfireNet.Identities
{
	public static class TrustChainUtil
	{
		public const int UNASSIGNED_DATA_SIZE = 254;

		// data   [parent id][this id][held permissions][grantable permissions][parent sig]
		// size   [256      ][256    ][1               ][1                    ][256       ]
		// offset [0        ][256    ][512             ][513                  ][514       ]
		// total   770

		// validates the given trust chain
		public static bool ValidateTrustChain(TrustChainNode[] trustChainNodes)
		{
			if (trustChainNodes == null || trustChainNodes.Length == 0)
			{
				return false;
			}

			if (trustChainNodes.Length == 1)
			{
				return ValidateRoot(trustChainNodes[0]);
			}

			for (int i = trustChainNodes.Length - 1; i > 0; i--)
			{
				if (!ValidateRelation(trustChainNodes[i - 1], trustChainNodes[i]))
				{
					return false;
				}
			}

			return true;
		}

		// validates two nodes' relation in a trust chain
		public static bool ValidateRelation(TrustChainNode parent, TrustChainNode child)
		{
			bool correctSignatures = parent.ValidateSignature() && child.ValidateSignature();
			bool correctRelation = parent.ThisId.SequenceEqual(child.ParentId);
			bool correctPermissions = ValidatePermissions(parent.GrantablePermissions, child.HeldPermissions) &&
				ValidatePermissions(child.HeldPermissions, child.GrantablePermissions);
			bool correctDescendantSignature = CryptoUtil.Verify(child.Payload, parent.ThisId, child.ParentSignature);

			return correctSignatures && correctRelation && correctPermissions && correctDescendantSignature;
		}

		// valides the specific case of a root node's trust chain
		public static bool ValidateRoot(TrustChainNode root)
		{
			bool parentIsThis = root.ParentId.SequenceEqual(root.ThisId);
			bool sanePermissions = ValidatePermissions(root.HeldPermissions, root.GrantablePermissions);
			bool correctSignature = CryptoUtil.Verify(root.Payload, root.ThisId, root.ParentSignature);

			return parentIsThis && sanePermissions && correctSignature;
		}

		public static byte[] SerializeTrustChain(TrustChainNode[] nodes)
		{
			int numNodes = nodes?.Length ?? 0;

			byte[] final = new byte[numNodes * TrustChainNode.NODE_BLOCK_SIZE];

			for (int i = 0; i < numNodes; i++)
			{
				Buffer.BlockCopy(nodes[i].FullData, 0, final, i * TrustChainNode.NODE_BLOCK_SIZE,
								 TrustChainNode.NODE_BLOCK_SIZE);
			}

			return final;
		}

		// generates a new trust chain based off an existing one and the new parameters
		public static byte[] GenerateNewChain(TrustChainNode[] existing, byte[] parentId, byte[] childId,
											  Permission heldPermissions, Permission grantablePermissions,
											  byte[] unassignedData, RSAParameters privateKey)
		{
			using (var buffer = new MemoryStream())
			using (var writer = new BinaryWriter(buffer))
			{
				TrustChainNode newChild = CreateNode(parentId, childId, heldPermissions, grantablePermissions,
													 unassignedData, privateKey);

				foreach (var node in existing)
				{
					writer.Write(node.FullData);
				}

				writer.Write(newChild.FullData);

				return buffer.ToArray();
			}
		}

		// creates a TrustChainNode with the given parameters
		public static TrustChainNode CreateNode(byte[] parentId, byte[] childId, Permission heldPermissions,
												Permission grantablePermissions, byte[] unassignedData,
												RSAParameters privateKey)
		{
			return new TrustChainNode(parentId, childId, heldPermissions, grantablePermissions, unassignedData,
									  privateKey);
		}

		public static bool ValidatePermissions(Permission superset, Permission subset)
		{
			return (superset | subset) == superset;
		}

		// static helper method to segment trust chain into an array of nodes
		public static TrustChainNode[] SegmentChain(byte[] data)
		{
			if (data == null || data.Length % TrustChainNode.NODE_BLOCK_SIZE != 0 || data.Length == 0)
			{
				throw new CryptographicException($"Data size is not multiple of block size ({data.Length} % {TrustChainNode.NODE_BLOCK_SIZE} != 0)");
			}

			int numNodes = data.Length / TrustChainNode.NODE_BLOCK_SIZE;
			TrustChainNode[] trustChain = new TrustChainNode[numNodes];

			for (int i = 0; i < numNodes; i++)
			{
				byte[] buffer = new byte[TrustChainNode.NODE_BLOCK_SIZE];
				Buffer.BlockCopy(data, i * TrustChainNode.NODE_BLOCK_SIZE, buffer, 0, TrustChainNode.NODE_BLOCK_SIZE);
				trustChain[i] = new TrustChainNode(buffer);
			}

			return trustChain;
		}

		public static string TrustChainToString(TrustChainNode[] nodes)
		{
			string ret = $"----BEGIN CHAIN OF TRUST----\nLENGTH {nodes.Length}";

			for (int i = 0; i < nodes.Length; i++)
			{
				ret += "\n----------------\n";
				ret += nodes[i].ToString();
			}

			ret += "\n----END CHAIN OF TRUST----";

			return ret;
		}

		public static void SaveTrustChainToFile(TrustChainNode[] trustChain, string filename)
		{
			var data = SerializeTrustChain(trustChain);

			File.WriteAllBytes(filename, data);
		}
	}

	// represents a node in a trust chain
	public class TrustChainNode
	{
		public const int PARENT_ID_OFFSET = 0;
		public const int THIS_ID_OFFSET = 256;
		public const int HELD_PERMISSIONS_OFFSET = 512;
		public const int GRANTABLE_PERMISSIONS_OFFSET = 513;
		public const int UNASSIGNED_DATA_OFFSET = 514;
		public const int PARENT_SIGNATURE_OFFSET = 768;
		public const int NODE_BLOCK_SIZE = CryptoUtil.ASYM_KEY_SIZE_BYTES * 2 + sizeof(Permission) * 2 +
													 TrustChainUtil.UNASSIGNED_DATA_SIZE + CryptoUtil.SIGNATURE_SIZE;

		public byte[] ParentId { get; }
		public byte[] ThisId { get; }
		public Permission HeldPermissions { get; }
		public Permission GrantablePermissions { get; }
		public byte[] UnassignedData { get; }
		public byte[] ParentSignature { get; }

		public string Name
		{
			get
			{
				return Encoding.UTF8.GetString(UnassignedData, 1, UnassignedData[0]);
			}
		}

		// everything but the signature (everything that was signed)
		public byte[] Payload { get; }

		public byte[] FullData { get; }

		// initializes node from raw byte data
		public TrustChainNode(byte[] rawData)
		{
			if (rawData.Length != NODE_BLOCK_SIZE)
			{
				throw new ArgumentException($"Node data is the wrong size (not {NODE_BLOCK_SIZE} bytes)");
			}

			// initialize arrays
			ParentId = new byte[CryptoUtil.ASYM_KEY_SIZE_BYTES];
			ThisId = new byte[CryptoUtil.ASYM_KEY_SIZE_BYTES];
			UnassignedData = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
			ParentSignature = new byte[CryptoUtil.SIGNATURE_SIZE];
			Payload = new byte[PARENT_SIGNATURE_OFFSET];
			FullData = new byte[NODE_BLOCK_SIZE];

			// populate single fields
			Buffer.BlockCopy(rawData, PARENT_ID_OFFSET, ParentId, 0, CryptoUtil.ASYM_KEY_SIZE_BYTES);
			Buffer.BlockCopy(rawData, THIS_ID_OFFSET, ThisId, 0, CryptoUtil.ASYM_KEY_SIZE_BYTES);
			HeldPermissions = (Permission)rawData[HELD_PERMISSIONS_OFFSET];
			GrantablePermissions = (Permission)rawData[GRANTABLE_PERMISSIONS_OFFSET];
			Buffer.BlockCopy(rawData, UNASSIGNED_DATA_OFFSET, UnassignedData, 0, TrustChainUtil.UNASSIGNED_DATA_SIZE);
			Buffer.BlockCopy(rawData, PARENT_SIGNATURE_OFFSET, ParentSignature, 0, CryptoUtil.SIGNATURE_SIZE);

			// populate combined fields
			Buffer.BlockCopy(rawData, 0, Payload, 0, PARENT_SIGNATURE_OFFSET);
			Buffer.BlockCopy(rawData, 0, FullData, 0, NODE_BLOCK_SIZE);
		}

		// initializes node from given parameters
		public TrustChainNode(byte[] parentId, byte[] thisId, Permission heldPermissions,
							  Permission grantablePermissions, byte[] unassignedData, RSAParameters privateKey)
		{
			// initialize arrays
			ParentId = new byte[CryptoUtil.ASYM_KEY_SIZE_BYTES];
			ThisId = new byte[CryptoUtil.ASYM_KEY_SIZE_BYTES];
			UnassignedData = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
			ParentSignature = new byte[CryptoUtil.SIGNATURE_SIZE];
			Payload = new byte[PARENT_SIGNATURE_OFFSET];
			FullData = new byte[NODE_BLOCK_SIZE];

			// populate single fields
			Buffer.BlockCopy(parentId, 0, ParentId, 0, CryptoUtil.ASYM_KEY_SIZE_BYTES);
			Buffer.BlockCopy(thisId, 0, ThisId, 0, CryptoUtil.ASYM_KEY_SIZE_BYTES);
			HeldPermissions = heldPermissions;
			GrantablePermissions = grantablePermissions;
			Buffer.BlockCopy(unassignedData, 0, UnassignedData, 0, TrustChainUtil.UNASSIGNED_DATA_SIZE);

			// populate payload
			Buffer.BlockCopy(parentId, 0, Payload, PARENT_ID_OFFSET, CryptoUtil.ASYM_KEY_SIZE_BYTES);
			Buffer.BlockCopy(thisId, 0, Payload, THIS_ID_OFFSET, CryptoUtil.ASYM_KEY_SIZE_BYTES);
			Payload[HELD_PERMISSIONS_OFFSET] = (byte)heldPermissions;
			Payload[GRANTABLE_PERMISSIONS_OFFSET] = (byte)grantablePermissions;
			Buffer.BlockCopy(unassignedData, 0, Payload, UNASSIGNED_DATA_OFFSET, TrustChainUtil.UNASSIGNED_DATA_SIZE);

			// populate full data and signature
			byte[] payloadSignature = CryptoUtil.Sign(Payload, privateKey);
			Buffer.BlockCopy(payloadSignature, 0, ParentSignature, 0, CryptoUtil.SIGNATURE_SIZE);
			Buffer.BlockCopy(Payload, 0, FullData, 0, PARENT_SIGNATURE_OFFSET);
			Buffer.BlockCopy(payloadSignature, 0, FullData, PARENT_SIGNATURE_OFFSET, CryptoUtil.SIGNATURE_SIZE);
		}

		// validates this node's signature
		public bool ValidateSignature()
		{
			return CryptoUtil.Verify(Payload, ParentId, ParentSignature);
		}

		public override string ToString()
		{
			string ret = "" +
				$"Name: {Encoding.UTF8.GetString(UnassignedData, 1, UnassignedData[0])}\n" +
				$"Parent ID ({ParentId.Length}): { ToHex(ParentId)}\n" +
				$"This ID   ({ThisId.Length}): {ToHex(ThisId)}\n" +
				$"Held permissions: {HeldPermissions}\n" +
				$"Grantable permissions: {GrantablePermissions}\n" +
				$"Signature ({ParentSignature.Length}): {ToHex(ParentSignature)}";

			return ret;
		}

		private static string ToHex(byte[] data)
		{
			return BitConverter.ToString(data).Replace("-", ":");
		}
	}

	// thrown when an invalid trust chain is encountered
	public class BadTrustChainException : Exception
	{
		public BadTrustChainException() : base() { }
		public BadTrustChainException(string message) : base(message) { }
		public BadTrustChainException(string message, Exception inner) : base(message, inner) { }
	}
}
