#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks.Internal;
using UnityEditor;
using UnityEngine;

namespace Cysharp.Threading.Tasks
{
    public enum DelayType
    {
        /// <summary>use Time.deltaTime.</summary>
        DeltaTime,

        /// <summary>Ignore timescale, use Time.unscaledDeltaTime.</summary>
        UnscaledDeltaTime,

        /// <summary>use Stopwatch.GetTimestamp().</summary>
        Realtime
    }

    public partial struct UniTask
    {
        public static YieldAwaitable Yield()
        {
            // optimized for single continuation
            return new YieldAwaitable(PlayerLoopTiming.Update);
        }

        public static YieldAwaitable Yield(PlayerLoopTiming timing)
        {
            // optimized for single continuation
            return new YieldAwaitable(timing);
        }

        public static UniTask Yield(CancellationToken cancellationToken, bool cancelImmediately = false)
        {
            return new UniTask(
                YieldPromise.Create(PlayerLoopTiming.Update, cancellationToken, cancelImmediately, out var token),
                token);
        }

        public static UniTask Yield(PlayerLoopTiming timing, CancellationToken cancellationToken,
            bool cancelImmediately = false)
        {
            return new UniTask(YieldPromise.Create(timing, cancellationToken, cancelImmediately, out var token), token);
        }

        /// <summary>
        ///     Similar as UniTask.Yield but guaranteed run on next frame.
        /// </summary>
        public static UniTask NextFrame()
        {
            return new UniTask(
                NextFramePromise.Create(PlayerLoopTiming.Update, CancellationToken.None, false, out var token), token);
        }

        /// <summary>
        ///     Similar as UniTask.Yield but guaranteed run on next frame.
        /// </summary>
        public static UniTask NextFrame(PlayerLoopTiming timing)
        {
            return new UniTask(NextFramePromise.Create(timing, CancellationToken.None, false, out var token), token);
        }

        /// <summary>
        ///     Similar as UniTask.Yield but guaranteed run on next frame.
        /// </summary>
        public static UniTask NextFrame(CancellationToken cancellationToken, bool cancelImmediately = false)
        {
            return new UniTask(
                NextFramePromise.Create(PlayerLoopTiming.Update, cancellationToken, cancelImmediately, out var token),
                token);
        }

        /// <summary>
        ///     Similar as UniTask.Yield but guaranteed run on next frame.
        /// </summary>
        public static UniTask NextFrame(PlayerLoopTiming timing, CancellationToken cancellationToken,
            bool cancelImmediately = false)
        {
            return new UniTask(NextFramePromise.Create(timing, cancellationToken, cancelImmediately, out var token),
                token);
        }

#if UNITY_2023_1_OR_NEWER
        public static async UniTask WaitForEndOfFrame(CancellationToken cancellationToken = default)
        {
            await Awaitable.EndOfFrameAsync(cancellationToken);
        }
#else
        [Obsolete("Use WaitForEndOfFrame(MonoBehaviour) instead or UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate). Equivalent for coroutine's WaitForEndOfFrame requires MonoBehaviour(runner of Coroutine).")]
        public static YieldAwaitable WaitForEndOfFrame()
        {
            return UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        }

        [Obsolete("Use WaitForEndOfFrame(MonoBehaviour) instead or UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate). Equivalent for coroutine's WaitForEndOfFrame requires MonoBehaviour(runner of Coroutine).")]
        public static UniTask WaitForEndOfFrame(CancellationToken cancellationToken, bool cancelImmediately = false)
        {
            return UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken, cancelImmediately);
        }
#endif

        public static UniTask WaitForEndOfFrame(MonoBehaviour coroutineRunner)
        {
            var source = WaitForEndOfFramePromise.Create(coroutineRunner, CancellationToken.None, false, out var token);
            return new UniTask(source, token);
        }

        public static UniTask WaitForEndOfFrame(MonoBehaviour coroutineRunner, CancellationToken cancellationToken,
            bool cancelImmediately = false)
        {
            var source =
                WaitForEndOfFramePromise.Create(coroutineRunner, cancellationToken, cancelImmediately, out var token);
            return new UniTask(source, token);
        }

        /// <summary>
        ///     Same as UniTask.Yield(PlayerLoopTiming.LastFixedUpdate).
        /// </summary>
        public static YieldAwaitable WaitForFixedUpdate()
        {
            // use LastFixedUpdate instead of FixedUpdate
            // https://github.com/Cysharp/UniTask/issues/377
            return Yield(PlayerLoopTiming.LastFixedUpdate);
        }

        /// <summary>
        ///     Same as UniTask.Yield(PlayerLoopTiming.LastFixedUpdate, cancellationToken).
        /// </summary>
        public static UniTask WaitForFixedUpdate(CancellationToken cancellationToken, bool cancelImmediately = false)
        {
            return Yield(PlayerLoopTiming.LastFixedUpdate, cancellationToken, cancelImmediately);
        }

        public static UniTask WaitForSeconds(float duration, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            return Delay(Mathf.RoundToInt(1000 * duration), ignoreTimeScale, delayTiming, cancellationToken,
                cancelImmediately);
        }

        public static UniTask WaitForSeconds(int duration, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            return Delay(1000 * duration, ignoreTimeScale, delayTiming, cancellationToken, cancelImmediately);
        }

        public static UniTask DelayFrame(int delayFrameCount, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default, bool cancelImmediately = false)
        {
            if (delayFrameCount < 0)
                throw new ArgumentOutOfRangeException("Delay does not allow minus delayFrameCount. delayFrameCount:" +
                                                      delayFrameCount);

            return new UniTask(
                DelayFramePromise.Create(delayFrameCount, delayTiming, cancellationToken, cancelImmediately,
                    out var token), token);
        }

        public static UniTask Delay(int millisecondsDelay, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            var delayTimeSpan = TimeSpan.FromMilliseconds(millisecondsDelay);
            return Delay(delayTimeSpan, ignoreTimeScale, delayTiming, cancellationToken, cancelImmediately);
        }

        public static UniTask Delay(TimeSpan delayTimeSpan, bool ignoreTimeScale = false,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            var delayType = ignoreTimeScale ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
            return Delay(delayTimeSpan, delayType, delayTiming, cancellationToken, cancelImmediately);
        }

        public static UniTask Delay(int millisecondsDelay, DelayType delayType,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            var delayTimeSpan = TimeSpan.FromMilliseconds(millisecondsDelay);
            return Delay(delayTimeSpan, delayType, delayTiming, cancellationToken, cancelImmediately);
        }

        public static UniTask Delay(TimeSpan delayTimeSpan, DelayType delayType,
            PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            if (delayTimeSpan < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("Delay does not allow minus delayTimeSpan. delayTimeSpan:" +
                                                      delayTimeSpan);

#if UNITY_EDITOR
            // force use Realtime.
            if (PlayerLoopHelper.IsMainThread && !EditorApplication.isPlaying) delayType = DelayType.Realtime;
#endif

            switch (delayType)
            {
                case DelayType.UnscaledDeltaTime:
                {
                    return new UniTask(
                        DelayIgnoreTimeScalePromise.Create(delayTimeSpan, delayTiming, cancellationToken,
                            cancelImmediately, out var token), token);
                }
                case DelayType.Realtime:
                {
                    return new UniTask(
                        DelayRealtimePromise.Create(delayTimeSpan, delayTiming, cancellationToken, cancelImmediately,
                            out var token), token);
                }
                case DelayType.DeltaTime:
                default:
                {
                    return new UniTask(
                        DelayPromise.Create(delayTimeSpan, delayTiming, cancellationToken, cancelImmediately,
                            out var token), token);
                }
            }
        }

        private sealed class YieldPromise : IUniTaskSource, IPlayerLoopItem, ITaskPoolNode<YieldPromise>
        {
            private static TaskPool<YieldPromise> pool;
            private bool cancelImmediately;

            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private UniTaskCompletionSourceCore<object> core;
            private YieldPromise nextNode;

            static YieldPromise()
            {
                TaskPool.RegisterSizeGetter(typeof(YieldPromise), () => pool.Size);
            }

            private YieldPromise()
            {
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                core.TrySetResult(null);
                return false;
            }

            public ref YieldPromise NextNode => ref nextNode;

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

            public static IUniTaskSource Create(PlayerLoopTiming timing, CancellationToken cancellationToken,
                bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new YieldPromise();

                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var promise = (YieldPromise)state;
                            promise.core.TrySetCanceled(promise.cancellationToken);
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
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                cancelImmediately = default;
                return pool.TryPush(this);
            }
        }

        private sealed class NextFramePromise : IUniTaskSource, IPlayerLoopItem, ITaskPoolNode<NextFramePromise>
        {
            private static TaskPool<NextFramePromise> pool;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private UniTaskCompletionSourceCore<AsyncUnit> core;

            private int frameCount;
            private NextFramePromise nextNode;

            static NextFramePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(NextFramePromise), () => pool.Size);
            }

            private NextFramePromise()
            {
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (frameCount == Time.frameCount) return true;

                core.TrySetResult(AsyncUnit.Default);
                return false;
            }

            public ref NextFramePromise NextNode => ref nextNode;

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

            public static IUniTaskSource Create(PlayerLoopTiming timing, CancellationToken cancellationToken,
                bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new NextFramePromise();

                result.frameCount = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var promise = (NextFramePromise)state;
                            promise.core.TrySetCanceled(promise.cancellationToken);
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
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                return pool.TryPush(this);
            }
        }

        private sealed class WaitForEndOfFramePromise : IUniTaskSource, ITaskPoolNode<WaitForEndOfFramePromise>,
            IEnumerator
        {
            private static TaskPool<WaitForEndOfFramePromise> pool;

            // Coroutine Runner implementation

            private static readonly WaitForEndOfFrame waitForEndOfFrameYieldInstruction = new();
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;

            private UniTaskCompletionSourceCore<object> core;
            private bool isFirst = true;
            private WaitForEndOfFramePromise nextNode;

            static WaitForEndOfFramePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(WaitForEndOfFramePromise), () => pool.Size);
            }

            private WaitForEndOfFramePromise()
            {
            }

            object IEnumerator.Current => waitForEndOfFrameYieldInstruction;

            bool IEnumerator.MoveNext()
            {
                if (isFirst)
                {
                    isFirst = false;
                    return true; // start WaitForEndOfFrame
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                core.TrySetResult(null);
                return false;
            }

            public void Reset()
            {
                isFirst = true;
            }

            public ref WaitForEndOfFramePromise NextNode => ref nextNode;

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

            public static IUniTaskSource Create(MonoBehaviour coroutineRunner, CancellationToken cancellationToken,
                bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new WaitForEndOfFramePromise();

                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var promise = (WaitForEndOfFramePromise)state;
                            promise.core.TrySetCanceled(promise.cancellationToken);
                        }, result);

                TaskTracker.TrackActiveTask(result, 3);

                coroutineRunner.StartCoroutine(result);

                token = result.core.Version;
                return result;
            }

            private bool TryReturn()
            {
                TaskTracker.RemoveTracking(this);
                core.Reset();
                Reset(); // Reset Enumerator
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                return pool.TryPush(this);
            }
        }

        private sealed class DelayFramePromise : IUniTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayFramePromise>
        {
            private static TaskPool<DelayFramePromise> pool;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;
            private UniTaskCompletionSourceCore<AsyncUnit> core;

            private int currentFrameCount;
            private int delayFrameCount;

            private int initialFrame;
            private DelayFramePromise nextNode;

            static DelayFramePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayFramePromise), () => pool.Size);
            }

            private DelayFramePromise()
            {
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (currentFrameCount == 0)
                {
                    if (delayFrameCount == 0) // same as Yield
                    {
                        core.TrySetResult(AsyncUnit.Default);
                        return false;
                    }

                    // skip in initial frame.
                    if (initialFrame == Time.frameCount)
                    {
#if UNITY_EDITOR
                        // force use Realtime.
                        if (PlayerLoopHelper.IsMainThread && !EditorApplication.isPlaying)
                        {
                            //goto ++currentFrameCount
                        }
                        else
                        {
                            return true;
                        }
#else
                        return true;
#endif
                    }
                }

                if (++currentFrameCount >= delayFrameCount)
                {
                    core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                return true;
            }

            public ref DelayFramePromise NextNode => ref nextNode;

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

            public static IUniTaskSource Create(int delayFrameCount, PlayerLoopTiming timing,
                CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new DelayFramePromise();

                result.delayFrameCount = delayFrameCount;
                result.cancellationToken = cancellationToken;
                result.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
                result.cancelImmediately = cancelImmediately;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var promise = (DelayFramePromise)state;
                            promise.core.TrySetCanceled(promise.cancellationToken);
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
                currentFrameCount = default;
                delayFrameCount = default;
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                cancelImmediately = default;
                return pool.TryPush(this);
            }
        }

        private sealed class DelayPromise : IUniTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayPromise>
        {
            private static TaskPool<DelayPromise> pool;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;

            private UniTaskCompletionSourceCore<object> core;
            private float delayTimeSpan;
            private float elapsed;

            private int initialFrame;
            private DelayPromise nextNode;

            static DelayPromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayPromise), () => pool.Size);
            }

            private DelayPromise()
            {
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (elapsed == 0.0f)
                    if (initialFrame == Time.frameCount)
                        return true;

                elapsed += Time.deltaTime;
                if (elapsed >= delayTimeSpan)
                {
                    core.TrySetResult(null);
                    return false;
                }

                return true;
            }

            public ref DelayPromise NextNode => ref nextNode;

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

            public static IUniTaskSource Create(TimeSpan delayTimeSpan, PlayerLoopTiming timing,
                CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new DelayPromise();

                result.elapsed = 0.0f;
                result.delayTimeSpan = (float)delayTimeSpan.TotalSeconds;
                result.cancellationToken = cancellationToken;
                result.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
                result.cancelImmediately = cancelImmediately;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var promise = (DelayPromise)state;
                            promise.core.TrySetCanceled(promise.cancellationToken);
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
                delayTimeSpan = default;
                elapsed = default;
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                cancelImmediately = default;
                return pool.TryPush(this);
            }
        }

        private sealed class DelayIgnoreTimeScalePromise : IUniTaskSource, IPlayerLoopItem,
            ITaskPoolNode<DelayIgnoreTimeScalePromise>
        {
            private static TaskPool<DelayIgnoreTimeScalePromise> pool;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;

            private UniTaskCompletionSourceCore<object> core;

            private float delayFrameTimeSpan;
            private float elapsed;
            private int initialFrame;
            private DelayIgnoreTimeScalePromise nextNode;

            static DelayIgnoreTimeScalePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayIgnoreTimeScalePromise), () => pool.Size);
            }

            private DelayIgnoreTimeScalePromise()
            {
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (elapsed == 0.0f)
                    if (initialFrame == Time.frameCount)
                        return true;

                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= delayFrameTimeSpan)
                {
                    core.TrySetResult(null);
                    return false;
                }

                return true;
            }

            public ref DelayIgnoreTimeScalePromise NextNode => ref nextNode;

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

            public static IUniTaskSource Create(TimeSpan delayFrameTimeSpan, PlayerLoopTiming timing,
                CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new DelayIgnoreTimeScalePromise();

                result.elapsed = 0.0f;
                result.delayFrameTimeSpan = (float)delayFrameTimeSpan.TotalSeconds;
                result.initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var promise = (DelayIgnoreTimeScalePromise)state;
                            promise.core.TrySetCanceled(promise.cancellationToken);
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
                delayFrameTimeSpan = default;
                elapsed = default;
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                cancelImmediately = default;
                return pool.TryPush(this);
            }
        }

        private sealed class DelayRealtimePromise : IUniTaskSource, IPlayerLoopItem, ITaskPoolNode<DelayRealtimePromise>
        {
            private static TaskPool<DelayRealtimePromise> pool;
            private bool cancelImmediately;
            private CancellationToken cancellationToken;
            private CancellationTokenRegistration cancellationTokenRegistration;

            private UniTaskCompletionSourceCore<AsyncUnit> core;

            private long delayTimeSpanTicks;
            private DelayRealtimePromise nextNode;
            private ValueStopwatch stopwatch;

            static DelayRealtimePromise()
            {
                TaskPool.RegisterSizeGetter(typeof(DelayRealtimePromise), () => pool.Size);
            }

            private DelayRealtimePromise()
            {
            }

            public bool MoveNext()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    core.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (stopwatch.IsInvalid)
                {
                    core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                if (stopwatch.ElapsedTicks >= delayTimeSpanTicks)
                {
                    core.TrySetResult(AsyncUnit.Default);
                    return false;
                }

                return true;
            }

            public ref DelayRealtimePromise NextNode => ref nextNode;

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

            public static IUniTaskSource Create(TimeSpan delayTimeSpan, PlayerLoopTiming timing,
                CancellationToken cancellationToken, bool cancelImmediately, out short token)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AutoResetUniTaskCompletionSource.CreateFromCanceled(cancellationToken, out token);

                if (!pool.TryPop(out var result)) result = new DelayRealtimePromise();

                result.stopwatch = ValueStopwatch.StartNew();
                result.delayTimeSpanTicks = delayTimeSpan.Ticks;
                result.cancellationToken = cancellationToken;
                result.cancelImmediately = cancelImmediately;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    result.cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                        state =>
                        {
                            var promise = (DelayRealtimePromise)state;
                            promise.core.TrySetCanceled(promise.cancellationToken);
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
                stopwatch = default;
                cancellationToken = default;
                cancellationTokenRegistration.Dispose();
                cancelImmediately = default;
                return pool.TryPush(this);
            }
        }
    }

    public readonly struct YieldAwaitable
    {
        private readonly PlayerLoopTiming timing;

        public YieldAwaitable(PlayerLoopTiming timing)
        {
            this.timing = timing;
        }

        public Awaiter GetAwaiter()
        {
            return new Awaiter(timing);
        }

        public UniTask ToUniTask()
        {
            return UniTask.Yield(timing, CancellationToken.None);
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly PlayerLoopTiming timing;

            public Awaiter(PlayerLoopTiming timing)
            {
                this.timing = timing;
            }

            public bool IsCompleted => false;

            public void GetResult()
            {
            }

            public void OnCompleted(Action continuation)
            {
                PlayerLoopHelper.AddContinuation(timing, continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                PlayerLoopHelper.AddContinuation(timing, continuation);
            }
        }
    }
}