using System;
using Android.Content;

namespace AndroidTest.Droid {
   public class LambdaBroadcastReceiver : BroadcastReceiver {
      private readonly Action<Context, Intent> handler;

      public LambdaBroadcastReceiver(Action<Context, Intent> handler) {
         this.handler = handler;
      }

      public override void OnReceive(Context context, Intent intent) {
         handler(context, intent);
      }
   }
}