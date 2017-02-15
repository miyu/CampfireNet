using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities.AsyncPrimatives;

namespace CampfireNet.Utilities.Channels {
   public static class ChannelsExtensions {
      public static void Write<T>(this WritableChannel<T> channel, T message) {
         channel.WriteAsync(message).Wait();
      }

      public static Task WriteAsync<T>(this WritableChannel<T> channel, T message) {
         return channel.WriteAsync(message, CancellationToken.None);
      }

      public static T Read<T>(this ReadableChannel<T> channel) {
         return channel.ReadAsync().Result;
      }

      public static Task<T> ReadAsync<T>(this ReadableChannel<T> channel) {
         return channel.ReadAsync(CancellationToken.None, acceptanceTest => true);
      }

      public static Task Run(Func<Task> task) {
         return Task.Run(task);
         //         TaskCompletionSource<byte> tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
         //         Task.Run(async () => {
         //            await task().ConfigureAwait(false);
         //            tcs.SetResult(0);
         //         });
         //         return tcs.Task;
      }

      public static Task<T> Run<T>(Func<Task<T>> task) {
         return Task.Run(task);
         //         TaskCompletionSource<T> tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
         //         Task.Run(async () => {
         //            tcs.SetResult(await task().ConfigureAwait(false));
         //         });
         //         return tcs.Task;
      }

      public static Task Go(Func<Task> task) => Run(task);

      public static Task<T> Go<T>(Func<Task<T>> task) => Run(task);

      public static ICaseTemporary Case<T>(ReadableChannel<T> channel, Action callback, Func<T, bool> additionalAcceptanceTest = null) {
         return new CaseTemporary<T>(channel, ToFuncTTaskConverter.Convert<T>(callback), additionalAcceptanceTest);
      }

      public static ICaseTemporary Case<T>(ReadableChannel<T> channel, Action<T> callback, Func<T, bool> additionalAcceptanceTest = null) {
         return new CaseTemporary<T>(channel, ToFuncTTaskConverter.Convert<T>(callback), additionalAcceptanceTest);
      }

      public static ICaseTemporary Case<T>(ReadableChannel<T> channel, Func<Task> callback, Func<T, bool> additionalAcceptanceTest = null) {
         return new CaseTemporary<T>(channel, ToFuncTTaskConverter.Convert<T>(callback), additionalAcceptanceTest);
      }

      public static ICaseTemporary Case<T>(ReadableChannel<T> channel, Func<T, Task> callback, Func<T, bool> additionalAcceptanceTest = null) {
         return new CaseTemporary<T>(channel, callback, additionalAcceptanceTest);
      }
   }

   public class CaseTemporary<T> : ICaseTemporary {
      private readonly ReadableChannel<T> channel;
      private readonly Func<T, Task> callback;
      private readonly Func<T, bool> additionalAcceptanceTest;

      public CaseTemporary(ReadableChannel<T> channel, Func<T, Task> callback, Func<T, bool> additionalAcceptanceTest) {
         this.channel = channel;
         this.callback = callback;
         this.additionalAcceptanceTest = additionalAcceptanceTest;
      }

      public void Register(DispatchContext dispatchContext) {
         dispatchContext.Case(channel, callback, additionalAcceptanceTest);
      }
   }

   public interface ICaseTemporary {
      void Register(DispatchContext dispatchContext);
   }

   public class DispatchContext {
      public const int kTimesInfinite = int.MinValue;

      private readonly CancellationTokenSource cts = new CancellationTokenSource();
      private readonly AsyncBox<bool> completionBox = new AsyncBox<bool>();
      private readonly ConcurrentQueue<Task> tasksToShutdown = new ConcurrentQueue<Task>();
      private int dispatchesRemaining;

      public DispatchContext(int times) {
         dispatchesRemaining = times;
      }

      public bool IsCompleted { get; private set; }

      public DispatchContext Case<T>(ReadableChannel<T> channel, Action callback, Func<T, bool> additionalAcceptanceTest = null) {
         return Case(channel, ToFuncTTaskConverter.Convert<T>(callback), additionalAcceptanceTest);
      }

      public DispatchContext Case<T>(ReadableChannel<T> channel, Action<T> callback, Func<T, bool> additionalAcceptanceTest = null) {
         return Case(channel, ToFuncTTaskConverter.Convert<T>(callback), additionalAcceptanceTest);
      }

      public DispatchContext Case<T>(ReadableChannel<T> channel, Func<Task> callback, Func<T, bool> additionalAcceptanceTest = null) {
         return Case(channel, ToFuncTTaskConverter.Convert<T>(callback), additionalAcceptanceTest);
      }

      public DispatchContext Case<T>(ReadableChannel<T> channel, Func<T, Task> callback, Func<T, bool> additionalAcceptanceTest = null) {
         var task = ProcessCaseAsync<T>(channel, callback, additionalAcceptanceTest ?? AcceptAlways);
         tasksToShutdown.Enqueue(task);
         return this;
      }

      private async Task ProcessCaseAsync<T>(ReadableChannel<T> channel, Func<T, Task> callback, Func<T, bool> additionalAcceptanceTest) {
         try {
            while (!cts.IsCancellationRequested) {
               bool isFinalDispatch = false;
               T item;
               try {
                  item = await channel.ReadAsync(
                     cts.Token,
                     x => {
                        if (!additionalAcceptanceTest(x)) {
                           return false;
                        }
                        if (Interlocked.CompareExchange(ref dispatchesRemaining, 0, 0) == kTimesInfinite) {
                           return true;
                        } else {
                           var spinner = new SpinWait();
                           while (true) {
                              var capturedDispatchesRemaining = Interlocked.CompareExchange(ref dispatchesRemaining, 0, 0);
                              var nextDispatchesRemaining = capturedDispatchesRemaining - 1;

                              if (nextDispatchesRemaining < 0) {
                                 return false;
                              }

                              if (Interlocked.CompareExchange(ref dispatchesRemaining, nextDispatchesRemaining, capturedDispatchesRemaining) == capturedDispatchesRemaining) {
                                 isFinalDispatch = nextDispatchesRemaining == 0;
                                 return true;
                              }
                              spinner.SpinOnce();
                           }
                        }
                     }).ConfigureAwait(false);
               } catch (OperationCanceledException) {
                  //Exit processing loop - no dispatches remaining
                  break;
               }

               // Signal other case workers to exit
               if (isFinalDispatch) {
                  cts.Cancel();
               }

               // Execute callback
               await callback(item).ConfigureAwait(false);

               // Mark dispatcher as completed, signal awaiters
               if (isFinalDispatch) {
                  IsCompleted = true;
                  completionBox.SetResult(true);
               }
            }
         } catch (Exception ex) {
            // Signal all other case workers to exit
            cts.Cancel();

            // Bubble all exceptions up to dispatcher awaiters
            IsCompleted = true;
            completionBox.SetException(ex);
         }
      }

      public async Task WaitAsync(CancellationToken token = default(CancellationToken)) {
         await completionBox.GetResultAsync(token).ConfigureAwait(false);
      }

      public async Task ShutdownAsync() {
         cts.Cancel();
         completionBox.SetResult(false);
         foreach (var task in tasksToShutdown) {
            try {
               await task.ConfigureAwait(false);
            } catch (TaskCanceledException) {
               // okay
            }
         }
      }

      public static bool AcceptAlways<T>(T x) => true;
   }

   public static class ToFuncTTaskConverter {
      public static Func<T, Task> Convert<T>(Action callback) {
         return Convert<T>(t => callback());
      }

      public static Func<T, Task> Convert<T>(Action<T> callback) {
         return t => {
            callback(t);
            return Task.CompletedTask;
         };
      }

      public static Func<T, Task> Convert<T>(Func<Task> callback) {
         return t => callback();
      }
   }

   public class BlockingChannel<T> : Channel<T> {
      private readonly ConcurrentQueue<WriterContext<T>> writerQueue = new ConcurrentQueue<WriterContext<T>>();
      private readonly AsyncSemaphore queueSemaphore = new AsyncSemaphore(0);

      public int Count => queueSemaphore.Count;

      public async Task WriteAsync(T message, CancellationToken cancellationToken) {
         var context = new WriterContext<T>(message);
         writerQueue.Enqueue(context);
         queueSemaphore.Release();
         try {
            await context.completionLatch.WaitAsync(cancellationToken).ConfigureAwait(false);
         } catch (OperationCanceledException) {
            while (true) {
               var originalValue = Interlocked.CompareExchange(ref context.state, WriterContext<T>.kStateCancelled, WriterContext<T>.kStatePending);
               if (originalValue == WriterContext<T>.kStatePending) {
                  throw;
               } else if (originalValue == WriterContext<T>.kStateCompleting) {
                  await context.completingFreedEvent.WaitAsync(CancellationToken.None).ConfigureAwait(false);
               } else if (originalValue == WriterContext<T>.kStateCompleted) {
                  return;
               }
            }
         } finally {
            Debug.Assert(context.state == WriterContext<T>.kStateCancelled ||
                         context.state == WriterContext<T>.kStateCompleted);
         }
      }

      public bool TryRead(out T message) {
         if (!queueSemaphore.TryTake()) {
            message = default(T);
            return false;
         }
         SpinWait spinner = new SpinWait();
         WriterContext<T> context;
         while (!writerQueue.TryDequeue(out context)) {
            spinner.SpinOnce();
         }
         var oldState = Interlocked.CompareExchange(ref context.state, WriterContext<T>.kStateCompleting, WriterContext<T>.kStatePending);
         if (oldState == WriterContext<T>.kStatePending) {
            Interlocked.CompareExchange(ref context.state, WriterContext<T>.kStateCompleted, WriterContext<T>.kStateCompleting);
            context.completingFreedEvent.Set();
            context.completionLatch.Set();
            message = context.value;
            return true;
         } else if (oldState == WriterContext<T>.kStateCompleted) {
            throw new InvalidStateException();
         } else if (oldState == WriterContext<T>.kStateCompleted) {
            throw new InvalidStateException();
         } else if (oldState == WriterContext<T>.kStateCompleted) {
            message = default(T);
            return false;
         } else {
            throw new InvalidStateException();
         }
      }

      public async Task<T> ReadAsync(CancellationToken cancellationToken, Func<T, bool> acceptanceTest) {
         while (!cancellationToken.IsCancellationRequested) {
            await queueSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            WriterContext<T> context;
            if (!writerQueue.TryDequeue(out context)) {
               throw new InvalidStateException();
            }
            var oldState = Interlocked.CompareExchange(ref context.state, WriterContext<T>.kStateCompleting, WriterContext<T>.kStatePending);
            if (oldState == WriterContext<T>.kStatePending) {
               if (acceptanceTest(context.value)) {
                  Interlocked.CompareExchange(ref context.state, WriterContext<T>.kStateCompleted, WriterContext<T>.kStateCompleting);
                  context.completingFreedEvent.Set();
                  context.completionLatch.Set();
                  return context.value;
               } else {
                  Interlocked.CompareExchange(ref context.state, WriterContext<T>.kStatePending, WriterContext<T>.kStateCompleting);
                  context.completingFreedEvent.Set();
                  writerQueue.Enqueue(context);
                  queueSemaphore.Release();
               }
            } else if (oldState == WriterContext<T>.kStateCompleting) {
               throw new InvalidStateException();
            } else if (oldState == WriterContext<T>.kStateCompleted) {
               throw new InvalidStateException();
            } else if (oldState == WriterContext<T>.kStateCancelled) {
               continue;
            }
         }
         // throw is guaranteed
         cancellationToken.ThrowIfCancellationRequested();
         throw new InvalidStateException();
      }

      private class WriterContext<T> {
         public const int kStatePending = 0;
         public const int kStateCompleting = 1;
         public const int kStateCompleted = 2;
         public const int kStateCancelled = 3;

         public readonly AsyncLatch completionLatch = new AsyncLatch();
         public readonly AsyncAutoResetLatch completingFreedEvent = new AsyncAutoResetLatch();
         public readonly T value;
         public int state = kStatePending;

         public WriterContext(T value) {
            this.value = value;
         }
      }
   }


}