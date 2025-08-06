using System;
using System.Threading;
using UnityEngine;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<AsyncUnit> Timer(TimeSpan dueTime,
            PlayerLoopTiming updateTiming = PlayerLoopTiming.Update, bool ignoreTimeScale = false,
            bool cancelImmediately = false)
        {
            return new Timer(dueTime, null, updateTiming, ignoreTimeScale, cancelImmediately);
        }

        public static IUniTaskAsyncEnumerable<AsyncUnit> Timer(TimeSpan dueTime, TimeSpan period,
            PlayerLoopTiming updateTiming = PlayerLoopTiming.Update, bool ignoreTimeScale = false,
            bool cancelImmediately = false)
        {
            return new Timer(dueTime, period, updateTiming, ignoreTimeScale, cancelImmediately);
        }

        public static IUniTaskAsyncEnumerable<AsyncUnit> Interval(TimeSpan period,
            PlayerLoopTiming updateTiming = PlayerLoopTiming.Update, bool ignoreTimeScale = false,
            bool cancelImmediately = false)
        {
            return new Timer(period, period, updateTiming, ignoreTimeScale, cancelImmediately);
        }

        public static IUniTaskAsyncEnumerable<AsyncUnit> TimerFrame(int dueTimeFrameCount,
            PlayerLoopTiming updateTiming = PlayerLoopTiming.Update, bool cancelImmediately = false)
        {
            if (dueTimeFrameCount < 0)
                throw new ArgumentOutOfRangeException("Delay does not allow minus delayFrameCount. dueTimeFrameCount:" +
                                                      dueTimeFrameCount);

            return new TimerFrame(dueTimeFrameCount, null, updateTiming, cancelImmediately);
        }

        public static IUniTaskAsyncEnumerable<AsyncUnit> TimerFrame(int dueTimeFrameCount, int periodFrameCount,
            PlayerLoopTiming updateTiming = PlayerLoopTiming.Update, bool cancelImmediately = false)
        {
            if (dueTimeFrameCount < 0)
                throw new ArgumentOutOfRangeException("Delay does not allow minus delayFrameCount. dueTimeFrameCount:" +
                                                      dueTimeFrameCount);
            if (periodFrameCount < 0)
                throw new ArgumentOutOfRangeException("Delay does not allow minus periodFrameCount. periodFrameCount:" +
                                                      dueTimeFrameCount);

            return new TimerFrame(dueTimeFrameCount, periodFrameCount, updateTiming, cancelImmediately);
        }

        public static IUniTaskAsyncEnumerable<AsyncUnit> IntervalFrame(int intervalFrameCount,
            PlayerLoopTiming updateTiming = PlayerLoopTiming.Update, bool cancelImmediately = false)
        {
            if (intervalFrameCount < 0)
                throw new ArgumentOutOfRangeException(
                    "Delay does not allow minus intervalFrameCount. intervalFrameCount:" + intervalFrameCount);
            return new TimerFrame(intervalFrameCount, intervalFrameCount, updateTiming, cancelImmediately);
        }
    }

    internal class Timer : IUniTaskAsyncEnumerable<AsyncUnit>
    {
        private readonly bool cancelImmediately;
        private readonly TimeSpan dueTime;
        private readonly bool ignoreTimeScale;
        private readonly TimeSpan? period;
        private readonly PlayerLoopTiming updateTiming;

        public Timer(TimeSpan dueTime, TimeSpan? period, PlayerLoopTiming updateTiming, bool ignoreTimeScale,
            bool cancelImmediately)
        {
            this.updateTiming = updateTiming;
            this.dueTime = dueTime;
            this.period = period;
            this.ignoreTimeScale = ignoreTimeScale;
            this.cancelImmediately = cancelImmediately;
        }

        public IUniTaskAsyncEnumerator<AsyncUnit> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _Timer(dueTime, period, updateTiming, ignoreTimeScale, cancellationToken, cancelImmediately);
        }

        private class _Timer : MoveNextSource, IUniTaskAsyncEnumerator<AsyncUnit>, IPlayerLoopItem
        {
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenRegistration cancellationTokenRegistration;
            private readonly float dueTime;
            private readonly bool ignoreTimeScale;
            private readonly float? period;
            private readonly PlayerLoopTiming updateTiming;
            private bool completed;
            private bool disposed;
            private bool dueTimePhase;
            private float elapsed;

            private readonly int initialFrame;

            public _Timer(TimeSpan dueTime, TimeSpan? period, PlayerLoopTiming updateTiming, bool ignoreTimeScale,
                CancellationToken cancellationToken, bool cancelImmediately)
            {
                this.dueTime = (float)dueTime.TotalSeconds;
                this.period = period == null ? null : (float?)period.Value.TotalSeconds;

                if (this.dueTime <= 0) this.dueTime = 0;
                if (this.period != null)
                    if (this.period <= 0)
                        this.period = 1;

                initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
                dueTimePhase = true;
                this.updateTiming = updateTiming;
                this.ignoreTimeScale = ignoreTimeScale;
                this.cancellationToken = cancellationToken;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (_Timer)state;
                        source.completionSource.TrySetCanceled(source.cancellationToken);
                    }, this);

                TaskTracker.TrackActiveTask(this, 2);
                PlayerLoopHelper.AddAction(updateTiming, this);
            }

            public bool MoveNext()
            {
                if (disposed)
                {
                    completionSource.TrySetResult(false);
                    return false;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return false;
                }

                if (dueTimePhase)
                {
                    if (elapsed == 0)
                        // skip in initial frame.
                        if (initialFrame == Time.frameCount)
                            return true;

                    elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

                    if (elapsed >= dueTime)
                    {
                        dueTimePhase = false;
                        completionSource.TrySetResult(true);
                    }
                }
                else
                {
                    if (period == null)
                    {
                        completed = true;
                        completionSource.TrySetResult(false);
                        return false;
                    }

                    elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

                    if (elapsed >= period) completionSource.TrySetResult(true);
                }

                return true;
            }

            public AsyncUnit Current => default;

            public UniTask<bool> MoveNextAsync()
            {
                // return false instead of throw
                if (disposed || completed) return CompletedTasks.False;

                // reset value here.
                elapsed = 0;

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

    internal class TimerFrame : IUniTaskAsyncEnumerable<AsyncUnit>
    {
        private readonly bool cancelImmediately;
        private readonly int dueTimeFrameCount;
        private readonly int? periodFrameCount;
        private readonly PlayerLoopTiming updateTiming;

        public TimerFrame(int dueTimeFrameCount, int? periodFrameCount, PlayerLoopTiming updateTiming,
            bool cancelImmediately)
        {
            this.updateTiming = updateTiming;
            this.dueTimeFrameCount = dueTimeFrameCount;
            this.periodFrameCount = periodFrameCount;
            this.cancelImmediately = cancelImmediately;
        }

        public IUniTaskAsyncEnumerator<AsyncUnit> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _TimerFrame(dueTimeFrameCount, periodFrameCount, updateTiming, cancellationToken,
                cancelImmediately);
        }

        private class _TimerFrame : MoveNextSource, IUniTaskAsyncEnumerator<AsyncUnit>, IPlayerLoopItem
        {
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenRegistration cancellationTokenRegistration;
            private readonly int dueTimeFrameCount;
            private readonly int? periodFrameCount;
            private bool completed;
            private int currentFrame;
            private bool disposed;
            private bool dueTimePhase;

            private readonly int initialFrame;

            public _TimerFrame(int dueTimeFrameCount, int? periodFrameCount, PlayerLoopTiming updateTiming,
                CancellationToken cancellationToken, bool cancelImmediately)
            {
                if (dueTimeFrameCount <= 0) dueTimeFrameCount = 0;
                if (periodFrameCount != null)
                    if (periodFrameCount <= 0)
                        periodFrameCount = 1;

                initialFrame = PlayerLoopHelper.IsMainThread ? Time.frameCount : -1;
                dueTimePhase = true;
                this.dueTimeFrameCount = dueTimeFrameCount;
                this.periodFrameCount = periodFrameCount;
                this.cancellationToken = cancellationToken;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (_TimerFrame)state;
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

                if (dueTimePhase)
                {
                    if (currentFrame == 0)
                    {
                        if (dueTimeFrameCount == 0)
                        {
                            dueTimePhase = false;
                            completionSource.TrySetResult(true);
                            return true;
                        }

                        // skip in initial frame.
                        if (initialFrame == Time.frameCount) return true;
                    }

                    if (++currentFrame >= dueTimeFrameCount)
                    {
                        dueTimePhase = false;
                        completionSource.TrySetResult(true);
                    }
                }
                else
                {
                    if (periodFrameCount == null)
                    {
                        completed = true;
                        completionSource.TrySetResult(false);
                        return false;
                    }

                    if (++currentFrame >= periodFrameCount) completionSource.TrySetResult(true);
                }

                return true;
            }

            public AsyncUnit Current => default;

            public UniTask<bool> MoveNextAsync()
            {
                if (disposed || completed) return CompletedTasks.False;

                if (cancellationToken.IsCancellationRequested) completionSource.TrySetCanceled(cancellationToken);

                // reset value here.
                currentFrame = 0;
                completionSource.Reset();
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