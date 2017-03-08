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
         AlertDialog.Builder builder = new AlertDialog.Builder(Activity)
            .SetView(Resource.Layout.NameDialog)
            .SetPositiveButton(Resource.String.Confirm, (sender, e) => {
               var prefs = Application.Context.GetSharedPreferences("CampfireChat", FileCreationMode.Private);
               Globals.CampfireNetClient.Identity.Name = this.Dialog.FindViewById<EditText>(Resource.Id.username).Text;
               Helper.UpdateName(prefs, Globals.CampfireNetClient.Identity.Name);
               Dismiss();})
            .SetTitle(Resource.String.InputName);
        return builder.Create();
    }
   }
}