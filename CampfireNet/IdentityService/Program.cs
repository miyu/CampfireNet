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
            Identity temp = new Identity(Permission.All, Permission.All, null);
            Console.WriteLine("coT length: {0}", temp.coT.Length);
            Identity temp2 = temp.CreateUser(Permission.All, Permission.Broadcast);
            Identity temp3 = temp2.CreateUser(Permission.All, Permission.Broadcast);
        }

        

        static byte[] EncodeMessage(Identity i1, string i2, string message, bool broadcast)
        {
            // TODO: sign encrypt then sign again, concatenate byte arrays
            // may switch to symmetric then asymmetric
            // cannot encrypt signature
            byte[] messageToEncrypt = Encoding.UTF8.GetBytes(message);
            byte[] signature = i1.Sign(messageToEncrypt);
            byte[] temp = new byte[signature.Length + messageToEncrypt.Length];
            Buffer.BlockCopy(signature, 0, temp, 0, signature.Length);
            Buffer.BlockCopy(messageToEncrypt, 0, temp, signature.Length, messageToEncrypt.Length);
            //byte[] temp1 = i1.Encrypt(temp, i2, false);
            return Encoding.UTF8.GetBytes(message);
        }

        static string DecodeMessage(Identity i1, string i2, byte[] message, bool broadcast)
        {
            /*if(message.Length != 0)
            {
                byte[] signature = new byte[SIGNATURE_LENGTH];
                Buffer.BlockCopy(message, 0, signature, 0, SIGNATURE_LENGTH);
                byte[] messageToDecrypt = new byte[message.Length - SIGNATURE_LENGTH];
                Buffer.BlockCopy(message, SIGNATURE_LENGTH, messageToDecrypt, 0, messageToDecrypt.Length);
                byte[] decryptedMessage = i1.Decrypt(messageToDecrypt, false);
                if (i1.Verify(decryptedMessage, i2, signature))
                {
                    return byteConverter.GetString(decryptedMessage);
                }
            }*/

            return Encoding.UTF8.GetString(message);
        }
    }
}
