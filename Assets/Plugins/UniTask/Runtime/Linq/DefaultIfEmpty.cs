using System;
using System.Threading;
using Cysharp.Threading.Tasks.Internal;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<TSource> DefaultIfEmpty<TSource>(
            this IUniTaskAsyncEnumerable<TSource> source)
        {
            Error.ThrowArgumentNullException(source, nameof(source));

            return new DefaultIfEmpty<TSource>(source, default);
        }

        public static IUniTaskAsyncEnumerable<TSource> DefaultIfEmpty<TSource>(
            this IUniTaskAsyncEnumerable<TSource> source, TSource defaultValue)
        {
            Error.ThrowArgumentNullException(source, nameof(source));

            return new DefaultIfEmpty<TSource>(source, defaultValue);
        }
    }

    internal sealed class DefaultIfEmpty<TSource> : IUniTaskAsyncEnumerable<TSource>
    {
        private readonly TSource defaultValue;
        private readonly IUniTaskAsyncEnumerable<TSource> source;

        public DefaultIfEmpty(IUniTaskAsyncEnumerable<TSource> source, TSource defaultValue)
        {
            this.source = source;
            this.defaultValue = defaultValue;
        }

        public IUniTaskAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _DefaultIfEmpty(source, defaultValue, cancellationToken);
        }

        private sealed class _DefaultIfEmpty : MoveNextSource, IUniTaskAsyncEnumerator<TSource>
        {
            private static readonly Action<object> MoveNextCoreDelegate = MoveNextCore;
            private readonly TSource defaultValue;

            private readonly IUniTaskAsyncEnumerable<TSource> source;
            private UniTask<bool>.Awaiter awaiter;
            private readonly CancellationToken cancellationToken;
            private IUniTaskAsyncEnumerator<TSource> enumerator;

            private IteratingState iteratingState;

            public _DefaultIfEmpty(IUniTaskAsyncEnumerable<TSource> source, TSource defaultValue,
                CancellationToken cancellationToken)
            {
                this.source = source;
                this.defaultValue = defaultValue;
                this.cancellationToken = cancellationToken;

                iteratingState = IteratingState.Empty;
                TaskTracker.TrackActiveTask(this, 3);
            }

            public TSource Current { get; private set; }


            public UniTask<bool> MoveNextAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();
                completionSource.Reset();

                if (iteratingState == IteratingState.Completed) return CompletedTasks.False;

                if (enumerator == null) enumerator = source.GetAsyncEnumerator(cancellationToken);

                awaiter = enumerator.MoveNextAsync().GetAwaiter();

                if (awaiter.IsCompleted)
                    MoveNextCore(this);
                else
                    awaiter.SourceOnCompleted(MoveNextCoreDelegate, this);

                return new UniTask<bool>(this, completionSource.Version);
            }

            public UniTask DisposeAsync()
            {
                TaskTracker.RemoveTracking(this);
                if (enumerator != null) return enumerator.DisposeAsync();
                return default;
            }

            private static void MoveNextCore(object state)
            {
                var self = (_DefaultIfEmpty)state;

                if (self.TryGetResult(self.awaiter, out var result))
                {
                    if (result)
                    {
                        self.iteratingState = IteratingState.Iterating;
                        self.Current = self.enumerator.Current;
                        self.completionSource.TrySetResult(true);
                    }
                    else
                    {
                        if (self.iteratingState == IteratingState.Empty)
                        {
                            self.iteratingState = IteratingState.Completed;

                            self.Current = self.defaultValue;
                            self.completionSource.TrySetResult(true);
                        }
                        else
                        {
                            self.completionSource.TrySetResult(false);
                        }
                    }
                }
            }

            private enum IteratingState : byte
            {
                Empty,
                Iterating,
                Completed
            }
        }
    }
}