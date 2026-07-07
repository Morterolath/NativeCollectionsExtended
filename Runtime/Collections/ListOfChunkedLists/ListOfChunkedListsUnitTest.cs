using NativeCollectionsExtended;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NativeCollectionsExtended.UnitTest
{
    internal class ListOfChunkedListsUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool Log;
        public int2 ChunkSize = new int2(0, 512);
        public int2 AddToListCount = new int2(0, 1000000);
        public float2 DiposeListChance = new float2(0, 0.005f);
        public float2 ClearListChance = new float2(0, 0.005f);
        public float2 AddListChance = new float2(0, 0.02f);
        private void Update()
        {
            if (!Run)
                return;

            ChunkSize = ClampAndMinMax(ChunkSize, 0, 10000);
            AddToListCount = ClampAndMinMax(AddToListCount, 0, 2000000);
            DiposeListChance = ClampAndMinMax(DiposeListChance, 0, 1);
            ClearListChance = ClampAndMinMax(ClearListChance, 0, 1);
            AddListChance = ClampAndMinMax(AddListChance, 0, 1);


            int chunkSize = UnityEngine.Random.Range(ChunkSize.x, ChunkSize.y);
            int addToListCount = UnityEngine.Random.Range(AddToListCount.x, AddToListCount.y);
            float disposeListChance = UnityEngine.Random.Range(DiposeListChance.x, DiposeListChance.y);
            float clearListChance = UnityEngine.Random.Range(ClearListChance.x, ClearListChance.y);
            float addListChance = UnityEngine.Random.Range(AddListChance.x, AddListChance.y);

            List<List<int>> example = new List<List<int>>();
            ListOfChunkedLists<int> examined = new ListOfChunkedLists<int>(chunkSize, Allocator.TempJob);
            NativeList<Operation> opBuffer = new NativeList<Operation>(Allocator.TempJob);

            FillOperationBufferJob fillOpBuffer = new FillOperationBufferJob
            {
                Seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                OperationBuffer = opBuffer,
                AddToListCount = addToListCount,
                DisposeListChance = disposeListChance,
                ClearListChance = clearListChance,
                AddListChance = addListChance,
            };
            fillOpBuffer.Run();

            ApplyOperationsToExaminedJob applyExamined = new ApplyOperationsToExaminedJob
            {
                List = examined,
                OperationBuffer = opBuffer,
            };
            applyExamined.Run();

            ApplyOperationsToExample(example, opBuffer);

            ChangeData(example, examined);

            TestResult(example, examined);

            if (Log)
            {
                int addToList = 0;
                int addList = 0;
                int disposeList = 0;
                int clearList = 0;
                NativeArray<Operation> opBufferAsArray = opBuffer.AsArray();
                for(int i = 0; i < opBufferAsArray.Length; i++)
                {
                    switch (opBufferAsArray[i].Type)
                    {
                        case OperationType.AddToList:
                            addToList++;
                            break;
                        case OperationType.AddList:
                            addList++;
                            break;
                        case OperationType.DisposeList:
                            disposeList++;
                            break;
                        case OperationType.ClearList:
                            clearList++;
                            break;
                    }
                }

                int listCount = example.Count;
                float totalListSize = 0;
                int minListSize = int.MaxValue;
                int minNonZeroListSize = int.MaxValue;
                int maxListSize = int.MinValue;
                int nullListCount = 0;
                for(int i = 0; i < example.Count; i++)
                {
                    if (example[i] != null)
                    {
                        int count = example[i].Count;
                        totalListSize += count;
                        minListSize = math.min(minListSize, count);
                        if (count != 0)
                        {
                            minNonZeroListSize = math.min(minNonZeroListSize, count);
                        }
                        maxListSize = math.max(maxListSize, count);
                    }
                    if (example[i] == null)
                    {
                        minListSize = math.min(minListSize, 0);
                        nullListCount++;
                    }
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Test Log");
                sb.AppendLine("AddToList: " + addToList);
                sb.AppendLine("AddList: " + addList);
                sb.AppendLine("ClearList: " + clearList);
                sb.AppendLine("DisposeList: " + disposeList);
                sb.AppendLine("ListCount: " + listCount);
                sb.AppendLine("TotalListSize: " + totalListSize);
                sb.AppendLine("AvgListSize: " + (totalListSize / listCount));
                sb.AppendLine("MinListSize: " + minListSize);
                sb.AppendLine("MinNonZeroListSize: " + minNonZeroListSize);
                sb.AppendLine("MaxListSize: " + maxListSize);
                sb.AppendLine("NullListCount: " + nullListCount);
                UnityEngine.Debug.Log(sb.ToString());
            }

            examined.Dispose();
            opBuffer.Dispose();
        }
        static int2 ClampAndMinMax(int2 val, int min, int max)
        {
            val = math.clamp(val, min, max);
            val.x = math.min(val.x, val.y);
            return val;
        }
        static float2 ClampAndMinMax(float2 val, int min, int max)
        {
            val = math.clamp(val, min, max);
            val.x = math.min(val.x, val.y);
            return val;
        }
        [BurstCompile]
        struct FillOperationBufferJob : IJob
        {
            internal uint Seed;
            internal int AddToListCount;
            internal float DisposeListChance;
            internal float ClearListChance;
            internal float AddListChance;
            internal NativeList<Operation> OperationBuffer;
            public void Execute()
            {
                OperationBuffer.Clear();
                
                Unity.Mathematics.Random rnd = new Unity.Mathematics.Random(Seed);

                int listCnt = 1;
                OperationBuffer.Add(new Operation { Type = OperationType.AddList });

                for(int i = 0; i < AddToListCount; i++)
                {
                    OperationBuffer.Add(new Operation 
                    { 
                        Type = OperationType.AddToList, Val = rnd.NextInt(), ListIndex = rnd.NextInt(0, listCnt)
                    });

                    float disposeListRoll = rnd.NextFloat(0, 1);
                    float clearListRoll = rnd.NextFloat(0, 1);
                    float addListRoll = rnd.NextFloat(0, 1);
                    
                    if(disposeListRoll <= DisposeListChance)
                    {
                        OperationBuffer.Add(new Operation { Type = OperationType.DisposeList, ListIndex = rnd.NextInt(0, listCnt) });
                    }

                    if(clearListRoll <= ClearListChance)
                    {
                        OperationBuffer.Add(new Operation { Type = OperationType.ClearList, ListIndex = rnd.NextInt(0, listCnt) });
                    }

                    if(addListRoll <= AddListChance)
                    {
                        OperationBuffer.Add(new Operation { Type = OperationType.AddList, });
                        listCnt++;
                    }
                }
            }
        }
        static void ApplyOperationsToExample(List<List<int>> example, NativeArray<Operation> operationBuffer)
        {
            for (int i = 0; i < operationBuffer.Length; i++)
            {
                Operation op = operationBuffer[i];
                switch (op.Type)
                {
                    case OperationType.DisposeList:
                        example[op.ListIndex] = null;
                        break;
                    case OperationType.ClearList:
                        if (example[op.ListIndex] == null)
                            example[op.ListIndex] = new List<int>();
                        example[op.ListIndex].Clear();
                        break;
                    case OperationType.AddList:
                        example.Add(new List<int>());
                        break;
                    case OperationType.AddToList:
                        if (example[op.ListIndex] == null)
                            example[op.ListIndex] = new List<int>();
                        example[op.ListIndex].Add(op.Val);
                        break;
                }
            }
        }
        [BurstCompile]
        struct ApplyOperationsToExaminedJob : IJob
        {
            [ReadOnly] internal NativeArray<Operation> OperationBuffer;
            internal ListOfChunkedLists<int> List;

            public void Execute()
            {
                for(int i = 0; i < OperationBuffer.Length; i++)
                {
                    Operation op = OperationBuffer[i];
                    switch (op.Type)
                    {
                        case OperationType.DisposeList:
                            List.DisposeList(op.ListIndex);
                            break;
                        case OperationType.ClearList:
                            List.ClearList(op.ListIndex);
                            break;
                        case OperationType.AddList:
                            List.AddList();
                            break;
                        case OperationType.AddToList:
                            List.AddToList(op.ListIndex, op.Val);
                            break;
                    }
                }
            }
        }
        static void ChangeData(List<List<int>> example, ListOfChunkedLists<int> examined)
        {
            for (int i = 0; i < example.Count; i++)
            {
                List<int> exampleList = example[i];
                ListOfChunkedLists<int>.RWEnumerator examinedList = examined.GetEnumerator(i);
                if (exampleList == null)
                    continue;

                int idx = 0;
                while(examinedList.MoveNext(out ListOfChunkedLists<int>.RWChunk chunk))
                {
                    for(int j = 0; j < chunk.Length; j++)
                    {
                        if (UnityEngine.Random.Range(0, 1f) <= 0.15f)
                        {
                            int newVal = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                            exampleList[idx] = newVal;
                            chunk[j] = newVal;
                        }
                        idx++;
                    }
                }
            }
        }
        static void TestResult(List<List<int>> example, ListOfChunkedLists<int> examined)
        {
            if(example.Count != examined.GetListCount())
            {
                UnityEngine.Debug.LogError($"Example list count ({example.Count}) and examined list count ({examined.GetListCount()}) does not match");
                return;
            }
            
            for(int listIdx = 0; listIdx < example.Count; listIdx++)
            {
                List<int> exampleList = example[listIdx];

                if(exampleList == null & examined.GetListLength(listIdx) != 0)
                {
                    UnityEngine.Debug.LogError($"Example inner list is null but examined inner list's length ({examined.GetListLength(listIdx)}) is not 0");
                    return;
                }

                if(exampleList != null && exampleList.Count != examined.GetListLength(listIdx))
                {
                    UnityEngine.Debug.LogError($"Example inner list length ({exampleList.Count}) does not match examined inner list's length ({examined.GetListLength(listIdx)})");
                    return;
                }
                ListOfChunkedLists<int>.RWEnumerator examinedEnumerator = examined.GetEnumerator(listIdx);

                int exampleListIdx = 0;
                while(examinedEnumerator.MoveNext(out ListOfChunkedLists<int>.RWChunk chunk))
                {
                    if(exampleList == null)
                    {
                        UnityEngine.Debug.LogError($"Example inner list is null but examined inner list enumerator enumerates");
                        return;
                    }
                    for(int i = 0; i < chunk.Length; i++)
                    {
                        int exampleData = exampleList[exampleListIdx];
                        int examinedData = chunk[i];
                        if(exampleData != examinedData)
                        {
                            UnityEngine.Debug.LogError($"Example inner list data does not match examined inner list data");
                            return;
                        }
                        exampleListIdx++;
                    }
                }
                int exampleListSize = 0;
                if(exampleList != null) exampleListSize = exampleList.Count;

                if(exampleListSize != exampleListIdx)
                {
                    UnityEngine.Debug.LogError($"Example inner list is not completely traversed");
                    return;
                }
            }
        }

        struct Operation
        {
            internal OperationType Type;
            internal int Val;
            internal int ListIndex;
        }
        enum OperationType
        {
            DisposeList,
            ClearList,
            AddList,
            AddToList,
        }
    }
}
