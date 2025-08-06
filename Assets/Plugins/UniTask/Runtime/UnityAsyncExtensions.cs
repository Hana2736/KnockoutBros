#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks.Internal;
using UnityEngine;
using Object = UnityEngine.Object;
#if ENABLE_UNITYWEBREQUEST && (!UNITY_2019_1_OR_NEWER || UNITASK_WEBREQUEST_SUPPORT)
using UnityEngine.Networking;
#endif

namespace Cysharp.Threading.Tasks
{
    public static partial class UnityAsyncExtensions
    {
        #region AsyncOperation

#if !UNITY_2023_1_OR_NEWER
        // from Unity2023.1.0a15, AsyncOperationAwaitableExtensions.GetAwaiter is defined in UnityEngine.
        public static AsyncOperationAwaiter GetAwaiter(this AsyncOperation asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new AsyncOperationAwaiter(asyncOperation);
        }
#endif

        public static UniTask WithCancellation(this AsyncOperation asyncOperation, CancellationToken cancellationToken)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static UniTask WithCancellation(this AsyncOperation asyncOperation, CancellationToken cancellationToken,
            bool cancelImmediately)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken,
                cancelImmediately: cancelImmediately);
        }

        public static UniTask ToUniTask(this AsyncOperation asyncOperation, IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled(cancellationToken);
            if (asyncOperation.isDone) return UniTask.CompletedTask;
            return new UniTask(
                AsyncOperationConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken,
                    cancelImmediately, out var token), token);
        }

        public struct AsyncOperationAwaiter : ICriticalNotifyCompletion
        {
            private AsyncOperation asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public AsyncOperationAwaiter(AsyncOperation asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                continuationAction = null;
            }

            public bool IsCompleted => asyncOperation.isDone;

            public void GetResult()
            {
                if (continuationAction != null)
                {
                    asyncOperation.completed -= continuationAction;
                    continuationAction = null;
                    asyncOperation = null;
                }
                else
                {
                    asyncOperation = null;
                }
            }

            public void OnCompleted(Action continuation)
            {
                UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(continuationAction);
                continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                asyncOperation.completed += continuationAction;
            }
        }

        private sealed class AsyncOperationConfiguredSource : IUniTaskSource, IPlayerLoopItem,
            ITaskPoolNode<AsyncOperationConfiguredSource>
        {
            private static TaskPool<AsyncOperationConfiguredSource> pool;

            private AsyncOperation asyncOperation;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool completed;

            private readonly Action<AsyncOperation> continuationAction;

            private UniTaskCompletionSourceCore<AsyncUnit> core;
            private AsyncOperationConfiguredSource nextNode;
            private IProgress<float> progress;

            static AsyncOperationConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AsyncOperationConfiguredSource), () => pool.Size);
            }

            private AsyncOperationConfiguredSource()
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
                    core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                return true;
            }

            public ref AsyncOperationConfiguredSource NextNode => ref nextNode;

            public void GetResult(short token)
            {
                try
                {
                    core.GetResult(token);
                }
                finally
                {
                    if (!(cancelImmediately && cancellationToken.IsCancellationRequested))
                        TryReturn();
                    else
                        TaskTracker.RemoveTracking(this);
                }
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

            public static IUniTaskSource Create(AsyncOperation asyncOperation, PlayerLoopTiming timing,
                IProgress<float> progress, CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new AsyncOperationConfiguredSource();

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
                            var source = (AsyncOperationConfiguredSource)state;
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
                    core.TrySetResult(AsyncUnit.Default);
            }
        }

        #endregion

        #region ResourceRequest

        public static ResourceRequestAwaiter GetAwaiter(this ResourceRequest asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new ResourceRequestAwaiter(asyncOperation);
        }

        public static UniTask<Object> WithCancellation(this ResourceRequest asyncOperation,
            CancellationToken cancellationToken)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static UniTask<Object> WithCancellation(this ResourceRequest asyncOperation,
            CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken,
                cancelImmediately: cancelImmediately);
        }

        public static UniTask<Object> ToUniTask(this ResourceRequest asyncOperation, IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<Object>(cancellationToken);
            if (asyncOperation.isDone) return UniTask.FromResult(asyncOperation.asset);
            return new UniTask<Object>(
                ResourceRequestConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken,
                    cancelImmediately, out var token), token);
        }

        public struct ResourceRequestAwaiter : ICriticalNotifyCompletion
        {
            private ResourceRequest asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public ResourceRequestAwaiter(ResourceRequest asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                continuationAction = null;
            }

            public bool IsCompleted => asyncOperation.isDone;

            public Object GetResult()
            {
                if (continuationAction != null)
                {
                    asyncOperation.completed -= continuationAction;
                    continuationAction = null;
                    var result = asyncOperation.asset;
                    asyncOperation = null;
                    return result;
                }
                else
                {
                    var result = asyncOperation.asset;
                    asyncOperation = null;
                    return result;
                }
            }

            public void OnCompleted(Action continuation)
            {
                UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(continuationAction);
                continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                asyncOperation.completed += continuationAction;
            }
        }

        private sealed class ResourceRequestConfiguredSource : IUniTaskSource<Object>, IPlayerLoopItem,
            ITaskPoolNode<ResourceRequestConfiguredSource>
        {
            private static TaskPool<ResourceRequestConfiguredSource> pool;

            private ResourceRequest asyncOperation;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool completed;

            private readonly Action<AsyncOperation> continuationAction;

            private UniTaskCompletionSourceCore<Object> core;
            private ResourceRequestConfiguredSource nextNode;
            private IProgress<float> progress;

            static ResourceRequestConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(ResourceRequestConfiguredSource), () => pool.Size);
            }

            private ResourceRequestConfiguredSource()
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
                    core.TrySetResult(asyncOperation.asset);
                    return false;
                }

                return true;
            }

            public ref ResourceRequestConfiguredSource NextNode => ref nextNode;

            public Object GetResult(short token)
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

            public static IUniTaskSource<Object> Create(ResourceRequest asyncOperation, PlayerLoopTiming timing,
                IProgress<float> progress, CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource<Object>.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new ResourceRequestConfiguredSource();

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
                            var source = (ResourceRequestConfiguredSource)state;
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
                    core.TrySetResult(asyncOperation.asset);
            }
        }

        #endregion

#if UNITASK_ASSETBUNDLE_SUPPORT

        #region AssetBundleRequest

        public static AssetBundleRequestAwaiter GetAwaiter(this AssetBundleRequest asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new AssetBundleRequestAwaiter(asyncOperation);
        }

        public static UniTask<Object> WithCancellation(this AssetBundleRequest asyncOperation,
            CancellationToken cancellationToken)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static UniTask<Object> WithCancellation(this AssetBundleRequest asyncOperation,
            CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken,
                cancelImmediately: cancelImmediately);
        }

        public static UniTask<Object> ToUniTask(this AssetBundleRequest asyncOperation,
            IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<Object>(cancellationToken);
            if (asyncOperation.isDone) return UniTask.FromResult(asyncOperation.asset);
            return new UniTask<Object>(
                AssetBundleRequestConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken,
                    cancelImmediately, out var token), token);
        }

        public struct AssetBundleRequestAwaiter : ICriticalNotifyCompletion
        {
            private AssetBundleRequest asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public AssetBundleRequestAwaiter(AssetBundleRequest asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                continuationAction = null;
            }

            public bool IsCompleted => asyncOperation.isDone;

            public Object GetResult()
            {
                if (continuationAction != null)
                {
                    asyncOperation.completed -= continuationAction;
                    continuationAction = null;
                    var result = asyncOperation.asset;
                    asyncOperation = null;
                    return result;
                }
                else
                {
                    var result = asyncOperation.asset;
                    asyncOperation = null;
                    return result;
                }
            }

            public void OnCompleted(Action continuation)
            {
                UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(continuationAction);
                continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                asyncOperation.completed += continuationAction;
            }
        }

        private sealed class AssetBundleRequestConfiguredSource : IUniTaskSource<Object>, IPlayerLoopItem,
            ITaskPoolNode<AssetBundleRequestConfiguredSource>
        {
            private static TaskPool<AssetBundleRequestConfiguredSource> pool;

            private AssetBundleRequest asyncOperation;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool completed;

            private readonly Action<AsyncOperation> continuationAction;

            private UniTaskCompletionSourceCore<Object> core;
            private AssetBundleRequestConfiguredSource nextNode;
            private IProgress<float> progress;

            static AssetBundleRequestConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AssetBundleRequestConfiguredSource), () => pool.Size);
            }

            private AssetBundleRequestConfiguredSource()
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
                    core.TrySetResult(asyncOperation.asset);
                    return false;
                }

                return true;
            }

            public ref AssetBundleRequestConfiguredSource NextNode => ref nextNode;

            public Object GetResult(short token)
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

            public static IUniTaskSource<Object> Create(AssetBundleRequest asyncOperation, PlayerLoopTiming timing,
                IProgress<float> progress, CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource<Object>.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new AssetBundleRequestConfiguredSource();

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
                            var source = (AssetBundleRequestConfiguredSource)state;
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
                    core.TrySetResult(asyncOperation.asset);
            }
        }

        #endregion

#endif

#if UNITASK_ASSETBUNDLE_SUPPORT

        #region AssetBundleCreateRequest

        public static AssetBundleCreateRequestAwaiter GetAwaiter(this AssetBundleCreateRequest asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new AssetBundleCreateRequestAwaiter(asyncOperation);
        }

        public static UniTask<AssetBundle> WithCancellation(this AssetBundleCreateRequest asyncOperation,
            CancellationToken cancellationToken)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static UniTask<AssetBundle> WithCancellation(this AssetBundleCreateRequest asyncOperation,
            CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken,
                cancelImmediately: cancelImmediately);
        }

        public static UniTask<AssetBundle> ToUniTask(this AssetBundleCreateRequest asyncOperation,
            IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<AssetBundle>(cancellationToken);
            if (asyncOperation.isDone) return UniTask.FromResult(asyncOperation.assetBundle);
            return new UniTask<AssetBundle>(
                AssetBundleCreateRequestConfiguredSource.Create(asyncOperation, timing, progress, cancellationToken,
                    cancelImmediately, out var token), token);
        }

        public struct AssetBundleCreateRequestAwaiter : ICriticalNotifyCompletion
        {
            private AssetBundleCreateRequest asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public AssetBundleCreateRequestAwaiter(AssetBundleCreateRequest asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                continuationAction = null;
            }

            public bool IsCompleted => asyncOperation.isDone;

            public AssetBundle GetResult()
            {
                if (continuationAction != null)
                {
                    asyncOperation.completed -= continuationAction;
                    continuationAction = null;
                    var result = asyncOperation.assetBundle;
                    asyncOperation = null;
                    return result;
                }
                else
                {
                    var result = asyncOperation.assetBundle;
                    asyncOperation = null;
                    return result;
                }
            }

            public void OnCompleted(Action continuation)
            {
                UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(continuationAction);
                continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                asyncOperation.completed += continuationAction;
            }
        }

        private sealed class AssetBundleCreateRequestConfiguredSource : IUniTaskSource<AssetBundle>, IPlayerLoopItem,
            ITaskPoolNode<AssetBundleCreateRequestConfiguredSource>
        {
            private static TaskPool<AssetBundleCreateRequestConfiguredSource> pool;

            private AssetBundleCreateRequest asyncOperation;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool completed;

            private readonly Action<AsyncOperation> continuationAction;

            private UniTaskCompletionSourceCore<AssetBundle> core;
            private AssetBundleCreateRequestConfiguredSource nextNode;
            private IProgress<float> progress;

            static AssetBundleCreateRequestConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(AssetBundleCreateRequestConfiguredSource), () => pool.Size);
            }

            private AssetBundleCreateRequestConfiguredSource()
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
                    core.TrySetResult(asyncOperation.assetBundle);
                    return false;
                }

                return true;
            }

            public ref AssetBundleCreateRequestConfiguredSource NextNode => ref nextNode;

            public AssetBundle GetResult(short token)
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

            public static IUniTaskSource<AssetBundle> Create(AssetBundleCreateRequest asyncOperation,
                PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken,
                bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource<AssetBundle>.CreateFromCanceled(cancellationToken,
                        out token);

                if (!pool.TryPop(out var result)) result = new AssetBundleCreateRequestConfiguredSource();

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
                            var source = (AssetBundleCreateRequestConfiguredSource)state;
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
                    core.TrySetResult(asyncOperation.assetBundle);
            }
        }

        #endregion

#endif

#if ENABLE_UNITYWEBREQUEST && (!UNITY_2019_1_OR_NEWER || UNITASK_WEBREQUEST_SUPPORT)

        #region UnityWebRequestAsyncOperation

        public static UnityWebRequestAsyncOperationAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOperation)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            return new UnityWebRequestAsyncOperationAwaiter(asyncOperation);
        }

        public static UniTask<UnityWebRequest> WithCancellation(this UnityWebRequestAsyncOperation asyncOperation,
            CancellationToken cancellationToken)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken);
        }

        public static UniTask<UnityWebRequest> WithCancellation(this UnityWebRequestAsyncOperation asyncOperation,
            CancellationToken cancellationToken, bool cancelImmediately)
        {
            return ToUniTask(asyncOperation, cancellationToken: cancellationToken,
                cancelImmediately: cancelImmediately);
        }

        public static UniTask<UnityWebRequest> ToUniTask(this UnityWebRequestAsyncOperation asyncOperation,
            IProgress<float> progress = null, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            Error.ThrowArgumentNullException(asyncOperation, nameof(asyncOperation));
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<UnityWebRequest>(cancellationToken);
            if (asyncOperation.isDone)
            {
                if (asyncOperation.webRequest.IsError())
                    return UniTask.FromException<UnityWebRequest>(
                        new UnityWebRequestException(asyncOperation.webRequest));
                return UniTask.FromResult(asyncOperation.webRequest);
            }

            return new UniTask<UnityWebRequest>(
                UnityWebRequestAsyncOperationConfiguredSource.Create(asyncOperation, timing, progress,
                    cancellationToken, cancelImmediately, out var token), token);
        }

        public struct UnityWebRequestAsyncOperationAwaiter : ICriticalNotifyCompletion
        {
            private UnityWebRequestAsyncOperation asyncOperation;
            private Action<AsyncOperation> continuationAction;

            public UnityWebRequestAsyncOperationAwaiter(UnityWebRequestAsyncOperation asyncOperation)
            {
                this.asyncOperation = asyncOperation;
                continuationAction = null;
            }

            public bool IsCompleted => asyncOperation.isDone;

            public UnityWebRequest GetResult()
            {
                if (continuationAction != null)
                {
                    asyncOperation.completed -= continuationAction;
                    continuationAction = null;
                    var result = asyncOperation.webRequest;
                    asyncOperation = null;
                    if (result.IsError()) throw new UnityWebRequestException(result);
                    return result;
                }
                else
                {
                    var result = asyncOperation.webRequest;
                    asyncOperation = null;
                    if (result.IsError()) throw new UnityWebRequestException(result);
                    return result;
                }
            }

            public void OnCompleted(Action continuation)
            {
                UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                Error.ThrowWhenContinuationIsAlreadyRegistered(continuationAction);
                continuationAction = PooledDelegate<AsyncOperation>.Create(continuation);
                asyncOperation.completed += continuationAction;
            }
        }

        private sealed class UnityWebRequestAsyncOperationConfiguredSource : IUniTaskSource<UnityWebRequest>,
            IPlayerLoopItem, ITaskPoolNode<UnityWebRequestAsyncOperationConfiguredSource>
        {
            private static TaskPool<UnityWebRequestAsyncOperationConfiguredSource> pool;

            private UnityWebRequestAsyncOperation asyncOperation;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private bool completed;

            private readonly Action<AsyncOperation> continuationAction;

            private UniTaskCompletionSourceCore<UnityWebRequest> core;
            private UnityWebRequestAsyncOperationConfiguredSource nextNode;
            private IProgress<float> progress;

            static UnityWebRequestAsyncOperationConfiguredSource()
            {
                TaskPool.RegisterSizeGetter(typeof(UnityWebRequestAsyncOperationConfiguredSource), () => pool.Size);
            }

            private UnityWebRequestAsyncOperationConfiguredSource()
            {
                continuationAction = Continuation;
            }

            public bool MoveNext()
            {
                // Already completed
                if (completed || asyncOperation == null) return false;

                if (cancellationToken.IsCancellationRequested)
                {
                    asyncOperation.webRequest.Abort();
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (progress != null) progress.Report(asyncOperation.progress);

                if (asyncOperation.isDone)
                {
                    if (asyncOperation.webRequest.IsError())
                        core.TrySetException(new UnityWebRequestException(asyncOperation.webRequest));
                    else
                        core.TrySetResult(asyncOperation.webRequest);
                    return false;
                }

                return true;
            }

            public ref UnityWebRequestAsyncOperationConfiguredSource NextNode => ref nextNode;

            public UnityWebRequest GetResult(short token)
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

            public static IUniTaskSource<UnityWebRequest> Create(UnityWebRequestAsyncOperation asyncOperation,
                PlayerLoopTiming timing, IProgress<float> progress, CancellationToken cancellationToken,
                bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource<UnityWebRequest>.CreateFromCanceled(cancellationToken,
                        out token);

                if (!pool.TryPop(out var result)) result = new UnityWebRequestAsyncOperationConfiguredSource();

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
                            var source = (UnityWebRequestAsyncOperationConfiguredSource)state;
                            source.asyncOperation.webRequest.Abort();
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
                else if (asyncOperation.webRequest.IsError())
                    core.TrySetException(new UnityWebRequestException(asyncOperation.webRequest));
                else
                    core.TrySetResult(asyncOperation.webRequest);
            }
        }

        #endregion

#endif
    }
}