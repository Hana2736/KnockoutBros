using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks.Internal;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<TSource> ToUniTaskAsyncEnumerable<TSource>(
            this IEnumerable<TSource> source)
        {
            Error.ThrowArgumentNullException(source, nameof(source));

            return new ToUniTaskAsyncEnumerable<TSource>(source);
        }

        public static IUniTaskAsyncEnumerable<TSource> ToUniTaskAsyncEnumerable<TSource>(this Task<TSource> source)
        {
            Error.ThrowArgumentNullException(source, nameof(source));

            return new ToUniTaskAsyncEnumerableTask<TSource>(source);
        }

        public static IUniTaskAsyncEnumerable<TSource> ToUniTaskAsyncEnumerable<TSource>(this UniTask<TSource> source)
        {
            return new ToUniTaskAsyncEnumerableUniTask<TSource>(source);
        }

        public static IUniTaskAsyncEnumerable<TSource> ToUniTaskAsyncEnumerable<TSource>(
            this IObservable<TSource> source)
        {
            Error.ThrowArgumentNullException(source, nameof(source));

            return new ToUniTaskAsyncEnumerableObservable<TSource>(source);
        }
    }

    internal class ToUniTaskAsyncEnumerable<T> : IUniTaskAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> source;

        public ToUniTaskAsyncEnumerable(IEnumerable<T> source)
        {
            this.source = source;
        }

        public IUniTaskAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _ToUniTaskAsyncEnumerable(source, cancellationToken);
        }

        private class _ToUniTaskAsyncEnumerable : IUniTaskAsyncEnumerator<T>
        {
            private readonly IEnumerable<T> source;
            private readonly CancellationToken cancellationToken;

            private IEnumerator<T> enumerator;

            public _ToUniTaskAsyncEnumerable(IEnumerable<T> source, CancellationToken cancellationToken)
            {
                this.source = source;
                this.cancellationToken = cancellationToken;
            }

            public T Current => enumerator.Current;

            public UniTask<bool> MoveNextAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (enumerator == null) enumerator = source.GetEnumerator();

                if (enumerator.MoveNext()) return CompletedTasks.True;

                return CompletedTasks.False;
            }

            public UniTask DisposeAsync()
            {
                enumerator.Dispose();
                return default;
            }
        }
    }

    internal class ToUniTaskAsyncEnumerableTask<T> : IUniTaskAsyncEnumerable<T>
    {
        private readonly Task<T> source;

        public ToUniTaskAsyncEnumerableTask(Task<T> source)
        {
            this.source = source;
        }

        public IUniTaskAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _ToUniTaskAsyncEnumerableTask(source, cancellationToken);
        }

        private class _ToUniTaskAsyncEnumerableTask : IUniTaskAsyncEnumerator<T>
        {
            private readonly Task<T> source;
            private bool called;
            private readonly CancellationToken cancellationToken;

            public _ToUniTaskAsyncEnumerableTask(Task<T> source, CancellationToken cancellationToken)
            {
                this.source = source;
                this.cancellationToken = cancellationToken;

                called = false;
            }

            public T Current { get; private set; }

            public async UniTask<bool> MoveNextAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (called) return false;
                called = true;

                Current = await source;
                return true;
            }

            public UniTask DisposeAsync()
            {
                return default;
            }
        }
    }

    internal class ToUniTaskAsyncEnumerableUniTask<T> : IUniTaskAsyncEnumerable<T>
    {
        private readonly UniTask<T> source;

        public ToUniTaskAsyncEnumerableUniTask(UniTask<T> source)
        {
            this.source = source;
        }

        public IUniTaskAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _ToUniTaskAsyncEnumerableUniTask(source, cancellationToken);
        }

        private class _ToUniTaskAsyncEnumerableUniTask : IUniTaskAsyncEnumerator<T>
        {
            private readonly UniTask<T> source;
            private bool called;
            private readonly CancellationToken cancellationToken;

            public _ToUniTaskAsyncEnumerableUniTask(UniTask<T> source, CancellationToken cancellationToken)
            {
                this.source = source;
                this.cancellationToken = cancellationToken;

                called = false;
            }

            public T Current { get; private set; }

            public async UniTask<bool> MoveNextAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (called) return false;
                called = true;

                Current = await source;
                return true;
            }

            public UniTask DisposeAsync()
            {
                return default;
            }
        }
    }

    internal class ToUniTaskAsyncEnumerableObservable<T> : IUniTaskAsyncEnumerable<T>
    {
        private readonly IObservable<T> source;

        public ToUniTaskAsyncEnumerableObservable(IObservable<T> source)
        {
            this.source = source;
        }

        public IUniTaskAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _ToUniTaskAsyncEnumerableObservable(source, cancellationToken);
        }

        private class _ToUniTaskAsyncEnumerableObservable : MoveNextSource, IUniTaskAsyncEnumerator<T>, IObserver<T>
        {
            private static readonly Action<object> OnCanceledDelegate = OnCanceled;
            private readonly Queue<T> queuedResult;

            private readonly IObservable<T> source;
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenRegistration cancellationTokenRegistration;
            private T current;
            private Exception error;
            private bool subscribeCompleted;
            private IDisposable subscription;


            private bool useCachedCurrent;

            public _ToUniTaskAsyncEnumerableObservable(IObservable<T> source, CancellationToken cancellationToken)
            {
                this.source = source;
                this.cancellationToken = cancellationToken;
                queuedResult = new Queue<T>();

                if (cancellationToken.CanBeCanceled)
                    cancellationTokenRegistration =
                        cancellationToken.RegisterWithoutCaptureExecutionContext(OnCanceledDelegate, this);
            }

            public void OnCompleted()
            {
                lock (queuedResult)
                {
                    subscribeCompleted = true;
                    completionSource.TrySetResult(false);
                }
            }

            public void OnError(Exception error)
            {
                lock (queuedResult)
                {
                    this.error = error;
                    completionSource.TrySetException(error);
                }
            }

            public void OnNext(T value)
            {
                lock (queuedResult)
                {
                    queuedResult.Enqueue(value);
                    completionSource.TrySetResult(true); // include callback execution, too long lock?
                }
            }

            public T Current
            {
                get
                {
                    if (useCachedCurrent) return current;

                    lock (queuedResult)
                    {
                        if (queuedResult.Count != 0)
                        {
                            current = queuedResult.Dequeue();
                            useCachedCurrent = true;
                            return current;
                        }

                        return default; // undefined.
                    }
                }
            }

            public UniTask<bool> MoveNextAsync()
            {
                lock (queuedResult)
                {
                    useCachedCurrent = false;

                    if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<bool>(cancellationToken);

                    if (subscription == null) subscription = source.Subscribe(this);

                    if (error != null) return UniTask.FromException<bool>(error);

                    if (queuedResult.Count != 0) return CompletedTasks.True;

                    if (subscribeCompleted) return CompletedTasks.False;

                    completionSource.Reset();
                    return new UniTask<bool>(this, completionSource.Version);
                }
            }

            public UniTask DisposeAsync()
            {
                subscription.Dispose();
                cancellationTokenRegistration.Dispose();
                completionSource.Reset();
                return default;
            }

            private static void OnCanceled(object state)
            {
                var self = (_ToUniTaskAsyncEnumerableObservable)state;
                lock (self.queuedResult)
                {
                    self.completionSource.TrySetCanceled(self.cancellationToken);
                }
            }
        }
    }
}