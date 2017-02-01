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

        static void Main()
        {
            try
            {
                UnicodeEncoding byteConverter = new UnicodeEncoding();
                Identity i1 = new Identity(Permission.All);
                Identity i2 = new Identity(Permission.All);
                byte[] dataToEncrypt = byteConverter.GetBytes("Data to Encrypt");
                byte[] encryptedData = i1.Encrypt(dataToEncrypt, i2.publicKey, false);
                byte[] signature = i1.Sign(encryptedData);

                if(i2.Verify(encryptedData, i1.publicKey, signature))
                {
                    byte[] decryptedData = i2.Decrypt(encryptedData, false);
                    Console.WriteLine("Decrypted plaintext: {0}", byteConverter.GetString(decryptedData));
                } else
                {
                    Console.WriteLine("Failed to Verify.");
                }
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("Encryption failed.");

            }
        }
    }
}
