using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeCollectionsExtended;
using Unity.Collections;
using Unity.Mathematics;
using System.Text;
using System;

namespace NativeCollectionsExtended.UnitTest
{

    public class ListOfFixedListsUnitTest : MonoBehaviour
    {
        public bool Run;
        [Range(1, 1000000)] public int MinInitialCount;
        [Range(1, 1000000)] public int MaxInitialCount;
        [Range(0, 1000000)] public int MinCapacity;
        [Range(0, 1000000)] public int MaxCapacity;
        [Range(0, 1000000)] public int MinOperation;
        [Range(0, 1000000)] public int MaxOperation;
        [Range(0, 1000000)] public int MinConsequtiveAdd;
        [Range(0, 1000000)] public int MaxConsequtiveAdd;
        void Update()
        {
            MinInitialCount = Math.Clamp(MinInitialCount, 1, 1000000);
            MaxInitialCount = Math.Clamp(MaxInitialCount, 1, 1000000);
            MinCapacity = Math.Clamp(MinCapacity, 0, 1000000);
            MaxCapacity = Math.Clamp(MaxCapacity, 0, 1000000);
            MinOperation = Math.Clamp(MinOperation, 0, 1000000);
            MaxOperation = Math.Clamp(MaxOperation, 0, 1000000);
            MinConsequtiveAdd = Math.Clamp(MinConsequtiveAdd, 0, 1000000);
            MaxConsequtiveAdd = Math.Clamp(MaxConsequtiveAdd, 0, 1000000);
            SetMinMax(MinInitialCount, MaxInitialCount, out MinInitialCount, out MaxInitialCount);
            SetMinMax(MinCapacity, MaxCapacity, out MinCapacity, out MaxCapacity);
            SetMinMax(MinOperation, MaxOperation, out MinOperation, out MaxOperation);
            SetMinMax(MinConsequtiveAdd, MaxConsequtiveAdd, out MinConsequtiveAdd, out MaxConsequtiveAdd);
            if (!Run) return;

            bool addUnitialized = UnityEngine.Random.Range(0, 2) == 0;
            Test(MinInitialCount, MaxInitialCount, MinCapacity, MaxCapacity, MinOperation, MaxOperation, MinConsequtiveAdd, MaxConsequtiveAdd, addUnitialized); 
        }
        static void SetMinMax(int v1, int v2, out int min, out int max)
        {
            min = math.min(v1, v2);
            max = math.max(v1, v2);
        }
        static void Test(
            int minInitialCount,
            int maxInitialCount,
            int minCapacity,
            int maxCapacity,
            int minOperation,
            int maxOperation,
            int minConsequtiveAdd,
            int maxConsequtiveAdd,
            bool addListUnitialized)
        {
            //Initialize lists
            int initialCount = UnityEngine.Random.Range(minInitialCount, maxInitialCount);
            initialCount = math.max(1, initialCount);
            NativeArray<int> expectedCapacity = new NativeArray<int>(initialCount, Allocator.Temp);
            NativeArray<int> expectedLength = new NativeArray<int>(initialCount, Allocator.Temp);
            for (int i = 0; i < expectedCapacity.Length; i++) expectedCapacity[i] = UnityEngine.Random.Range(minCapacity, maxCapacity);

            ListOfFixedLists<int> listOfFixedLists = new ListOfFixedLists<int>(Allocator.Temp);
            List<NativeList<int>> nestedList = new List<NativeList<int>>();
            for (int i = 0; i < initialCount; i++)
            {
                int capacity = expectedCapacity[i];
                if (addListUnitialized) listOfFixedLists.AddListUninitialized(capacity);
                else listOfFixedLists.AddList(capacity);
                nestedList.Add(new NativeList<int>(Allocator.Temp));
            }
            if (addListUnitialized) listOfFixedLists.ReinitializeAllListValues();
            bool success = true;
            success &= TestContent(listOfFixedLists, nestedList);
            success &= TestCapacities(listOfFixedLists, expectedCapacity);
            success &= TestLengths(listOfFixedLists, expectedLength);
            
            //Add random elements
            NativeArray<Operation> operations = GetOperations(listOfFixedLists, minOperation, maxOperation, minConsequtiveAdd, maxConsequtiveAdd, out int operationCount);
            ListOfFixedLists<int>.Array listOfFixedListsArray = listOfFixedLists.AsArray();
            for(int i = 0; i < operations.Length; i++)
            {
                Operation op = operations[i];
                ListOfFixedLists<int>.ListWriter writer = listOfFixedListsArray.GetListWriter(op.ListIndex);
                for(int j = 0; j < op.ConsequtiveAdd; j++)
                {
                    int randomNumber = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    if (writer.IsFull())
                    {
                        continue;
                    }
                    expectedLength[op.ListIndex]++;
                    writer.AddNoResize(randomNumber);
                    nestedList[op.ListIndex].Add(randomNumber);
                }
                writer.Submit();
            }

            success &= TestLengths(listOfFixedLists, expectedLength);
            success &= TestContent(listOfFixedLists, nestedList);
            success &= TestCapacities(listOfFixedLists, expectedCapacity);


            string testInfo = (success ? "Passed\n" : "Failed\n") +
                "ListCount: " + listOfFixedLists.Count + "\n" +
                "Operations Applied: " + operationCount;

            UnityEngine.Debug.Log(testInfo);
        }
        static NativeArray<Operation> GetOperations(ListOfFixedLists<int> listOfFixedLists, int minOperation, int maxOperation, 
            int minConsequtiveAdd, int maxConsequtiveAdd, out int operationCountOut)
        {
            ListOfFixedLists<int>.Array listOfFixedListsArray = listOfFixedLists.AsArray();
            int operationCount = UnityEngine.Random.Range(minOperation, maxOperation);
            operationCountOut = operationCount;
            NativeList<Operation> operations = new NativeList<Operation>(Allocator.Temp);
            while(operationCount > 0)
            {
                int listIndex = UnityEngine.Random.Range(0, listOfFixedListsArray.Count);
                int consequtiveAdd = UnityEngine.Random.Range(minConsequtiveAdd, maxConsequtiveAdd);
                consequtiveAdd = math.max(1, consequtiveAdd);
                consequtiveAdd = math.min(consequtiveAdd, operationCount);
                operations.Add(new Operation { ConsequtiveAdd = consequtiveAdd, ListIndex = listIndex, });
                operationCount -= consequtiveAdd;
            }
            return operations.AsArray();
        }
        static bool TestCapacities(ListOfFixedLists<int> listOfFixedLists, NativeArray<int> expectedCapacities)
        {
            for(int i = 0; i < listOfFixedLists.Count; i++) 
            {
                if (expectedCapacities[i] != listOfFixedLists.Capacity(i) && expectedCapacities[i] != 0)
                {
                    UnityEngine.Debug.Log($"Capacities dont match. Expected: {expectedCapacities[i]}, Capacity: {listOfFixedLists.Capacity(i)}");
                    return false;
                }
            }
            return true;
        }
        static bool TestLengths(ListOfFixedLists<int> listOfFixedLists, NativeArray<int> expectedLengths)
        {
            for(int i = 0; i < listOfFixedLists.Count; i++) 
            {
                if (expectedLengths[i] != listOfFixedLists.Length(i))
                {
                    UnityEngine.Debug.Log($"Lengths dont match. Expected: {expectedLengths[i]}, Length: {listOfFixedLists.Length(i)}");
                    return false;
                }
            }
            return true;
        }
        static bool TestContent(ListOfFixedLists<int> listOfFixedLists, List<NativeList<int>> nestedList)
        {
            ListOfFixedLists<int>.Array listOfFixedListsArray = listOfFixedLists.AsArray();
            for (int i = 0; i < listOfFixedListsArray.Count; i++)
            {
                NativeSlice<int> fixedData = listOfFixedListsArray.GetListData(i);
                NativeList<int> nestedData = nestedList[i];
                for(int j = 0; j < fixedData.Length; j++)
                {
                    if (fixedData[j] != nestedData[j])
                    {
                        UnityEngine.Debug.Log("Content does not match");
                        return false;
                    }
                }
            }
            return true;
        }

        struct Operation
        {
            public int ListIndex;
            public int ConsequtiveAdd;
        }
    }
}