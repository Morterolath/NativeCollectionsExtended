using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct ListOfLinkedLists<T>
        where T : unmanaged
    {
        public struct Enumerator
        {
            readonly int _bucketSize;
            readonly int _startBucketIndex;
            readonly int _lastBucketIndex;
            readonly int _lastBucketLength;
            NativeArray<T> _listData;
            NativeArray<int> _nextBucketIndexEachBucket;

            int _iterationBucketIndex;
            NativeSlice<T> _currentBucket;

            internal Enumerator(NativeArray<T> listData, NativeArray<int> nextBucketIndexEachBucket, ListPointer listPtr, int bucketSize)
            {
                _bucketSize = bucketSize;
                _startBucketIndex = listPtr.StartIndex / bucketSize;
                _lastBucketIndex = listPtr.LastElementIndex / bucketSize;
                _lastBucketLength = (listPtr.LastElementIndex % bucketSize) + 1;
                _listData = listData;
                _nextBucketIndexEachBucket = nextBucketIndexEachBucket;
                _iterationBucketIndex = _startBucketIndex;
                _currentBucket = default;
            }
            public bool MoveNext()
            {
                if (_iterationBucketIndex == 0) return false;
                bool firstBucket = _iterationBucketIndex == _startBucketIndex;
                bool lastBucket = _iterationBucketIndex == _lastBucketIndex;
                int bucketStart = _iterationBucketIndex * _bucketSize;
                bucketStart = math.select(bucketStart, bucketStart + 1, firstBucket);
                int bucketLength = math.select(_bucketSize, _lastBucketLength, lastBucket);
                bucketLength = math.select(bucketLength, bucketLength - 1, firstBucket);
                _currentBucket = _listData.Slice(bucketStart, bucketLength);
                _iterationBucketIndex = _nextBucketIndexEachBucket[_iterationBucketIndex];
                _iterationBucketIndex = math.select(_iterationBucketIndex, 0, lastBucket);
                return true;
            }
            public NativeSlice<T> Current
            {
                get
                {
                    return _currentBucket;
                }
            }
            public void Reset()
            {
                _iterationBucketIndex = _startBucketIndex;
                _currentBucket = default;
            }
        }
        internal struct ListPointer
        {
            internal int StartIndex;
            internal int EndIndex;
            internal int LastElementIndex;
            internal int Capacity;
        }
        readonly int _bucketSize;
        NativeList<ListPointer> _listPointers;
        NativeList<T> _listData;
        NativeList<int> _nextBucketIndexEachBucket;
        NativeList<int> _unusedBucketIndicies;

        //Min bucket size can not be less than 2
        const int MIN_BUCKET_SIZE = 16;
        public ListOfLinkedLists(int bucketSize, Allocator allocator)
        {
            _listPointers = new NativeList<ListPointer>(allocator);
            _listData = new NativeList<T>(allocator);
            _nextBucketIndexEachBucket = new NativeList<int>(allocator);
            _unusedBucketIndicies = new NativeList<int>(allocator);

            bucketSize = math.max(MIN_BUCKET_SIZE, bucketSize);
            _bucketSize = bucketSize;
            _listData.Length += bucketSize;
            _nextBucketIndexEachBucket.Length += 1;
        }

        public int Count
        {
            get
            {
                return _listPointers.Length;
            }
            set
            {
                int oldCount = _listPointers.Length;
                int newCount = math.max(value, 0);
                _listPointers.Length = newCount;

                for(int i = newCount; i < oldCount; i++)
                {
                    DeallocateList(i);
                }
                for(int i = oldCount; i < newCount; i++)
                {
                    _listPointers[i] = default;
                }
            }
        }
        public void DeallocateList(int listIndex)
        {
            ListPointer listPtr = _listPointers[listIndex];
            _listPointers[listIndex] = default;

            int startBucketIndex = listPtr.StartIndex / _bucketSize;
            _unusedBucketIndicies.Add(startBucketIndex);
            _unusedBucketIndicies.Length -= math.select(0, 1, startBucketIndex == 0);
        }
        public void Append(int listIndex, T element)
        {
            ListPointer listPtr = _listPointers[listIndex];
            listPtr = GoNextIndex(listPtr);
            _listPointers[listIndex] = listPtr;
            _listData[listPtr.LastElementIndex] = element;
        }
        public void IncreaseCapacity(int listIndex, int newCapacity)
        {
            ListPointer listPtr = _listPointers[listIndex];
            int oldCapacity = listPtr.Capacity;
            newCapacity = math.max(newCapacity, oldCapacity);

            int oldBucketCnt = oldCapacity / _bucketSize;
            int newBucketCnt = newCapacity / _bucketSize;

            for(int i = oldBucketCnt; i < newBucketCnt; i++)
            {
                listPtr = AllocateBucketFor(listPtr);
            }
            _listPointers[listIndex] = listPtr;
        }
        public Enumerator GetEnumerator(int listIndex)
        {
            return new Enumerator(_listData.AsArray(), _nextBucketIndexEachBucket.AsArray(), _listPointers[listIndex], _bucketSize);
        }
        ListPointer GoNextIndex(ListPointer listPtr)
        {
            int lastBucketIndex = listPtr.LastElementIndex / _bucketSize;
            bool lastBucketDoesNotExist = listPtr.LastElementIndex == 0;
            bool nextBucketDoesNotExist = listPtr.LastElementIndex == listPtr.EndIndex;
            bool needToAllocate = lastBucketDoesNotExist | nextBucketDoesNotExist;
            bool goNextBlock = (listPtr.LastElementIndex + 1) % _bucketSize == 0;
            if (!needToAllocate)
            {
                if (goNextBlock) listPtr.LastElementIndex = _nextBucketIndexEachBucket[lastBucketIndex] * _bucketSize;
                else listPtr.LastElementIndex++;
            }
            else
            {
                int newBucketIndex = AllocateBucket();
                int newBucketStartIndex = newBucketIndex * _bucketSize;
                listPtr.StartIndex = math.select(listPtr.StartIndex, newBucketStartIndex, lastBucketDoesNotExist);
                listPtr.EndIndex = newBucketStartIndex + _bucketSize - 1;
                listPtr.Capacity += _bucketSize;
                listPtr.LastElementIndex = math.select(newBucketStartIndex, newBucketStartIndex + 1, lastBucketDoesNotExist);
                _nextBucketIndexEachBucket[lastBucketIndex] = math.select(newBucketIndex, 0, lastBucketDoesNotExist);
            }
            return listPtr;
        }
        ListPointer AllocateBucketFor(ListPointer listPtr)
        {
            int lastBucketIndex = listPtr.EndIndex / _bucketSize;
            bool lastBucketDoesNotExist = listPtr.EndIndex == 0;
            int newBucketIndex = AllocateBucket();
            int newBucketStartIndex = newBucketIndex * _bucketSize;

            listPtr.StartIndex = math.select(listPtr.StartIndex, newBucketStartIndex,lastBucketDoesNotExist);
            listPtr.EndIndex = newBucketStartIndex + _bucketSize - 1;
            listPtr.Capacity += _bucketSize;
            listPtr.LastElementIndex = math.select(listPtr.LastElementIndex, newBucketStartIndex, lastBucketDoesNotExist);

            _nextBucketIndexEachBucket[lastBucketIndex] = math.select(newBucketIndex, 0, lastBucketDoesNotExist);
            
            return listPtr;
        }
        int AllocateBucket()
        {
            if (_unusedBucketIndicies.IsEmpty)
            {
                int newBucketIndex = _listData.Length / _bucketSize;
                _listData.Length += _bucketSize;
                _nextBucketIndexEachBucket.Add(0);
                return newBucketIndex;
            }
            else
            {
                int unusedListLastIndex = _unusedBucketIndicies.Length - 1;
                int newBucketIndex = _unusedBucketIndicies[unusedListLastIndex];
                int nextOfNewBucket = _nextBucketIndexEachBucket[newBucketIndex];
                _nextBucketIndexEachBucket[newBucketIndex] = 0;
                _unusedBucketIndicies[unusedListLastIndex] = nextOfNewBucket;
                _unusedBucketIndicies.Length -= math.select(0, 1, nextOfNewBucket == 0);
                return newBucketIndex;
            }
        }
    }
}
