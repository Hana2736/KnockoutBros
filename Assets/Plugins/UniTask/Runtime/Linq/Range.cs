using System.Threading;
using Cysharp.Threading.Tasks.Internal;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<int> Range(int start, int count)
        {
            if (count < 0) throw Error.ArgumentOutOfRange(nameof(count));

            var end = (long)start + count - 1L;
            if (end > int.MaxValue) throw Error.ArgumentOutOfRange(nameof(count));

            if (count == 0) Empty<int>();

            return new Range(start, count);
        }
    }

    internal class Range : IUniTaskAsyncEnumerable<int>
    {
        private readonly int end;
        private readonly int start;

        public Range(int start, int count)
        {
            this.start = start;
            end = start + count;
        }

        public IUniTaskAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _Range(start, end, cancellationToken);
        }

        private class _Range : IUniTaskAsyncEnumerator<int>
        {
            private readonly int end;
            private readonly int start;
            private readonly CancellationToken cancellationToken;

            public _Range(int start, int end, CancellationToken cancellationToken)
            {
                this.start = start;
                this.end = end;
                this.cancellationToken = cancellationToken;

                Current = start - 1;
            }

            public int Current { get; private set; }

            public UniTask<bool> MoveNextAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();

                Current++;

                if (Current != end) return CompletedTasks.True;

                return CompletedTasks.False;
            }

            public UniTask DisposeAsync()
            {
                return default;
            }
        }
    }
}