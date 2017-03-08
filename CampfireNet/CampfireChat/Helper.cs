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
using Android.Bluetooth;
using CampfireNet.Identities;

namespace CampfireChat {
   class Helper {
      public const int REQUEST_ENABLE_BT = 1;

      public static byte[] HexStringToByteArray(string hex) {
         int NumberChars = hex.Length;
         byte[] bytes = new byte[NumberChars / 2];
         for (int i = 0; i < NumberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
         return bytes;
      }

      public static string ByteArrayToString(byte[] ba) {
         string hex = BitConverter.ToString(ba);
         return hex.Replace("-", "");
      }

      public static RSAParameters InitRSA(ISharedPreferences prefs) {
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

      public static void UpdateIdentity(ISharedPreferences prefs, Identity identity) {
         ISharedPreferencesEditor editor = prefs.Edit();
         //WTF? How should I restore the key then? Should I set it to private?
         editor.PutString("D", ByteArrayToString(identity.PrivateKeyDebug.D));
         editor.PutString("Exp", ByteArrayToString(identity.PrivateKeyDebug.Exponent));
         editor.PutString("Mod", ByteArrayToString(identity.PrivateKeyDebug.Modulus));
         editor.PutString("Q", ByteArrayToString(identity.PrivateKeyDebug.Q));
         editor.PutString("InvQ", ByteArrayToString(identity.PrivateKeyDebug.InverseQ));
         editor.PutString("DP", ByteArrayToString(identity.PrivateKeyDebug.DP));
         editor.PutString("DQ", ByteArrayToString(identity.PrivateKeyDebug.DQ));
         editor.Commit();
      }

      public static void UpdateName(ISharedPreferences prefs, string name) {
         ISharedPreferencesEditor editor = prefs.Edit();
         editor.PutString("Name", name);
         editor.Commit();
      }

      public static void UpdateTrustChain(ISharedPreferences prefs, byte[] trustChain) {
         ISharedPreferencesEditor editor = prefs.Edit();
         editor.PutString("TC", ByteArrayToString(trustChain));
         editor.Commit();
      }

      public static BluetoothAdapter EnableBluetooth(Activity activity) {
         var nativeBluetoothAdapter = BluetoothAdapter.DefaultAdapter;
         if (!nativeBluetoothAdapter.IsEnabled) {
            System.Console.WriteLine("Enabling bluetooth");
            Intent enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
            activity.StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
         }
         return nativeBluetoothAdapter;
      }




   }
}