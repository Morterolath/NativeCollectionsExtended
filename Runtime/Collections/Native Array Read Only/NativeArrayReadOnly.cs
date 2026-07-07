using Unity.Collections;

namespace NativeCollectionsExtended
{
    public struct NativeArrayReadOnly<T> where T : unmanaged
    {
        [ReadOnly] NativeArray<T> _array;

        public int Length => _array.Length;
        public bool IsCreated => _array.IsCreated;
        public NativeArrayReadOnly(NativeArray<T> array) => _array = array;

        public T this[int index] => _array[index];

        public NativeSliceReadOnly<T> Slice(int start, int length)
        {
            return new NativeSliceReadOnly<T>(_array.Slice(start, length));
        }
        public void CopyTo(NativeList<T> list)
        {
            list.CopyFrom(_array);
        }
        public void CopyTo(NativeSlice<T> slice)
        {
            slice.CopyFrom(_array);
        }
    }
}
