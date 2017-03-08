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
   class TagDialog : DialogFragment {
      public override Dialog OnCreateDialog(Bundle savedInstanceState) {
         base.OnCreateDialog(savedInstanceState);

         AlertDialog.Builder builder = new AlertDialog.Builder(Activity)
            .SetView(Resource.Layout.NameDialog)
            .SetTitle(Resource.String.TagName)
            .SetPositiveButton(Resource.String.Confirm, (sender, e) => {
               var editText = Dialog.FindViewById<EditText>(Resource.Id.Username).Text;
               var prefs = Application.Context.GetSharedPreferences("CampfireChat", FileCreationMode.Private);
               var userHash = savedInstanceState.GetString("UserHash");
               Helper.UpdateString(prefs, userHash, editText);
               Dismiss();
            })
            .SetNegativeButton(Resource.String.Cancel, (sender, e) => {
               Dismiss();
            });

         return builder.Create();
      }

      public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
         Dialog.SetCanceledOnTouchOutside(false);
 

         return base.OnCreateView(inflater, container, savedInstanceState);
      }
   }
}