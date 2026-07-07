using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine;
using NativeCollectionsExtended;
using System.Text;

namespace NativeCollectionsExtended.UnitTest
{
    internal class ListOfDLinkedListsUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool Info;
        public int MinLinkedListCount;
        public int MaxLinkedListCount;
        public int MinAddCount;
        public int MaxAddCount;
        public int MinRemoveLastCount;
        public int MaxRemoveLastCount;
        public int MinRemoveCount;
        public int MaxRemoveCount;
        public int MinRemoveAllListCount;
        public int MaxRemoveAllListCount;
        private void Update()
        {
            if (!Run) return;
            Test(MinLinkedListCount, MaxLinkedListCount, MinAddCount, MaxAddCount, MinRemoveLastCount, MaxRemoveLastCount,
                MinRemoveCount, MaxRemoveCount, MinRemoveAllListCount, MaxRemoveAllListCount, Info);
        }

        static void Test(int minLinkedListCount, int maxLinkedListCount, int minAddCount, int maxAddCount, int minRemoveLastCount, int maxRemoveLastCount,
            int minRemoveCount, int maxRemoveCount, int minRemoveAllListCount, int maxRemoveAllListCount, bool info)
        {
            StringBuilder sb = new StringBuilder();
            ListOfDLinkedLists<int> nativeLists = new ListOfDLinkedLists<int>(Allocator.Temp);
            List<LinkedList<int>> lists = new List<LinkedList<int>>();
            List<List<int>> nativeNodeEachList = new List<List<int>>();
            List<List<LinkedListNode<int>>> nodeEachList = new List<List<LinkedListNode<int>>>();
            AddLinkedList(minLinkedListCount, maxLinkedListCount, nativeLists, lists, nativeNodeEachList, nodeEachList, out int addedLinkedListCount_1);
            AddLast(minAddCount, maxAddCount, nativeLists, lists, nativeNodeEachList, nodeEachList, out int addLastCount_1);
            Remove(minRemoveCount, maxRemoveCount, nativeLists, lists, nativeNodeEachList, nodeEachList, out int removeCount_1);
            RemoveAll(minRemoveAllListCount, maxRemoveAllListCount, nativeLists, lists, nativeNodeEachList, nodeEachList);
            AddLinkedList(minLinkedListCount, maxLinkedListCount, nativeLists, lists, nativeNodeEachList, nodeEachList, out int addedLinkedListCount_2);
            AddLast(minAddCount, maxAddCount, nativeLists, lists, nativeNodeEachList, nodeEachList, out int addLastCount_2);
            Remove(minRemoveCount, maxRemoveCount, nativeLists, lists, nativeNodeEachList, nodeEachList, out int removeCount_2);
            AddLast(minAddCount, maxAddCount, nativeLists, lists, nativeNodeEachList, nodeEachList, out int addLastCount_3);

            bool success = CheckContent(lists, nativeLists, sb);
            sb.AppendLine($"{addedLinkedListCount_1} linked lists created");
            sb.AppendLine($"{addLastCount_1} nodes added to the tail");
            sb.AppendLine($"{removeCount_1} nodes are removed");
            sb.AppendLine("all nodes are removed");
            sb.AppendLine($"{addedLinkedListCount_2} linked lists created");
            sb.AppendLine($"{addLastCount_2} nodes added to the tail");
            sb.AppendLine($"{removeCount_2} nodes are removed");
            sb.AppendLine($"{addLastCount_3} nodes added to the tail");
            if (!success | info) UnityEngine.Debug.Log(sb);
        }
        static void AddLinkedList(int minLinkedListCount, int maxLinkedListCount, ListOfDLinkedLists<int> nativeLists, List<LinkedList<int>> lists,
             List<List<int>> nativeNodeEachList, List<List<LinkedListNode<int>>> nodeEachList, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minLinkedListCount, maxLinkedListCount);
            for(int i = 0; i < count; i++)
            {
                nativeLists.AddList();
                lists.Add(new LinkedList<int>());
                nativeNodeEachList.Add(new List<int>());
                nodeEachList.Add(new List<LinkedListNode<int>>());
                opCount++;
            }
        }
        static void AddLast(int minAddCount, int maxAddCount, ListOfDLinkedLists<int> nativeLists, List<LinkedList<int>> lists,
            List<List<int>> nativeNodeEachList, List<List<LinkedListNode<int>>> nodeEachList, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minAddCount, maxAddCount);
            for(int i = 0; i < count & lists.Count != 0; i++)
            {
                int listIndexToAdd = UnityEngine.Random.Range(0, lists.Count);
                int data = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                nodeEachList[listIndexToAdd].Add(lists[listIndexToAdd].AddLast(data));
                nativeNodeEachList[listIndexToAdd].Add(nativeLists.AddLast(listIndexToAdd, data));
                opCount++;
            }
        }
        static void RemoveLast(int minRemoveLastCount, int maxRemoveLastCount, ListOfDLinkedLists<int> nativeLists, List<LinkedList<int>> lists,
            List<List<int>> nativeNodeEachList, List<List<LinkedListNode<int>>> nodeEachList, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minRemoveLastCount, maxRemoveLastCount);
            for(int i = 0; i < count & lists.Count != 0; i++)
            {
                int listIndex = UnityEngine.Random.Range(0, lists.Count);
            }
        }
        static void Remove(int minRemoveCount, int maxRemoveCount, ListOfDLinkedLists<int> nativeLists, List<LinkedList<int>> lists,
            List<List<int>> nativeNodeEachList, List<List<LinkedListNode<int>>> nodeEachList, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minRemoveCount, maxRemoveCount);
            for(int i = 0; i < count & lists.Count != 0; i++)
            {
                int listIndex = UnityEngine.Random.Range(0, lists.Count);
                if (nodeEachList[listIndex].Count == 0) continue;
                int nodeIndex = UnityEngine.Random.Range(0, nodeEachList[listIndex].Count);
                lists[listIndex].Remove(nodeEachList[listIndex][nodeIndex]);
                nativeLists.RemoveUnchecked(listIndex, nativeNodeEachList[listIndex][nodeIndex]);
                nodeEachList[listIndex].RemoveAtSwapBack(nodeIndex);
                nativeNodeEachList[listIndex].RemoveAtSwapBack(nodeIndex);
                opCount++;
            }
        }
        static void RemoveAll(int minRemoveAllListCount, int maxRemoveAllListCount, ListOfDLinkedLists<int> nativeLists, List<LinkedList<int>> lists,
            List<List<int>> nativeNodeEachList, List<List<LinkedListNode<int>>> nodeEachList)
        {
            int count = UnityEngine.Random.Range(minRemoveAllListCount, maxRemoveAllListCount);
            for(int i = 0; i < count & lists.Count != 0; i++)
            {
                int listIndex = UnityEngine.Random.Range(0, lists.Count);
                List<LinkedListNode<int>> listNodes = nodeEachList[listIndex];
                List<int> nativeListNodes = nativeNodeEachList[listIndex];
                LinkedList<int> list = lists[listIndex];
                for(int j = 0; j < listNodes.Count; j++)
                {
                    list.Remove(listNodes[j]);
                    nativeLists.RemoveUnchecked(listIndex, nativeListNodes[j]);
                }
                listNodes.Clear();
                nativeListNodes.Clear();
            }
        }
        static void RemoveHeadAndTail()
        {

        }
        static bool CheckContent(List<LinkedList<int>> lists, ListOfDLinkedLists<int> nativeLists, StringBuilder sb)
        {
            List<List<int>> listsContent = new List<List<int>>();
            List<List<int>> nativeListsContent = new List<List<int>>();
            List<List<int>> nativeListsContentReverse = new List<List<int>>();

            //Get content
            for (int i = 0; i < lists.Count; i++)
            {
                List<int> listContent = new List<int>();
                LinkedList<int>.Enumerator listEnumerator = lists[i].GetEnumerator();
                while (listEnumerator.MoveNext())
                {
                    listContent.Add(listEnumerator.Current);
                }
                listsContent.Add(listContent);
            }
            for (int i = 0; i < nativeLists.ListCount(); i++)
            {
                nativeListsContent.Add(new List<int>());
                nativeListsContentReverse.Add(new List<int>());
            }
            nativeLists.ToList(nativeListsContent);
            nativeLists.ToListReverse(nativeListsContentReverse);

            //Check content

            if(listsContent.Count != nativeListsContent.Count)
            {
                sb.AppendLine("linked list count does not match the expected");
                return false;
            }

            for(int i = 0; i < listsContent.Count; i++)
            {
                List<int> listContent = listsContent[i];
                List<int> nativeListContent = nativeListsContent[i];
                List<int> nativeListContentReverse = nativeListsContentReverse[i];
                if(listContent.Count != nativeListContent.Count)
                {
                    sb.AppendLine("head to tail linked list length does not match the expected");
                    return false;
                }
                if(listContent.Count != nativeListContentReverse.Count)
                {
                    sb.AppendLine("tail to head linked list length does not match the expected");
                    return false;
                }

                for(int j = 0; j < listContent.Count; j++)
                {
                    if (listContent[j] != nativeListContent[j])
                    {
                        sb.AppendLine("head to tail linked list content does not match the expected");
                        return false;
                    }
                }
                for(int j = 0; j < listContent.Count; j++)
                {
                    if (listContent[j] != nativeListContentReverse[nativeListContentReverse.Count - 1 - j])
                    {
                        sb.AppendLine("tail to head linked list content does not match the expected");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
