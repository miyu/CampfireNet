using System;
using CampfireNet.IO;
using CampfireNet.IO.Transport;

namespace CampfireNet {
   public class CSE561MessageReceivedEventArgs : EventArgs {
      public CSE561MessageReceivedEventArgs(IBluetoothNeighbor router, BroadcastMessage message) {
         Router = router;
         Message = message;
      }

      public IBluetoothNeighbor Router { get; }
      public BroadcastMessage Message { get; }
   }

   public delegate void CSE561MessageReceivedEventHandler(CSE561MessageReceivedEventArgs args);
}