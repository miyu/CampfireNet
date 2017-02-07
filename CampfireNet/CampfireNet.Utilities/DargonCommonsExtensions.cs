using System;
using System.Threading.Tasks;

namespace CampfireNet.Utilities {
   public static partial class DargonCommonsExtensions {
      public static Task Forgettable(this Task task) {
         return task.ContinueWith(
            (t, _) => {
               if (t.IsFaulted) {
                  Console.WriteLine("Forgotten task threw: " + t.Exception);
               }
            }, null);
      }

      public static void Forget(this Task task) {
         var throwaway = task.Forgettable();
      }
   }
}