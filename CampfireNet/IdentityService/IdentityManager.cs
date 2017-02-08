using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IdentityService
{
	class IdentityManager
	{
		// keys are strings of public keys, formatted as:
		//     ItentityService.getIdentityString(publicKey);
		private ConcurrentDictionary<string, TrustChainNode> identityTable;

		public IdentityManager()
		{
			identityTable = new ConcurrentDictionary<string, TrustChainNode>();
		}

		public static string GetIdentityString(byte[] publicKey)
		{
			return BitConverter.ToString(publicKey).Replace("-", "");
		}

		public bool AddIdentity(TrustChainNode identity)
		{
			return identityTable.TryAdd(GetIdentityString(identity.ThisId), identity);
		}

		public TrustChainNode LookupIdentity(string publicKey)
		{
			TrustChainNode identity;
			if (identityTable.TryGetValue(publicKey, out identity))
			{
				return identity;
			}
			return null;
		}
	}
}
