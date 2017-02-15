using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace CampfireNet.Identities
{
	public class IdentityManager
	{
		// keys are strings of public keys, formatted as:
		//     ItentityService.getIdentityString(publicKey);
		private ConcurrentDictionary<string, TrustChainNode> identityTable;

		public IdentityManager()
		{
			identityTable = new ConcurrentDictionary<string, TrustChainNode>();
		}

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
			bool successAdded = identityTable.TryAdd(GetIdentityString(identity.ThisId), identity);
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
			if (identityTable.TryGetValue(publicKeyHash, out identity))
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
   }
}
