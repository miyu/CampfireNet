using System;
using System.Linq;
using System.Security.Cryptography;

namespace IdentityService
{
	class Identity
	{
		public const int ASYM_KEY_SIZE_BITS = 2048;
		public const int ASYM_KEY_SIZE_BYTES = ASYM_KEY_SIZE_BITS / 8;
		public const int SYM_KEY_SIZE = 32;
		public const int IV_SIZE = 16;
		public const int SALT_SIZE = 64;

		public TrustChainNode[] TrustChain { get; private set; }
		public Permission HeldPermissions { get; private set; }
		public Permission GrantablePermissions { get; private set; }

		public byte[] PublicIdentity
		{
			get
			{
				return privateKey.Modulus;
			}
		}

		private RSAParameters privateKey;
		private IdentityManager identityManager;

		public Identity(IdentityManager identityManager)
		{
			// generate new public and private keys
			var rsa = new RSACryptoServiceProvider(ASYM_KEY_SIZE_BITS);
			privateKey = rsa.ExportParameters(true);

			this.identityManager = identityManager;

			HeldPermissions = Permission.None;
			GrantablePermissions = Permission.None;
			TrustChain = null;
		}

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
			}
			else
			{
				throw new BadTrustChainException("Could not validate trust chain ending with this");
			}
		}

		// generates a new trust chain with this as the root node
		public void GenerateRootChain()
		{
			byte[] rootChain = TrustChainUtil.GenerateNewChain(null, PublicIdentity, PublicIdentity, Permission.All,
															   Permission.All, privateKey);
			HeldPermissions = Permission.All;
			GrantablePermissions = Permission.All;
			AddTrustChain(rootChain);
		}

		// generates a trust chain to pass to another client
		public byte[] GenerateNewChain(byte[] childId, Permission heldPermissions, Permission grantablePermissions)
		{
			bool canGrant = CanGrantPermissions(heldPermissions, grantablePermissions);

			if (canGrant)
			{
				return TrustChainUtil.GenerateNewChain(TrustChain, PublicIdentity, childId, heldPermissions,
													   grantablePermissions, privateKey);
			}
			else
			{
				throw new InvalidPermissionException($"Insufficient authorization to grant permissions");
			}
		}

		// validates the given trust chain and adds the nodes to the list of known nodes, or returns false
		public bool ValidateAndAdd(byte[] trustChain)
		{
			if (TrustChain == null || TrustChain.Length == 0)
			{
				throw new BadTrustChainException("This trust chain is empty, can't validate others");
			}

			TrustChainNode[] trustChainNodes = TrustChainUtil.SegmentChain(trustChain);

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
				if (!identityManager.AddIdentity(trustChainNodes[i]))
				{
					break;
				}
			}

			return true;
		}


		public byte[] AsymmetricEncrypt(byte[] data, RSAParameters privateKey, bool doOAEPPadding)
		{
			try
			{
				byte[] encryptedData;
				using (var rsa = new RSACryptoServiceProvider())
				{
					rsa.ImportParameters(privateKey);
					encryptedData = rsa.Encrypt(data, doOAEPPadding);
					return encryptedData;
				}
			}
			catch (CryptographicException e)
			{
				Console.WriteLine(e.Message);
				throw e;
			}
		}

		public byte[] AsymmetricDecrypt(byte[] data, bool doOAEPPadding)
		{
			try
			{
				byte[] decryptedData;
				using (var rsa = new RSACryptoServiceProvider())
				{
					rsa.ImportParameters(privateKey);
					decryptedData = rsa.Decrypt(data, doOAEPPadding);
					return decryptedData;
				}
			}
			catch (CryptographicException e)
			{
				Console.WriteLine(e.ToString());
				throw e;
			}
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
