using System.Collections.Generic;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NativeCollectionsExtended.UnitTest
{
    internal class SLSFAllocatorUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool Log;
        public int2 AllocCount = new int2(0, 2000);
        public int2 AllocSize = new int2(0, 2000);
        public float2 DisposeChance = new float2(0, 0.3f);
        private void Update()
        {
            AllocCount = ClampAndSetMinMax(AllocCount, 0, 10000000);
            AllocSize = ClampAndSetMinMax(AllocSize, 0, 10000000);
            DisposeChance = ClampAndSetMinMax(DisposeChance, 0, 100);

            if (!Run)
                return;
            
            SLSFAllocator<int> allocator = new SLSFAllocator<int>(Allocator.TempJob);
            NativeList<AllocRef> examined = new NativeList<AllocRef>(Allocator.TempJob);
            List<int[]> example = new List<int[]>();
            
            ApplyOperations(allocator, examined, example, AllocCount, AllocSize, DisposeChance,
                out int allocsMade, out int disposalsMade);

            InternalTestJob testJob = new InternalTestJob
            {
                Examined = examined,
                SLSFAlloc = allocator,
            };
            testJob.Run();

            Compare(allocator, examined, example);

            if (Log)
            {
                LogInfo(allocator, examined, example, allocsMade, disposalsMade);
            }

            examined.Dispose();
            allocator.Dispose();
        }
        static int2 ClampAndSetMinMax(int2 v, int min, int max)
        {
            v = math.clamp(v, min, max);
            return new int2(math.min(v.x, v.y), math.max(v.x, v.y));
        }
        static float2 ClampAndSetMinMax(float2 v, float min, float max)
        {
            v = math.clamp(v, min, max);
            return new float2(math.min(v.x, v.y), math.max(v.x, v.y));
        }
        void LogInfo(SLSFAllocator<int> allocator, NativeArray<AllocRef> examined, List<int[]> example, int allocsMade, int disposalsMade)
        {
            int totalAllocSize = 0;
            float averageAllocSize = 0;
            int minAllocSize = int.MaxValue;
            int maxAllocSize = int.MinValue;

            for(int i = 0; i < examined.Length; i++)
            {
                totalAllocSize += examined[i].Capacity;
                minAllocSize = math.min(minAllocSize, examined[i].Capacity);
                maxAllocSize = math.max(maxAllocSize, examined[i].Capacity);
            }
            averageAllocSize = totalAllocSize / (float)examined.Length;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Test Info");
            sb.AppendLine("Allocations Made: " + allocsMade);
            sb.AppendLine("Disposals Made: " + disposalsMade);
            sb.AppendLine("Existing Allocations: " + examined.Length);
            sb.AppendLine("Total Alloc Size: " + totalAllocSize);
            sb.AppendLine("Avg Alloc Size: " + averageAllocSize);
            sb.AppendLine("Min Alloc Size: " + minAllocSize);
            sb.AppendLine("Max Alloc Size: " + maxAllocSize);
            UnityEngine.Debug.Log(sb.ToString());
        }
        [BurstCompile]
        struct ApplyOperationsPerfTestJob : IJob
        {
            internal uint Seed;
            internal NativeList<AllocRef> references;
            internal SLSFAllocator<int> allocator;
            internal int2 allocCountMinMax;
            internal int2 allocSizeMinMax;
            internal float2 deallocChanceMinMax;
            internal NativeReference<int> allocationsMade;
            internal NativeReference<int> disposalsMade;
            public void Execute()
            {
                Unity.Mathematics.Random rnd = new Unity.Mathematics.Random(Seed);
                int allocCnt = rnd.NextInt(allocCountMinMax.x, allocCountMinMax.y);
                float disposeChance = rnd.NextFloat(deallocChanceMinMax.x, deallocChanceMinMax.y);

                allocationsMade.Value = allocCnt;
                disposalsMade.Value = 0; ;

                for (int i = 0; i < allocCnt; i++)
                {
                    int allocSize = rnd.NextInt(allocSizeMinMax.x, allocSizeMinMax.y);
                    allocSize = math.max(allocSize, 2);

                    allocator.Allocate(allocSize, out int start, out int cap, out int allocId);
                    references.Add(new AllocRef { AllocId = allocId, Capacity = cap, Length = allocSize, Start = start });

                    float disposeRoll = rnd.NextFloat(0, 1f);
                    if (disposeRoll <= disposeChance & references.Length != 0)
                    {
                        int indexToDispose = rnd.NextInt(0, references.Length);
                        allocator.Deallocate(references[indexToDispose].AllocId);
                        references.RemoveAtSwapBack(indexToDispose);
                        disposalsMade.Value++;
                    }
                }
            }
        }
        static void ApplyOperations(SLSFAllocator<int> allocator, NativeList<AllocRef> examined, List<int[]> example,
            int2 allocCountMinMax, int2 allocSizeMinMax, float2 deallocChanceMinMax, out int allocationsMade,
            out int disposalsMade)
        {
            int allocCnt = UnityEngine.Random.Range(allocCountMinMax.x, allocCountMinMax.y);
            float disposeChance = UnityEngine.Random.Range(deallocChanceMinMax.x, deallocChanceMinMax.y);

            allocationsMade = allocCnt;
            disposalsMade = 0;

            for(int i = 0; i < allocCnt; i++)
            {
                int allocSize = UnityEngine.Random.Range(allocSizeMinMax.x, allocSizeMinMax.y);
                allocSize = math.max(allocSize, 2);

                int[] exampleAlloc = new int[allocSize];
                allocator.Allocate(allocSize, out int start, out int cap, out int allocId);
                for(int j = 0; j < allocSize; j++)
                {
                    int data = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    exampleAlloc[j] = data;
                    allocator.DataBuffer[start + j] = data;
                }
                example.Add(exampleAlloc);
                examined.Add(new AllocRef { AllocId = allocId, Capacity = cap, Length = allocSize, Start = start });

                float disposeRoll = UnityEngine.Random.Range(0, 1f);
                if (disposeRoll <= disposeChance & examined.Length != 0)
                {
                    int indexToDispose = UnityEngine.Random.Range(0, examined.Length);
                    allocator.Deallocate(examined[indexToDispose].AllocId);
                    example[indexToDispose] = null;
                    examined.RemoveAtSwapBack(indexToDispose);
                    example.RemoveAtSwapBack(indexToDispose);
                    disposalsMade++;
                }
            }
        }
        static void Compare(SLSFAllocator<int> allocator, NativeArray<AllocRef> examined, List<int[]> example)
        {
            for (int i = 0; i < example.Count; i++)
            {
                int[] exampleData = example[i];
                NativeSlice<int> examinedData = allocator.DataBuffer.AsArray().Slice(examined[i].Start, examined[i].Length);
                for(int j = 0; j < exampleData.Length; j++)
                {
                    if (exampleData[j] != examinedData[j])
                    {
                        UnityEngine.Debug.LogError("Examined data does not match example data");
                        return;
                    }
                }
            }
        }
        [BurstCompile]
        struct InternalTestJob : IJob
        {
            internal SLSFAllocator<int> SLSFAlloc;
            internal NativeArray<AllocRef> Examined;
            public void Execute()
            {
                //test alloc ref:
                //(done)alloc id within bounds
                //(done)chunk pointed by id matches start and capacity
                //(done)set all chunks as "allocated"

                //test interval data
                //(done)is last chunk really last
                //(done)is greatest interval correct
                //(done)are all chunks pointed by the interval correct size
                //(done)are there any cycles
                //(done)chunk.prevFree.nextFree must be equal to chunk
                //(done)set all chunks as "free"

                //test chunk buffer:
                //(done)are chunks in ascending order
                //(done)first chunk start index 0
                //(done)cur.prev.next must be equal to cur
                //(done)a chunk mus be either "allocated", or "free", or "unused". Not more than one of them, not none of them.
                //there should not be any consequtive "free" blocks
                //(done)chunk.capacity mus match chunk.next.start - chun.start

                //test unused chunk buffer
                //(done)does it contain duplicate indicies
                //(done)does it contain out of bound indicies
                //(done)set all as "unused"

                const int INVALID_CHUNK_IDX = SLSFAllocator<int>.INVALID_CHUNK_IDX;
                NativeArray<int> unusedChunkBuffer = SLSFAlloc.UnusedChunkIndexBuffer.AsArray();
                NativeArray<int> dataBuffer = SLSFAlloc.DataBuffer;
                SLSFAllocator<int>.IntervalData intervalData = SLSFAlloc.IntervalDataRef.Value;
                NativeArray<SLSFAllocator<int>.Chunk> chunkBuffer = SLSFAlloc.ChunkBuffer;

                NativeArray<bool> unusedFlagEachChunk = new NativeArray<bool>(chunkBuffer.Length, Allocator.Temp);
                NativeArray<bool> freeFlagEachChunk = new NativeArray<bool>(chunkBuffer.Length, Allocator.Temp);
                NativeArray<bool> allocatedFlagEachChunk = new NativeArray<bool>(chunkBuffer.Length, Allocator.Temp);


                for (int i = 0; i < unusedChunkBuffer.Length; i++)
                {
                    int chunkIdx = unusedChunkBuffer[i];
                    if(chunkIdx < 0 | chunkIdx >= chunkBuffer.Length)
                    {
                        UnityEngine.Debug.Log("unused chunk buffer contains out of bounds index");
                        return;
                    }
                    if (unusedFlagEachChunk[chunkIdx])
                    {
                        UnityEngine.Debug.Log("unused chunk buffer contains duplicate index");
                        return;
                    }
                    unusedFlagEachChunk[chunkIdx] = true;
                }

                for (int i = 0; i < Examined.Length; i++)
                {
                    AllocRef aref = Examined[i];

                    if(aref.AllocId < 0 | aref.AllocId >= chunkBuffer.Length)
                    {
                        UnityEngine.Debug.Log("allocation contains out of bounds allocId");
                        return;
                    }

                    SLSFAllocator<int>.Chunk chunk = chunkBuffer[aref.AllocId];
                    int chunkStart = chunk.DataStartIdx;
                    int chunkCapacity = chunk.Capacity;
                    if(aref.Start != chunkStart)
                    {
                        UnityEngine.Debug.Log($"allocation start ({aref.Start}) does not match start of its chunk ({chunk.DataStartIdx}) pointed by allocId");
                        return;
                    }
                    if(aref.Capacity != chunkCapacity)
                    {
                        UnityEngine.Debug.Log($"allocation capacity ({aref.Capacity}) does not match capacity of its chunk ({chunkCapacity}) pointed by allocId");
                        return;
                    }
                    if (allocatedFlagEachChunk[aref.AllocId])
                    {
                        UnityEngine.Debug.Log("multiple allocations have the same allocId");
                        return;
                    }

                    allocatedFlagEachChunk[aref.AllocId] = true;
                }

                if(intervalData.LastChunkIdx != INVALID_CHUNK_IDX)
                {
                    if (chunkBuffer[intervalData.LastChunkIdx].NextChunkIdx != INVALID_CHUNK_IDX)
                    {
                        UnityEngine.Debug.Log("intervalData.LastChunkIdx is not the last one. It has a next.");
                        return;
                    }
                }

                for(int i = 0; i < intervalData.IntervalBuffer.Length; i++)
                {
                    int iterationChunkIdx = intervalData.IntervalBuffer[i].FreeChunksHeadIdx;
                    int intervalMinCapacity = 2 << i;
                    int intervalMaxCapacity = (2 << (i + 1)) - 1;

                    while(iterationChunkIdx != INVALID_CHUNK_IDX)
                    {
                        SLSFAllocator<int>.Chunk chunk = chunkBuffer[iterationChunkIdx];
                        if(chunk.FreeListNextIdx != INVALID_CHUNK_IDX)
                        {
                            SLSFAllocator<int>.Chunk nextChunk = chunkBuffer[chunk.FreeListNextIdx];
                            if(nextChunk.FreeListPrevIdx != iterationChunkIdx)
                            {
                                UnityEngine.Debug.Log($"Cur.PrevFree.NextFree is not equal to Cur");
                                return;
                            }
                        }

                        int chunkCapacity = chunk.Capacity;
                        if(chunkCapacity < intervalMinCapacity | chunkCapacity > intervalMaxCapacity)
                        {
                            UnityEngine.Debug.Log($"chunk capacity ({chunkCapacity}) does not match interval capacity ({i}: {intervalMinCapacity}, {intervalMaxCapacity})");
                            return;
                        }
                        if (freeFlagEachChunk[iterationChunkIdx])
                        {
                            UnityEngine.Debug.Log($"Free chunk linked list has a cycle");
                            return;
                        }
                        freeFlagEachChunk[iterationChunkIdx] = true;
                        iterationChunkIdx = chunk.FreeListNextIdx;
                    }
                }

                int lastChunkStart = dataBuffer.Length;
                int chunkIterationIdx = intervalData.LastChunkIdx;
                while(chunkIterationIdx != INVALID_CHUNK_IDX)
                {
                    SLSFAllocator<int>.Chunk chunk = chunkBuffer[chunkIterationIdx];
                    if(chunk.DataStartIdx >= lastChunkStart)
                    {
                        UnityEngine.Debug.Log($"Data start of chunks is not in ascending order. (Start: {chunk.DataStartIdx}, Next Start: {lastChunkStart})");
                        return;
                    }
                    if (chunk.PrevChunkIdx != INVALID_CHUNK_IDX)
                    {
                        int nextOfPrev = chunkBuffer[chunk.PrevChunkIdx].NextChunkIdx;
                        if (nextOfPrev != chunkIterationIdx)
                        {
                            UnityEngine.Debug.Log($"Chunk.Prev.Next ({nextOfPrev}) is not equal to the Chunk ({chunkIterationIdx})");
                            return;
                        }
                    }
                    int startOfNext = dataBuffer.Length;
                    if (chunk.NextChunkIdx != INVALID_CHUNK_IDX)
                    {
                        startOfNext = chunkBuffer[chunk.NextChunkIdx].DataStartIdx;

                        if (freeFlagEachChunk[chunkIterationIdx] & freeFlagEachChunk[chunk.NextChunkIdx])
                        {
                            UnityEngine.Debug.Log("Consequtive free blocks");
                            return;
                        }
                    }
                    if(chunk.Capacity != startOfNext - chunk.DataStartIdx)
                    {
                        UnityEngine.Debug.Log($"Chunk capacity ({chunk.Capacity}) does not match Chunk.Next.Start ({startOfNext}) - Chun.Start ({chunk.DataStartIdx})");
                        return;
                    }
                    if(chunk.Capacity >= (2 << (intervalData.GreatestIntervalReached + 1)))
                    {
                        UnityEngine.Debug.Log($"gretest interval reached is not correct. Capacity: {chunk.Capacity}, Greatest Interval: {intervalData.GreatestIntervalReached}");
                        return;
                    }

                    lastChunkStart = chunk.DataStartIdx;
                    chunkIterationIdx = chunk.PrevChunkIdx;
                }

                if(lastChunkStart != 0)
                {
                    UnityEngine.Debug.Log($"Chunk linked list does not start from index 0 ({lastChunkStart})");
                    return;
                }
                
                for (int i = 0; i < chunkBuffer.Length; i++)
                {
                    bool allocated = allocatedFlagEachChunk[i];
                    bool free = freeFlagEachChunk[i];
                    bool unused = unusedFlagEachChunk[i];

                    int cnt = 0;
                    cnt += math.select(0, 1, allocated);
                    cnt += math.select(0, 1, free);
                    cnt += math.select(0, 1, unused);

                    if(cnt != 1)
                    {
                        UnityEngine.Debug.Log($"A chunk can be only allocated, free, or unused. (Allocated: {allocated}, Free: {free}, Unused: {unused})");
                        return;
                    }
                }

            }
        }
        static void InternalTest()
        {
        }
        struct AllocRef
        {
            internal int Start;
            internal int Length;
            internal int Capacity;
            internal int AllocId;
        }
    }
}
