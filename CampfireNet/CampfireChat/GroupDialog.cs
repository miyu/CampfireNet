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
   class GroupDialog : DialogFragment {
      public override Dialog OnCreateDialog(Bundle savedInstanceState) {
         base.OnCreateDialog(savedInstanceState);

         AlertDialog.Builder builder = new AlertDialog.Builder(Activity)
            .SetView(Resource.Layout.Dialog)
            .SetPositiveButton(Resource.String.Join, (sender, e) => {
               var editText = Dialog.FindViewById<EditText>(Resource.Id.Userinput);
               Dismiss();
            })
            .SetNegativeButton(Resource.String.Cancel, (sender, e) => {
               Dismiss();
            });
         var title = Dialog.FindViewById<TextView>(Resource.Id.Prompt);
         title.SetText(Resource.String.InputGroup);

         return builder.Create();
      }

      public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
         Dialog.SetCanceledOnTouchOutside(false);
         Cancelable = false;

         return base.OnCreateView(inflater, container, savedInstanceState);
      }
   }
}