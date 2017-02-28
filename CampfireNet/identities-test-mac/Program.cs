using System;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace CampfireNet.Identities {
	public class Program {
		public static void Main(string[] args) {
			Identity A = new Identity(new IdentityManager(), "A");
			Identity B = new Identity(new IdentityManager(), "B");
			//Identity C = new Identity(new IdentityManager(), "C");
			Identity D = new Identity(new IdentityManager(), "D");
			//Identity E = new Identity(new IdentityManager(), "E");
			//Identity F = new Identity(new IdentityManager(), "F");
			//Identity G = new Identity(new IdentityManager(), "G");
			//Identity H = new Identity(new IdentityManager(), "H");
			//Identity I = new Identity(new IdentityManager(), "I");

			A.GenerateRootChain();
			B.AddTrustChain(A.GenerateNewChain(B.PublicIdentity, Permission.Invite | Permission.Broadcast, Permission.Invite, "B"));
			//C.AddTrustChain(B.GenerateNewChain(C.PublicIdentity, Permission.Invite, Permission.Invite, "C"));
			D.AddTrustChain(A.GenerateNewChain(D.PublicIdentity, Permission.Invite, Permission.Invite, "D"));
			//E.AddTrustChain(C.GenerateNewChain(E.PublicIdentity, Permission.Invite, Permission.Invite, "E"));
			//F.AddTrustChain(D.GenerateNewChain(F.PublicIdentity, Permission.Invite, Permission.None, "F"));
			//G.AddTrustChain(E.GenerateNewChain(G.PublicIdentity, Permission.Invite, Permission.Invite, "G"));
			//H.AddTrustChain(C.GenerateNewChain(H.PublicIdentity, Permission.None, Permission.None, "H"));
			//I.AddTrustChain(F.GenerateNewChain(I.PublicIdentity, Permission.None, Permission.None, "I"));

			B.ValidateAndAdd(D.TrustChain);
			D.ValidateAndAdd(B.TrustChain);


			//bool ba = B.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(A.TrustChain));
			//bool bd = B.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(D.TrustChain));
			//bool bf = B.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(F.TrustChain));
			//bool bi = B.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(I.TrustChain));

			//bool ig = I.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(G.TrustChain));
			//bool ic = I.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(C.TrustChain));
			//bool ia = I.ValidateAndAdd(TrustChainUtil.SerializeTrustChain(A.TrustChain));

			//Console.WriteLine($"\nvalidated chains {ba} {bd} {bf} {bi}");
			//Console.WriteLine($"validated chains {ig} {ic} {ia}");
			//Console.WriteLine(TrustChainUtil.TrustChainToString(G.TrustChain));



			//Identity alice = new Identity(new IdentityManager(), "alice");
			//Identity bob = new Identity(new IdentityManager(), "bob");
			//Identity eve = new Identity(new IdentityManager(), "eve");

			//string message = "This is a long message being sent over a cryptographically secure channel.";

			//byte[] data = alice.GenerateMessage(message, bob.PublicIdentity);

			//Console.WriteLine($"Message len {data.Length}");

			//string received = eve.ReadMessage(data);

			//Console.WriteLine($"Got '{received}' from alice");

			var encoded = B.EncodePacket(Encoding.UTF8.GetBytes("This is a broadcast test #415 (to B from D)"), D.PublicIdentity);

			byte[] fakePayload = new byte[2 * CryptoUtil.HASH_SIZE + CryptoUtil.ASYM_KEY_SIZE_BYTES];
			Buffer.BlockCopy(A.PublicIdentityHash, 0, fakePayload, 0, CryptoUtil.HASH_SIZE);
			Buffer.BlockCopy(D.PublicIdentityHash, 0, fakePayload, CryptoUtil.HASH_SIZE, CryptoUtil.HASH_SIZE);
			Buffer.BlockCopy(encoded.Payload, 0, fakePayload, 2 * CryptoUtil.HASH_SIZE, CryptoUtil.ASYM_KEY_SIZE_BYTES);

			byte[] fakeSignature = CryptoUtil.Sign(fakePayload, A.privateKeyDebug);

			BroadcastMessageDto fake = new BroadcastMessageDto();
			fake.SourceIdHash = A.PublicIdentityHash;
			fake.DestinationIdHash = D.PublicIdentityHash;
			fake.Payload = encoded.Payload;
			fake.Signature = fakeSignature;

			byte[] decoded;
			if (D.TryDecodePayload(fake, out decoded)) {
				Console.WriteLine(BitConverter.ToString(fake.Payload));
				Console.WriteLine(Encoding.UTF8.GetString(decoded));
			} else {
				Console.WriteLine("Not our packet");
			}
		}
	}
}
