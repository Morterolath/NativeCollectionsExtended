using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct TLSFAllocator<T> where T : unmanaged
    {
        internal struct AllocBlock
        {
            internal int DataStart;
            internal int FreeNodeIndex;
        }
        internal struct FreeBlock
        {
            internal int AllocNodeIndex;
        }
        internal const int INVALID_FREE_NODE_INDEX = ListOfDLinkedLists<FreeBlock>.INVALID_INDEX;
        public const int INVALID_ALLOC_NODE_INDEX = DLinkedList<AllocBlock>.INVALID_NODE_INDEX;
        internal const int UPPER_LEVEL_START = 128;
        internal const int UPPER_LEVEL_START_POWER_OF_TWO = 7;
        internal const int LOWER_LEVEL_SEGREGATION = 8;
        internal const int UPPER_INTERVAL_COUNT = 31 - UPPER_LEVEL_START_POWER_OF_TWO;
        internal const int TOTAL_INTERVAL_COUNT = UPPER_INTERVAL_COUNT * LOWER_LEVEL_SEGREGATION;
        internal const int MIN_ALLOC_SIZE = UPPER_LEVEL_START / LOWER_LEVEL_SEGREGATION;
        internal NativeArray<int> FreeBlockCountEachUpperInterval;
        internal ListOfDLinkedLists<FreeBlock> FreeBlockListEachInverval;
        internal NativeList<T> Data;
        internal DLinkedList<AllocBlock> AllocBlockLinkedList;

        public TLSFAllocator(Allocator allocator)
        {
            FreeBlockListEachInverval = new ListOfDLinkedLists<FreeBlock>(allocator);
            Data = new NativeList<T>(allocator);
            AllocBlockLinkedList = new DLinkedList<AllocBlock>(allocator);
            FreeBlockCountEachUpperInterval = new NativeArray<int>(UPPER_INTERVAL_COUNT, allocator);

            for (int i = 0; i < TOTAL_INTERVAL_COUNT; i++) FreeBlockListEachInverval.AddList();
        }
        public void Dispose()
        {
            FreeBlockCountEachUpperInterval.Dispose();
            FreeBlockListEachInverval.Dispose();
            Data.Dispose();
            AllocBlockLinkedList.Dispose();
        }
        public void Allocate(int size, out int start, out int newSize, out int allocNodeIndex)
        {
            //Cannot allocate less than minimum allocation size
            size = math.max(size, MIN_ALLOC_SIZE);

            int savedSize = size;
            int interval = RequestedSizeToInterval(size, out size, out int upperInterval);

            //Check lower intervals of current upper interval
            for(int i = interval; i < upperInterval * LOWER_LEVEL_SEGREGATION + LOWER_LEVEL_SEGREGATION; i++)
            {
                if (FreeBlockListEachInverval.TryRemoveLast(i, out FreeBlock freeBlock))
                {
                    FreeBlockCountEachUpperInterval[upperInterval]--;
                    AllocateFromFreeBlock_FitUnchecked(freeBlock, size, out start, out newSize, out allocNodeIndex);
                    return;
                }
            }
            //Check suitabable upper interval
            int suitableUpperInterval = upperInterval;
            for(int i = upperInterval + 1; i < UPPER_INTERVAL_COUNT; i++)
            {
                suitableUpperInterval = math.select(suitableUpperInterval, i, FreeBlockCountEachUpperInterval[i] > 0);
            }

            //Check lower intervals of suitable upper interval
            if(suitableUpperInterval != upperInterval)
            {
                int lowerIntervalStart = suitableUpperInterval * LOWER_LEVEL_SEGREGATION;
                int lowerIntervalEnd = lowerIntervalStart + LOWER_LEVEL_SEGREGATION;
                for(int i = lowerIntervalStart; i < lowerIntervalEnd; i++)
                {
                    if (FreeBlockListEachInverval.TryRemoveLast(i, out FreeBlock freeBlock))
                    {
                        FreeBlockCountEachUpperInterval[suitableUpperInterval]--;
                        AllocateFromFreeBlock_FitUnchecked(freeBlock, size, out start, out newSize, out allocNodeIndex);
                        return;
                    }
                }
            }

            //If no interval has free block, allocate new block
            allocNodeIndex = AllocBlockLinkedList.AddLast(new AllocBlock { DataStart = Data.Length, FreeNodeIndex = INVALID_FREE_NODE_INDEX });
            start = Data.Length;
            newSize = size;
            Data.Length += size;
        }

        public void Deallocate(int allocNodeIndex)
        {
            if (allocNodeIndex == INVALID_ALLOC_NODE_INDEX) return;

            AllocBlock allocBlock = AllocBlockLinkedList.GetNodeData(allocNodeIndex);
            if (allocBlock.FreeNodeIndex != INVALID_FREE_NODE_INDEX) return;

            MergeNext(allocNodeIndex, allocBlock, out int allocBlockSize);
            MergePrev(allocNodeIndex, ref allocBlock, ref allocBlockSize);

            int allocBlockInterval = BlockSizeToInterval(allocBlockSize);
            allocBlock.FreeNodeIndex = FreeBlockListEachInverval.AddLast(allocBlockInterval, new FreeBlock { AllocNodeIndex = allocNodeIndex });
            AllocBlockLinkedList.SetNodeData(allocNodeIndex, allocBlock);
            FreeBlockCountEachUpperInterval[IntervalToUpperInterval(allocBlockInterval)]++;
        }
        public void AllocBlockInfo(int allocNodeIndex, out bool allocated, out int start, out int size)
        {
            if(allocNodeIndex == INVALID_ALLOC_NODE_INDEX)
            {
                allocated = false;
                start = 0;
                size = 0;
                return;
            }

            AllocBlock allocBlock = AllocBlockLinkedList.GetNodeData(allocNodeIndex);
            allocated = allocBlock.FreeNodeIndex == INVALID_FREE_NODE_INDEX;
            start = allocBlock.DataStart;

            size = Data.Length - allocBlock.DataStart;
            if (AllocBlockLinkedList.TryGetNext(allocNodeIndex, out AllocBlock nextData))
            {
                size = nextData.DataStart - allocBlock.DataStart;
            }
        }
        public NativeArray<T> DataAsDeferredJobArray()
        {
            return Data.AsDeferredJobArray();
        }
        public NativeArray<T> DataAsArray()
        {
            return Data.AsArray();
        }
        void AllocateFromFreeBlock_FitUnchecked(FreeBlock freeBlock, int size, out int start, out int newSize, out int allocNodeIndex)
        {
            //Get the free block
            AllocBlock allocBlock = AllocBlockLinkedList.GetNodeData(freeBlock.AllocNodeIndex);

            //Take the size required from the block
            AllocBlock newAllocBlock = new AllocBlock { DataStart = allocBlock.DataStart, FreeNodeIndex = INVALID_FREE_NODE_INDEX };
            start = newAllocBlock.DataStart;
            newSize = size;
            allocNodeIndex = AllocBlockLinkedList.AddBeforeUnchecked(freeBlock.AllocNodeIndex, newAllocBlock);

            //Set the remianing size
            allocBlock.DataStart += size;
            int remaningBlockSize = Data.Length - allocBlock.DataStart;
            if (AllocBlockLinkedList.TryGetNext(freeBlock.AllocNodeIndex, out AllocBlock nextAllocBlock))
            {
                remaningBlockSize = nextAllocBlock.DataStart - allocBlock.DataStart;
            }
            //If remaining block's size is 0, remove. Else, add it to the proper interval
            if (remaningBlockSize == 0)
            {
                AllocBlockLinkedList.RemoveUnchecked(freeBlock.AllocNodeIndex);
            }
            else
            {
                int remainingAllocBlockIntervalIndex = BlockSizeToInterval(remaningBlockSize);
                int freeNodeIndex = FreeBlockListEachInverval.AddLast(remainingAllocBlockIntervalIndex, new FreeBlock { AllocNodeIndex = freeBlock.AllocNodeIndex });
                allocBlock.FreeNodeIndex = freeNodeIndex;
                AllocBlockLinkedList.SetNodeData(freeBlock.AllocNodeIndex, allocBlock);
                FreeBlockCountEachUpperInterval[IntervalToUpperInterval(remainingAllocBlockIntervalIndex)]++;
            }
        }
        void MergePrev(int originalAllocNodeIndex, ref AllocBlock originalAllocBlock, ref int originalAllocBlockSize)
        {
            if (AllocBlockLinkedList.TryGetPrev(originalAllocNodeIndex, out int prevAllocNodeIndex))
            {
                AllocBlock prevAllocBlock = AllocBlockLinkedList.GetNodeData(prevAllocNodeIndex);
                if (prevAllocBlock.FreeNodeIndex != INVALID_FREE_NODE_INDEX)
                {
                    int prevBlockSize = originalAllocBlock.DataStart - prevAllocBlock.DataStart;
                    originalAllocBlockSize += prevBlockSize;

                    int prevBlockInterval = BlockSizeToInterval(prevBlockSize);
                    FreeBlockListEachInverval.RemoveUnchecked(prevBlockInterval, prevAllocBlock.FreeNodeIndex);
                    AllocBlockLinkedList.RemoveUnchecked(prevAllocNodeIndex);
                    originalAllocBlock.DataStart = prevAllocBlock.DataStart;
                    FreeBlockCountEachUpperInterval[IntervalToUpperInterval(prevBlockInterval)]--;
                }
            }
        }
        void MergeNext(int originalAllocNodeIndex, AllocBlock originalAllocBlock, out int originalAllocBlockSize)
        {
            originalAllocBlockSize = Data.Length - originalAllocBlock.DataStart;

            if (AllocBlockLinkedList.TryGetNext(originalAllocNodeIndex, out int nextAllocNodeIndex))
            {
                AllocBlock nextAllocBlock = AllocBlockLinkedList.GetNodeData(nextAllocNodeIndex);
                originalAllocBlockSize = nextAllocBlock.DataStart - originalAllocBlock.DataStart;

                if (nextAllocBlock.FreeNodeIndex != INVALID_FREE_NODE_INDEX)
                {
                    int nextblockSize = Data.Length - nextAllocBlock.DataStart;
                    if (AllocBlockLinkedList.TryGetNext(nextAllocNodeIndex, out AllocBlock nextNextAllocBlock))
                    {
                        nextblockSize = nextNextAllocBlock.DataStart - nextAllocBlock.DataStart;
                    }
                    originalAllocBlockSize = nextAllocBlock.DataStart - originalAllocBlock.DataStart + nextblockSize;
                    int nextBlockInterval = BlockSizeToInterval(nextblockSize);
                    FreeBlockListEachInverval.RemoveUnchecked(nextBlockInterval, nextAllocBlock.FreeNodeIndex);
                    AllocBlockLinkedList.RemoveUnchecked(nextAllocNodeIndex);
                    FreeBlockCountEachUpperInterval[IntervalToUpperInterval(nextBlockInterval)]--;
                }
            }
        }
        int IntervalToUpperInterval(int interval)
        {
            return interval / LOWER_LEVEL_SEGREGATION;
        }
        int RequestedSizeToInterval(int size, out int alignedSize, out int upperInterval)
        {
            upperInterval = math.max(0, math.ceillog2(size) - UPPER_LEVEL_START_POWER_OF_TWO);
            int upperIntervalMax = math.max(UPPER_LEVEL_START, math.ceilpow2(size));
            int upperIntervalSize = math.max(upperIntervalMax / 2, UPPER_LEVEL_START);
            int prevUpperIntervalMax = upperIntervalMax - upperIntervalSize;
            int lowerIntervalSize = upperIntervalSize / LOWER_LEVEL_SEGREGATION;
            int lowerInterval = (size - prevUpperIntervalMax - 1) / lowerIntervalSize;
            alignedSize = prevUpperIntervalMax + lowerInterval * lowerIntervalSize + lowerIntervalSize;
            return upperInterval * LOWER_LEVEL_SEGREGATION + lowerInterval;
        }
        int BlockSizeToInterval(int size)
        {
            int upperInterval = math.max(0, math.ceillog2(size) - UPPER_LEVEL_START_POWER_OF_TWO);
            int upperIntervalMax = math.max(UPPER_LEVEL_START, math.ceilpow2(size));
            int upperIntervalSize = math.max(upperIntervalMax / 2, UPPER_LEVEL_START);
            int prevUpperIntervalMax = upperIntervalMax - upperIntervalSize;
            int lowerIntervalSize = upperIntervalSize / LOWER_LEVEL_SEGREGATION;
            int lowerInterval = (size - prevUpperIntervalMax - 1) / lowerIntervalSize;
            return upperInterval * LOWER_LEVEL_SEGREGATION + lowerInterval - math.select(1, 0, size == (prevUpperIntervalMax + (lowerInterval + 1) * lowerIntervalSize));
        }
    }
}
