using System;
using System.Threading;
using Cysharp.Threading.Tasks.Internal;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<TSource> TakeUntil<TSource>(this IUniTaskAsyncEnumerable<TSource> source,
            UniTask other)
        {
            Error.ThrowArgumentNullException(source, nameof(source));

            return new TakeUntil<TSource>(source, other, null);
        }

        public static IUniTaskAsyncEnumerable<TSource> TakeUntil<TSource>(this IUniTaskAsyncEnumerable<TSource> source,
            Func<CancellationToken, UniTask> other)
        {
            Error.ThrowArgumentNullException(source, nameof(source));
            Error.ThrowArgumentNullException(source, nameof(other));

            return new TakeUntil<TSource>(source, default, other);
        }
    }

    internal sealed class TakeUntil<TSource> : IUniTaskAsyncEnumerable<TSource>
    {
        private readonly UniTask other;
        private readonly Func<CancellationToken, UniTask> other2;
        private readonly IUniTaskAsyncEnumerable<TSource> source;

        public TakeUntil(IUniTaskAsyncEnumerable<TSource> source, UniTask other,
            Func<CancellationToken, UniTask> other2)
        {
            this.source = source;
            this.other = other;
            this.other2 = other2;
        }

        public IUniTaskAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (other2 != null) return new _TakeUntil(source, other2(cancellationToken), cancellationToken);

            return new _TakeUntil(source, other, cancellationToken);
        }

        private sealed class _TakeUntil : MoveNextSource, IUniTaskAsyncEnumerator<TSource>
        {
            private static readonly Action<object> CancelDelegate1 = OnCanceled1;
            private static readonly Action<object> MoveNextCoreDelegate = MoveNextCore;

            private readonly IUniTaskAsyncEnumerable<TSource> source;
            private UniTask<bool>.Awaiter awaiter;
            private readonly CancellationToken cancellationToken1;
            private readonly CancellationTokenRegistration cancellationTokenRegistration1;

            private bool completed;
            private IUniTaskAsyncEnumerator<TSource> enumerator;
            private Exception exception;

            public _TakeUntil(IUniTaskAsyncEnumerable<TSource> source, UniTask other,
                CancellationToken cancellationToken1)
            {
                this.source = source;
                this.cancellationToken1 = cancellationToken1;

                if (cancellationToken1.CanBeCanceled)
                    cancellationTokenRegistration1 =
                        cancellationToken1.RegisterWithoutCaptureExecutionContext(CancelDelegate1, this);

                TaskTracker.TrackActiveTask(this, 3);

                RunOther(other).Forget();
            }

            public TSource Current { get; private set; }

            public UniTask<bool> MoveNextAsync()
            {
                if (completed) return CompletedTasks.False;

                if (exception != null) return UniTask.FromException<bool>(exception);

                if (cancellationToken1.IsCancellationRequested) return UniTask.FromCanceled<bool>(cancellationToken1);

                if (enumerator == null) enumerator = source.GetAsyncEnumerator(cancellationToken1);

                completionSource.Reset();
                SourceMoveNext();
                return new UniTask<bool>(this, completionSource.Version);
            }

            public UniTask DisposeAsync()
            {
                TaskTracker.RemoveTracking(this);
                cancellationTokenRegistration1.Dispose();
                if (enumerator != null) return enumerator.DisposeAsync();
                return default;
            }

            private void SourceMoveNext()
            {
                try
                {
                    awaiter = enumerator.MoveNextAsync().GetAwaiter();
                    if (awaiter.IsCompleted)
                        MoveNextCore(this);
                    else
                        awaiter.SourceOnCompleted(MoveNextCoreDelegate, this);
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                }
            }

            private static void MoveNextCore(object state)
            {
                var self = (_TakeUntil)state;

                if (self.TryGetResult(self.awaiter, out var result))
                {
                    if (result)
                    {
                        if (self.exception != null)
                        {
                            self.completionSource.TrySetException(self.exception);
                        }
                        else if (self.cancellationToken1.IsCancellationRequested)
                        {
                            self.completionSource.TrySetCanceled(self.cancellationToken1);
                        }
                        else
                        {
                            self.Current = self.enumerator.Current;
                            self.completionSource.TrySetResult(true);
                        }
                    }
                    else
                    {
                        self.completionSource.TrySetResult(false);
                    }
                }
            }

            private async UniTaskVoid RunOther(UniTask other)
            {
                try
                {
                    await other;
                    completed = true;
                    completionSource.TrySetResult(false);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    completionSource.TrySetException(ex);
                }
            }

            private static void OnCanceled1(object state)
            {
                var self = (_TakeUntil)state;
                self.completionSource.TrySetCanceled(self.cancellationToken1);
            }
        }
    }
}