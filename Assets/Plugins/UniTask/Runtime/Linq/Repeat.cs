using System.Threading;
using Cysharp.Threading.Tasks.Internal;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<TElement> Repeat<TElement>(TElement element, int count)
        {
            if (count < 0) throw Error.ArgumentOutOfRange(nameof(count));

            return new Repeat<TElement>(element, count);
        }
    }

    internal class Repeat<TElement> : IUniTaskAsyncEnumerable<TElement>
    {
        private readonly int count;
        private readonly TElement element;

        public Repeat(TElement element, int count)
        {
            this.element = element;
            this.count = count;
        }

        public IUniTaskAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _Repeat(element, count, cancellationToken);
        }

        private class _Repeat : IUniTaskAsyncEnumerator<TElement>
        {
            private readonly int count;
            private readonly CancellationToken cancellationToken;
            private int remaining;

            public _Repeat(TElement element, int count, CancellationToken cancellationToken)
            {
                this.Current = element;
                this.count = count;
                this.cancellationToken = cancellationToken;

                remaining = count;
            }

            public TElement Current { get; }

            public UniTask<bool> MoveNextAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (remaining-- != 0) return CompletedTasks.True;

                return CompletedTasks.False;
            }

            public UniTask DisposeAsync()
            {
                return default;
            }
        }
    }
}