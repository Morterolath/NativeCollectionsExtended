using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
using NativeCollectionsExtended;

namespace NativeCollectionsExtended.UnitTest
{
    public class NestedNativeHashMapUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool LogTestInfo;
        public int MinMapCount;
        public int MaxMapCount;
        public int MinKey;
        public int MaxKey;
        public int MinOperationCount;
        public int MaxOperationCount;
        public int MinConsequtiveAddsToMap;
        public int MaxConsequtiveAddsToMap;
        public int MinConsequtiveRemove;
        public int MaxConsequtiveRemove;
        public int MinBucketSize;
        public int MaxBucketSize;
        public int MinSectorMatrixColAmount;
        public int MaxSectorMatrixColAmount;
        public int MinHashGridColAmount;
        public int MaxHashGridColAmount;
        public int MinMapCapacity;
        public int MaxMapCapacity;
        public float MapRemovalChance;

        private void Update()
        {
            MinMapCount = Math.Clamp(MinMapCount, 0, 1000000);
            MaxMapCount = Math.Clamp(MaxMapCount, 0, 1000000);
            MinKey = Math.Clamp(MinKey, 0, int.MaxValue);
            MaxKey = Math.Clamp(MaxKey, 0, int.MaxValue);
            MinOperationCount = Math.Clamp(MinOperationCount, 0, 1000000);
            MaxOperationCount = Math.Clamp(MaxOperationCount, 0, 1000000);
            MinConsequtiveAddsToMap = Math.Clamp(MinConsequtiveAddsToMap, 1, 1000000);
            MaxConsequtiveAddsToMap = Math.Clamp(MaxConsequtiveAddsToMap, 1, 1000000);
            MinConsequtiveRemove = Math.Clamp(MinConsequtiveRemove, 1, 1000000);
            MaxConsequtiveRemove = Math.Clamp(MaxConsequtiveRemove, 1, 1000000);
            MinBucketSize = Math.Clamp(MinBucketSize, 1, 1000000);
            MaxBucketSize = Math.Clamp(MaxBucketSize, 1, 1000000);
            MinSectorMatrixColAmount = Math.Clamp(MinSectorMatrixColAmount, 16, 1000000);
            MaxSectorMatrixColAmount = Math.Clamp(MaxSectorMatrixColAmount, 16, 1000000);
            MinHashGridColAmount = Math.Clamp(MinHashGridColAmount, 16, 100);
            MaxHashGridColAmount = Math.Clamp(MaxHashGridColAmount, 16, 100);
            MapRemovalChance = Math.Clamp(MapRemovalChance, 0, 100f);
            MinMapCapacity = Math.Clamp(MinMapCapacity, 0, 1000000);
            MaxMapCapacity = Math.Clamp(MaxMapCapacity, 0, 1000000);

            SetMinMax(MinMapCount,  MaxMapCount, out MinMapCount, out MaxMapCount);
            SetMinMax(MinKey, MaxKey, out MinKey, out MaxKey);
            SetMinMax(MinOperationCount, MaxOperationCount, out MinOperationCount, out MaxOperationCount);
            SetMinMax(MinConsequtiveAddsToMap, MaxConsequtiveAddsToMap, out MinConsequtiveAddsToMap, out MaxConsequtiveAddsToMap);
            SetMinMax(MinConsequtiveRemove, MaxConsequtiveRemove, out MinConsequtiveRemove, out MaxConsequtiveRemove);
            SetMinMax(MinBucketSize, MaxBucketSize, out MinBucketSize, out MaxBucketSize);
            SetMinMax(MinSectorMatrixColAmount, MaxSectorMatrixColAmount, out MinSectorMatrixColAmount, out MaxSectorMatrixColAmount);
            SetMinMax(MinHashGridColAmount, MaxHashGridColAmount, out MinHashGridColAmount, out MaxHashGridColAmount);
            SetMinMax(MinMapCapacity, MaxMapCapacity, out MinMapCapacity, out MaxMapCapacity);

            if (!Run) return;
            NativeNestedHashMapTestNew test = new NativeNestedHashMapTestNew
            {
                LogTestInfo = LogTestInfo,
                Seed1 = (uint) UnityEngine.Random.Range(0, int.MaxValue),
                Seed2 = (uint) UnityEngine.Random.Range(0, int.MaxValue),
                Seed3 = (uint) UnityEngine.Random.Range(0, int.MaxValue),
                MaxConsequtiveAddsToMap = MaxConsequtiveAddsToMap,
                MaxKey = MaxKey,
                MaxMapCount = MaxMapCount,
                MaxOperationCount = MaxOperationCount,
                MinConsequtiveAddsToMap = MinConsequtiveAddsToMap,
                MinKey = MinKey,
                MinOperationCount = MinOperationCount,
                MapRemovalChance = MapRemovalChance,
                MaxConsequtiveRemove = MaxConsequtiveRemove,
                MinConsequtiveRemove = MinConsequtiveRemove,
                MinMapCount = MinMapCount,
                MaxBucketSize = MaxBucketSize,
                MinSectorMatrixColAmount = MinSectorMatrixColAmount,
                MaxHashGridColAmount = MaxHashGridColAmount,
                MaxSectorMatrixColAmount = MaxSectorMatrixColAmount,
                MinHashGridColAmount = MinHashGridColAmount,
                MinBucketSize = MinBucketSize,
                MinMapCapacity = MinMapCapacity,
                MaxMapCapacity = MaxMapCapacity,
            };
            test.Run();
        }
        static void SetMinMax(int v1, int v2, out int min, out int max)
        {
            min = math.min(v1, v2);
            max = math.max(v1, v2);
        }
    }
    [BurstCompile]
    internal struct NativeNestedHashMapTestNew : IJob
    {
        enum OperationType
        {
            RemoveMap,
            AddToMap,
        }
        struct Operation
        {
            internal OperationType Type;
            internal int MapIndex;

            internal Operation(OperationType type, int mapIndex)
            {
                Type = type;
                MapIndex = mapIndex;
            }
        }
        public bool LogTestInfo;
        public uint Seed1;
        public uint Seed2;
        public uint Seed3;
        public int MinMapCount;
        public int MaxMapCount;
        public int MinKey;
        public int MaxKey;
        public int MinOperationCount;
        public int MaxOperationCount;
        public int MinConsequtiveAddsToMap;
        public int MaxConsequtiveAddsToMap;
        public int MinConsequtiveRemove;
        public int MaxConsequtiveRemove;
        public int MinHashGridColAmount;
        public int MaxHashGridColAmount;
        public int MinBucketSize;
        public int MaxBucketSize;
        public int MinSectorMatrixColAmount;
        public int MaxSectorMatrixColAmount;
        public int MinMapCapacity;
        public int MaxMapCapacity;
        public float MapRemovalChance;
        public void Execute()
        {
            Unity.Mathematics.Random rnd = TestUtils.GetRandom(Seed1, Seed2, Seed3);
            int mapCount = rnd.NextInt(MinMapCount, MaxMapCount);
            int hashGridColAmount = rnd.NextInt(MinHashGridColAmount, MaxHashGridColAmount);
            int bucketSize = rnd.NextInt(MinBucketSize, MaxBucketSize);
            int sectorMatrixColAmount = rnd.NextInt(MinSectorMatrixColAmount, MaxSectorMatrixColAmount);

            
            InitializeMaps(rnd, sectorMatrixColAmount, bucketSize, hashGridColAmount, mapCount, MinMapCapacity, MaxMapCapacity,
                out NativeHashMap<int2, int> nativeMap, out NestedSectorHashMap<int> nestedMap);
            
            NativeArray<Operation> operations = GetOperations(ref rnd, MapRemovalChance, MinOperationCount, MaxOperationCount, MinConsequtiveRemove, MaxConsequtiveRemove,
                MinConsequtiveAddsToMap, MaxConsequtiveAddsToMap, nestedMap.Count, out int operationCount, out int removeCount, out int addCount);
            
            ApplyOperations(operations, nestedMap, nativeMap, ref rnd, MinKey, MaxKey);

            bool passed = TestContent(nestedMap, nativeMap);
            passed &= TestKeyCount(nestedMap);

            FixedString32Bytes passedOrNot = passed ? "Passed" : "Failed";
            FixedString512Bytes testInfo = $"{passedOrNot}\n MapCount: {mapCount}\nHashGridColAmount: {hashGridColAmount}\nBucketSize: {bucketSize}\nSectorMatrixColAmount: {sectorMatrixColAmount}\nOperations Applied: {operationCount}\nMap Removals: {removeCount}\nKey/Value Adds {addCount}";
            if (LogTestInfo) UnityEngine.Debug.Log(testInfo);
            else UnityEngine.Debug.Log(passedOrNot);
        }
        static bool TestKeyCount(NestedSectorHashMap<int> nestedMap)
        {
            for (int i = 0; i < nestedMap.Count; i++)
            {
                NestedSectorHashMap<int>.Enumerator nestedEnumerator = nestedMap.GetEnumerator(i);
                int expectedCount = nestedMap.KeyCount(i);
                int counted = 0;
                while (nestedEnumerator.MoveNext())
                {
                    counted++;
                }
                if(expectedCount != counted)
                {
                    UnityEngine.Debug.Log($"Key count does not match: Map.KeyCount = {expectedCount}, Enumerated Key Count = {counted}");
                    return false;
                }
            }
            return true;
        }
        static bool TestContent(NestedSectorHashMap<int> nestedMap, NativeHashMap<int2, int> nativeMap)
        {
            NativeHashMap<int2, int>.Enumerator nativeEnumerator = nativeMap.GetEnumerator();
            while (nativeEnumerator.MoveNext())
            {
                if (!nestedMap.Contains(nativeEnumerator.Current.Key.x, nativeEnumerator.Current.Key.y))
                {
                    UnityEngine.Debug.Log("Missing key/value in hashmap");
                    return false;
                }
            }

            for (int i = 0; i < nestedMap.Count; i++)
            {
                NestedSectorHashMap<int>.Enumerator nestedEnumerator = nestedMap.GetEnumerator(i);
                while (nestedEnumerator.MoveNext())
                {
                    if (!nativeMap.ContainsKey(new int2(i, nestedEnumerator.CurrentKey)))
                    {
                        UnityEngine.Debug.Log("Hahmap contains a key/value it should not");
                        return false;
                    }
                }
            }
            return true;
        }
        static void ApplyOperations(NativeArray<Operation> operations, NestedSectorHashMap<int> nestedMap, NativeHashMap<int2, int> nativeMap,
            ref Unity.Mathematics.Random rnd, int minKey, int maxKey)
        {
            NativeList<int2> keysToRemoveFromNativeMap = new NativeList<int2>(Allocator.Temp);
            for (int i = 0; i < operations.Length; i++)
            {
                Operation operation = operations[i];
                int map = operation.MapIndex;
                switch (operation.Type)
                {
                    case OperationType.RemoveMap:
                        if (nestedMap.IsRemoved(map))
                            break;
                        nestedMap.RemoveMap(map);
                        NativeHashMap<int2, int>.Enumerator mapenum = nativeMap.GetEnumerator();
                        while (mapenum.MoveNext())
                        {
                            if (mapenum.Current.Key.x == map)
                            {
                                keysToRemoveFromNativeMap.Add(mapenum.Current.Key);
                            }
                        }
                        for (int j = 0; j < keysToRemoveFromNativeMap.Length; j++)
                        {
                            nativeMap.Remove(keysToRemoveFromNativeMap[j]);
                        }
                        break;
                    case OperationType.AddToMap:
                        int key = rnd.NextInt(minKey, maxKey);
                        nativeMap.TryAdd(new int2(map, key), 0);
                        nestedMap.Add(map, key, 0);
                        break;
                }
                keysToRemoveFromNativeMap.Clear();
            }

        }
        static void InitializeMaps(Unity.Mathematics.Random rnd, int sectorMatrixColAmount, int bucketSize, int hashGridColAmount, 
            int mapCount, int minMapCapactiy, int maxMapCapacity, out NativeHashMap<int2, int> nativeMap, out NestedSectorHashMap<int> nestedMap)
        {
            nativeMap = new NativeHashMap<int2, int>(0, Allocator.Temp);
            nestedMap = new NestedSectorHashMap<int>(0, bucketSize, sectorMatrixColAmount, hashGridColAmount, Allocator.Temp);
            nestedMap.Count = mapCount;
            
            for(int i = 0; i < nestedMap.Count; i++)
            {
                int cap = rnd.NextInt(minMapCapactiy, maxMapCapacity);
                nestedMap.IncreaseCapacity(i, cap);
            }
        }
        static NativeArray<Operation> GetOperations(
            ref Unity.Mathematics.Random rnd, 
            float MapRemovalChance,
            int minOperationCount,
            int maxOperationCount,
            int minConsequtiveRemove,
            int maxConsequtiveRemove,
            int minConsequtiveAddsToMap,
            int maxConsequtiveAddsToMap,
            int nestedMapCount,
            out int operationCountOut,
            out int removeCountOut,
            out int addCountOut)
        {
            removeCountOut = 0;
            addCountOut = 0;
            int operationCount = rnd.NextInt(minOperationCount, maxOperationCount);
            operationCountOut = operationCount;
            NativeList<Operation> operations = new NativeList<Operation>(Allocator.Temp);
            while (operationCount > 0)
            {
                OperationType opType = rnd.NextFloat(1f, 100f) <= MapRemovalChance ? OperationType.RemoveMap : OperationType.AddToMap;

                switch (opType)
                {
                    case OperationType.RemoveMap:
                        int removeCount = rnd.NextInt(minConsequtiveRemove, maxConsequtiveRemove);
                        removeCount = math.max(removeCount, 1);
                        removeCount = math.min(removeCount, operationCount);
                        for (int i = 0; i < removeCount; i++)
                        {
                            int mapIndexToRemove = rnd.NextInt(0, nestedMapCount);
                            if (mapIndexToRemove < 0 || mapIndexToRemove >= nestedMapCount) continue;
                            operations.Add(new Operation(OperationType.RemoveMap, mapIndexToRemove));
                            removeCountOut++;
                        }
                        operationCount -= removeCount;
                        break;

                    default:
                        int mapIndex = rnd.NextInt(0, nestedMapCount);
                        int addCount = rnd.NextInt(minConsequtiveAddsToMap, maxConsequtiveAddsToMap);
                        addCount = math.max(addCount, 1);
                        addCount = math.min(addCount, operationCount);
                        for (int i = 0; i < addCount; i++)
                        {
                            operations.Add(new Operation(opType, mapIndex));
                            addCountOut++;
                        }
                        operationCount -= addCount;
                        break;
                }
            }
            return operations.AsArray();
        }
    }
}