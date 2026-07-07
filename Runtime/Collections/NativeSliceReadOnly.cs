using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Collections.NativeSortExtension;

namespace NativeCollectionsExtended
{
    public struct NativeSliceReadOnly<T> where T : unmanaged
    {
        NativeSlice<T> _slice;

        public NativeSliceReadOnly(NativeSlice<T> slice)
        {
            _slice = slice;
        }
        public int Length
        {
            get
            {
                return _slice.Length;
            }
        }
        public T this[int index]
        {
            get
            {
                return _slice[index];
            }
        }
        public void CopyTo(NativeList<T> list)
        {
            int sliceLength = _slice.Length;
            int oldListLength = list.Length;
            int newListLength = oldListLength + sliceLength;
            list.Length = newListLength;
            list.AsArray().Slice(oldListLength, sliceLength).CopyFrom(_slice);
        }
        public void CopyTo(NativeSlice<T> slice)
        {
            slice.CopyFrom(_slice);
        }
        public NativeSliceReadOnly<T> Slice(int start, int length)
        {
            return new NativeSliceReadOnly<T>(_slice.Slice(start, length));
        }
        public static int BinarySearch<T>(NativeSliceReadOnly<T> slice, T value)
            where T : unmanaged, IComparable<T>
        {
            return slice._slice.BinarySearch(value);
        }
        public static int BinarySearch<T,U>(NativeSliceReadOnly<T> slice, T value, U comparer)
            where T : unmanaged, IComparable<T>
            where U : IComparer<T>
        {
            return slice._slice.BinarySearch(value, comparer);
        }
        public static int LinearSearch<T>(NativeSliceReadOnly<T> slice, T value)
            where T : unmanaged, IEquatable<T>
        {
            for(int i = 0; i < slice._slice.Length; i++)
            {
                if (slice[i].Equals(value))
                    return i;
            }
            return -1;
        }
    }
    public static class NativeSliceSearchHelper
    {
        public static int LinearSearch<T>(NativeSlice<T> slice, T value)
            where T : unmanaged, IEquatable<T>
        {
            for (int i = 0; i < slice.Length; i++)
            {
                if (slice[i].Equals(value))
                    return i;
            }
            return -1;
        }
    }
}
