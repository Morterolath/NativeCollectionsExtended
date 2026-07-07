using NativeCollectionsExtended;
using Unity.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Mathematics;

namespace NativeCollectionsExtended.UnitTest
{
    internal class ListOfAppendOnlyUnrolledLinkedListsUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool Log;
        public int2 BlockSize;
        public int2 OperationCount;
        public int2 ListAddPercentage;
        public int2 MaxListCnt;
        private void Update()
        {
            OperationCount = math.clamp(OperationCount, 0, 10000000);
            OperationCount.x = math.min(OperationCount.x, OperationCount.y);

            BlockSize = math.clamp(BlockSize, 0, 300);
            BlockSize.x = math.min(BlockSize.x, BlockSize.y);

            ListAddPercentage = math.clamp(ListAddPercentage, 0, 100);
            ListAddPercentage.x = math.min(ListAddPercentage.x, ListAddPercentage.y);

            MaxListCnt = math.clamp(MaxListCnt, 0, 1000000);
            MaxListCnt.x = math.min(MaxListCnt.x, MaxListCnt.y);

            int blockSize = UnityEngine.Random.Range(BlockSize.x, BlockSize.y);
            int listAddPercentage = UnityEngine.Random.Range(ListAddPercentage.x, ListAddPercentage.y);
            int operationCount = UnityEngine.Random.Range(OperationCount.x, OperationCount.y);
            int maxListCnt = UnityEngine.Random.Range(MaxListCnt.x, MaxListCnt.y);

            List<NativeList<int>> exampleCollection = new List<NativeList<int>>();
            ListOfAppendOnlyUnrolledLinkedLists<int> testedCollection = new ListOfAppendOnlyUnrolledLinkedLists<int>(Allocator.Temp, blockSize);

            int listAddedCount = 0;
            int appendCount = 0;
            for(int i = 0; i < operationCount; i++)
            {
                bool addList = (UnityEngine.Random.Range(0, 100) < listAddPercentage | exampleCollection.Count == 0)
                    & exampleCollection.Count < maxListCnt;
                if (addList)
                {
                    exampleCollection.Add(new NativeList<int>(Allocator.Temp));
                    testedCollection.AppendList();
                    listAddedCount++;
                }
                else
                {
                    int listIndex = UnityEngine.Random.Range(0, testedCollection.Count);
                    int val = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    exampleCollection[listIndex].Add(val);
                    testedCollection.Append(listIndex, val);
                    appendCount++;
                }
            }

            if(exampleCollection.Count != testedCollection.Count)
            {
                UnityEngine.Debug.LogError("List counts do not match");
                return;
            }

            NativeList<int> tempDataBuffer = new NativeList<int>(Allocator.Temp);
            for(int i = 0; i <  testedCollection.Count; i++)
            {
                NativeArray<int> exampleData = exampleCollection[i].AsArray();
                ListOfAppendOnlyUnrolledLinkedLists<int>.Enumerator enumerator = testedCollection.GetEnumerator(i);

                tempDataBuffer.Clear();
                while(enumerator.MoveNext(out NativeSliceReadOnly<int> block))
                {
                    block.CopyTo(tempDataBuffer);
                }
                NativeArray<int> tempData_array = tempDataBuffer.AsArray();
                if(tempData_array.Length != exampleData.Length)
                {
                    UnityEngine.Debug.LogError($"List lengths do not match: {tempData_array.Length} , {exampleData.Length}");
                    return;
                }
                for(int j = 0; j < tempData_array.Length; j++)
                {
                    if (tempData_array[j] != exampleData[j])
                    {
                        UnityEngine.Debug.LogError("List content does not match");
                        return;
                    }
                }
            }

            if (!Log)
                return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Max List Cnt: {maxListCnt}");
            sb.AppendLine($"List added: {listAddedCount}");
            sb.AppendLine($"Data added: {appendCount}");
            sb.AppendLine($"Block Size: {testedCollection.BlockSize}");
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
