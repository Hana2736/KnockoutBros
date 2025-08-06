using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks.Internal;
using Object = UnityEngine.Object;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<TProperty> EveryValueChanged<TTarget, TProperty>(TTarget target,
            Func<TTarget, TProperty> propertySelector, PlayerLoopTiming monitorTiming = PlayerLoopTiming.Update,
            IEqualityComparer<TProperty> equalityComparer = null, bool cancelImmediately = false)
            where TTarget : class
        {
            var unityObject = target as Object;
            var isUnityObject = target is Object; // don't use (unityObject == null)

            if (isUnityObject)
                return new EveryValueChangedUnityObject<TTarget, TProperty>(target, propertySelector,
                    equalityComparer ?? UnityEqualityComparer.GetDefault<TProperty>(), monitorTiming,
                    cancelImmediately);

            return new EveryValueChangedStandardObject<TTarget, TProperty>(target, propertySelector,
                equalityComparer ?? UnityEqualityComparer.GetDefault<TProperty>(), monitorTiming, cancelImmediately);
        }
    }

    internal sealed class EveryValueChangedUnityObject<TTarget, TProperty> : IUniTaskAsyncEnumerable<TProperty>
    {
        private readonly bool cancelImmediately;
        private readonly IEqualityComparer<TProperty> equalityComparer;
        private readonly PlayerLoopTiming monitorTiming;
        private readonly Func<TTarget, TProperty> propertySelector;
        private readonly TTarget target;

        public EveryValueChangedUnityObject(TTarget target, Func<TTarget, TProperty> propertySelector,
            IEqualityComparer<TProperty> equalityComparer, PlayerLoopTiming monitorTiming, bool cancelImmediately)
        {
            this.target = target;
            this.propertySelector = propertySelector;
            this.equalityComparer = equalityComparer;
            this.monitorTiming = monitorTiming;
            this.cancelImmediately = cancelImmediately;
        }

        public IUniTaskAsyncEnumerator<TProperty> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _EveryValueChanged(target, propertySelector, equalityComparer, monitorTiming, cancellationToken,
                cancelImmediately);
        }

        private sealed class _EveryValueChanged : MoveNextSource, IUniTaskAsyncEnumerator<TProperty>, IPlayerLoopItem
        {
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenRegistration cancellationTokenRegistration;
            private readonly IEqualityComparer<TProperty> equalityComparer;
            private readonly Func<TTarget, TProperty> propertySelector;
            private readonly TTarget target;
            private readonly Object targetAsUnityObject;
            private bool disposed;

            private bool first;

            public _EveryValueChanged(TTarget target, Func<TTarget, TProperty> propertySelector,
                IEqualityComparer<TProperty> equalityComparer, PlayerLoopTiming monitorTiming,
                CancellationToken cancellationToken, bool cancelImmediately)
            {
                this.target = target;
                targetAsUnityObject = target as Object;
                this.propertySelector = propertySelector;
                this.equalityComparer = equalityComparer;
                this.cancellationToken = cancellationToken;
                first = true;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (_EveryValueChanged)state;
                        source.completionSource.TrySetCanceled(source.cancellationToken);
                    }, this);

                TaskTracker.TrackActiveTask(this, 2);
                PlayerLoopHelper.AddAction(monitorTiming, this);
            }

            public bool MoveNext()
            {
                if (disposed || targetAsUnityObject == null)
                {
                    completionSource.TrySetResult(false);
                    DisposeAsync().Forget();
                    return false;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return false;
                }

                var nextValue = default(TProperty);
                try
                {
                    nextValue = propertySelector(target);
                    if (equalityComparer.Equals(Current, nextValue)) return true;
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                    DisposeAsync().Forget();
                    return false;
                }

                Current = nextValue;
                completionSource.TrySetResult(true);
                return true;
            }

            public TProperty Current { get; private set; }

            public UniTask<bool> MoveNextAsync()
            {
                if (disposed) return CompletedTasks.False;

                completionSource.Reset();

                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return new UniTask<bool>(this, completionSource.Version);
                }

                if (first)
                {
                    first = false;
                    if (targetAsUnityObject == null) return CompletedTasks.False;
                    Current = propertySelector(target);
                    return CompletedTasks.True;
                }

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

    internal sealed class EveryValueChangedStandardObject<TTarget, TProperty> : IUniTaskAsyncEnumerable<TProperty>
        where TTarget : class
    {
        private readonly bool cancelImmediately;
        private readonly IEqualityComparer<TProperty> equalityComparer;
        private readonly PlayerLoopTiming monitorTiming;
        private readonly Func<TTarget, TProperty> propertySelector;
        private readonly WeakReference<TTarget> target;

        public EveryValueChangedStandardObject(TTarget target, Func<TTarget, TProperty> propertySelector,
            IEqualityComparer<TProperty> equalityComparer, PlayerLoopTiming monitorTiming, bool cancelImmediately)
        {
            this.target = new WeakReference<TTarget>(target, false);
            this.propertySelector = propertySelector;
            this.equalityComparer = equalityComparer;
            this.monitorTiming = monitorTiming;
            this.cancelImmediately = cancelImmediately;
        }

        public IUniTaskAsyncEnumerator<TProperty> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _EveryValueChanged(target, propertySelector, equalityComparer, monitorTiming, cancellationToken,
                cancelImmediately);
        }

        private sealed class _EveryValueChanged : MoveNextSource, IUniTaskAsyncEnumerator<TProperty>, IPlayerLoopItem
        {
            private readonly CancellationToken cancellationToken;
            private readonly CancellationTokenRegistration cancellationTokenRegistration;
            private readonly IEqualityComparer<TProperty> equalityComparer;
            private readonly Func<TTarget, TProperty> propertySelector;
            private readonly WeakReference<TTarget> target;
            private bool disposed;

            private bool first;

            public _EveryValueChanged(WeakReference<TTarget> target, Func<TTarget, TProperty> propertySelector,
                IEqualityComparer<TProperty> equalityComparer, PlayerLoopTiming monitorTiming,
                CancellationToken cancellationToken, bool cancelImmediately)
            {
                this.target = target;
                this.propertySelector = propertySelector;
                this.equalityComparer = equalityComparer;
                this.cancellationToken = cancellationToken;
                first = true;

                if (cancelImmediately && cancellationToken.CanBeCanceled)
                    cancellationTokenRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(state =>
                    {
                        var source = (_EveryValueChanged)state;
                        source.completionSource.TrySetCanceled(source.cancellationToken);
                    }, this);

                TaskTracker.TrackActiveTask(this, 2);
                PlayerLoopHelper.AddAction(monitorTiming, this);
            }

            public bool MoveNext()
            {
                if (disposed || !target.TryGetTarget(out var t))
                {
                    completionSource.TrySetResult(false);
                    DisposeAsync().Forget();
                    return false;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return false;
                }

                var nextValue = default(TProperty);
                try
                {
                    nextValue = propertySelector(t);
                    if (equalityComparer.Equals(Current, nextValue)) return true;
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                    DisposeAsync().Forget();
                    return false;
                }

                Current = nextValue;
                completionSource.TrySetResult(true);
                return true;
            }

            public TProperty Current { get; private set; }

            public UniTask<bool> MoveNextAsync()
            {
                if (disposed) return CompletedTasks.False;

                completionSource.Reset();

                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return new UniTask<bool>(this, completionSource.Version);
                }

                if (first)
                {
                    first = false;
                    if (!target.TryGetTarget(out var t)) return CompletedTasks.False;
                    Current = propertySelector(t);
                    return CompletedTasks.True;
                }

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