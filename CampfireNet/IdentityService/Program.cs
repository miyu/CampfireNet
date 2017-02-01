using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace CampfireNet.Simulator
{
    class IdentityService
    {

        static void Main()
        {
            try
            {
                RSACryptoServiceProvider RSA = new RSACryptoServiceProvider(2048);
                UnicodeEncoding ByteConverter = new UnicodeEncoding();
                //Create byte arrays to hold original, encrypted, and decrypted data.
                byte[] dataToEncrypt = ByteConverter.GetBytes("Data to Encrypt");
                byte[] encryptedData;
                byte[] decryptedData;

				Console.WriteLine("Encrypt");

				RSA.ExportParameters(false);
				RSA.ExportParameters(true);


				Console.WriteLine("Export");

				encryptedData = RSAEncrypt(dataToEncrypt, RSA.ExportParameters(false), false);

				//Pass the data to DECRYPT, the private key information 
				//(using RSACryptoServiceProvider.ExportParameters(true),
				//and a boolean flag specifying no OAEP padding.
				decryptedData = RSADecrypt(encryptedData, RSA.ExportParameters(true), false);

				//Display the decrypted plaintext to the console. 
				Console.WriteLine("Decrypted plaintext: {0}", ByteConverter.GetString(decryptedData));

                byte[] signature = RSASign(dataToEncrypt, RSA.ExportParameters(true));
				signature[0] = 0;

                if(RSAVerify(dataToEncrypt, signature, RSA.ExportParameters(false)))
                {
                    Console.WriteLine("True");
                } else
                {
                    Console.WriteLine("false");
                }
            }
            catch (ArgumentNullException)
            {
                //Catch this exception in case the encryption did
                //not succeed.
                Console.WriteLine("Encryption failed.");

            }
        }

        static public byte[] RSAEncrypt(byte[] DataToEncrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {

            try
            {
                byte[] encryptedData;
                //Create a new instance of RSACryptoServiceProvider.
                using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
                {

                    //Import the RSA Key information. This only needs
                    //toinclude the public key information.
                    RSA.ImportParameters(RSAKeyInfo);

                    //Encrypt the passed byte array and specify OAEP padding.  
                    //OAEP padding is only available on Microsoft Windows XP or
                    //later.  
                    encryptedData = RSA.Encrypt(DataToEncrypt, DoOAEPPadding);
                }
                return encryptedData;
            }
            //Catch and display a CryptographicException  
            //to the console.
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);

                return null;
            }

        }

        static public byte[] RSADecrypt(byte[] DataToDecrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {
            try
            {
                byte[] decryptedData;
                //Create a new instance of RSACryptoServiceProvider.
                using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
                {
                    //Import the RSA Key information. This needs
                    //to include the private key information.
                    RSA.ImportParameters(RSAKeyInfo);

                    //Decrypt the passed byte array and specify OAEP padding.  
                    //OAEP padding is only available on Microsoft Windows XP or
                    //later.  
                    decryptedData = RSA.Decrypt(DataToDecrypt, DoOAEPPadding);
                }
                return decryptedData;
            }
            //Catch and display a CryptographicException  
            //to the console.
            catch (CryptographicException e)
            {
                Console.WriteLine(e.ToString());

                return null;
            }

        }

        static public byte[] RSASign(byte[] dataToSign, RSAParameters keyInfo)
        {
            byte[] signedData;

            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                RSA.ImportParameters(keyInfo);
                signedData = RSA.SignData(dataToSign, new SHA256Cng());
                Console.WriteLine(BitConverter.ToString(signedData));
            }

            return signedData;
        }

        static public bool RSAVerify(byte[] data, byte[] signature, RSAParameters keyInfo)
        {
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                RSA.ImportParameters(keyInfo);
                return RSA.VerifyData(data, new SHA256Cng(), signature);
            }
        }
    }


    


}
