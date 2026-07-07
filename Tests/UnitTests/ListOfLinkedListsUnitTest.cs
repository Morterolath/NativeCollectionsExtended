using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using NativeCollectionsExtended;
using System.Collections.Generic;
using UnityEngine;

namespace NativeCollectionsExtended.UnitTest
{
    internal class ListOfLinkedListsUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool LogTestInfo;
        public int MinInitialCount;
        public int MaxInitialCount;
        public int MinBucketSize;
        public int MaxBucketSize;
        public int MinOperation;
        public int MaxOperation;
        public int MinConsequtiveAdd;
        public int MaxConsequtiveAdd;
        public float DeallocateChance;
        public int MinConsequtiveDealloc;
        public int MaxConsequtiveDealloc;
        public int MinInitialCapacity;
        public int MaxInitialCapacity;
        private void Update()
        {
            if (!Run) return;
            MinInitialCount = math.clamp(MinInitialCount, 0, 1000);
            MaxInitialCount = math.clamp(MaxInitialCount, 0, 1000);
            MinBucketSize = math.clamp(MinBucketSize, 0, 100);
            MaxBucketSize = math.clamp(MaxBucketSize, 0, 100);
            MinOperation = math.clamp(MinOperation, 0, 1000000);
            MaxOperation = math.clamp(MaxOperation, 0, 1000000);
            MinConsequtiveAdd = math.clamp(MinConsequtiveAdd, 0, 1000000);
            MaxConsequtiveAdd = math.clamp(MaxConsequtiveAdd, 0, 1000000);
            DeallocateChance = math.clamp(DeallocateChance, 0, 100);
            MinConsequtiveDealloc = math.clamp(MinConsequtiveDealloc, 0, 1000000);
            MaxConsequtiveDealloc = math.clamp(MaxConsequtiveDealloc, 0, 1000000);
            MinInitialCapacity = math.clamp(MinInitialCapacity, 0, 10000);
            MaxInitialCapacity = math.clamp(MaxInitialCapacity, 0, 10000);
            SetMinMax(MinInitialCount, MaxInitialCount, out MinInitialCount, out MaxInitialCount);
            SetMinMax(MinBucketSize, MaxBucketSize, out MinBucketSize, out MaxBucketSize);
            SetMinMax(MinOperation, MaxOperation, out MinOperation, out MaxOperation);
            SetMinMax(MinConsequtiveAdd, MaxConsequtiveAdd, out MinConsequtiveAdd, out MaxConsequtiveAdd);
            SetMinMax(MinConsequtiveDealloc, MaxConsequtiveDealloc, out MinConsequtiveDealloc, out MaxConsequtiveDealloc);
            SetMinMax(MinInitialCapacity, MaxInitialCapacity, out MinInitialCapacity, out MaxInitialCapacity);
            Test(LogTestInfo, MinInitialCount, MaxInitialCount, MinBucketSize, MaxBucketSize, MinOperation, MaxOperation,
                MinConsequtiveAdd, MaxConsequtiveAdd, DeallocateChance, MinConsequtiveDealloc, MaxConsequtiveDealloc, MinInitialCapacity, MaxInitialCapacity);
        }
        static void SetMinMax(int v1, int v2, out int min, out int max)
        {
            min = math.min(v1, v2);
            max = math.max(v1, v2);
        }
        static void Test(
            bool logTestInfo,
            int minInitialCount,
            int maxInitialCount,
            int minBucketSize,
            int maxBucketSize, 
            int minOperation, 
            int maxOperation, 
            int minConsequtiveAdd, 
            int maxConsequtiveAdd, 
            float deallocateChance, 
            int minConsequtiveDealloc, 
            int maxConsequtiveDealloc,
            int minInitialCap,
            int maxInitialCap)
        {
            int initialCount = UnityEngine.Random.Range(minInitialCount, maxInitialCount);
            initialCount = math.max(1, initialCount);
            int bucketSize = UnityEngine.Random.Range(minBucketSize, maxBucketSize);

            Initialize(initialCount, bucketSize, minInitialCap, maxInitialCap, out ListOfLinkedLists<int> linkedList, out List<NativeList<int>> nestedList);
            NativeArray<Operation> operations = GetOperations(linkedList, minOperation, maxOperation, minConsequtiveAdd,
                maxConsequtiveAdd, deallocateChance, minConsequtiveDealloc, maxConsequtiveDealloc);
            ApplyOperations(operations, linkedList, nestedList, out int opCount, out int addCount, out int deallocCount);
            bool success = TestContent(linkedList, nestedList);

            string testInfo = (success ? "Passed\n" : "Failed\n");
            if (logTestInfo)
            {
                testInfo += "InitialCount: " + initialCount + "\n" +
                    "Bucket Size: " + bucketSize + "\n" +
                    "Operations Applied: " + opCount + "\n" +
                    "Additions: " + addCount + "\n" +
                    "Deallocations: " + deallocCount + "\n" +
                    "Min-Max Initial List Capacity: " + minInitialCap + " - " + maxInitialCap;
            }
            UnityEngine.Debug.Log(testInfo);
        }
        static void ApplyOperations(NativeArray<Operation> operations, ListOfLinkedLists<int> linkedList, List<NativeList<int>> nestedList
            , out int opCount, out int addCount, out int deallocCount)
        {
            opCount = 0;
            addCount = 0;
            deallocCount = 0;
            if (linkedList.Count == 0) return;
            for(int i = 0; i < operations.Length; i++)
            {
                Operation op = operations[i];
                int listIndex = op.ListIndex;
                int opcnt = op.OpCount;
                switch (op.Optype)
                {
                    case Optype.Append:
                        for(int j = 0; j < opcnt; j++)
                        {
                            int val = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                            linkedList.Append(listIndex, val);
                            nestedList[listIndex].Add(val);
                            opCount++;
                            addCount++;
                        }
                        break;
                    case Optype.Deallocate:
                        for(int j = 0; j < opcnt; j++)
                        {
                            int listToDealloc = UnityEngine.Random.Range(0, linkedList.Count);
                            linkedList.DeallocateList(listToDealloc);
                            nestedList[listToDealloc].Clear();
                            opCount++;
                            deallocCount++;
                        }
                        break;
                }
            }
        }
        static void Initialize(
            int initialCount,
            int bucketSize,
            int minInitialCap,
            int maxInitialCap,
            out ListOfLinkedLists<int> linkedListOut, out List<NativeList<int>> nestedListOut)
        {
            ListOfLinkedLists<int> listOfLinkedLists = new ListOfLinkedLists<int>(bucketSize, Allocator.Temp);
            List<NativeList<int>> nestedList = new List<NativeList<int>>();
            listOfLinkedLists.Count = initialCount;
            for (int i = 0; i < initialCount; i++)
            {
                nestedList.Add(new NativeList<int>(Allocator.Temp));
                listOfLinkedLists.IncreaseCapacity(i, UnityEngine.Random.Range(minInitialCap, maxInitialCap));
            }
            linkedListOut = listOfLinkedLists;
            nestedListOut = nestedList;
        }
        static NativeArray<Operation> GetOperations(ListOfLinkedLists<int> listOfFixedLists, int minOperation, int maxOperation,
            int minConsequtiveAdd, int maxConsequtiveAdd, float deallocateChance, int minConsequtiveDealloc, int maxConsequtiveDealloc)
        {
            int operationCount = UnityEngine.Random.Range(minOperation, maxOperation);
            NativeList<Operation> operations = new NativeList<Operation>(Allocator.Temp);
            while (operationCount > 0)
            {
                int listIndex = UnityEngine.Random.Range(0, listOfFixedLists.Count);

                float removeRng = UnityEngine.Random.Range(1, 100f);

                if(removeRng < deallocateChance)
                {
                    int consequtiveDealloc = UnityEngine.Random.Range(minConsequtiveDealloc, maxConsequtiveDealloc);
                    consequtiveDealloc = math.max(1, consequtiveDealloc);
                    consequtiveDealloc = math.min(consequtiveDealloc, operationCount);
                    operations.Add(new Operation { Optype = Optype.Deallocate, OpCount = consequtiveDealloc, ListIndex = listIndex, });
                    operationCount -= consequtiveDealloc;
                }
                else
                {
                    int consequtiveAdd = UnityEngine.Random.Range(minConsequtiveAdd, maxConsequtiveAdd);
                    consequtiveAdd = math.max(1, consequtiveAdd);
                    consequtiveAdd = math.min(consequtiveAdd, operationCount);
                    operations.Add(new Operation { Optype = Optype.Append, OpCount = consequtiveAdd, ListIndex = listIndex, });
                    operationCount -= consequtiveAdd;
                }
            }
            return operations.AsArray();
        }
        static bool TestContent(ListOfLinkedLists<int> listOfLinkedLists, List<NativeList<int>> nestedList)
        {
            for(int i = 0; i < listOfLinkedLists.Count; i++)
            {
                NativeList<int> list = nestedList[i];
                ListOfLinkedLists<int>.Enumerator enumerator = listOfLinkedLists.GetEnumerator(i);

                int index = 0;
                while (enumerator.MoveNext())
                {
                    NativeSlice<int> bucket = enumerator.Current;
                    for(int j = 0; j < bucket.Length; j++)
                    {
                        if (bucket[j] != list[index++])
                        {
                            UnityEngine.Debug.Log("Content does not match");
                            return false;
                        }
                    }
                }
                if(index < list.Length)
                {
                    UnityEngine.Debug.Log("Content does not match");
                    return false;
                }
            }
            return true;
        }


        struct Operation
        {
            internal Optype Optype;
            internal int ListIndex;
            internal int OpCount;
        }
        enum Optype
        {
            Append,
            Deallocate
        }
    }
}
