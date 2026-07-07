using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeCollectionsExtended
{
    public struct NativeListReadOnly<T> where T : unmanaged
    {
        [ReadOnly] internal NativeList<T> InternalList;

        public int Length => InternalList.Length;
        public int Capacity => InternalList.Capacity;
        public bool IsCreated => InternalList.IsCreated;
        public T this[int index] => InternalList[index];

        public NativeListReadOnly(NativeList<T> list)
        {
            InternalList = list;
        }
        public NativeArrayReadOnly<T> AsDeferredJobArray()
        {
            return new NativeArrayReadOnly<T>(InternalList.AsDeferredJobArray());
        }
        public NativeArrayReadOnly<T> AsArray()
        {
            return new NativeArrayReadOnly<T>(InternalList.AsArray());
        }
    }
}
namespace NativeCollectionsExtended.Unsafe
{
    public static class NativeListReadOnlyUnsafeUtility
    {
        public static NativeList<T> GetInternalList<T>(in NativeListReadOnly<T> list)
            where T : unmanaged
        {
            return list.InternalList;
        }
    }
}
