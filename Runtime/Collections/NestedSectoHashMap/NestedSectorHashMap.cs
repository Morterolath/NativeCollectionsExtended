using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Linq;

namespace NativeCollectionsExtended
{
    public struct NestedSectorHashMap<V> 
        where V : unmanaged
    {
        public struct ParallelWriter
        {
            readonly int _hashWidth;
            readonly int _bucketSize;
            readonly int _sectorMatrixColAmount;
            readonly int _hashGridColAmount;
            NativeArray<Map> _maps;
            [NativeDisableParallelForRestriction] NativeArray<Hash> _hashes;
            [NativeDisableParallelForRestriction] NativeArray<Key> _keys;
            [NativeDisableParallelForRestriction] NativeArray<V> _values;
            [ReadOnly] NativeArray<int> _nextBucketIndexEachBucket;
            internal ParallelWriter(NativeArray<Map> maps, NativeArray<Hash> hashes, NativeArray<Key> keys, NativeArray<V> values, NativeArray<int> nextBuckeIndexEachBucket,
                int hashWidth, int bucketSize, int sectorMatrixColAmount, int hashGridColAmount)
            {
                _maps = maps;
                _hashes = hashes;
                _keys = keys;
                _values = values;
                _nextBucketIndexEachBucket = nextBuckeIndexEachBucket;
                _hashWidth = hashWidth;
                _bucketSize = bucketSize;
                _sectorMatrixColAmount = sectorMatrixColAmount;
                _hashGridColAmount = hashGridColAmount;
            }
            public MapWriter GetMapWriter(int mapIndex)
            {
                return new MapWriter(mapIndex, _hashWidth, _bucketSize, _sectorMatrixColAmount, _hashGridColAmount, _maps, _hashes, _keys, _values, _nextBucketIndexEachBucket);
            }
        }
        public struct MapWriter
        {
            readonly int _mapIndex;
            readonly int _hashWidth;
            readonly int _bucketSize;
            readonly int _sectorMatrixColAmount;
            readonly int _hashGridColAmount;
            NativeArray<Map> _maps;
            NativeArray<Hash> _hashes;
            NativeArray<Key>_keys;
            NativeArray<V> _values;
            NativeArray<int> _nextBucketIndexEachBucket;

            internal MapWriter(int mapIndex, int hashWidth, int bucketSize, int sectorMatrixColAmount, int hashGridColAmount,
                NativeArray<Map> maps, NativeArray<Hash> hashes, NativeArray<Key> keys, NativeArray<V> values, NativeArray<int> nextBucketIndexEachBucket)
            {
                _mapIndex = mapIndex;
                _hashWidth = hashWidth;
                _bucketSize = bucketSize;
                _sectorMatrixColAmount = sectorMatrixColAmount;
                _hashGridColAmount = hashGridColAmount;
                _maps = maps;
                _hashes = hashes;
                _keys = keys;
                _values = values;
                _nextBucketIndexEachBucket = nextBucketIndexEachBucket;
            }

            public bool AddNoResize(int key, V value)
            {
                int hashIndex = GetHashIndex(_mapIndex, key, _hashWidth, _sectorMatrixColAmount, _hashGridColAmount);
                Hash hash = _hashes[hashIndex];

                bool keyFound = ContainsKeyInHash(hash, key, _keys);

                Map map = _maps[_mapIndex];
                bool isFull = map.EndIndex == map.KeyTailIndex;
                if (keyFound || isFull) return false;

                bool tailKeyIsAtTheEndOfBucket = (map.KeyTailIndex + 1) % _bucketSize == 0;
                if (tailKeyIsAtTheEndOfBucket)
                {
                    int curTailBucketIndex = map.KeyTailIndex / _bucketSize;
                    map.KeyTailIndex = _nextBucketIndexEachBucket[curTailBucketIndex] * _bucketSize;
                }
                map.KeyTailIndex = math.select(map.KeyTailIndex + 1, map.KeyTailIndex, tailKeyIsAtTheEndOfBucket);
                map.KeyCount++;

                Key newKey = new Key { _key = key, _nextIndex = hash.HeadKeyIndex };
                hash.HeadKeyIndex = map.KeyTailIndex;
                _keys[map.KeyTailIndex] = newKey;
                _values[map.KeyTailIndex] = value;
                _maps[_mapIndex] = map;
                _hashes[hashIndex] = hash;
                return true;
            }
            public bool AddGetNoResize(int key, V value, out V valueOut, out DirectAccess directAccess)
            {
                int hashIndex = GetHashIndex(_mapIndex, key, _hashWidth, _sectorMatrixColAmount, _hashGridColAmount);
                Hash hash = _hashes[hashIndex];

                bool keyFound = ContainsValueInHash(hash, key, _keys, _values, out valueOut, out int valueDirectAccess);

                Map map = _maps[_mapIndex];
                bool isFull = map.EndIndex == map.KeyTailIndex;
                if (keyFound || isFull)
                {
                    directAccess = new DirectAccess(valueDirectAccess, _values);
                    return !isFull;
                }

                bool tailKeyIsAtTheEndOfBucket = (map.KeyTailIndex + 1) % _bucketSize == 0;
                if (tailKeyIsAtTheEndOfBucket)
                {
                    int curTailBucketIndex = map.KeyTailIndex / _bucketSize;
                    map.KeyTailIndex = _nextBucketIndexEachBucket[curTailBucketIndex] * _bucketSize;
                }
                map.KeyTailIndex = math.select(map.KeyTailIndex + 1, map.KeyTailIndex, tailKeyIsAtTheEndOfBucket);
                map.KeyCount++;

                Key newKey = new Key { _key = key, _nextIndex = hash.HeadKeyIndex };
                hash.HeadKeyIndex = map.KeyTailIndex;
                _keys[map.KeyTailIndex] = newKey;
                _values[map.KeyTailIndex] = value;
                _maps[_mapIndex] = map;
                _hashes[hashIndex] = hash;
                directAccess = new DirectAccess(map.KeyTailIndex, _values);
                return true;
            }
            public bool GetValue(int key, out V value, out DirectAccess directAccess)
            {
                int hashStart = _mapIndex * _hashWidth;
                int hashOffset = GetHashOffset(key, _hashWidth, _sectorMatrixColAmount, _hashGridColAmount);
                Hash hash = _hashes[hashStart + hashOffset];
                bool found = ContainsValueInHash(hash, key, _keys, _values, out value, out int valueDirectAccess);
                directAccess = new DirectAccess(valueDirectAccess, _values);
                return found;
            }
        }
        public struct DirectAccess
        {
            public readonly int _valueDirectAccess;
            NativeArray<V> _values;
            internal DirectAccess(int valueDirectAccess, NativeArray<V> values)
            {
                _valueDirectAccess = valueDirectAccess;
                _values = values;
            }

            public void SetValueDirect(V value)
            {
                if(_valueDirectAccess == 0) return;
                _values[_valueDirectAccess] = value;
            }
        }
        public struct Enumerator
        {
            readonly NativeArray<int> _nextBucketEachBucket;
            readonly NativeArray<Key> _keys;
            readonly NativeArray<V> _values;
            readonly int _bucketSize;
            readonly int _keyHeadIndex;
            readonly int _keyTailIndex;
            int _curKeyIndex;
            V _valueToReturn;
            int _keyToReturn;
            bool _continute;

            internal Enumerator(Map map, NativeArray<Key> keys, NativeArray<V> values, NativeArray<int> nextBucketEachBucket, int bucketSize)
            {
                _nextBucketEachBucket = nextBucketEachBucket;
                _keys = keys;
                _values = values;
                _bucketSize = bucketSize;
                _keyHeadIndex = map.KeyHeadIndex;
                _keyTailIndex = map.KeyTailIndex;
                _keyToReturn = 0;
                _valueToReturn = default;
                _curKeyIndex = map.KeyHeadIndex + 1;
                _continute = map.KeyHeadIndex != map.KeyTailIndex;
            }
            public int CurrentKey
            {
                get
                {
                    return _keyToReturn;
                }
            }
            public V CurrentValue
            {
                get
                {
                    return _valueToReturn;
                }
            }
            public bool MoveNext()
            {
                if (_continute)
                {
                    _keyToReturn = _keys[_curKeyIndex]._key;
                    _valueToReturn = _values[_curKeyIndex];
                    _continute = _curKeyIndex != _keyTailIndex;
                    if((_curKeyIndex + 1) % _bucketSize == 0 && _continute)
                    {
                        _curKeyIndex = _nextBucketEachBucket[_curKeyIndex / _bucketSize] * _bucketSize;
                    }
                    else if(_continute)
                    {
                        _curKeyIndex++;
                    }
                    return true;
                }
                return false;
            }
            public void Reset()
            {
                _curKeyIndex = _keyHeadIndex + 1;
                _keyToReturn = 0;
                _valueToReturn = default;
                _continute = _keyHeadIndex != _keyTailIndex;
            }
        }
        internal struct Map
        {
            internal int BucketCount;
            internal int KeyCount;
            internal int EndIndex;
            internal int KeyHeadIndex;
            internal int KeyTailIndex;

            internal bool Equals(Map other)
            {
                return
                    KeyHeadIndex == other.KeyHeadIndex
                    & KeyTailIndex == other.KeyTailIndex
                    & BucketCount == other.BucketCount
                    & EndIndex == other.EndIndex
                    & KeyCount == other.KeyCount;
            }
        }
        internal struct Hash
        {
            internal int HeadKeyIndex;
        }
        internal struct Key
        {
            internal int _key;
            internal int _nextIndex;
        }

        readonly int _hashWidth;
        readonly int _bucketSize;
        readonly int _sectorMatrixColAmount;
        readonly int _hashGridColAmount;
        NativeList<Map> _maps;
        NativeList<Hash> _hashes;
        NativeList<Key> _keys;
        NativeList<V> _values;
        NativeList<int> _nextBucketIndexEachBucket;
        NativeList<int> _unusedBucketIndicies;

        //MIN_BUCKET_SIZE must be greater than 1.
        const int MIN_BUCKET_SIZE = 16;
        public NestedSectorHashMap(int initialCount, int bucketSize, int sectorMatrixColAmount, int hashGridColAmount, Allocator allocator)
        {
            _maps = new NativeList<Map>(allocator);
            _hashes = new NativeList<Hash>(allocator);
            _keys = new NativeList<Key>(allocator);
            _values = new NativeList<V>(allocator);
            _nextBucketIndexEachBucket = new NativeList<int>(allocator);
            _unusedBucketIndicies = new NativeList<int>(allocator);

            bucketSize = math.max(bucketSize, MIN_BUCKET_SIZE);
            sectorMatrixColAmount = math.max(sectorMatrixColAmount, 4);
            hashGridColAmount = math.max(hashGridColAmount, 4);
            initialCount = math.max(initialCount, 0);

            _bucketSize = bucketSize;
            _sectorMatrixColAmount = sectorMatrixColAmount;
            _hashGridColAmount = hashGridColAmount;
            _hashWidth = hashGridColAmount * hashGridColAmount;

            _maps.Length = initialCount;
            _hashes.Length = initialCount * _hashWidth;
            _keys.Length = _bucketSize;
            _values.Length = _bucketSize;
            _nextBucketIndexEachBucket.Length = 1;
        }

        public int Count
        {
            get
            {
                return _maps.Length;
            }
            set
            {
                value = math.max(value, 0);

                int oldCount = _maps.Length;
                int newCount = value;

                _maps.Length = math.max(oldCount, newCount);
                for (int i = oldCount; i < newCount; i++)
                {
                    _maps[i] = default;
                }

                for(int i = newCount; i < oldCount; i++)
                {
                    RemoveMap(i);
                }
                _maps.Length = newCount;

                int oldHashArrayLength = oldCount * _hashWidth;
                int newHashArrayLength = newCount * _hashWidth;
                _hashes.Length = newHashArrayLength;
                for (int i = oldHashArrayLength; i < newHashArrayLength; i++) _hashes[i] = default;
            }
        }
        public void Dispose()
        {
            _maps.Dispose();
            _hashes.Dispose();
            _keys.Dispose();
            _values.Dispose();
            _nextBucketIndexEachBucket.Dispose();
            _unusedBucketIndicies.Dispose();
        }
        public void RemoveMap(int mapIndex)
        {
            Map map = _maps[mapIndex];
            if (!map.Equals(default))
            {
                _maps[mapIndex] = default;
                int headBucketIndex = map.KeyHeadIndex / _bucketSize;
                _unusedBucketIndicies.Add(headBucketIndex);

                for(int i = 0; i < _hashWidth; i++)
                {
                    _hashes[mapIndex * _hashWidth + i] = default;
                }
            }
        }
        public int KeyCount(int mapIndex)
        {
            return _maps[mapIndex].KeyCount;
        }
        public void IncreaseCapacity(int mapIndex, int increasedCapacity)
        {
            Map map = _maps[mapIndex];
            int curBucketCount = map.BucketCount;
            int newBucketCount = (increasedCapacity / _bucketSize) + math.select(1, 0, increasedCapacity % _bucketSize == 0);
            newBucketCount = math.max(curBucketCount, newBucketCount);
            int bucketRequired = newBucketCount - curBucketCount;
            for(int i = 0; i < bucketRequired; i++)
            {
                int endBucketIndex = map.EndIndex / _bucketSize;
                int newBucketIndex = AllocateKeyBucket(_unusedBucketIndicies, _keys, _values, _nextBucketIndexEachBucket, _bucketSize);
                int newBucketStartIndex = newBucketIndex * _bucketSize;
                _nextBucketIndexEachBucket[endBucketIndex] = math.select(newBucketIndex, 0, endBucketIndex == 0);
                map.KeyHeadIndex = math.select(map.KeyHeadIndex, newBucketStartIndex, map.KeyHeadIndex == 0);
                map.KeyTailIndex = math.select(map.KeyTailIndex, newBucketStartIndex, map.KeyTailIndex == 0);
                map.EndIndex = newBucketStartIndex + _bucketSize - 1;
            }
            map.BucketCount = newBucketCount;
            _maps[mapIndex] = map;
        }
        public bool IsRemoved(int mapIndex)
        {
            return _maps[mapIndex].Equals(default);
        }
        public bool Add(int mapIndex, int key, V value)
        {
            int hashIndex = GetHashIndex(mapIndex, key, _hashWidth, _sectorMatrixColAmount, _hashGridColAmount);
            Hash hash = _hashes[hashIndex];
            bool keyFound = ContainsKeyInHash(hash, key, _keys.AsArray());
            if (keyFound) return false;

            Map map = _maps[mapIndex];
            map = GetNextTail(map);

            int keyIndexForNewKey = map.KeyTailIndex;
            Key newKey = new Key { _key = key, _nextIndex = hash.HeadKeyIndex };
            hash.HeadKeyIndex = keyIndexForNewKey;
            _keys[keyIndexForNewKey] = newKey;
            _values[keyIndexForNewKey] = value;
            _maps[mapIndex] = map;
            _hashes[hashIndex] = hash;
            return true;
        }
        public bool Contains(int mapIndex, int key)
        {
            int hashStart = mapIndex * _hashWidth;
            int hashOffset = GetHashOffset(key, _hashWidth, _sectorMatrixColAmount, _hashGridColAmount);
            return ContainsKeyInHash(_hashes[hashStart + hashOffset], key, _keys.AsArray());
        }
        public bool GetValue(int mapIndex, int key, out V value)
        {
            int hashStart = mapIndex * _hashWidth;
            int hashOffset = GetHashOffset(key, _hashWidth, _sectorMatrixColAmount, _hashGridColAmount);
            Hash hash = _hashes[hashStart + hashOffset];
            return ContainsValueInHash(hash, key, _keys.AsArray(), _values.AsArray(), out value);
        }
        public bool GetValue(int mapIndex, int key, out V value, out int valueDirectAccess)
        {
            int hashStart = mapIndex * _hashWidth;
            int hashOffset = GetHashOffset(key, _hashWidth, _sectorMatrixColAmount, _hashGridColAmount);
            Hash hash = _hashes[hashStart + hashOffset];
            return ContainsValueInHash(hash, key, _keys.AsArray(), _values.AsArray(), out value, out valueDirectAccess);
        }
        public void SetValueDirect(int directAccess, V value)
        {
            _values[directAccess] = value;
        }
        public Enumerator GetEnumerator(int mapIndex)
        {
            return new Enumerator(_maps[mapIndex], _keys.AsArray(), _values.AsArray(), _nextBucketIndexEachBucket.AsArray(), _bucketSize);
        }
        public ParallelWriter GetParallelWriter()
        {
            return new ParallelWriter(_maps.AsArray(), _hashes.AsArray(), _keys.AsArray(), _values.AsArray(), _nextBucketIndexEachBucket.AsArray(),
                _hashWidth, _bucketSize, _sectorMatrixColAmount, _hashGridColAmount);
        }
        public ParallelWriter GetDeferredParallelWriter()
        {
            return new ParallelWriter(_maps.AsDeferredJobArray(), _hashes.AsDeferredJobArray(), _keys.AsDeferredJobArray(), _values.AsDeferredJobArray(),
                _nextBucketIndexEachBucket.AsDeferredJobArray(), _hashWidth, _bucketSize, _sectorMatrixColAmount, _hashGridColAmount);
        }
        Map GetNextTail(Map map)
        {
            int curTailBucketIndex = map.KeyTailIndex / _bucketSize;
            bool goNextBucket = (map.KeyTailIndex + 1) % _bucketSize == 0;
            bool curBucketDoesNotExist = map.KeyHeadIndex == 0;
            bool nextBucketDoesNotExist = map.KeyTailIndex == map.EndIndex;

            bool needToAllocate = nextBucketDoesNotExist || curBucketDoesNotExist;
            if (!needToAllocate)
            {
                if (goNextBucket) map.KeyTailIndex = _nextBucketIndexEachBucket[curTailBucketIndex] * _bucketSize;
                else map.KeyTailIndex++;
            }
            else
            {
                //Allocate new block
                int newBucketIndex = AllocateKeyBucket(_unusedBucketIndicies, _keys, _values, _nextBucketIndexEachBucket, _bucketSize);
                int newBucketStartIndex = newBucketIndex * _bucketSize;

                //Set as tail
                _nextBucketIndexEachBucket[curTailBucketIndex] = math.select(newBucketIndex, 0, curBucketDoesNotExist);
                map.KeyHeadIndex = math.select(map.KeyHeadIndex, newBucketStartIndex, curBucketDoesNotExist);
                map.KeyTailIndex = math.select(newBucketStartIndex, newBucketStartIndex + 1, curBucketDoesNotExist);
                map.BucketCount++;
                map.EndIndex = newBucketStartIndex + _bucketSize - 1;
            }
            map.KeyCount++;
            return map;
        }
        static int AllocateKeyBucket(NativeList<int> unusedBucketIndicies, NativeList<Key> keys, NativeList<V> values, NativeList<int> nextBucketIndexEachBucket, int bucketSize)
        {
            if (unusedBucketIndicies.IsEmpty)
            {
                int newBucketIndex = keys.Length / bucketSize;
                keys.Length += bucketSize;
                values.Length += bucketSize;
                nextBucketIndexEachBucket.Add(0);
                return newBucketIndex;
            }
            else
            {
                int lastIndex = unusedBucketIndicies.Length - 1;
                int newBucketIndex = unusedBucketIndicies[lastIndex];
                int nextBucketIndex = nextBucketIndexEachBucket[newBucketIndex];
                unusedBucketIndicies[lastIndex] = nextBucketIndex;
                unusedBucketIndicies.Length -= math.select(0, 1, nextBucketIndex == 0);
                nextBucketIndexEachBucket[newBucketIndex] = 0;
                return newBucketIndex;
            }
        }
        static bool ContainsKeyInHash(Hash hash, int key, NativeArray<Key> keys)
        {
            int curKeyIndex = hash.HeadKeyIndex;
            while(curKeyIndex != 0)
            {
                Key curKey = keys[curKeyIndex];
                if (curKey._key == key) return true;
                curKeyIndex = curKey._nextIndex;
            }
            return false;
        }
        static bool ContainsValueInHash(Hash hash, int key, NativeArray<Key> keys, NativeArray<V> values, out V value)
        {
            int curKeyIndex = hash.HeadKeyIndex;
            while (curKeyIndex != 0)
            {
                Key curKey = keys[curKeyIndex];
                if (curKey._key == key)
                {
                    value = values[curKeyIndex];
                    return true;
                }
                curKeyIndex = curKey._nextIndex;
            }
            value = default;
            return false;
        }
        static bool ContainsValueInHash(Hash hash, int key, NativeArray<Key> keys, NativeArray<V> values, out V value, out int directAccess)
        {
            int curKeyIndex = hash.HeadKeyIndex;
            while (curKeyIndex != 0)
            {
                Key curKey = keys[curKeyIndex];
                if (curKey._key == key)
                {
                    value = values[curKeyIndex];
                    directAccess = curKeyIndex;
                    return true;
                }
                curKeyIndex = curKey._nextIndex;
            }
            directAccess = 0;
            value = default;
            return false;
        }
        static int GetHashIndex(int mapIndex, int key, int hashWidth, int sectorMatrixColAmount, int hashGridColAmount)
        {
            int hashStart = mapIndex * hashWidth;
            int hashOffset = GetHashOffset(key, hashWidth, sectorMatrixColAmount, hashGridColAmount);
            return hashStart + hashOffset;
        }
        static int GetHashOffset(int key, int hashWidth, int sectorMatrixColAmount, int hashGridColAmount)
        {
            return GetHashcode(key, sectorMatrixColAmount, hashGridColAmount) % hashWidth;
        }
        static int GetHashcode(int key, int sectorMatrixColAmount, int hashGridColAmount)
        {
            int2 key2d = new int2(key % sectorMatrixColAmount, key / sectorMatrixColAmount);
            int2 key2d_hashgrid = key2d % hashGridColAmount;
            return key2d_hashgrid.y * hashGridColAmount + key2d_hashgrid.x;
        }

    }
}