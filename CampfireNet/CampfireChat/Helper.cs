using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Security.Cryptography;

namespace CampfireChat
{
    class Helper
    {
        public static byte[] HexStringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static RSAParameters InitRSA(ISharedPreferences prefs)
        {
            RSAParameters rsa = new RSAParameters();
            rsa.D = HexStringToByteArray(prefs.GetString("D", null));
            rsa.Exponent = HexStringToByteArray(prefs.GetString("Exp", null));
            rsa.Modulus = HexStringToByteArray(prefs.GetString("Mod", null));
            rsa.Q = HexStringToByteArray(prefs.GetString("Q", null));
            rsa.InverseQ = HexStringToByteArray(prefs.GetString("InvQ", null));
            rsa.DP = HexStringToByteArray(prefs.GetString("DP", null));
            rsa.DQ = HexStringToByteArray(prefs.GetString("DQ", null));
            return rsa;
        }
    }
}