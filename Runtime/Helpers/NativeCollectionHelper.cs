using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeCollectionsExtended
{
    public static class NativeCollectionHelper
    {
        public static void CopyToHashSet<T>(NativeArray<T> arr, NativeHashSet<T> set)
            where T : unmanaged, IEquatable<T>
        {
            for (int i = 0; i < arr.Length; i++)
                set.Add(arr[i]);
        }
        public static void CopyToHashSet<T>(NativeArrayReadOnly<T> arr, NativeHashSet<T> set)
            where T : unmanaged, IEquatable<T>
        {
            for (int i = 0; i < arr.Length; i++)
                set.Add(arr[i]);
        }
        public static void CopyToList<T>(NativeList<T> list, NativeHashSet<T> set)
            where T : unmanaged, IEquatable<T>
        {
            list.ResizeUninitialized(set.Count);
            NativeArray<T> arr = list.AsArray();
            NativeHashSet<T>.Enumerator enumerator = set.GetEnumerator();
            int idx = 0;
            while (enumerator.MoveNext())
                arr[idx++] = enumerator.Current;
        }
        public static void Rearrange<T>(NativeSlice<T> data, NativeSlice<int> rearrangedIndicies, NativeList<byte> helperBuffer)
            where T : unmanaged
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.LengthsMustMatch(data, rearrangedIndicies);
#endif
            helperBuffer.ResizeUninitialized(UnsafeUtility.SizeOf<T>());
            NativeArray<T> helperBuffer_asT = helperBuffer.AsArray().Reinterpret<T>(1);
            helperBuffer_asT.Slice().CopyFrom(data);

            for(int i = 0; i < rearrangedIndicies.Length; i++)
                data[rearrangedIndicies[i]] = helperBuffer_asT[i];
        }
        public static void Rearrange<T>(NativeArray<T> data, NativeArray<int> rearrangedIndicies, NativeList<byte> helperBuffer)
            where T : unmanaged
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.LengthsMustMatch(data, rearrangedIndicies);
#endif
            helperBuffer.ResizeUninitialized(UnsafeUtility.SizeOf<T>());
            NativeArray<T> helperBuffer_asT = helperBuffer.AsArray().Reinterpret<T>(1);
            helperBuffer_asT.CopyFrom(data);

            for(int i = 0; i < rearrangedIndicies.Length; i++)
                data[rearrangedIndicies[i]] = helperBuffer_asT[i];
        }
        public static void CopyToArray<T>(NativeArray<T> arr, NativeHashSet<T> set)
            where T : unmanaged, IEquatable<T>
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.LengthsMustMatch(arr, set);
#endif
            NativeHashSet<T>.Enumerator enumerator = set.GetEnumerator();
            int idx = 0;
            while (enumerator.MoveNext())
                arr[idx++] = enumerator.Current;
        }
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
        struct SafetyCheckHelper
        {
            public static void LengthsMustMatch<T>(NativeArray<T> arr, NativeHashSet<T> set)
                where T : unmanaged, IEquatable<T>
            {
                if(arr.Length != set.Count)
                {
                    throw new Exception($"Array legnth ({arr.Length}) must be equal to set count ({set.Count})");
                }
            }
            public static void LengthsMustMatch<T,E >(NativeArray<T> arr1, NativeArray<E> arr2)
                where T : unmanaged
                where E : unmanaged
            {
                if(arr1.Length != arr2.Length)
                {
                    throw new Exception($"Array 1 legnth ({arr1.Length}) must be equal to Array 2 length ({arr2.Length})");
                }
            }
            public static void LengthsMustMatch<T, E>(NativeSlice<T> arr1, NativeSlice<E> arr2)
                where T : unmanaged
                where E : unmanaged
            {
                if(arr1.Length != arr2.Length)
                {
                    throw new Exception($"Array 1 legnth ({arr1.Length}) must be equal to Array 2 length ({arr2.Length})");
                }
            }
        }
#endif
    }
}
