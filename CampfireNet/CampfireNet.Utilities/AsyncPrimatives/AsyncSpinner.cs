using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.AsyncPrimatives {
   public class AsyncSpinner {
      private static readonly int PROCESSOR_COUNT = Environment.ProcessorCount;

      private int count = 0;

      public bool NextSpinWillYield
      {
         get
         {
            if (count < 10)
               return PROCESSOR_COUNT == 1;
            return true;
         }
      }

      public async Task SpinAsync() {
         count++;

         if (NextSpinWillYield) {
            await Task.Yield();
         } else {
            // Ideally just Thread.SpinWait(count << 4);
            // but we're using pcl... so sadface
            var spinner = new SpinWait();
            for (int i = 0; i < count; i++) {
               spinner.SpinOnce();
            }
         }
      }
   }
}
