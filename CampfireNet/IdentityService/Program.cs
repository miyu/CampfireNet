using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using IdentityService;

namespace CampfireNet.Simulator
{
    class IdentityService
    {
        public static int SIGNATURE_LENGTH = 256;
        static void Main()
        {
            IdentityManager manager = new IdentityManager();
            Identity root = CreateRoot();
            manager.AddIdentity(root.publicKey, root);
            Identity i2 = root.CreateUser(Permission.All, Permission.All);
            manager.AddIdentity(i2.publicKey, i2);
            byte[] message = EncodeMessage(i2, root.publicKey, "Hello World!");
            DecodeMessage(root, message, manager);
        }

        static Identity CreateRoot()
        {
            return new Identity(Permission.All, Permission.All, null);
        }

        static byte[] EncodeMessage(Identity i1, string i2, string message)
        {
            if (message != null)
            {
                byte[] messageToEncrypt = Encoding.UTF8.GetBytes(message);
                Console.WriteLine("Message is: {0}", messageToEncrypt.Length);
                byte[] signature = i1.Sign(messageToEncrypt);
                Console.WriteLine("Signature is {0}", BitConverter.ToString(signature));
                byte[] segment = new byte[signature.Length + messageToEncrypt.Length];
                Buffer.BlockCopy(signature, 0, segment, 0, signature.Length);
                Buffer.BlockCopy(messageToEncrypt, 0, segment, signature.Length, messageToEncrypt.Length);
                byte[] key = i1.GenerateKey();
                byte[] IV = i1.GenerateIV();
                byte[] encryptedMessage = i1.SEncrypt(segment, key, IV);
                Console.WriteLine("Encrypted Message is: {0}", encryptedMessage.Length);
                Console.WriteLine("Encrypted Message is: {0}", BitConverter.ToString(encryptedMessage));
                byte[] fSignature = i1.Sign(encryptedMessage);
                byte[] decoder = new byte[key.Length + IV.Length];
                Buffer.BlockCopy(key, 0, decoder, 0, key.Length);
                Buffer.BlockCopy(IV, 0, decoder, key.Length, IV.Length);
                byte[] encryptedDecoder = i1.Encrypt(decoder, i2, false);
                byte[] eDLength = BitConverter.GetBytes(encryptedDecoder.Length);
                byte[] sender = Encoding.UTF8.GetBytes(i1.publicKey);
                byte[] messageToSend = new byte[sender.Length + encryptedDecoder.Length +
                    fSignature.Length + encryptedMessage.Length];
                Buffer.BlockCopy(sender, 0, messageToSend, 0, sender.Length);
                Buffer.BlockCopy(encryptedDecoder, 0, messageToSend, sender.Length, encryptedDecoder.Length);
                Buffer.BlockCopy(fSignature, 0, messageToSend, sender.Length + encryptedDecoder.Length, fSignature.Length);
                Buffer.BlockCopy(encryptedMessage, 0, messageToSend, sender.Length + encryptedDecoder.Length + fSignature.Length, encryptedMessage.Length);
                return messageToSend;
            }
            return null;
        }

        static string DecodeMessage(Identity i1, byte[] message, IdentityManager manager)
        {
            if(message.Length != 0)
            {
                byte[] sender = new byte[CoTProcessor.NAME_SIZE];
                Buffer.BlockCopy(message, 0, sender, 0, sender.Length);
                Identity i2 = manager.LookupIdentity(Encoding.UTF8.GetString(sender));
                if(CoTProcessor.CommonRoot(i1, i2) && CoTProcessor.GetPermission(i2).HasFlag(Permission.Unicast)) //Verify CoT
                {
                    byte[] encryptedDecoder = new byte[SIGNATURE_LENGTH];
                    Buffer.BlockCopy(message, sender.Length, encryptedDecoder, 0, encryptedDecoder.Length);
                    byte[] decoder = i1.Decrypt(encryptedDecoder, false);
                    byte[] key = new byte[Identity.SKEY_SIZE];
                    byte[] IV = new byte[Identity.BLOCK_SIZE];
                    Buffer.BlockCopy(decoder, 0, key, 0, key.Length);
                    Buffer.BlockCopy(decoder, key.Length, IV, 0, IV.Length);
                    byte[] fSignature = new byte[SIGNATURE_LENGTH];
                    byte[] segment = new byte[message.Length - sender.Length - encryptedDecoder.Length - fSignature.Length];
                    Buffer.BlockCopy(message, sender.Length + encryptedDecoder.Length, fSignature, 0, fSignature.Length);
                    Buffer.BlockCopy(message, sender.Length + encryptedDecoder.Length + fSignature.Length, segment, 0, segment.Length);
                    if(i1.Verify(segment, i2.publicKey, fSignature))
                    {
                        byte[] decryptedSegment = i1.SDecrypt(segment, key, IV);
                        Console.WriteLine("Decrypted Segment is: {0}", decryptedSegment.Length);
                        Console.WriteLine("Decrypted Message: {0}", BitConverter.ToString(decryptedSegment));
                        byte[] signature = new byte[SIGNATURE_LENGTH];
                        byte[] decryptedMessage = new byte[decryptedSegment.Length - signature.Length];
                        Buffer.BlockCopy(decryptedSegment, 0, signature, 0, signature.Length);
                        Buffer.BlockCopy(decryptedSegment, signature.Length, decryptedMessage, 0, decryptedMessage.Length);
                        Console.WriteLine("Signature is {0}", BitConverter.ToString(signature));
                       
                        if (i1.Verify(decryptedMessage, i2.publicKey, signature))
                        {
                            return Encoding.UTF8.GetString(decryptedMessage);
                        }
                    }
                }
            }
            return null;
        }
    }
}
