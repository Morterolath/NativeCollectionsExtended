using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace NativeCollectionsExtended
{
    public struct ListOfFixedLists<T>
        where T : unmanaged
    {
        public struct Array
        {
            NativeArray<FixedListPointer> _fixedListPointers;
            NativeArray<T> _listBuffer;

            internal Array(NativeArray<FixedListPointer> fixedListPointers, NativeArray<T> listBuffer)
            {
                _fixedListPointers = fixedListPointers;
                _listBuffer = listBuffer;
            }

            public int Count
            {
                get
                {
                    return _fixedListPointers.Length;
                }
            }
            public void ReinitializeAllListValues()
            {
                for (int i = 0; i < _listBuffer.Length; i++) _listBuffer[i] = default;
            }
            public ListWriter GetListWriter(int listIndex)
            {
                return new ListWriter(_fixedListPointers, _listBuffer, listIndex);
            }
            public NativeSlice<T> GetListData(int listIndex)
            {
                FixedListPointer listPtr = _fixedListPointers[listIndex];
                return _listBuffer.Slice(listPtr.Start, listPtr.Length);
            }
            public NativeSlice<T> this[int listIndex]
            {
                get
                {
                    FixedListPointer listPtr = _fixedListPointers[listIndex];
                    return _listBuffer.Slice(listPtr.Start, listPtr.Length);
                }
            }
            public void AddToListNoResize(int listIndex, T value)
            {
                FixedListPointer listPointer = _fixedListPointers[listIndex];
                bool isFull = listPointer.Length == listPointer.Capacity;
                int indexToAdd = listPointer.Start + math.select(listPointer.Length, listPointer.Length - 1, isFull);
                int newLength = math.select(listPointer.Length + 1, listPointer.Length, isFull);
                _listBuffer[indexToAdd] = value;
                listPointer.Length = newLength;
                _fixedListPointers[listIndex] = listPointer;
            }

        }
        public struct ListWriter
        {
            NativeArray<FixedListPointer> _fixedListPointers;
            NativeArray<T> _listBuffer;
            readonly int _listIndex;
            readonly int _start;
            readonly int _capacity;
            int _length;

            internal ListWriter(NativeArray<FixedListPointer> fixedListPointers, NativeArray<T> listBuffer, int listIndex)
            {
                _fixedListPointers = fixedListPointers;
                _listBuffer = listBuffer;
                FixedListPointer listPtr = fixedListPointers[listIndex];
                _listIndex = listIndex;
                _start = listPtr.Start;
                _capacity = listPtr.Capacity;
                _length = listPtr.Length;
            }

            public int Length
            {
                get
                {
                    return _length;
                }
                set
                {
                    value = math.max(value, 0);
                    value = math.min(value, _capacity);
                    _length = value;
                }
            }
            public int Capacity
            {
                get
                {
                    return _capacity;
                }
            }
            public bool IsFull()
            {
                return _capacity == _length;
            }

            //Appends the value
            //If full, list[lastIndex] = value
            public void AddNoResize(T value)
            {
                bool isFull = _length == _capacity;
                int indexToAdd = _start + math.select(_length, _length - 1, isFull);
                int newLength = math.select(_length + 1, _length, isFull);
                _listBuffer[indexToAdd] = value;
                _length = newLength;
            }
            public void Submit()
            {
                FixedListPointer pointer = _fixedListPointers[_listIndex];
                pointer.Length = _length;
                _fixedListPointers[_listIndex] = pointer;
            }
        }
        internal struct FixedListPointer
        {
            internal int Start;
            internal int Length;
            internal int Capacity;
        }
        NativeList<FixedListPointer> _fixedListPointers;
        NativeList<T> _listBuffer;

        public ListOfFixedLists(Allocator allocator)
        {

            _fixedListPointers = new NativeList<FixedListPointer>(allocator);
            _listBuffer = new NativeList<T>(allocator);
        }
        public void Dispose()
        {
            _fixedListPointers.Dispose();
            _listBuffer.Dispose();
            this = default;
        }
        public int Count
        {
            get
            {
                return _fixedListPointers.Length;
            }
        }
        public void Clear()
        {
            _fixedListPointers.Clear();
            _listBuffer.Clear();
        }
        public int Capacity(int listIndex)
        {
            return _fixedListPointers[listIndex].Capacity;
        }
        public int Length(int listIndex)
        {
            return _fixedListPointers[listIndex].Length;
        }
        public void TryDecreaseCount(int newCount)
        {
            newCount = math.max(0, newCount);
            newCount = math.min(newCount, _fixedListPointers.Length);
            _fixedListPointers.Length = newCount;

            int newListBufferLength = 0;
            if (_fixedListPointers.Length != 0)
            {
                FixedListPointer lastPointer = _fixedListPointers[_fixedListPointers.Length - 1];
                newListBufferLength = lastPointer.Start + lastPointer.Capacity;
            }

            _listBuffer.Length = newListBufferLength;
        }
        public void AddList(int capacity)
        {
            capacity = math.max(capacity, 1);
            FixedListPointer listPointer = new FixedListPointer
            {
                Start = _listBuffer.Length,
                Length = 0,
                Capacity = capacity
            };
            _fixedListPointers.Add(listPointer);
            int oldBufferLength = _listBuffer.Length;
            int newBufferLength = oldBufferLength + capacity;
            _listBuffer.Length = newBufferLength;
            for (int i = oldBufferLength; i < newBufferLength; i++) _listBuffer[i] = default;
        }
        public void AddListUninitialized(int capacity)
        {
            capacity = math.max(capacity, 1);
            FixedListPointer listPointer = new FixedListPointer
            {
                Start = _listBuffer.Length,
                Length = 0,
                Capacity = capacity
            };
            _fixedListPointers.Add(listPointer);
            _listBuffer.Length += capacity;
        }
        public void AddToListNoResize(int listIndex, T value)
        {
            FixedListPointer listPointer = _fixedListPointers[listIndex];
            bool isFull = listPointer.Length == listPointer.Capacity;
            int indexToAdd = listPointer.Start + math.select(listPointer.Length, listPointer.Length - 1, isFull);
            int newLength = math.select(listPointer.Length + 1, listPointer.Length, isFull);
            _listBuffer[indexToAdd] = value;
            listPointer.Length = newLength;
            _fixedListPointers[listIndex] = listPointer;
        }
        public void ReinitializeAllListValues()
        {
            NativeArray<T> listBuffer = _listBuffer.AsArray();
            for(int i = 0; i < listBuffer.Length; i++) listBuffer[i] = default;
        }
        public ListWriter GetListWriter(int listIndex)
        {
            return new ListWriter(_fixedListPointers.AsArray(), _listBuffer.AsArray(), listIndex);
        }
        public Array AsArray()
        {
            return new Array(_fixedListPointers.AsArray(), _listBuffer.AsArray());
        }
        public Array AsDeferredJobArray()
        {
            return new Array(_fixedListPointers.AsDeferredJobArray(), _listBuffer.AsDeferredJobArray());
        }
    }
}
