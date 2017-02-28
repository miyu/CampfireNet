using Java.Util;

namespace AndroidTest.Droid {
   public class CampfireNetBluetoothConstants {
      public static readonly string NAME = "Test Chat";

      // fetchUuidsWithSdp firmware issue: https://code.google.com/p/android/issues/detail?id=197341
      public static readonly UUID APP_UUID = UUID.FromString("fa87c0d0-afac-11de-8a39-0800200c9a66");
      public static readonly UUID FIRMWARE_BUG_REVERSE_APP_UUID = UUID.FromString("669a0c20-0008-398a-de11-acafd0c087fa");
   }
}