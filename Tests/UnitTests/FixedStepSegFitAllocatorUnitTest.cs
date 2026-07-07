using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using NativeCollectionsExtended;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace NativeCollectionsExtended.UnitTest
{
    internal class FixedStepSegFitAllocatorUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool Info;
        public int MinAllocCount;
        public int MaxAllocCount;
        public int MinAllocSize;
        public int MaxAllocSize;
        public int MinDeallocCount;
        public int MaxDeallocCount;
        public int AllocatorMinAllocSize;
        public int AllocatorMaxAllocSize;
        public int MinIntervalStepSize;
        public int MaxIntervalStepSize;
        private void Update()
        {
            MinAllocCount = Mathf.Clamp(MinAllocCount, 0, 100000);
            MaxAllocCount = Mathf.Clamp(MaxAllocCount, 0, 100000);
            MinDeallocCount = Mathf.Clamp(MinDeallocCount, 0, 100000);
            MaxDeallocCount = Mathf.Clamp(MaxDeallocCount, 0, 100000);
            AllocatorMinAllocSize = Mathf.Clamp(AllocatorMinAllocSize, 4, 1000);
            AllocatorMaxAllocSize = Mathf.Clamp(AllocatorMaxAllocSize, 4, 1000);
            MinAllocSize = Mathf.Clamp(MinAllocSize, 1, AllocatorMaxAllocSize);
            MaxAllocSize = Mathf.Clamp(MaxAllocSize, 2, AllocatorMaxAllocSize);
            MinIntervalStepSize = Mathf.Clamp(MinIntervalStepSize, 1, 1000);
            MaxIntervalStepSize = Mathf.Clamp(MaxIntervalStepSize, 1, 1000);
            TestUtils.SetMinMax(MinAllocCount, MaxAllocCount, out MinAllocCount, out MaxAllocCount);
            TestUtils.SetMinMax(MinAllocSize, MaxAllocSize, out MinAllocSize, out MaxAllocSize);
            TestUtils.SetMinMax(MinDeallocCount, MaxDeallocCount, out MinDeallocCount, out MaxDeallocCount);
            TestUtils.SetMinMax(AllocatorMinAllocSize, AllocatorMaxAllocSize, out AllocatorMinAllocSize, out AllocatorMaxAllocSize);
            TestUtils.SetMinMax(MinIntervalStepSize, MaxIntervalStepSize, out MinIntervalStepSize, out MaxIntervalStepSize);

            if (!Run) return;
            Test(MinAllocCount, MaxAllocCount, MinAllocSize, MaxAllocSize, MinDeallocCount, MaxDeallocCount, AllocatorMinAllocSize, AllocatorMaxAllocSize,
                MinIntervalStepSize, MaxIntervalStepSize, Info);
        }
        static void Test(int minAllocCount, int maxAllocCount, int minAllocSize, int maxAllocSize, int minDeallocCount, int maxDeallocCount, 
            int allocatorMinAllocSize, int allocatorMaxAllocSize, int minIntervalStepSize, int maxIntervalStepSize, bool info)
        {
            StringBuilder sb = new StringBuilder();
            List<int3> lists = new List<int3>();
            List<int[]> expecedLists = new List<int[]>();
            List<int> aliveListIndicies = new List<int>();
            int allocatorMaxAlloc = UnityEngine.Random.Range(allocatorMinAllocSize, allocatorMaxAllocSize);
            int allocatorIntervalStepSize = UnityEngine.Random.Range(minIntervalStepSize, maxIntervalStepSize);
            FixedStepSegFitAllocator<int> allocator = new FixedStepSegFitAllocator<int>(allocatorMaxAlloc, allocatorIntervalStepSize, Allocator.Temp);
            AllocSome(minAllocCount, maxAllocCount, minAllocSize, maxAllocSize, allocator, lists, expecedLists, aliveListIndicies, out int allocCount_1,
                out int totalAllocSize_1);
            DeallocSome(minDeallocCount, maxDeallocCount, allocator, lists, expecedLists, aliveListIndicies, out int deallocCount_1);
            AllocSome(minAllocCount, maxAllocCount, minAllocSize, maxAllocSize, allocator, lists, expecedLists, aliveListIndicies, out int allocCount_2,
                out int totalAllocSize_2);
            DeallocAll(allocator, lists, expecedLists, aliveListIndicies);
            AllocSome(minAllocCount, maxAllocCount, minAllocSize, maxAllocSize, allocator, lists, expecedLists, aliveListIndicies, out int allocCount_3,
                out int totalAllocSize_3);
            DeallocSome(minDeallocCount, maxDeallocCount, allocator, lists, expecedLists, aliveListIndicies, out int deallocCount_2);
            AllocSome(minAllocCount, maxAllocCount, minAllocSize, maxAllocSize, allocator, lists, expecedLists, aliveListIndicies, out int allocCount_4,
                out int totalAllocSize_4);
            bool successInternal = CheckInternals(allocator, sb);
            bool successContent = CheckContent(allocator, lists, expecedLists, sb);
            sb.AppendLine($"Allocator max allocation size: {allocatorMaxAlloc}");
            sb.AppendLine($"Allocator interval step size: {allocatorIntervalStepSize}");
            sb.AppendLine($"{allocCount_1} allocations are made with total size of {totalAllocSize_1}");
            sb.AppendLine($"{deallocCount_1} deallocations are made");
            sb.AppendLine($"{allocCount_2} allocations are made with total size of {totalAllocSize_2}");
            sb.AppendLine("everything deallocated");
            sb.AppendLine($"{allocCount_3} allocations are made with total size of {totalAllocSize_3}");
            sb.AppendLine($"{deallocCount_2} deallocations are made");
            sb.AppendLine($"{allocCount_4} allocations are made with total size of {totalAllocSize_4}");
            if (!successContent | info | !successInternal) UnityEngine.Debug.Log(sb);
        }

        static void AllocSome(int minAllocCount, int maxAllocCount, int minAllocSize, int maxAllocSize, FixedStepSegFitAllocator<int> allocator, 
            List<int3> lists, List<int[]> expectedLists, List<int> aliveListIndicies, out int allocationCount, out int totalAllocationSize)
        {
            allocationCount = 0;
            totalAllocationSize = 0;
            int count = UnityEngine.Random.Range(minAllocCount, maxAllocCount);
            for(int i = 0; i < count; i++)
            {
                allocationCount++;
                int allocSize = UnityEngine.Random.Range(minAllocSize, maxAllocSize);
                allocSize = Mathf.Clamp(allocSize, 1, allocator.GetMaxAllocationSize());
                allocator.Allocate(allocSize, out int start, out int size, out int allocNodeIndex);
                lists.Add(new int3(start, size, allocNodeIndex));
                expectedLists.Add(new int[size]);
                aliveListIndicies.Add(expectedLists.Count - 1);
                for(int j = 0; j < size; j++)
                {
                    int data = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    allocator.Data[start + j] = data;
                    expectedLists[expectedLists.Count - 1][j] = data;
                    totalAllocationSize++;
                }
            }
        }
        static void DeallocSome(int minDeallocCount, int maxDeallocCount, FixedStepSegFitAllocator<int> allocator, 
            List<int3> lists, List<int[]> expectedLists, List<int> aliveListIndicies, out int deallocationCount)
        {
            deallocationCount = 0;
            int count = UnityEngine.Random.Range(minDeallocCount, maxDeallocCount);
            for(int i = 0; i < count & aliveListIndicies.Count != 0; i++)
            {
                int aliveListIndiciesIndex = UnityEngine.Random.Range(0, aliveListIndicies.Count);
                int listIndexToDeallocate = aliveListIndicies[aliveListIndiciesIndex];
                aliveListIndicies.RemoveAtSwapBack(aliveListIndiciesIndex);

                expectedLists[listIndexToDeallocate] = null;
                allocator.Deallocate(lists[listIndexToDeallocate].z);
                lists[listIndexToDeallocate] = 0;
                deallocationCount++;
            }
        }
        static void DeallocAll(FixedStepSegFitAllocator<int> allocator, List<int3> lists, List<int[]> expectedLists, List<int> aliveListIndicies)
        {
            for(int i = 0; i < lists.Count; i++)
            {
                if (lists[i].y == 0) continue;
                allocator.Deallocate(lists[i].z);
                lists[i] = 0;
            }
            for(int i = 0; i < expectedLists.Count; i++)
            {
                expectedLists[i] = null;
            }
            aliveListIndicies.Clear();
        }
        static bool CheckContent(FixedStepSegFitAllocator<int> allocator, List<int3> lists, List<int[]> expectedLists, StringBuilder sb)
        {
            if (lists.Count != expectedLists.Count)
            {
                sb.AppendLine("list count does not match expected list count");
                return false;
            }
            
            for(int i = 0; i < lists.Count; i++)
            {
                if (lists[i].Equals(0) & expectedLists[i] != null)
                {
                    sb.AppendLine("it was expected to not be deallocated");
                    return false;
                }
                else if (!lists[i].Equals(0) & expectedLists[i] == null)
                {
                    sb.AppendLine("it was expected to be deallocated");
                    return false;
                }
                if (expectedLists[i] == null) continue;

                int[] expectedContent = expectedLists[i];
                NativeSlice<int> content = allocator.Data.AsArray().Slice(lists[i].x, lists[i].y);
                if(expectedContent.Length != content.Length)
                {
                    sb.AppendLine($"allocation size does not match the expected. Expected: {expectedContent.Length}, Result: {content.Length}");
                    return false;
                }
                for(int j = 0; j < expectedContent.Length; j++)
                {
                    if (expectedContent[j] != content[j])
                    {
                        sb.Append("content does not match");
                        return false;
                    }
                }
            }
            return true;
        }
        static bool CheckInternals(FixedStepSegFitAllocator<int> allocator, StringBuilder sb)
        {
            NativeList<FixedStepSegFitAllocator<int>.AllocBlock> allocBlockList = new NativeList<FixedStepSegFitAllocator<int>.AllocBlock>(Allocator.Temp);
            allocator.AllocBlockLinkedList.ToNativeList(allocBlockList);

            int lastDataStart = -1;
            for(int i = 0; i < allocBlockList.Length; i++)
            {
                if (allocBlockList[i].DataStart <= lastDataStart)
                {
                    sb.AppendLine($"allocation block data starts do not go sequential. Expected: greater than {lastDataStart}, Result: {allocBlockList[i].DataStart}");
                    return false;
                }
                lastDataStart = allocBlockList[i].DataStart;
            }

            for(int i = 0; i < allocBlockList.Length; i++)
            {
                int blockSize = allocator.Data.Length - allocBlockList[i].DataStart;
                if (i != allocBlockList.Length - 1) blockSize = allocBlockList[i + 1].DataStart - allocBlockList[i].DataStart;
                if(blockSize > allocator.GetMaxAllocationSize())
                {
                    sb.AppendLine($"contains block more than maximum allocation size. Max Alloc: {allocator.GetMaxAllocationSize()}, Block Size: {blockSize}");
                }
            }
            for(int i = 0; i < allocBlockList.Length - 1; i++)
            {
                bool firstBlockFree = allocBlockList[i].FreeNodeIndex != FixedStepSegFitAllocator<int>.INVALID_FREE_NODE_INDEX;
                bool secondBlockFree = allocBlockList[i + 1].FreeNodeIndex != FixedStepSegFitAllocator<int>.INVALID_FREE_NODE_INDEX;
                if (!(firstBlockFree & secondBlockFree)) continue;

                int curBlockSize = allocBlockList[i + 1].DataStart - allocBlockList[i].DataStart;
                int nextBlockSize = allocator.Data.Length - allocBlockList[i + 1].DataStart;
                if(i + 1 != allocBlockList.Length - 1) nextBlockSize = allocBlockList[i + 2].DataStart - allocBlockList[i + 1].DataStart;
                if(curBlockSize + nextBlockSize <= allocator.GetMaxAllocationSize())
                {
                    sb.AppendLine($"contains blocks that can be merget but not merged. First Block: {curBlockSize}, Second Block: {nextBlockSize}, Max Block Size: {allocator.GetMaxAllocationSize()}");
                    return false;
                }
            }
            int usedMemoryInBytes = 0;
            int unusedMemoryInBytes = 0;
            for (int i = 0; i < allocBlockList.Length; i++)
            {
                bool blockFree = allocBlockList[i].FreeNodeIndex != FixedStepSegFitAllocator<int>.INVALID_FREE_NODE_INDEX;
                int blockSize = allocator.Data.Length - allocBlockList[i].DataStart;
                if (i != allocBlockList.Length - 1) blockSize = allocBlockList[i + 1].DataStart - allocBlockList[i].DataStart;
                usedMemoryInBytes += math.select(sizeof(int) * blockSize, 0, blockFree);
                unusedMemoryInBytes += math.select(0, sizeof(int) * blockSize, blockFree);
            }
            sb.AppendLine($"Used memory: {usedMemoryInBytes}, Unused memory: {unusedMemoryInBytes}");

            return true;
            //(d)check if any block is bigger than it can handle
            //(d)check for mergable but not merged blocks
            //(d)check if block starts go sequential
            //check if free node indicies are valid
            //(d)check memory use
        }
    }
}
