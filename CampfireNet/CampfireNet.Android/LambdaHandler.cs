using System;
using Android.OS;

namespace AndroidTest.Droid {
   public class LambdaHandler : Handler {
      private readonly Action<Message> cb;

      public LambdaHandler(Action<Message> cb) {
         this.cb = cb;
      }

      public override void HandleMessage(Message msg) {
         base.HandleMessage(msg);
         cb(msg);
      }
   }
}