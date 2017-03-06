using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CampfireNet.Identities {
   public static class CryptoUtil {
      public const int ASYM_KEY_SIZE_BITS = 2048;
      public const int ASYM_KEY_SIZE_BYTES = ASYM_KEY_SIZE_BITS / 8;
      public const int SIGNATURE_SIZE = 256;
      public const int SYM_KEY_SIZE = 32;
      public const int IV_SIZE = 16;
      public const int HASH_SIZE = 32;

      public const int MAX_RSA_MESSAGE_SIZE = 214;

      public static readonly byte[] RSA_EXPONENT = { 0x01, 0x00, 0x01 };


      public static byte[] Sign(byte[] data, RSAParameters privateKey) {
         try {
            using (var rsa = new RSACryptoServiceProvider()) {
               rsa.ImportParameters(privateKey);
               return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
         } catch (CryptographicException e) {
            Console.WriteLine(e);
            throw e;
         }
      }

      public static bool Verify(byte[] data, RSAParameters publicKey, byte[] signature) {
         if (signature.Length != SIGNATURE_SIZE) {
            throw new CryptographicException("Bad key size");
         }

         try {
            using (var rsa = new RSACryptoServiceProvider()) {
               rsa.ImportParameters(publicKey);
               return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
         } catch (CryptographicException e) {
            Console.WriteLine(e);
            return false;
         }
      }

      public static bool Verify(BroadcastMessageDto broadcastMessage, byte[] modulus, byte[] signature) {
         var data = broadcastMessage?.SourceIdHash.Concat(broadcastMessage.DestinationIdHash).
                              Concat(broadcastMessage.Payload)
                              .ToArray();
         return Verify(data, modulus, signature);
      }

      public static bool Verify(byte[] data, byte[] modulus, byte[] signature) {
         if (modulus.Length != ASYM_KEY_SIZE_BYTES || signature.Length != SIGNATURE_SIZE) {
            throw new CryptographicException("Bad key size");
         }

         try {
            using (var rsa = new RSACryptoServiceProvider()) {
               RSAParameters parameters = new RSAParameters {
                  Modulus = modulus,
                  Exponent = RSA_EXPONENT
               };
               rsa.ImportParameters(parameters);
               return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
         } catch (CryptographicException e) {
            Console.WriteLine(e);
            return false;
         }
      }

      public static byte[] SymmetricEncrypt(byte[] data, byte[] key, byte[] iv) {
         if (key.Length != SYM_KEY_SIZE || iv.Length != IV_SIZE) {
            throw new CryptographicException("Bad key size");
         }

         using (var aes = Aes.Create()) {
            aes.Key = key;
            aes.IV = iv;
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream()) {
               using (var cryptStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write)) {
                  var buffer = new byte[1024];
                  var read = input.Read(buffer, 0, buffer.Length);
                  while (read > 0) {
                     cryptStream.Write(buffer, 0, read);
                     read = input.Read(buffer, 0, buffer.Length);
                  }
                  cryptStream.FlushFinalBlock();
                  return output.ToArray();
               }
            }
         }
      }



      public static byte[] SymmetricDecrypt(byte[] data, byte[] key, byte[] iv) {
         if (key.Length != SYM_KEY_SIZE || iv.Length != IV_SIZE) {
            throw new CryptographicException("Bad key size");
         }

         using (var aes = Aes.Create()) {
            aes.Key = key;
            aes.IV = iv;
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream()) {
               using (var cryptStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read)) {
                  var buffer = new byte[1024];
                  var read = cryptStream.Read(buffer, 0, buffer.Length);
                  while (read > 0) {
                     output.Write(buffer, 0, read);
                     read = cryptStream.Read(buffer, 0, buffer.Length);
                  }
                  cryptStream.Flush();

                  return output.ToArray();
               }
            }
         }
      }

      public static byte[] AsymmetricEncrypt(byte[] data, RSAParameters publicKey) {
         int numBlocks = (data.Length - 1) / MAX_RSA_MESSAGE_SIZE + 1;
         byte[] totalEncryption = new byte[numBlocks * ASYM_KEY_SIZE_BYTES];
         int dataLeft = data.Length;

         try {
            using (var rsa = new RSACryptoServiceProvider()) {
               rsa.ImportParameters(publicKey);

               for (int i = 0; i < numBlocks; i++) {
                  byte[] dataSegment = new byte[Math.Min(MAX_RSA_MESSAGE_SIZE, dataLeft)];
                  Buffer.BlockCopy(data, i * MAX_RSA_MESSAGE_SIZE, dataSegment, 0, dataSegment.Length);

                  byte[] buffer = rsa.Encrypt(dataSegment, true);
                  Buffer.BlockCopy(buffer, 0, totalEncryption, i * ASYM_KEY_SIZE_BYTES, ASYM_KEY_SIZE_BYTES);

                  dataLeft -= dataSegment.Length;
               }

               return totalEncryption;
            }
         } catch (CryptographicException e) {
            Console.WriteLine(e.Message);
            throw e;
         }
      }

      public static byte[] AsymmetricEncrypt(byte[] data, byte[] modulus) {
         RSAParameters parameters = new RSAParameters {
            Modulus = modulus,
            Exponent = RSA_EXPONENT
         };

         return AsymmetricEncrypt(data, parameters);
      }

      public static byte[] AsymmetricDecrypt(byte[] data, RSAParameters privateKey) {
         if (data.Length % ASYM_KEY_SIZE_BYTES != 0) {
            throw new CryptographicException("Data size is not multiple of RSA message size");
         }

         int numBlocks = data.Length / ASYM_KEY_SIZE_BYTES;
         byte[] allDecrypted = new byte[numBlocks * MAX_RSA_MESSAGE_SIZE];
         int totalSize = 0;

         try {
            using (var rsa = new RSACryptoServiceProvider()) {
               rsa.ImportParameters(privateKey);

               for (int i = 0; i < numBlocks; i++) {
                  byte[] dataSegment = new byte[ASYM_KEY_SIZE_BYTES];
                  Buffer.BlockCopy(data, i * ASYM_KEY_SIZE_BYTES, dataSegment, 0, ASYM_KEY_SIZE_BYTES);

                  byte[] buffer = rsa.Decrypt(dataSegment, true);
                  Buffer.BlockCopy(buffer, 0, allDecrypted, i * MAX_RSA_MESSAGE_SIZE, buffer.Length);
                  totalSize += buffer.Length;
               }

               byte[] totalDecryption = new byte[totalSize];
               Buffer.BlockCopy(allDecrypted, 0, totalDecryption, 0, totalSize);

               return totalDecryption;
            }
         } catch (CryptographicException e) {
            Console.WriteLine(e.Message);
            throw e;
         }
      }

      public static byte[] GetHash(byte[] publicKey) {
         if (publicKey == null) {
            return new byte[HASH_SIZE];
         } else {
            using (var sha256 = SHA256.Create()) {
               return sha256.ComputeHash(publicKey);
            }
         }
      }


      public static byte[] SerializeKey(RSAParameters privateKey) {
         if (privateKey.D.Length == ASYM_KEY_SIZE_BYTES && privateKey.DP.Length == ASYM_KEY_SIZE_BYTES / 2 &&
            privateKey.DQ.Length == ASYM_KEY_SIZE_BYTES / 2 && privateKey.Exponent.Length == RSA_EXPONENT.Length &&
            privateKey.InverseQ.Length == ASYM_KEY_SIZE_BYTES / 2 && privateKey.Modulus.Length == ASYM_KEY_SIZE_BYTES &&
            privateKey.P.Length == ASYM_KEY_SIZE_BYTES / 2 && privateKey.Q.Length == ASYM_KEY_SIZE_BYTES / 2) {
            using (var output = new MemoryStream()) {
               output.Write(privateKey.D, 0, ASYM_KEY_SIZE_BYTES);
               output.Write(privateKey.DP, 0, ASYM_KEY_SIZE_BYTES / 2);
               output.Write(privateKey.DQ, 0, ASYM_KEY_SIZE_BYTES / 2);
               output.Write(privateKey.Exponent, 0, RSA_EXPONENT.Length);
               output.Write(privateKey.InverseQ, 0, ASYM_KEY_SIZE_BYTES / 2);
               output.Write(privateKey.Modulus, 0, ASYM_KEY_SIZE_BYTES);
               output.Write(privateKey.P, 0, ASYM_KEY_SIZE_BYTES / 2);
               output.Write(privateKey.Q, 0, ASYM_KEY_SIZE_BYTES / 2);

               return output.ToArray();
            }
         }

         throw new CryptographicException("Can't serialize private key");
      }

      public static RSAParameters DeserializeKey(byte[] key) {
         RSAParameters parameters = new RSAParameters {
            D = new byte[ASYM_KEY_SIZE_BYTES],
            DP = new byte[ASYM_KEY_SIZE_BYTES / 2],
            DQ = new byte[ASYM_KEY_SIZE_BYTES / 2],
            Exponent = new byte[RSA_EXPONENT.Length],
            InverseQ = new byte[ASYM_KEY_SIZE_BYTES / 2],
            Modulus = new byte[ASYM_KEY_SIZE_BYTES],
            P = new byte[ASYM_KEY_SIZE_BYTES / 2],
            Q = new byte[ASYM_KEY_SIZE_BYTES / 2]
         };


         using (var input = new MemoryStream(key)) {
            input.Read(parameters.D, 0, ASYM_KEY_SIZE_BYTES);
            input.Read(parameters.DP, 0, ASYM_KEY_SIZE_BYTES / 2);
            input.Read(parameters.DQ, 0, ASYM_KEY_SIZE_BYTES / 2);
            input.Read(parameters.Exponent, 0, RSA_EXPONENT.Length);
            input.Read(parameters.InverseQ, 0, ASYM_KEY_SIZE_BYTES / 2);
            input.Read(parameters.Modulus, 0, ASYM_KEY_SIZE_BYTES);
            input.Read(parameters.P, 0, ASYM_KEY_SIZE_BYTES / 2);
            input.Read(parameters.Q, 0, ASYM_KEY_SIZE_BYTES / 2);

            return parameters;
         }
      }
   }
}
