using System;
using Unity.Collections;

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
        }
#endif
    }
}
