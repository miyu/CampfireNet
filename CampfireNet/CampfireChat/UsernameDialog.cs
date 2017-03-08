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

namespace CampfireChat {
   class UsernameDialog : DialogFragment {
      public override Dialog OnCreateDialog(Bundle savedInstanceState) {
         base.OnCreateDialog(savedInstanceState);

         AlertDialog.Builder builder = new AlertDialog.Builder(Activity)
            .SetView(Resource.Layout.NameDialog)
            .SetPositiveButton(Resource.String.Confirm, (sender, e) => {
               var editText = Dialog.FindViewById<EditText>(Resource.Id.Username);
               Globals.CampfireNetClient.Identity.Name = editText.Text;

               var prefs = Application.Context.GetSharedPreferences("CampfireChat", FileCreationMode.Private);
               Helper.UpdateName(prefs, Globals.CampfireNetClient.Identity.Name);
               Dismiss();
            })
            .SetTitle(Resource.String.InputName);
         return builder.Create();
      }

      public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
         Dialog.SetCanceledOnTouchOutside(false);
         this.Cancelable = false;
         return base.OnCreateView(inflater, container, savedInstanceState);
      }
   }
}