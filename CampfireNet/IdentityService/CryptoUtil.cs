using System;
using System.IO;
using System.Security.Cryptography;

namespace IdentityService
{
	public static class CryptoUtil
	{
		public static readonly byte[] RSA_EXPONENT = { 0x01, 0x00, 0x01 };

		public static byte[] Sign(byte[] data, RSAParameters privateKey)
		{
			try
			{
				using (var rsa = new RSACryptoServiceProvider())
				{
					rsa.ImportParameters(privateKey);
					return rsa.SignData(data, new SHA256Cng());
				}
			}
			catch (CryptographicException e)
			{
				Console.WriteLine(e);
				throw e;
			}
		}

		public static bool Verify(byte[] data, RSAParameters publicKey, byte[] signature)
		{
			try
			{
				using (var rsa = new RSACryptoServiceProvider())
				{
					rsa.ImportParameters(publicKey);
					return rsa.VerifyData(data, new SHA256Cng(), signature);
				}
			}
			catch (CryptographicException e)
			{
				Console.WriteLine(e);
				return false;
			}
		}

		public static bool Verify(byte[] data, byte[] modulus, byte[] signature)
		{
			try
			{
				using (var rsa = new RSACryptoServiceProvider())
				{
					RSAParameters parameters = new RSAParameters();
					parameters.Modulus = modulus;
					parameters.Exponent = RSA_EXPONENT;
					rsa.ImportParameters(parameters);
					return rsa.VerifyData(data, new SHA256Cng(), signature);
				}
			}
			catch (CryptographicException e)
			{
				Console.WriteLine(e);
				return false;
			}
		}

		public static byte[] SymmetricEncrypt(byte[] data, byte[] key, byte[] IV)
		{
			using (var aes = new AesManaged())
			{
				aes.Key = key;
				aes.IV = IV;
				ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

				using (var input = new MemoryStream(data))
				using (var output = new MemoryStream())
				{
					using (var cryptStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
					{
						var buffer = new byte[1024];
						var read = input.Read(buffer, 0, buffer.Length);
						while (read > 0)
						{
							cryptStream.Write(buffer, 0, buffer.Length);
							read = input.Read(buffer, 0, buffer.Length);
						}
						cryptStream.FlushFinalBlock();
						return output.ToArray();
					}
				}
			}
		}

		public static byte[] SymmetricDecrypt(byte[] data, byte[] key, byte[] IV)
		{
			using (var aes = new AesManaged())
			{
				aes.Key = key;
				aes.IV = IV;
				ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

				using (var input = new MemoryStream(data))
				using (var output = new MemoryStream())
				{
					using (var cryptStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
					{
						var buffer = new byte[1024];
						var read = cryptStream.Read(buffer, 0, buffer.Length);
						while (read > 0)
						{
							output.Write(buffer, 0, read);
							read = cryptStream.Read(buffer, 0, buffer.Length);
						}
						cryptStream.Flush();

						return output.ToArray();
					}
				}
			}
		}
	}
}
