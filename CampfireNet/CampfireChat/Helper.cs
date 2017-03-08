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
         return CryptoUtil.DeserializeKey(HexStringToByteArray(prefs.GetString("Key", null)));
      }

      public static void UpdateIdentity(ISharedPreferences prefs, Identity identity) {
         ISharedPreferencesEditor editor = prefs.Edit();
         editor.PutString("Key", ByteArrayToString(identity.ExportKey()));
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