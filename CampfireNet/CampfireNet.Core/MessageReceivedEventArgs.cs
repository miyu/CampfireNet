using System;
using CampfireNet.IO;
using CampfireNet.IO.Transport;

namespace CampfireNet {
   public class MessageReceivedEventArgs : EventArgs {
      public MessageReceivedEventArgs(IBluetoothNeighbor router, BroadcastMessage message) {
         Router = router;
         Message = message;
      }

      public IBluetoothNeighbor Router { get; }
      public BroadcastMessage Message { get; }
   }

   public delegate void MessageReceivedEventHandler(MessageReceivedEventArgs args);
}