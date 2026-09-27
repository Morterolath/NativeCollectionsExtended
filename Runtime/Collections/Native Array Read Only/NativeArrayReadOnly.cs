using Unity.Collections;

namespace NativeCollectionsExtended
{
    public struct NativeArrayReadOnly<T> where T : unmanaged
    {
        [ReadOnly] internal NativeArray<T> _array;

        public int Length => _array.Length;
        public bool IsCreated => _array.IsCreated;
        public NativeArrayReadOnly(NativeArray<T> array) => _array = array;

        public T this[int index] => _array[index];

        public NativeSliceReadOnly<T> Slice(int start, int length)
        {
            return new NativeSliceReadOnly<T>(_array.Slice(start, length));
        }
        public NativeArrayReadOnly<U> Reinterpret<U>()
            where U : unmanaged
        {
            return new NativeArrayReadOnly<U>(_array.Reinterpret<U>());
        }
        public NativeArrayReadOnly<U> Reinterpret<U>(int expectedTypeSize)
            where U : unmanaged
        {
            return new NativeArrayReadOnly<U>(_array.Reinterpret<U>(expectedTypeSize));
        }
        public void ReinterpretStore<U>(int destIndex, U data)
            where U : unmanaged
        {
            _array.ReinterpretStore(destIndex, data);
        }
        public void ReinterpretLoad<U>(int sourceIndex)
            where U : unmanaged
        {
            _array.ReinterpretLoad<U>(sourceIndex);
        }
        public void CopyTo(NativeList<T> list)
        {
            list.CopyFrom(_array);
        }
        public void CopyTo(NativeSlice<T> slice)
        {
            slice.CopyFrom(_array);
        }
        public void CopyTo(NativeArray<T> slice)
        {
            slice.CopyFrom(_array);
        }
        public NativeArray<T> ToArray(Allocator allocator)
        {
            return new NativeArray<T>(_array, allocator);
        }
    }
    public static class NativeArrayReadOnlyLowLevelHelper
    {
        public static NativeArray<T> GetRawArray<T>(NativeArrayReadOnly<T> array)
            where T : unmanaged
        {
            return array._array;
        }
    }
}
