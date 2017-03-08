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
using CampfireNet.Identities;

namespace CampfireChat {
   class GroupDialog : DialogFragment {
      private Handler uiHandler;

      public GroupDialog(Handler uiHandler) {
         this.uiHandler = uiHandler;
      }

      public override Dialog OnCreateDialog(Bundle savedInstanceState) {
         base.OnCreateDialog(savedInstanceState);

         AlertDialog.Builder builder = new AlertDialog.Builder(Activity)
            .SetView(Resource.Layout.Dialog)
            .SetPositiveButton(Resource.String.Join, (sender, e) => {
               var roomName = Dialog.FindViewById<EditText>(Resource.Id.Userinput).Text;

               ChatRoomContext context = Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes(roomName)))));
               context.FriendlyName = roomName;

               Globals.CampfireNetClient.IdentityManager.AddMulticastKey(
               IdentityHash.GetFlyweight(CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes(roomName)))),
                  CryptoUtil.GetHash(Encoding.UTF8.GetBytes(roomName)));

               byte[] roomKey = CryptoUtil.GetHash(CryptoUtil.GetHash(Encoding.UTF8.GetBytes(roomName)));
               Globals.JoinedRooms.Add(roomKey);

               uiHandler.ObtainMessage(0, new ChatEntry(roomKey, context)).SendToTarget();

               Dismiss();
            })
            .SetNegativeButton(Resource.String.Cancel, (sender, e) => {
               Dismiss();
            });

         return builder.Create();
      }

      public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
         View v = base.OnCreateView(inflater, container, savedInstanceState);

         Dialog.SetCanceledOnTouchOutside(false);
         Cancelable = true;

         return v;
      }
   }
}