using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct SLSFAllocator<T>
        where T : unmanaged
    {
        public const int INVALID_ALLOC_ID = INVALID_CHUNK_IDX;
        internal const int INVALID_CHUNK_IDX = -1;
        [NoAlias] internal NativeList<T> DataBuffer;
        [NoAlias] internal NativeList<Chunk> ChunkBuffer;
        [NoAlias] internal NativeList<int> UnusedChunkIndexBuffer;
        [NoAlias] internal NativeReference<IntervalData> IntervalDataRef;

        public SLSFAllocator(Allocator allocator)
        {
            DataBuffer = new NativeList<T>(allocator);
            ChunkBuffer = new NativeList<Chunk>(allocator);
            UnusedChunkIndexBuffer = new NativeList<int>(allocator);

            IntervalData intervalData = default;
            intervalData.IntervalBuffer = new FixedList128Bytes<Interval>();
            intervalData.IntervalBuffer.Length = 31;
            for (int i = 0; i < intervalData.IntervalBuffer.Length; i++)
                intervalData.IntervalBuffer[i] = new Interval
                {
                    FreeChunksHeadIdx = INVALID_CHUNK_IDX,
                };
            intervalData.LastChunkIdx = INVALID_CHUNK_IDX;
            intervalData.GreatestIntervalReached = 0;
            IntervalDataRef = new NativeReference<IntervalData>(intervalData, allocator);
        }
        public void Dispose()
        {
            DataBuffer.Dispose();
            ChunkBuffer.Dispose();
            UnusedChunkIndexBuffer.Dispose();
            IntervalDataRef.Dispose();
        }
        public void Allocate(int size, out int start, out int capacity, out int allocId)
        {
            int powerOf2 = math.ceillog2(math.max(size, 2));
            capacity = 1 << powerOf2;

            int intervalIndex = powerOf2 - 1;

            IntervalData intervalData = IntervalDataRef.Value;
            for(int i = intervalIndex; i <= intervalData.GreatestIntervalReached; i++)
            {
                Interval interval = intervalData.IntervalBuffer[i];
                int freeChunkIndex = interval.FreeChunksHeadIdx;
                if (freeChunkIndex == INVALID_CHUNK_IDX)
                    continue;

                Chunk freeChunk = ChunkBuffer[freeChunkIndex];

                //disconnect from free linked list
                interval.FreeChunksHeadIdx = freeChunk.FreeListNextIdx;
                intervalData.IntervalBuffer[i] = interval;
                if (freeChunk.FreeListNextIdx != INVALID_CHUNK_IDX)
                {
                    Chunk nextInFreeList = ChunkBuffer[freeChunk.FreeListNextIdx];
                    nextInFreeList.FreeListPrevIdx = INVALID_CHUNK_IDX;
                    ChunkBuffer[freeChunk.FreeListNextIdx] = nextInFreeList;
                }
                freeChunk.FreeListNextIdx = INVALID_CHUNK_IDX;

                //split if capacity remains
                int remainingCapacity = freeChunk.Capacity - capacity;
                if(remainingCapacity > 0)
                {
                    freeChunk.Capacity = capacity;

                    int indexForRemainingChunk = GetIndexForNewChunk();
                    
                    //insert new chunk between freeChunk and freeChunk.next
                    int nextIndexOfFreeChunk = freeChunk.NextChunkIdx;
                    if(freeChunk.NextChunkIdx != INVALID_CHUNK_IDX)
                    {
                        Chunk nextOfFreeChunk = ChunkBuffer[freeChunk.NextChunkIdx];
                        nextOfFreeChunk.PrevChunkIdx = indexForRemainingChunk;
                        ChunkBuffer[freeChunk.NextChunkIdx] = nextOfFreeChunk;
                    }
                    intervalData.LastChunkIdx = math.select(intervalData.LastChunkIdx, indexForRemainingChunk, intervalData.LastChunkIdx == freeChunkIndex);
                    freeChunk.NextChunkIdx = indexForRemainingChunk;

                    //point interval to the remaining chunk
                    int intervalIndexForRemainingChunk = math.floorlog2(remainingCapacity) - 1;
                    Interval intervalForRemainingChunk = intervalData.IntervalBuffer[intervalIndexForRemainingChunk];
                    int headIndexOfInterval = intervalForRemainingChunk.FreeChunksHeadIdx;
                    if(headIndexOfInterval != INVALID_CHUNK_IDX)
                    {
                        Chunk headOfInterval = ChunkBuffer[headIndexOfInterval];
                        headOfInterval.FreeListPrevIdx = indexForRemainingChunk;
                        ChunkBuffer[headIndexOfInterval] = headOfInterval;
                    }
                    intervalForRemainingChunk.FreeChunksHeadIdx = indexForRemainingChunk;
                    intervalData.IntervalBuffer[intervalIndexForRemainingChunk] = intervalForRemainingChunk;

                    //write remaining chunk data
                    Chunk remainingChunk = new Chunk
                    {
                        Capacity = remainingCapacity,
                        DataStartIdx = freeChunk.DataStartIdx + capacity,
                        FreeListNextIdx = headIndexOfInterval,
                        FreeListPrevIdx = INVALID_CHUNK_IDX,
                        PrevChunkIdx = freeChunkIndex,
                        NextChunkIdx = nextIndexOfFreeChunk,
                    };
                    ChunkBuffer[indexForRemainingChunk] = remainingChunk;
                }

                ChunkBuffer[freeChunkIndex] = freeChunk;
                IntervalDataRef.Value = intervalData;

                start = freeChunk.DataStartIdx;
                allocId = freeChunkIndex;
                return;
            }

            Chunk newChunk = new Chunk
            {
                DataStartIdx = DataBuffer.Length,
                Capacity = capacity,
                FreeListNextIdx = INVALID_CHUNK_IDX,
                FreeListPrevIdx = INVALID_CHUNK_IDX,
                PrevChunkIdx = intervalData.LastChunkIdx,
                NextChunkIdx = INVALID_CHUNK_IDX,
            };
            DataBuffer.Resize(newChunk.DataStartIdx + capacity, NativeArrayOptions.UninitializedMemory);

            int newChunkIdx = GetIndexForNewChunk();
            ChunkBuffer[newChunkIdx] = newChunk;
            if(intervalData.LastChunkIdx != INVALID_CHUNK_IDX)
            {
                Chunk lastChunk = ChunkBuffer[intervalData.LastChunkIdx];
                lastChunk.NextChunkIdx = newChunkIdx;
                ChunkBuffer[intervalData.LastChunkIdx] = lastChunk;
            }
            intervalData.LastChunkIdx = newChunkIdx;
            intervalData.GreatestIntervalReached = math.max(intervalData.GreatestIntervalReached, intervalIndex);
            IntervalDataRef.Value = intervalData;

            start = newChunk.DataStartIdx;
            allocId = newChunkIdx;
        }
        public void Deallocate(int allocId)
        {
            IntervalData intervalData = IntervalDataRef.Value;

            Chunk chunk = ChunkBuffer[allocId];

            int nextIndex = chunk.NextChunkIdx;
            if(nextIndex != INVALID_CHUNK_IDX)
            {
                Chunk chunk_next = ChunkBuffer[nextIndex];

                int intervalIndex_next = CapacityToIntervalIndex_RoundDown(chunk_next.Capacity);
                Interval interval_next = intervalData.IntervalBuffer[intervalIndex_next];
                bool freeHead_next = interval_next.FreeChunksHeadIdx == nextIndex;
                bool nextFreeValid_next = chunk_next.FreeListNextIdx != INVALID_CHUNK_IDX;
                bool prevFreeValid_next = chunk_next.FreeListPrevIdx != INVALID_CHUNK_IDX;
                bool nextValid_next = chunk_next.NextChunkIdx != INVALID_CHUNK_IDX;
                bool isFree_next = freeHead_next | nextFreeValid_next | prevFreeValid_next;
                if (isFree_next)
                {
                    chunk.NextChunkIdx = chunk_next.NextChunkIdx;
                    chunk.Capacity += chunk_next.Capacity;
                    if (nextValid_next)
                    {
                        Chunk chunk_nextOfNext = ChunkBuffer[chunk_next.NextChunkIdx];
                        chunk_nextOfNext.PrevChunkIdx = allocId;
                        ChunkBuffer[chunk_next.NextChunkIdx] = chunk_nextOfNext;
                    }
                    if (freeHead_next)
                    {
                        interval_next.FreeChunksHeadIdx = chunk_next.FreeListNextIdx;
                        intervalData.IntervalBuffer[intervalIndex_next] = interval_next;
                    }
                    if (nextFreeValid_next)
                    {
                        Chunk chunk_nextFreeOfNext = ChunkBuffer[chunk_next.FreeListNextIdx];
                        chunk_nextFreeOfNext.FreeListPrevIdx = chunk_next.FreeListPrevIdx;
                        ChunkBuffer[chunk_next.FreeListNextIdx] = chunk_nextFreeOfNext;
                    }
                    if (prevFreeValid_next)
                    {
                        Chunk chunk_prevFreeOfNext = ChunkBuffer[chunk_next.FreeListPrevIdx];
                        chunk_prevFreeOfNext.FreeListNextIdx = chunk_next.FreeListNextIdx;
                        ChunkBuffer[chunk_next.FreeListPrevIdx] = chunk_prevFreeOfNext;
                    }
                    UnusedChunkIndexBuffer.Add(nextIndex);
                    intervalData.LastChunkIdx = math.select(intervalData.LastChunkIdx, allocId, intervalData.LastChunkIdx == nextIndex);
                }
            }

            int prevIndex = chunk.PrevChunkIdx;
            if(prevIndex != INVALID_CHUNK_IDX)
            {
                Chunk chunk_prev = ChunkBuffer[prevIndex];

                int intervalIndex_prev = CapacityToIntervalIndex_RoundDown(chunk_prev.Capacity);
                Interval interval_prev = intervalData.IntervalBuffer[intervalIndex_prev];
                bool freeHead_prev = interval_prev.FreeChunksHeadIdx == prevIndex;
                bool nextFreeValid_prev = chunk_prev.FreeListNextIdx != INVALID_CHUNK_IDX;
                bool prevFreeValid_prev = chunk_prev.FreeListPrevIdx != INVALID_CHUNK_IDX;
                bool prevValid_prev = chunk_prev.PrevChunkIdx != INVALID_CHUNK_IDX;
                bool isFree_prev = freeHead_prev | nextFreeValid_prev | prevFreeValid_prev;
                if (isFree_prev)
                {
                    chunk.PrevChunkIdx = chunk_prev.PrevChunkIdx;
                    chunk.DataStartIdx = chunk_prev.DataStartIdx;
                    chunk.Capacity += chunk_prev.Capacity;
                    if (prevValid_prev)
                    {
                        Chunk chunk_prevOfPrev = ChunkBuffer[chunk_prev.PrevChunkIdx];
                        chunk_prevOfPrev.NextChunkIdx = allocId;
                        ChunkBuffer[chunk_prev.PrevChunkIdx] = chunk_prevOfPrev;
                    }
                    if (freeHead_prev)
                    {
                        interval_prev.FreeChunksHeadIdx = chunk_prev.FreeListNextIdx;
                        intervalData.IntervalBuffer[intervalIndex_prev] = interval_prev;
                    }
                    if (nextFreeValid_prev)
                    {
                        Chunk chunk_nextFreeOfPrev = ChunkBuffer[chunk_prev.FreeListNextIdx];
                        chunk_nextFreeOfPrev.FreeListPrevIdx = chunk_prev.FreeListPrevIdx;
                        ChunkBuffer[chunk_prev.FreeListNextIdx] = chunk_nextFreeOfPrev;
                    }
                    if (prevFreeValid_prev)
                    {
                        Chunk chunk_prevFreeOfPrev = ChunkBuffer[chunk_prev.FreeListPrevIdx];
                        chunk_prevFreeOfPrev.FreeListNextIdx = chunk_prev.FreeListNextIdx;
                        ChunkBuffer[chunk_prev.FreeListPrevIdx] = chunk_prevFreeOfPrev;
                    }
                    UnusedChunkIndexBuffer.Add(prevIndex);
                }
            }

            int intervalIndex_chunk = CapacityToIntervalIndex_RoundDown(chunk.Capacity);
            Interval interval_chunk = intervalData.IntervalBuffer[intervalIndex_chunk];
            if(interval_chunk.FreeChunksHeadIdx != INVALID_CHUNK_IDX)
            {
                Chunk intervalHeadChunk = ChunkBuffer[interval_chunk.FreeChunksHeadIdx];
                intervalHeadChunk.FreeListPrevIdx = allocId;
                ChunkBuffer[interval_chunk.FreeChunksHeadIdx] = intervalHeadChunk;
            }
            chunk.FreeListNextIdx = interval_chunk.FreeChunksHeadIdx;
            interval_chunk.FreeChunksHeadIdx = allocId;
            intervalData.GreatestIntervalReached = math.max(intervalData.GreatestIntervalReached, intervalIndex_chunk);
            intervalData.IntervalBuffer[intervalIndex_chunk] = interval_chunk;
            ChunkBuffer[allocId] = chunk;
            IntervalDataRef.Value = intervalData;
        }
        internal int GetIndexForNewChunk()
        {
            int newChunkIdx;
            if (UnusedChunkIndexBuffer.Length == 0)
            {
                newChunkIdx = ChunkBuffer.Length;
                ChunkBuffer.Length++;
            }
            else
            {
                int last = UnusedChunkIndexBuffer.Length - 1;
                newChunkIdx = UnusedChunkIndexBuffer[last];
                UnusedChunkIndexBuffer.Length--;
            }
            return newChunkIdx;
        }
        static int CapacityToIntervalIndex_RoundUp(int capacity)
        {
            return math.ceillog2(capacity) - 1;
        }
        static int CapacityToIntervalIndex_RoundDown(int capacity)
        {
            return math.floorlog2(capacity) - 1;
        }
        internal struct IntervalData
        {
            internal FixedList128Bytes<Interval> IntervalBuffer;
            internal int LastChunkIdx;
            internal int GreatestIntervalReached;
        }
        internal struct Interval
        {
            internal int FreeChunksHeadIdx;
        }
        internal struct Chunk
        {
            internal int DataStartIdx;
            internal int Capacity;
            internal int FreeListNextIdx;
            internal int FreeListPrevIdx;
            internal int PrevChunkIdx;
            internal int NextChunkIdx;
        }
    }
}
