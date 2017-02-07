using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;

namespace IdentityService
{
    class Identity
    {
        public const int KEY_SIZE = 2048;
        public const int SKEY_SIZE = 32;
        public const int BLOCK_SIZE = 16;
        public const int SSIZE = 64;
        public string publicKey;
        public Permission permission;
        public Permission gPermission;
        public byte[] coT;
        private RSAParameters keyInfo;

        public Identity(Permission permission, Permission gPermission, byte[] coT)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(KEY_SIZE);
            keyInfo = rsa.ExportParameters(true);
            publicKey = rsa.ToXmlString(false);
            this.permission = permission;
            this.gPermission = gPermission;
            this.coT = coT ?? Encoding.UTF8.GetBytes(publicKey);
        }

        public Identity CreateUser(Permission permission, Permission gPermission)
        {
            if (CoTProcessor.CheckCoT(this) && GrantPermission(permission) && GrantPermission(gPermission))
            {
                return new Identity(permission, gPermission, CoTProcessor.FormCoT(this, permission, gPermission));
            }
            throw new InvalidPermissionException("Insufficient Authorization.");
        }

        public byte[] SEncrypt(byte[] dataToEncrypt, byte[] key, byte[] IV)
        {
            byte[] encryptedData;
            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Key = key;
                aesAlg.IV = IV;
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                        csEncrypt.FlushFinalBlock();
                        encryptedData = msEncrypt.ToArray();
                    }
                }
            }
            return encryptedData;
        }

        public byte[] SDecrypt(byte[] dataToDecrypt, byte[] key, byte[] IV)
        {
            byte[] decryptedData;
            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Key = key;
                aesAlg.IV = IV;
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msDecrypt = new MemoryStream(dataToDecrypt))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        csDecrypt.Read(dataToDecrypt, 0, dataToDecrypt.Length);
                        decryptedData = msDecrypt.ToArray();
                    }
                }
            }
            return decryptedData;
        }

        public byte[] Encrypt(byte[] dataToEncrypt, string key, bool doOAEPPadding)
        {
            try
            {
                byte[] encryptedData;
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(key);
                    encryptedData = rsa.Encrypt(dataToEncrypt, doOAEPPadding);
                    return encryptedData;
                }
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public byte[] Decrypt(byte[] dataToDecrypt, bool doOAEPPadding)
        {
            try
            {
                byte[] decryptedData;
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.ImportParameters(keyInfo);
                    decryptedData = rsa.Decrypt(dataToDecrypt, doOAEPPadding);
                    return decryptedData;
                }
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public byte[] Sign(byte[] dataToSign)
        {
            try
            {
                byte[] signedData;
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.ImportParameters(keyInfo);
                    signedData = rsa.SignData(dataToSign, new SHA256Cng());
                    return signedData;
                }
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public bool Verify(byte[] data, string key, byte[] signature)
        {
            try {
                bool verified;
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(key);
                    verified = rsa.VerifyData(data, new SHA256Cng(), signature);
                    return verified;
                }
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public bool GrantPermission(Permission permission)
        {
            if (this.permission.HasFlag(Permission.Invite))
            {
                if(permission.HasFlag(Permission.Unicast) && !this.gPermission.HasFlag(Permission.Unicast))
                {
                    return false;
                }
                if(permission.HasFlag(Permission.Broadcast) && !this.gPermission.HasFlag(Permission.Broadcast))
                {
                    return false;
                }
                if(permission.HasFlag(Permission.Invite) && !this.gPermission.HasFlag(Permission.Invite))
                {
                    return false;
                }
            }
            return true;
        }

        public byte[] GenerateKey()
        {
            DeriveBytes rgb = new Rfc2898DeriveBytes(publicKey, SSIZE);
            return rgb.GetBytes(SKEY_SIZE);
        }

        public byte[] GenerateIV()
        {
            DeriveBytes rgb = new Rfc2898DeriveBytes(publicKey, SSIZE);
            return rgb.GetBytes(BLOCK_SIZE);
        }

        public class InvalidPermissionException : Exception
        {
            public InvalidPermissionException() : base() { }
            public InvalidPermissionException(string message) : base(message) { }
            public InvalidPermissionException(string message, Exception inner) : base(message, inner) { }

            public InvalidPermissionException(System.Runtime.Serialization.SerializationInfo info,
                                              System.Runtime.Serialization.StreamingContext context)
            { }
        }
    }
}
