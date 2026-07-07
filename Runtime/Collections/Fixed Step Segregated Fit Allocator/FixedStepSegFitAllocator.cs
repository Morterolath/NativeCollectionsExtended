using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct FixedStepSegFitAllocator<T> where T : unmanaged
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
        internal readonly int IntervalStepSize;
        internal ListOfDLinkedLists<FreeBlock> FreeBlockListEachInverval;
        internal NativeList<T> Data;
        internal DLinkedList<AllocBlock> AllocBlockLinkedList;
        public FixedStepSegFitAllocator(int maxAllocSize, int intervalStepSize, Allocator allocator)
        {
            intervalStepSize = math.max(4, intervalStepSize);
            maxAllocSize = math.max(4, maxAllocSize);

            IntervalStepSize = intervalStepSize;
            FreeBlockListEachInverval = new ListOfDLinkedLists<FreeBlock>(allocator);
            Data = new NativeList<T>(allocator);
            AllocBlockLinkedList = new DLinkedList<AllocBlock>(allocator);

            int intervalCount = SizeToIntervalIndex(maxAllocSize);
            for (int i = 0; i <= intervalCount; i++) FreeBlockListEachInverval.AddList();
        }

        public void Allocate(int size, out int start, out int newSize, out int allocNodeIndex)
        {
            size = math.max(size, IntervalStepSize);

            int intervalIndex = SizeToIntervalIndex(size);

            //If outside the max allocation
            if(intervalIndex >= FreeBlockListEachInverval.ListCount())
            {
                allocNodeIndex = INVALID_ALLOC_NODE_INDEX;
                start = 0;
                newSize = 0;
                return;
            }

            size = AlidngWithIntervalSize(size);

            //Best interval has free block
            if (FreeBlockListEachInverval.TryRemoveLast(intervalIndex, out FreeBlock freeBlock))
            {
                AllocBlock allocBlock = AllocBlockLinkedList.GetNodeData(freeBlock.AllocNodeIndex);
                allocBlock.FreeNodeIndex = INVALID_FREE_NODE_INDEX;
                AllocBlockLinkedList.SetNodeData(freeBlock.AllocNodeIndex, allocBlock);

                start = allocBlock.DataStart;
                newSize = size;
                allocNodeIndex = freeBlock.AllocNodeIndex;
                return;
            }

            //If best interval does not have free block, look for others
            for(int i = intervalIndex + 1; i < FreeBlockListEachInverval.ListCount(); i++)
            {
                if(FreeBlockListEachInverval.TryRemoveLast(i, out freeBlock))
                {
                    AllocBlock allocBlock = AllocBlockLinkedList.GetNodeData(freeBlock.AllocNodeIndex);
                    AllocBlock newAllocBlock = new AllocBlock { DataStart = allocBlock.DataStart, FreeNodeIndex = INVALID_FREE_NODE_INDEX };

                    allocBlock.DataStart += size;
                    MergeNext(freeBlock.AllocNodeIndex, allocBlock, out int allocBlockNewSize);
                    int remainingAllocBlockIntervalIndex = SizeToIntervalIndex(allocBlockNewSize);
                    int freeNodeIndex = FreeBlockListEachInverval.AddLast(remainingAllocBlockIntervalIndex, new FreeBlock { AllocNodeIndex = freeBlock.AllocNodeIndex });
                    allocBlock.FreeNodeIndex = freeNodeIndex;
                    AllocBlockLinkedList.SetNodeData(freeBlock.AllocNodeIndex, allocBlock);

                    start = newAllocBlock.DataStart;
                    newSize = size;
                    allocNodeIndex = AllocBlockLinkedList.AddBeforeUnchecked(freeBlock.AllocNodeIndex, newAllocBlock);
                    return;
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

            int allocBlockInterval = SizeToIntervalIndex(allocBlockSize);
            allocBlock.FreeNodeIndex = FreeBlockListEachInverval.AddLast(allocBlockInterval, new FreeBlock { AllocNodeIndex = allocNodeIndex });
            AllocBlockLinkedList.SetNodeData(allocNodeIndex, allocBlock);
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
            if(AllocBlockLinkedList.TryGetNext(allocNodeIndex, out AllocBlock nextData))
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
        public void Dispose()
        {
            FreeBlockListEachInverval.Dispose();
            Data.Dispose();
            AllocBlockLinkedList.Dispose();
        }
        void MergePrev(int originalAllocNodeIndex, ref AllocBlock originalAllocBlock, ref int originalAllocBlockSize)
        {
            if (AllocBlockLinkedList.TryGetPrev(originalAllocNodeIndex, out int prevAllocNodeIndex))
            {
                AllocBlock prevAllocBlock = AllocBlockLinkedList.GetNodeData(prevAllocNodeIndex);
                if (prevAllocBlock.FreeNodeIndex != INVALID_FREE_NODE_INDEX)
                {
                    int prevBlockSize = originalAllocBlock.DataStart - prevAllocBlock.DataStart;
                    if (originalAllocBlockSize + prevBlockSize > GetMaxAllocationSize()) return;
                    originalAllocBlockSize += prevBlockSize;

                    int prevBlockInterval = SizeToIntervalIndex(prevBlockSize);
                    FreeBlockListEachInverval.RemoveUnchecked(prevBlockInterval, prevAllocBlock.FreeNodeIndex);
                    AllocBlockLinkedList.RemoveUnchecked(prevAllocNodeIndex);
                    originalAllocBlock.DataStart = prevAllocBlock.DataStart;
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
                    if (nextAllocBlock.DataStart - originalAllocBlock.DataStart + nextblockSize > GetMaxAllocationSize()) return;
                    originalAllocBlockSize = nextAllocBlock.DataStart - originalAllocBlock.DataStart + nextblockSize;
                    int nextBlockInterval = SizeToIntervalIndex(nextblockSize);
                    FreeBlockListEachInverval.RemoveUnchecked(nextBlockInterval, nextAllocBlock.FreeNodeIndex);
                    AllocBlockLinkedList.RemoveUnchecked(nextAllocNodeIndex);
                }
            }
        }
        public int GetMaxAllocationSize()
        {
            return FreeBlockListEachInverval.ListCount() * IntervalStepSize;
        }
        int SizeToIntervalIndex(int size)
        {
            return (size - 1) / IntervalStepSize;
        }
        int AlidngWithIntervalSize(int size)
        {
            int size_modulo_intervalStepSize = size % IntervalStepSize;
            int size_ifNotDivisibleByIntervalStepSize = size + (IntervalStepSize - size_modulo_intervalStepSize);
            return math.select(size_ifNotDivisibleByIntervalStepSize, size, size_modulo_intervalStepSize == 0);
        }
    }
}
