using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using CampfireNet.Utilities;

namespace CampfireNet.Identities
{
	public class IdentityManager
	{
      // keys are strings of public keys, formatted as:
      //     ItentityService.getIdentityString(publicKey);
	   private readonly ConcurrentDictionary<string, TrustChainNode> userIdentityTable = new ConcurrentDictionary<string, TrustChainNode>();

	   private readonly ConcurrentDictionary<IdentityHash, byte[]> multicastEncryptionKeysByIdentity = new ConcurrentDictionary<IdentityHash, byte[]>();

		// returns the canonical identity string of a public key
		public static string GetIdentityString(byte[] publicKey)
		{
			if (publicKey.Length == CryptoUtil.HASH_SIZE)
			{
				return BitConverter.ToString(publicKey).Replace("-", "");
			}
			else if (publicKey.Length == CryptoUtil.ASYM_KEY_SIZE_BYTES)
			{
				return BitConverter.ToString(CryptoUtil.GetHash(publicKey)).Replace("-", "");
			}
			else
			{
				throw new CryptographicException("Invalid key size");
			}
		}

		// adds the given identity to the known nodes, and returns true if the node was not already trusted
		// TODO remove name crap
		public bool AddIdentity(TrustChainNode identity, string name = "")
		{
			bool successAdded = userIdentityTable.TryAdd(GetIdentityString(identity.ThisId), identity);
			string existsString = successAdded ? "not found" : "found";
			Console.WriteLine($"{name} adding {identity.Name}: identity {existsString}");
			return successAdded;
		}

		public void AddIdentities(IEnumerable<TrustChainNode> identities)
		{
			foreach (var identity in identities)
			{
				AddIdentity(identity);
			}
		}

		// looks up and returns the identity of the public key if it exists in the dict, else returns null
		public TrustChainNode LookupIdentity(string publicKeyHash)
		{
			TrustChainNode identity;
			if (userIdentityTable.TryGetValue(publicKeyHash, out identity))
			{
				return identity;
			}
			return null;
		}

		public TrustChainNode LookupIdentity(byte[] publicKey)
		{
			return LookupIdentity(GetIdentityString(publicKey));
		}

      public bool IsKnownIdentity(byte[] idOrIdHash) {
         return LookupIdentity(GetIdentityString(idOrIdHash)) != null;
      }

	   public void AddMulticastKey(IdentityHash identityHash, byte[] symmetricKey) {
	      multicastEncryptionKeysByIdentity[identityHash] = symmetricKey;
	   }

      public bool TryLookupMulticastKey(IdentityHash identityHash, out byte[] symmetricKey) {
         return multicastEncryptionKeysByIdentity.TryGetValue(identityHash, out symmetricKey);
      }
   }
}
