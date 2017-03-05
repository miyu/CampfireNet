using System;
using System.Linq;
using CampfireNet.Utilities;

namespace AndroidTest.Droid {
   public class MacUtilities {
      public static Guid ConvertMacToGuid(string macAddress) {
         var bytes = macAddress.Split(':').Select(hex => Convert.ToByte(hex, 16)).Reverse().ToArray();
         if (bytes.Length != 6) {
            throw new InvalidStateException("MAC more than 6 bytes?");
         }
         var sap = BitConverter.ToInt32(bytes, 0);
         var nap = BitConverter.ToInt16(bytes, 4);
         return new Guid(sap, nap, 0, 0, 0, 0, 0, 0, 0, 0, 0);
      }
   }
}