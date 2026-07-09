using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct ListOfArrays<T> where T : unmanaged
    {
        public struct Array
        {
            internal NativeArray<ArrayPointer> _arrayPointers;
            internal NativeArray<T> _arrayData;

            internal Array(NativeArray<ArrayPointer> arrayPointers, NativeArray<T> arrayData)
            {
                _arrayPointers = arrayPointers;
                _arrayData = arrayData;
            }
            public bool IsCreated => _arrayPointers.IsCreated;
            public int Count
            {
                get
                {
                    return _arrayPointers.Length;
                }
            }
            public NativeSlice<T> this[int arrayIndex]
            {
                get
                {
                    ArrayPointer ap = _arrayPointers[arrayIndex];
                    return _arrayData.Slice(ap.Start, ap.Length);
                }
            }
            public T this[int arrayIndex, int elementIndex]
            {
                set
                {
                    ArrayPointer ap = _arrayPointers[arrayIndex];
                    _arrayData[ap.Start + elementIndex] = value;
                }
                get
                {
                    ArrayPointer ap = _arrayPointers[arrayIndex];
                    return _arrayData[ap.Start + elementIndex];
                }
            }
            public int ArrayLength(int arrayIndex)
            {
                return _arrayPointers[arrayIndex].Length;
            }
            public void ReinitilizeAll()
            {
                for (int i = 0; i < _arrayData.Length; i++) _arrayData[i] = default;
            }
        }
        public struct ReadOnly
        {
            internal NativeArray<ArrayPointer> _arrayPointers;
            internal NativeArray<T> _arrayData;

            internal ReadOnly(NativeArray<ArrayPointer> arrayPointers, NativeArray<T> arrayData)
            {
                _arrayPointers = arrayPointers;
                _arrayData = arrayData;
            }
            public bool IsCreated => _arrayPointers.IsCreated;
            public int Count
            {
                get
                {
                    return _arrayPointers.Length;
                }
            }
            public NativeSliceReadOnly<T> this[int arrayIndex]
            {
                get
                {
                    ArrayPointer ap = _arrayPointers[arrayIndex];
                    return new NativeSliceReadOnly<T>(_arrayData.Slice(ap.Start, ap.Length));
                }
            }
            public T this[int arrayIndex, int elementIndex]
            {
                get
                {
                    return _arrayData[_arrayPointers[arrayIndex].Start + elementIndex];
                }
            }
            public int ArrayLength(int arrayIndex)
            {
                return _arrayPointers[arrayIndex].Length;
            }
        }
        public struct ParallelWriter
        {
            internal NativeArray<ArrayPointer> _arrayPointers;
            [NativeDisableParallelForRestriction] internal NativeArray<T> _arrayData;

            internal ParallelWriter(NativeArray<ArrayPointer> arrayPointers, NativeArray<T> arrayData)
            {
                _arrayPointers = arrayPointers;
                _arrayData = arrayData;
            }
            public int Count
            {
                get
                {
                    return _arrayPointers.Length;
                }
            }
            public NativeSlice<T> this[int index]
            {
                get
                {
                    ArrayPointer ap = _arrayPointers[index];
                    return _arrayData.Slice(ap.Start, ap.Length);
                }
            }
        }
        public struct ArrayPointer
        {
            internal int Start;
            internal int Length;
        }
        internal NativeList<ArrayPointer> _arrayPointers;
        internal NativeList<T> _arrayData;

        public ListOfArrays(Allocator allocator)
        {
            _arrayPointers = new NativeList<ArrayPointer>(allocator);
            _arrayData = new NativeList<T>(allocator);
        }
        public void Dispose()
        {
            _arrayPointers.Dispose();
            _arrayData.Dispose();
        }
        public int Count
        {
            get
            {
                return _arrayPointers.Length;
            }
        }
        public bool IsCreated => _arrayData.IsCreated;
        public NativeSlice<T> this[int arrayIndex]
        {
            get
            {
                ArrayPointer ap = _arrayPointers[arrayIndex];
                return _arrayData.AsArray().Slice(ap.Start, ap.Length);
            }
        }
        public T this[int arrayIndex, int elementIndex]
        {
            set
            {
                ArrayPointer ap = _arrayPointers[arrayIndex];
                _arrayData[ap.Start + elementIndex] = value;
            }
            get
            {
                ArrayPointer ap = _arrayPointers[arrayIndex];
                return _arrayData[ap.Start + elementIndex];
            }
        }
        public void Clear()
        {
            _arrayPointers.Clear();
            _arrayData.Clear();
        }
        public void AddArray(int length)
        {
            int oldDataLength = _arrayData.Length;
            int newDataLength = oldDataLength + length;
            _arrayData.ResizeUninitialized(newDataLength);
            for (int i = oldDataLength; i < newDataLength; i++) _arrayData[i] = default;

            _arrayPointers.Add(new ArrayPointer { Start = oldDataLength, Length = length });
        }
        public void AddArray(int length, T defaultValue)
        {
            int oldDataLength = _arrayData.Length;
            int newDataLength = oldDataLength + length;
            _arrayData.ResizeUninitialized(newDataLength);
            for (int i = oldDataLength; i < newDataLength; i++) _arrayData[i] = defaultValue;

            _arrayPointers.Add(new ArrayPointer { Start = oldDataLength, Length = length });
        }
        public void AddArrayUnitialized(int length)
        {
            int oldDataLength = _arrayData.Length;
            int newDataLength = oldDataLength + length;
            _arrayData.ResizeUninitialized(newDataLength);

            _arrayPointers.Add(new ArrayPointer { Start = oldDataLength, Length = length });
        }
        public NativeList<ArrayPointer> GetInternalListForDeferredJob()
        {
            return _arrayPointers;
        }
        public Array AsArray()
        {
            return new Array(_arrayPointers.AsArray(), _arrayData.AsArray());
        }
        public Array AsDeferredJobArray()
        {
            return new Array(_arrayPointers.AsDeferredJobArray(), _arrayData.AsDeferredJobArray());
        }
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(_arrayPointers.AsArray(), _arrayData.AsArray());
        }
        public ParallelWriter AsDeferredJobParallelWriter()
        {
            return new ParallelWriter(_arrayPointers.AsDeferredJobArray(), _arrayData.AsDeferredJobArray());
        }
        public ReadOnly AsReadOnly()
        {
            return new ReadOnly(_arrayPointers.AsArray(), _arrayData.AsArray());
        }
        public ReadOnly AsDeferrebJobArrayReadOnly()
        {
            return new ReadOnly(_arrayPointers.AsDeferredJobArray(), _arrayData.AsDeferredJobArray());
        }
        public void ReinitilizeAll()
        {
            NativeArray<T> arrayData = _arrayData.AsArray();
            for (int i = 0; i < arrayData.Length; i++) arrayData[i] = default;
        }
        public void Reinitilize(int arrayIndex)
        {
            ArrayPointer ptr = _arrayPointers[arrayIndex];
            for (int i = ptr.Start; i < ptr.Start + ptr.Length; i++) _arrayData[i] = default;
        }
        public void Reinitilize(int arrayIndex, T defaultValue)
        {
            ArrayPointer ptr = _arrayPointers[arrayIndex];
            for (int i = ptr.Start; i < ptr.Start + ptr.Length; i++) _arrayData[i] = defaultValue;
        }
    }
    public static class ListOfArraysLowLevelHelper
    {
        public static void GetArrayRaw<T>(ListOfArrays<T> listOfArrays, int arrayIndex, out NativeArray<T> dataBuffer, out int arrayStart, out int arrayLength)
            where T : unmanaged
        {
            dataBuffer = listOfArrays._arrayData.AsArray();
            ListOfArrays<T>.ArrayPointer arrPtr = listOfArrays._arrayPointers[arrayIndex];
            arrayStart = arrPtr.Start;
            arrayLength = arrPtr.Length;
        }
        public static void GetArrayRaw<T>(ListOfArrays<T>.Array listOfArrays, int arrayIndex, out NativeArray<T> dataBuffer, out int arrayStart, out int arrayLength)
            where T : unmanaged
        {
            dataBuffer = listOfArrays._arrayData;
            ListOfArrays<T>.ArrayPointer arrPtr = listOfArrays._arrayPointers[arrayIndex];
            arrayStart = arrPtr.Start;
            arrayLength = arrPtr.Length;
        }
        public static void GetArrayRaw<T>(ListOfArrays<T>.ReadOnly listOfArrays, int arrayIndex, out NativeArray<T> dataBuffer, out int arrayStart, out int arrayLength)
            where T : unmanaged
        {
            dataBuffer = listOfArrays._arrayData;
            ListOfArrays<T>.ArrayPointer arrPtr = listOfArrays._arrayPointers[arrayIndex];
            arrayStart = arrPtr.Start;
            arrayLength = arrPtr.Length;
        }
        public static void GetDeferredArrayRaw<T>(ListOfArrays<T> listOfArrays, int arrayIndex, out NativeArray<T> dataBuffer, out int arrayStart, out int arrayLength)
            where T : unmanaged
        {
            dataBuffer = listOfArrays._arrayData.AsDeferredJobArray();
            ListOfArrays<T>.ArrayPointer arrPtr = listOfArrays._arrayPointers[arrayIndex];
            arrayStart = arrPtr.Start;
            arrayLength = arrPtr.Length;
        }
    }
}