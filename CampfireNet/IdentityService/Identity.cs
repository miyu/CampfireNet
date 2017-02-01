using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace IdentityService
{
    class Identity
    {
        public const int KEY_SIZE = 2048;
        private RSAParameters keyInfo;
        public string publicKey;
        private Permission permission;

        public Identity(Permission permission)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            keyInfo = rsa.ExportParameters(true);
            publicKey = rsa.ToXmlString(false);
            this.permission = permission;
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

        public Permission GrantPermission(bool unicast, bool broadcast, bool invite)
        {
            Permission newPermission = Permission.None;
            if (permission.HasFlag(Permission.Invite))
            {
                if(permission.HasFlag(Permission.Unicast) && unicast)
                {
                    newPermission |= Permission.Unicast;
                }
                if(permission.HasFlag(Permission.Broadcast) && broadcast)
                {
                    newPermission |= Permission.Broadcast;
                }
                if(invite)
                {
                    newPermission |= Permission.Invite;
                }
            }
            return newPermission;
        }
    }
}
