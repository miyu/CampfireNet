using System;
using System.Threading.Tasks;
using CampfireNet.Utilities.Collections;

namespace CampfireNet.Utilities.AsyncPrimatives {
   public class AsyncRouter<TInput, TPassed> {
      private readonly CopyOnAddDictionary<Type, Func<TPassed, Task>> typeHandlers = new CopyOnAddDictionary<Type, Func<TPassed, Task>>();
      private readonly Func<TInput, Type> typeProjector;
      private readonly Func<TInput, TPassed> passedProjector;

      public AsyncRouter(Func<TInput, Type> typeProjector, Func<TInput, TPassed> passedProjector) {
         this.typeProjector = typeProjector;
         this.passedProjector = passedProjector;
      }

      public void RegisterHandler<T>(Func<TPassed, Task> handler) {
         typeHandlers.AddOrThrow(typeof(T), handler);
      }

      public async Task<bool> TryRouteAsync(TInput x) {
         var typeProjection = typeProjector(x);
         var passedProjection = passedProjector(x);
         Func<TPassed, Task> handler;
         if (typeHandlers.TryGetValue(typeProjection, out handler)) {
            await handler(passedProjection).ConfigureAwait(false);
            return true;
         }
         return false;
      }
   }
}
