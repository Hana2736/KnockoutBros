// AsyncInstantiateOperation was added since Unity 2022.3.20 / 2023.3.0b7

#if UNITY_2022_3 && !(UNITY_2022_3_0 || UNITY_2022_3_1 || UNITY_2022_3_2 || UNITY_2022_3_3 || UNITY_2022_3_4 || UNITY_2022_3_5 || UNITY_2022_3_6 || UNITY_2022_3_7 || UNITY_2022_3_8 || UNITY_2022_3_9 || UNITY_2022_3_10 || UNITY_2022_3_11 || UNITY_2022_3_12 || UNITY_2022_3_13 || UNITY_2022_3_14 || UNITY_2022_3_15 || UNITY_2022_3_16 || UNITY_2022_3_17 || UNITY_2022_3_18 || UNITY_2022_3_19)
#define UNITY_2022_SUPPORT
#endif

#if UNITY_2022_SUPPORT || UNITY_2023_3_OR_NEWER

using System;
using System.Threading;
using Cysharp.Threading.Tasks.Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cysharp.Threading.Tasks
{
    public static class AsyncInstantiateOperationExtensions
    {
        // AsyncInstantiateOperation<T> has GetAwaiter so no need to impl
        // public static UniTask<T[]>.Awaiter GetAwaiter<T>(this AsyncInstantiateOperation<T> operation) where T : Object

        public static UniTask<Object[]> WithCancellation<T>(this AsyncInstantiateOperation asyncOperation,
            CancellationToken cancellationToken)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static UniTask<Object[]> WithCancellation<T>(this AsyncInstantiateOperation asyncOperation,
            CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken,
                cancelImmediately: cancelImmediately);
        }

        public static UniTask<Object[]> ToUniTask(this AsyncInstantiateOperation asyncOperation,
            IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<Object[]>(cancellationToken);
            if (asyncOperation.isDone) return UniTask.FromResult(asyncOperation.Result);
            return new UniTask<Object[]>(
                AsyncInstantiateOperationConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken,
                    cancelImmediately, out var token), token);
        }

        public static UniTask<T[]> WithCancellation<T>(this AsyncInstantiateOperation<T> asyncOperation,
            CancellationToken cancellationToken)
            where T : Object
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static UniTask<T[]> WithCancellation<T>(this AsyncInstantiateOperation<T> asyncOperation,
            CancellationToken cancellationToken, bool cancelImmediately)
            where T : Object
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken,
                cancelImmediately: cancelImmediately);
        }

        public static UniTask<T[]> ToUniTask<T>(this AsyncInstantiateOperation<T> asyncOperation,
            IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
            where T : Object
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<T[]>(cancellationToken);
            if (asyncOperation.isDone) return UniTask.FromResult(asyncOperation.Result);
            return new UniTask<T[]>(
                AsyncInstantiateOperationConfiguredSource<T>.Create(asyncOperation, timing, progress, cancellationToken,
                    cancelImmediately, out var token), token);
        }

        private sealed class AsyncInstantiateOperationConfiguredSource : IUniTaskSource<Object[]>, IPlayerLoopItem,
            ITaskPoolNode<AsyncInstantiateOperationConfiguredSource>
        {
            private static TaskPool<AsyncInstantiateOperationConfiguredSource> pool;

            private AsyncInstantiateOperation asyncOperation;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool completed;

            private readonly Action<AsyncOperation> continuationAction;

            private UniTaskCompletionSourceCore<Object[]> core;
            private AsyncInstantiateOperationConfiguredSource nextNode;
            private IProgress<float> progress;

            static AsyncInstantiateOperationConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AsyncInstantiateOperationConfiguredSource), () => pool.Size);
            }

            private AsyncInstantiateOperationConfiguredSource()
            {
                continuationAction = Continuation;
            }

            public bool MoveNext()
            {
                // Already completed
                if (completed || asyncOperation == null) return false;

                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (progress != null) progress.Report(asyncOperation.progress);

                if (asyncOperation.isDone)
                {
                    core.TrySetResult(asyncOperation.Result);
                    return false;
                }

                return true;
            }

            public ref AsyncInstantiateOperationConfiguredSource NextNode => ref nextNode;

            public Object[] GetResult(short token)
            {
                try
                {
                    return core.GetResult(token);
                }
                finally
                {
                    if (!(cancelImmediately && cancellationToken.IsCancellationRequested))
                        TryReturn();
                    else
                        TaskTracker.RemoveTracking(this);
                }
            }

            void IUniTaskSource.GetResult(short token)
            {
                GetResult(token);
            }

            public UniTaskStatus GetStatus(short token)
            {
                return core.GetStatus(token);
            }

            public UniTaskStatus UnsafeGetStatus()
            {
                return core.UnsafeGetStatus();
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                core.OnCompleted(continuation, state, token);
            }

            public static IUniTaskSource<Object[]> Create(AsyncInstantiateOperation asyncOperation,
                PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken,
                bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource<Object[]>.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new AsyncInstantiateOperationConfiguredSource();

                result.asyncOperation = asyncOperation;
                result.progress = progress;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;
                result.completed = false;

                asyncOperation.completed += result.continuationAction;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var source = (AsyncInstantiateOperationConfiguredSource)state;
                            source.core.TrySetCanceled(source.cancellationToken);
                        }, result);

                TaskTracker.TrackActiveTask(result, 3);

                PlayerLoopHelper.AddAction(timing, result);

                token = result.core.Version;
                return result;
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                core.Reset();
                asyncOperation.completed -= continuationAction;
                asyncOperation = default;
                progress = default;
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                cancelImmediately = default;
                return pool.TryPush(this);
            }

            private void Continuation(AsyncOperation _)
            {
                if (completed) return;
                completed = true;
                if (cancellationToken.IsCancellationRequested)
                    core.TrySetCanceled(cancellationToken);
                else
                    core.TrySetResult(asyncOperation.Result);
            }
        }

        private sealed class AsyncInstantiateOperationConfiguredSource<T> : IUniTaskSource<T[]>, IPlayerLoopItem,
            ITaskPoolNode<AsyncInstantiateOperationConfiguredSource<T>>
            where T : Object
        {
            private static TaskPool<AsyncInstantiateOperationConfiguredSource<T>> pool;

            private AsyncInstantiateOperation<T> asyncOperation;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool completed;

            private readonly Action<AsyncOperation> continuationAction;

            private UniTaskCompletionSourceCore<T[]> core;
            private AsyncInstantiateOperationConfiguredSource<T> nextNode;
            private IProgress<float> progress;

            static AsyncInstantiateOperationConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AsyncInstantiateOperationConfiguredSource<T>), () => pool.Size);
            }

            private AsyncInstantiateOperationConfiguredSource()
            {
                continuationAction = Continuation;
            }

            public bool MoveNext()
            {
                // Already completed
                if (completed || asyncOperation == null) return false;

                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (progress != null) progress.Report(asyncOperation.progress);

                if (asyncOperation.isDone)
                {
                    core.TrySetResult(asyncOperation.Result);
                    return false;
                }

                return true;
            }

            public ref AsyncInstantiateOperationConfiguredSource<T> NextNode => ref nextNode;

            public T[] GetResult(short token)
            {
                try
                {
                    return core.GetResult(token);
                }
                finally
                {
                    if (!(cancelImmediately && cancellationToken.IsCancellationRequested))
                        TryReturn();
                    else
                        TaskTracker.RemoveTracking(this);
                }
            }

            void IUniTaskSource.GetResult(short token)
            {
                GetResult(token);
            }

            public UniTaskStatus GetStatus(short token)
            {
                return core.GetStatus(token);
            }

            public UniTaskStatus UnsafeGetStatus()
            {
                return core.UnsafeGetStatus();
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                core.OnCompleted(continuation, state, token);
            }

            public static IUniTaskSource<T[]> Create(AsyncInstantiateOperation<T> asyncOperation,
                PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken,
                bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource<T[]>.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new AsyncInstantiateOperationConfiguredSource<T>();

                result.asyncOperation = asyncOperation;
                result.progress = progress;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;
                result.completed = false;

                asyncOperation.completed += result.continuationAction;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var source = (AsyncInstantiateOperationConfiguredSource<T>)state;
                            source.core.TrySetCanceled(source.cancellationToken);
                        }, result);

                TaskTracker.TrackActiveTask(result, 3);

                PlayerLoopHelper.AddAction(timing, result);

                token = result.core.Version;
                return result;
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                core.Reset();
                asyncOperation.completed -= continuationAction;
                asyncOperation = default;
                progress = default;
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                cancelImmediately = default;
                return pool.TryPush(this);
            }

            private void Continuation(AsyncOperation _)
            {
                if (completed) return;
                completed = true;
                if (cancellationToken.IsCancellationRequested)
                    core.TrySetCanceled(cancellationToken);
                else
                    core.TrySetResult(asyncOperation.Result);
            }
        }
    }
}

#endif