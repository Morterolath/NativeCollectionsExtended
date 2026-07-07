using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct NativeNestedList<T> where T : unmanaged
    {
        public struct ParallelWriter
        {
            NativeArray<ListPointer> _listPointers;
            [NativeDisableParallelForRestriction]
            NativeArray<T> _listData;

            internal ParallelWriter(NativeArray<ListPointer> listPointers, NativeArray<T> listData)
            {
                _listPointers = listPointers;
                _listData = listData;
            }
            public int Length
            {
                get { return _listPointers.Length; }
            }
            public T this[int listIndex, int dataIndex]
            {
                get
                {
                    return _listData[_listPointers[listIndex].StartIndex + dataIndex];
                }
                set
                {
                    _listData[_listPointers[listIndex].StartIndex + dataIndex] = value;
                }
            }
            public NativeSlice<T> this[int listIndex]
            {
                get
                {
                    ListPointer listPointer = _listPointers[listIndex];
                    return new NativeSlice<T>(_listData, listPointer.StartIndex, listPointer.Length);
                }
            }
            public int ListLength(int listIndex)
            {
                return _listPointers[listIndex].Length;
            }
            public int ListCapacity(int listIndex)
            {
                return _listPointers[listIndex].Capacity;
            }
            public void ClearList(int listIndex)
            {
                ListPointer listPtr = _listPointers[listIndex];
                listPtr.Length = 0;
                _listPointers[listIndex] = listPtr;
            }
            public void AddToListNoResize(int listIndex, T element)
            {
                ListPointer listPointer = _listPointers[listIndex];
                if (listPointer.Length == listPointer.Capacity)
                {
                    return;
                }
                _listData[listPointer.StartIndex + listPointer.Length] = element;
                listPointer.Length++;
                _listPointers[listIndex] = listPointer;
            }
            public bool IsFull(int listIndex)
            {
                ListPointer listPointer = _listPointers[listIndex];
                return listPointer.Length == listPointer.Capacity;
            }
            public void RemoveAtSwapBack(int listIndex, int elementIndex)
            {
                ListPointer listPointer = _listPointers[listIndex];
                if (listPointer.Length == 0) { return; }
                int lastIndex = listPointer.Length - 1;
                _listData[listPointer.StartIndex + elementIndex] = _listData[listPointer.StartIndex + lastIndex];
                listPointer.Length--;
                _listPointers[listIndex] = listPointer;
            }
        }
        internal NativeList<ListPointer> _dataPointersEachList;
        internal NativeList<T> _listData;
        internal NativeList<int> _blockLinkedListIndiciesEachList;
        internal NativeLinkedList<Block> _blockLinkedList;
        internal NativeMaxIntHeap<int> _freeBlockHeap;

        const int DEFAULT_LIST_CAPACITY = 32;
        public bool IsCreated
        {
            get
            {
                return _listData.IsCreated;
            }
        }
        public int Length
        {
            get { return _dataPointersEachList.Length; }
            set
            {
                if(value > _dataPointersEachList.Length)
                {
                    _dataPointersEachList.Length = value;
                    _blockLinkedListIndiciesEachList.Length = value;
                }
                if(value < _dataPointersEachList.Length)
                {
                    for(int i = value; i < _dataPointersEachList.Length; i++)
                    {
                        RemoveList(i);
                    }
                    _dataPointersEachList.Length = value;
                    _blockLinkedListIndiciesEachList.Length = value;
                }
            }
        }

        public NativeNestedList(int capacity, Allocator allocator)
        {
            _dataPointersEachList = new NativeList<ListPointer>(capacity, allocator);
            _listData = new NativeList<T>(allocator);
            _blockLinkedListIndiciesEachList = new NativeList<int>(capacity, allocator);
            _blockLinkedList = new NativeLinkedList<Block>(allocator);
            _freeBlockHeap = new NativeMaxIntHeap<int>(0,allocator);
        }
        public ParallelWriter AsDeferredParallelWriter()
        {
            return new ParallelWriter(_dataPointersEachList.AsDeferredJobArray(), _listData.AsDeferredJobArray());
        }
        public void Dispose()
        {
            _dataPointersEachList.Dispose();
            _listData.Dispose();
            _blockLinkedListIndiciesEachList.Dispose();
            _blockLinkedList.Dispose();
            _freeBlockHeap.Dispose();
        }
        public void AddList(int initialCapacity = DEFAULT_LIST_CAPACITY)
        {
            initialCapacity = math.max(0, initialCapacity);
            RequestMemoryBlock(initialCapacity, out int blockStart, out int blockLinkedListIndex);
            _dataPointersEachList.Add(new ListPointer(blockStart, 0, initialCapacity));
            _blockLinkedListIndiciesEachList.Add(blockLinkedListIndex);
        }
        public void SetList(int index, int initialCapacity = DEFAULT_LIST_CAPACITY)
        {
            initialCapacity = math.max(0, initialCapacity);
            ListPointer pointer = _dataPointersEachList[index];
            if (!pointer.IsNull())
            {
                RemoveList(index);
            }
            int blockStart = 0;
            if(initialCapacity != 0)
            {
                RequestMemoryBlock(initialCapacity, out blockStart, out int blockLinkedListIndex);
                _blockLinkedListIndiciesEachList[index] = blockLinkedListIndex;
            }
            _dataPointersEachList[index] = new ListPointer(blockStart, 0, initialCapacity);
        }
        public void AddToList(int listIndex, T data)
        {
            ListPointer listPointer = _dataPointersEachList[listIndex];
            if (listPointer.IsNull()) return;
            if(listPointer.Length == listPointer.Capacity)
            {
                int newCapacity = math.select(listPointer.Capacity * 2, 1, listPointer.Capacity == 0);
                RequestMemoryBlock(newCapacity, out int blockStart, out int blockLinkedListIndex);
                if (listPointer.Capacity != 0) SetMemoryBlockFree(_blockLinkedListIndiciesEachList[listIndex]);
                CopyData(listPointer.StartIndex, listPointer.Length, blockStart);
                listPointer.StartIndex = blockStart;
                listPointer.Capacity = newCapacity;
                _blockLinkedListIndiciesEachList[listIndex] = blockLinkedListIndex;
            }
            _listData[listPointer.StartIndex + listPointer.Length] = data;
            listPointer.Length++;
            _dataPointersEachList[listIndex] = listPointer;
        }
        public void RemoveAtSwapBack(int listIndex, int elementIndex)
        {
            ListPointer listPointer = _dataPointersEachList[listIndex];
            if(listPointer.Length == 0) { return; }
            int lastIndex = listPointer.Length - 1;
            _listData[listPointer.StartIndex + elementIndex] = _listData[listPointer.StartIndex + lastIndex];
            listPointer.Length--;
            _dataPointersEachList[listIndex] = listPointer;
        }
        public int GetLengthOfList(int listIndex)
        {
            return _dataPointersEachList[listIndex].Length;
        }
        public int GetCapacityOfList(int listIndex)
        {
            return _dataPointersEachList[listIndex].Capacity;
        }
        public void SetCapacityOfListIfGreater(int listIndex, int newGreaterCapacity)
        {
            ListPointer listPointer = _dataPointersEachList[listIndex];
            if (newGreaterCapacity > listPointer.Capacity)
            {
                int newCapacity = math.ceilpow2(newGreaterCapacity);
                RequestMemoryBlock(newCapacity, out int blockStart, out int blockLinkedListIndex);
                if (listPointer.Capacity != 0) SetMemoryBlockFree(_blockLinkedListIndiciesEachList[listIndex]);
                CopyData(listPointer.StartIndex, listPointer.Length, blockStart);
                listPointer.StartIndex = blockStart;
                listPointer.Capacity = newCapacity;
                _blockLinkedListIndiciesEachList[listIndex] = blockLinkedListIndex;
                _dataPointersEachList[listIndex] = listPointer;
            }
        }
        public void SetLengthOfList(int listIndex, int newLength)
        {
            ListPointer listPointer = _dataPointersEachList[listIndex];
            if (newLength > listPointer.Capacity)
            {
                int newCapacity = math.ceilpow2(newLength);
                RequestMemoryBlock(newCapacity, out int blockStart, out int blockLinkedListIndex);
                if (listPointer.Capacity != 0) SetMemoryBlockFree(_blockLinkedListIndiciesEachList[listIndex]);
                CopyData(listPointer.StartIndex, listPointer.Length, blockStart);
                listPointer.StartIndex = blockStart;
                listPointer.Capacity = newCapacity;
                _blockLinkedListIndiciesEachList[listIndex] = blockLinkedListIndex;
            }
            listPointer.Length = newLength;
            _dataPointersEachList[listIndex] = listPointer;
        }
        public void ClearList(int listIndex)
        {
            ListPointer listPointer = _dataPointersEachList[listIndex];
            listPointer.Length = 0;
            _dataPointersEachList[listIndex] = listPointer;
        }
        public T this[int listIndex, int dataIndex]
        {
            get
            {
                return _listData[_dataPointersEachList[listIndex].StartIndex + dataIndex];
            }
            set
            {
                _listData[_dataPointersEachList[listIndex].StartIndex + dataIndex] = value;
            }
        }
        public NativeSlice<T> this[int listIndex]
        {
            get
            {
                ListPointer listPointer = _dataPointersEachList[listIndex];
                return new NativeSlice<T>(_listData.AsArray(), listPointer.StartIndex, listPointer.Length);
            }
        }
        public void RemoveList(int index)
        {
            ListPointer pointer = _dataPointersEachList[index];
            if (pointer.IsNull()) { return; }
            _dataPointersEachList[index] = ListPointer.NULL;
            SetMemoryBlockFree(_blockLinkedListIndiciesEachList[index]);
        }
        public bool IsAllocated(int listIndex)
        {
            return !_dataPointersEachList[listIndex].IsNull();
        }
        void RemoveTrashDataFromHeap()
        {
            bool wasNull = true;
            while (!_freeBlockHeap.IsEmpty && wasNull)
            {
                int peak = _freeBlockHeap.GetMax();
                wasNull = _blockLinkedList.FreeMemoryIfNull(peak);
                if (wasNull) { _freeBlockHeap.Dequeue(); }
            }
        }
        void CopyData(int fromStart, int fromLength, int toStart)
        {
            for(int i = 0; i < fromLength; i++)
            {
                _listData[toStart + i] = _listData[fromStart + i];
            }
        }
        void SetMemoryBlockFree(int blockLinkedListIndex)
        {
            Block currentBlock = _blockLinkedList.GetData(blockLinkedListIndex);
            if(_blockLinkedList.TryGetPreviousIndex(blockLinkedListIndex, out int previousIndex))
            {
                Block previousBlock = _blockLinkedList.GetData(previousIndex);
                if (previousBlock.IsFree)
                {
                    currentBlock.BlockStart = previousBlock.BlockStart;
                    currentBlock.BlockLength += previousBlock.BlockLength;
                    _blockLinkedList.TryRemove(previousIndex, FreedMemoryArgument.DoNotReuseFreedMemory);
                }
            }
            if (_blockLinkedList.TryGetNextIndex(blockLinkedListIndex, out int nextIndex))
            {
                Block nextBlock = _blockLinkedList.GetData(nextIndex);
                if(nextBlock.IsFree)
                {
                    currentBlock.BlockLength += nextBlock.BlockLength;
                    _blockLinkedList.TryRemove(nextIndex, FreedMemoryArgument.DoNotReuseFreedMemory);
                }
            }
            currentBlock.IsFree = true;
            _blockLinkedList.SetData(blockLinkedListIndex, currentBlock);
            _freeBlockHeap.Enqueue(blockLinkedListIndex, currentBlock.BlockLength);
        }
        void RequestMemoryBlock(int capacity, out int blockStart, out int blockLinkedListIndex)
        {
            blockLinkedListIndex = -1;
            blockStart = -1;
            if(capacity == 0) { return; }
            RemoveTrashDataFromHeap();
            if (!_freeBlockHeap.IsEmpty)
            {
                int freeBlockNodeLinkedListIndex = _freeBlockHeap.GetMax();
                Block freeBlock = _blockLinkedList.GetData(freeBlockNodeLinkedListIndex);
                if(freeBlock.BlockLength == capacity)
                {
                    _freeBlockHeap.Dequeue();
                    freeBlock.IsFree = false;
                    _blockLinkedList.SetData(freeBlockNodeLinkedListIndex, freeBlock);
                    blockStart = freeBlock.BlockStart;
                    blockLinkedListIndex = freeBlockNodeLinkedListIndex;
                    return;
                }
                else if(freeBlock.BlockLength > capacity)
                {
                    _freeBlockHeap.Dequeue();
                    Block newBlock = new Block(freeBlock.BlockStart, capacity, false);
                    int newBlockLinkedListIndex = _blockLinkedList.InsertPrevious(freeBlockNodeLinkedListIndex, newBlock);

                    Block remainingBlock = new Block(freeBlock.BlockStart + capacity, freeBlock.BlockLength - capacity, true);
                    _blockLinkedList.SetData(freeBlockNodeLinkedListIndex, remainingBlock);
                    _freeBlockHeap.Enqueue(freeBlockNodeLinkedListIndex, remainingBlock.BlockLength);

                    blockStart = newBlock.BlockStart;
                    blockLinkedListIndex = newBlockLinkedListIndex;
                    return;
                }
            }
            blockStart = _listData.Length;
            _listData.Length += capacity;
            blockLinkedListIndex = _blockLinkedList.AddToTail(new Block(blockStart, capacity, false));
        }
    }
}
