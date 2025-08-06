using System.Threading;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<AsyncUnit> EveryUpdate(
            PlayerLoopTiming updateTiming = PlayerLoopTiming.Update, bool cancelImmediately = false)
        {
            return new EveryUpdate(updateTiming, cancelImmediately);
        }
    }

    internal class EveryUpdate : IUniTaskAsyncEnumerable<AsyncUnit>
    {
        private readonly bool cancelImmediately;
        private readonly PlayerLoopTiming updateTiming;

        public EveryUpdate(PlayerLoopTiming updateTiming, bool cancelImmediately)
        {
            this.updateTiming = updateTiming;
            this.cancelImmediately = cancelImmediately;
        }

        public IUniTaskAsyncEnumerator<AsyncUnit> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _EveryUpdate(updateTiming, cancellationToken, cancelImmediately);
        }

        private class _EveryUpdate : MoveNextSource, IUniTaskAsyncEnumerator<AsyncUnit>, IPlayerLoopItem
        {
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenRegistration cancellationTokenRegistration;
            private readonly PlayerLoopTiming updateTiming;

            private bool disposed;

            public _EveryUpdate(PlayerLoopTiming updateTiming, CancellationToken cancellationToken,
                bool cancelImmediately)
            {
                this.updateTiming = updateTiming;
                this.cancellationToken = cancellationToken;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (_EveryUpdate)state;
                        source.completionSource.TrySetCanceled(source.cancellationToken);
                    }, this);

                TaskTracker.TrackActiveTask(this, 2);
                PlayerLoopHelper.AddAction(updateTiming, this);
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (disposed)
                {
                    completionSource.TrySetResult(false);
                    return false;
                }

                completionSource.TrySetResult(true);
                return true;
            }

            public AsyncUnit Current => default;

            public UniTask<bool> MoveNextAsync()
            {
                if (disposed) return CompletedTasks.False;

                completionSource.Reset();

                if (cancellationToken.IsCancellationRequested) completionSource.TrySetCanceled(cancellationToken);
                return new UniTask<bool>(this, completionSource.Version);
            }

            public UniTask DisposeAsync()
            {
                if (!disposed)
                {
                    cancellationTokenRegistration.Dispose();
                    disposed = true;
                    TaskTracker.RemoveTracking(this);
                }

                return default;
            }
        }
    }
}