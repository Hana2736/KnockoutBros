using UnityEngine;

#if UNITY_2023_1_OR_NEWER
namespace Cysharp.Threading.Tasks
{
    public static class UnityAwaitableExtensions
    {
        public static async UniTask AsUniTask(this Awaitable awaitable)
        {
            await awaitable;
        }

        public static async UniTask<T> AsUniTask<T>(this Awaitable<T> awaitable)
        {
            return await awaitable;
        }
    }
}
#endif