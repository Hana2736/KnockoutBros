using System.Threading;
using Cysharp.Threading.Tasks.Internal;

namespace Cysharp.Threading.Tasks.Linq
{
    public static partial class UniTaskAsyncEnumerable
    {
        public static IUniTaskAsyncEnumerable<TSource> Skip<TSource>(this IUniTaskAsyncEnumerable<TSource> source,
            int count)
        {
            Error.ThrowArgumentNullException(source, nameof(source));

            return new Skip<TSource>(source, count);
        }
    }

    internal sealed class Skip<TSource> : IUniTaskAsyncEnumerable<TSource>
    {
        private readonly int count;
        private readonly IUniTaskAsyncEnumerable<TSource> source;

        public Skip(IUniTaskAsyncEnumerable<TSource> source, int count)
        {
            this.source = source;
            this.count = count;
        }

        public IUniTaskAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new _Skip(source, count, cancellationToken);
        }

        private sealed class _Skip : AsyncEnumeratorBase<TSource, TSource>
        {
            private readonly int count;

            private int index;

            public _Skip(IUniTaskAsyncEnumerable<TSource> source, int count, CancellationToken cancellationToken)
                : base(source, cancellationToken)
            {
                this.count = count;
            }

            protected override bool TryMoveNextCore(bool sourceHasCurrent, out bool result)
            {
                if (sourceHasCurrent)
                {
                    if (count <= checked(index++))
                    {
                        Current = SourceCurrent;
                        result = true;
                        return true;
                    }

                    result = default;
                    return false;
                }

                result = false;
                return true;
            }
        }
    }
}