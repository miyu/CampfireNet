using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using IdentityService;
using System.IO;

namespace CampfireNet.Simulator
{
	class IdentityService
	{
		static void Main()
		{
			//IdentityManager manager = new IdentityManager();

			//Identity root = CreateRoot();
			//manager.AddIdentity(root.publicKey, root);

			//Identity i2 = root.CreateUser(Permission.All, Permission.All);
			//manager.AddIdentity(i2.publicKey, i2);

			//// 46 66...66 46
			//byte[] message = EncodeMessage(i2, root.publicKey, "FffffffffffffffffF");
			//Console.WriteLine();
			//DecodeMessage(root, message, manager);

			IdentityManager manager = new IdentityManager();
			Identity root = new Identity(manager);
			root.GenerateRootChain();

			Identity sub1 = new Identity(new IdentityManager());
			sub1.AddTrustChain(root.GenerateNewChain(sub1.PublicIdentity, Permission.Invite | Permission.Broadcast, Permission.Invite));


			Console.WriteLine(TrustChainUtil.TrustChainToString(sub1.TrustChain));

			bool y = root.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(sub1.TrustChain));
			Console.WriteLine($"validated chain {y}");
		}

		/*

		static Identity CreateRoot()
		{
			return new Identity(Permission.All, Permission.All, null);
		}

		static byte[] EncodeMessage(Identity identity, string publicKey, string message)
		{
			if (message != null)
			{
				byte[] messageToEncrypt = Encoding.UTF8.GetBytes(message);
				Console.WriteLine($"Message is (len {messageToEncrypt.Length}): {BitConverter.ToString(messageToEncrypt)}");

				byte[] signature = identity.Sign(messageToEncrypt);
				Console.WriteLine($"Signature is (len {signature.Length}):\n{BitConverter.ToString(signature)}");

				byte[] macAndMessage = new byte[signature.Length + messageToEncrypt.Length];
				Buffer.BlockCopy(signature, 0, macAndMessage, 0, signature.Length);
				Buffer.BlockCopy(messageToEncrypt, 0, macAndMessage, signature.Length, messageToEncrypt.Length);
				Console.WriteLine($"Unencrypted mac and message is (len {macAndMessage.Length}):\n{BitConverter.ToString(macAndMessage)}");

				byte[] key = identity.GenerateKey(); // symmetric encryption
				byte[] IV = identity.GenerateIV();
				byte[] encryptedMessage = identity.SymmetricEncrypt(macAndMessage, key, IV);
				Console.WriteLine($"Symmetric encrypted mac+msg (len {encryptedMessage.Length}):\n{BitConverter.ToString(encryptedMessage)}");

				byte[] macAndMessageSignature = identity.Sign(encryptedMessage);
				byte[] symKey = new byte[key.Length + IV.Length];
				Buffer.BlockCopy(key, 0, symKey, 0, key.Length);
				Buffer.BlockCopy(IV, 0, symKey, key.Length, IV.Length);

				byte[] encryptedSymKey = identity.Encrypt(symKey, publicKey, false);

				byte[] remotePublicKey = Encoding.UTF8.GetBytes(identity.publicKey);
				byte[] messageToSend = new byte[remotePublicKey.Length + encryptedSymKey.Length + macAndMessageSignature.Length + encryptedMessage.Length];
				Buffer.BlockCopy(remotePublicKey, 0, messageToSend, 0, remotePublicKey.Length);
				Buffer.BlockCopy(encryptedSymKey, 0, messageToSend, remotePublicKey.Length, encryptedSymKey.Length);
				Buffer.BlockCopy(macAndMessageSignature, 0, messageToSend, remotePublicKey.Length + encryptedSymKey.Length, macAndMessageSignature.Length);
				Buffer.BlockCopy(encryptedMessage, 0, messageToSend, remotePublicKey.Length + encryptedSymKey.Length + macAndMessageSignature.Length, encryptedMessage.Length);

				return messageToSend;
			}
			return null;
		}

		// [remote public key][encrypted symmetric key][encrypted message+mac signature][encrypted message+mac]
		static string DecodeMessage(Identity identity, byte[] message, IdentityManager manager)
		{
			if (message.Length != 0)
			{
				byte[] remotePublicKey = new byte[TrustChainUtil.NAME_SIZE];
				Buffer.BlockCopy(message, 0, remotePublicKey, 0, remotePublicKey.Length);

				Identity remoteIdentity = manager.LookupIdentity(Encoding.UTF8.GetString(remotePublicKey));

				if (TrustChainUtil.CommonRoot(identity, remoteIdentity) && TrustChainUtil.GetPermission(remoteIdentity).HasFlag(Permission.Unicast)) //Verify CoT
				{
					byte[] encryptedSymKey = new byte[SIGNATURE_LENGTH];
					Buffer.BlockCopy(message, remotePublicKey.Length, encryptedSymKey, 0, encryptedSymKey.Length);
					byte[] symKey = identity.Decrypt(encryptedSymKey, false);

					byte[] key = new byte[Identity.SYM_KEY_SIZE];
					byte[] IV = new byte[Identity.BLOCK_SIZE];

					Buffer.BlockCopy(symKey, 0, key, 0, key.Length);
					Buffer.BlockCopy(symKey, key.Length, IV, 0, IV.Length);

					byte[] messageSignature = new byte[SIGNATURE_LENGTH];
					byte[] encryptedMessage = new byte[message.Length - remotePublicKey.Length - encryptedSymKey.Length - messageSignature.Length];
					Buffer.BlockCopy(message, remotePublicKey.Length + encryptedSymKey.Length, messageSignature, 0, messageSignature.Length);
					Buffer.BlockCopy(message, remotePublicKey.Length + encryptedSymKey.Length + messageSignature.Length, encryptedMessage, 0, encryptedMessage.Length);

					if (identity.Verify(encryptedMessage, remoteIdentity.publicKey, messageSignature))
					{
						byte[] decryptedMacAndMessage = identity.SymmetricDecrypt(encryptedMessage, key, IV);
						Console.WriteLine($"Decrypted mac and message is (len {decryptedMacAndMessage.Length}):\n{BitConverter.ToString(decryptedMacAndMessage)}");

						byte[] signature = new byte[SIGNATURE_LENGTH];
						byte[] decryptedMessage = new byte[decryptedMacAndMessage.Length - signature.Length];

						Buffer.BlockCopy(decryptedMacAndMessage, 0, signature, 0, signature.Length);
						Buffer.BlockCopy(decryptedMacAndMessage, signature.Length, decryptedMessage, 0, decryptedMessage.Length);
						Console.WriteLine("Signature is {0}", BitConverter.ToString(signature));

						if (identity.Verify(decryptedMessage, remoteIdentity.publicKey, signature))
						{
							return Encoding.UTF8.GetString(decryptedMessage);
						}
						else
						{
							Console.WriteLine("Message verification error");
						}
					}
					else
					{
						Console.WriteLine("Identity verification error");
					}
				}
				else
				{
					Console.WriteLine("CoT verification error");
				}
			}
			return null;
		}*/
	}
}
