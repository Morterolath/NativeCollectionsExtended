using System.Collections.Generic;
using UnityEngine;
using NativeCollectionsExtended;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using System.Text;
using System.Text.RegularExpressions;

namespace NativeCollectionsExtended.UnitTest
{
    internal class LinkedListUntTest : MonoBehaviour
    {
        public bool Run;
        public bool SeeInfo;
        public int MinAddToTailCount;
        public int MaxAddToTailCount;
        public int MinInsertBeforeCount;
        public int MaxInsertBeforeCount;
        public int MinRemoveCount;
        public int MaxRemoveCount;
        public int MinSetNextDataCount;
        public int MaxSetNextDataCount;
        public int MinSetPrevDataCount;
        public int MaxSetPrevDataCount;
        private void Update()
        {
            if (!Run) return;
            Test(SeeInfo, MinAddToTailCount, MaxAddToTailCount, MinInsertBeforeCount, MaxInsertBeforeCount, MinRemoveCount, MaxRemoveCount, 
                MinSetNextDataCount, MaxSetNextDataCount, MinSetPrevDataCount, MaxSetPrevDataCount);
        }
        static void Test(bool seeInfo, int minAddToTailCount, int maxAddToTailCount, int minInsertBeforeCount, int maxInsertBeforeCount,
            int minRemoveCount, int maxRemoveCount, int minSetNextDataCount, int maxSetNextDataCount, int minSetPrevDataCount,
            int maxSetPrevDataCount)
        {
            StringBuilder debugStr = new StringBuilder();

            LinkedList<int> linkedList = new LinkedList<int>();
            DLinkedList<int> nativeLinkedList = new DLinkedList<int>(Allocator.Temp);
            List<LinkedListNode<int>> nodes = new List<LinkedListNode<int>>();
            NativeList<int> nativeNodes = new NativeList<int>(Allocator.Temp);
            FillWithAddToTail(minAddToTailCount, maxAddToTailCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int addToTailCount_1);
            FillWithInsertBefore(minInsertBeforeCount, maxInsertBeforeCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int insertBeforeCount_1);
            RemoveSomeNodes(minRemoveCount, maxRemoveCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int removeCount_1);
            FillWithAddToTail(minAddToTailCount, maxAddToTailCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int addToTailCount_2);
            FillWithInsertBefore(minInsertBeforeCount, maxInsertBeforeCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int insertBeforeCount_2);
            RemoveAllNodes(linkedList, nativeLinkedList, nativeNodes, nodes);
            FillWithAddToTail(minAddToTailCount, maxAddToTailCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int addToTailCount_3);
            FillWithInsertBefore(minInsertBeforeCount, maxInsertBeforeCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int insertBeforeCount_3);
            SetSomeNextNodeData(minSetNextDataCount, maxSetNextDataCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int setNextDataCount_1);
            SetSomePrevNodeData(minSetPrevDataCount, maxSetPrevDataCount, nativeLinkedList, nativeNodes, nodes, out int setPrevDataCount_1);
            RemoveSomeNodes(minRemoveCount, maxRemoveCount, linkedList, nativeLinkedList, nativeNodes, nodes, out int removeCount_2);
            RemoveHeadAndTail(linkedList, nativeLinkedList, nodes);

            debugStr.AppendLine($"{addToTailCount_1} nodes are added to the tail");
            debugStr.AppendLine($"{insertBeforeCount_1} nodes are inserted before nodes");
            debugStr.AppendLine($"{removeCount_1} nodes are removed");
            debugStr.AppendLine($"{addToTailCount_2} nodes are added to the tail");
            debugStr.AppendLine($"{insertBeforeCount_2} nodes are inserted before nodes");
            debugStr.AppendLine("all nodes are removed");
            debugStr.AppendLine($"{addToTailCount_3} nodes are added to the tail");
            debugStr.AppendLine($"{insertBeforeCount_3} nodes are inserted before nodes");
            debugStr.AppendLine($"{setNextDataCount_1} nodes' data are changed");
            debugStr.AppendLine($"{setPrevDataCount_1} nodes' data are changed");
            debugStr.AppendLine($"{removeCount_2} nodes are removed");
            debugStr.AppendLine("head and tail are removed");
            bool succesfull = CheckContent(linkedList, nativeLinkedList, debugStr);
            if(!succesfull | seeInfo) UnityEngine.Debug.Log(debugStr);
        }

        static void FillWithAddToTail(int minAddToTailCount, int maxAddToTailCount, LinkedList<int> linkedList, DLinkedList<int> nativeLinkedList, 
            NativeList<int> nativeNodes, List<LinkedListNode<int>> nodes, out int opCount)
        {
            int count = UnityEngine.Random.Range(minAddToTailCount, maxAddToTailCount);
            opCount = 0;
            for(int i = 0; i < count; i++)
            {
                int data = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                nodes.Add(linkedList.AddLast(data));
                nativeNodes.Add(nativeLinkedList.AddLast(data));
                opCount++;
            }
        }
        static void FillWithInsertBefore(int minInsertBeforeCount, int maxInsertBeforeCount, LinkedList<int> linkedList, DLinkedList<int> nativeLinkedList, 
            NativeList<int> nativeNodes, List<LinkedListNode<int>> nodes, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minInsertBeforeCount, maxInsertBeforeCount);
            for(int i = 0; i < count & nodes.Count != 0; i++)
            {
                int originNode = UnityEngine.Random.Range(0, nodes.Count);
                int data = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                nodes.Add(linkedList.AddBefore(nodes[originNode], data));
                nativeNodes.Add(nativeLinkedList.AddBeforeUnchecked(nativeNodes[originNode], data));
                opCount++;
            }
        }
        static void RemoveSomeNodes(int minRemoveCount, int maxRemoveCount, LinkedList<int> linkedList, DLinkedList<int> nativeLinkedList,
            NativeList<int> nativeNodes, List<LinkedListNode<int>> nodes, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minRemoveCount, maxRemoveCount);
            for(int i = 0; i < count & nodes.Count != 0; i++)
            {
                int nodeIndex = UnityEngine.Random.Range(0, nodes.Count);
                linkedList.Remove(nodes[nodeIndex]);
                nativeLinkedList.RemoveUnchecked(nativeNodes[nodeIndex]);
                nativeNodes.RemoveAtSwapBack(nodeIndex);
                nodes.RemoveAtSwapBack(nodeIndex);
                opCount++;
            }
        }
        static void RemoveAllNodes(LinkedList<int> linkedList, DLinkedList<int> nativeLinkedList,
            NativeList<int> nativeNodes, List<LinkedListNode<int>> nodes)
        {
            for(int i = 0; i < nodes.Count; i++)
            {
                linkedList.Remove(nodes[i]);
                nativeLinkedList.RemoveUnchecked(nativeNodes[i]);
            }
            nativeNodes.Clear();
            nodes.Clear();
        }
        static void SetSomeNextNodeData(int minSetNextDataCount, int maxSetNextDataCount, LinkedList<int> linkedList, DLinkedList<int> nativeLinkedList,
            NativeList<int> nativeNodes, List<LinkedListNode<int>> nodes, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minSetNextDataCount, maxSetNextDataCount);
            for(int i = 0; i < count & nodes.Count != 0; i++)
            {
                int originNodeIndex = UnityEngine.Random.Range(0, nodes.Count);
                int data = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

                LinkedListNode<int> nextNode = nodes[originNodeIndex].Next;
                if (nextNode != null) nextNode.Value = data;

                if(nativeLinkedList.TryGetNext(nativeNodes[originNodeIndex], out int nextNodeIndex))
                {
                    nativeLinkedList.SetNodeData(nextNodeIndex, data);
                }
                opCount++;
            }
        }
        static void SetSomePrevNodeData(int minSetPrevDataCount, int maxSetPrevDataCount, DLinkedList<int> nativeLinkedList,
            NativeList<int> nativeNodes, List<LinkedListNode<int>> nodes, out int opCount)
        {
            opCount = 0;
            int count = UnityEngine.Random.Range(minSetPrevDataCount, maxSetPrevDataCount);
            for (int i = 0; i < count & nodes.Count != 0; i++)
            {
                int originNodeIndex = UnityEngine.Random.Range(0, nodes.Count);
                int data = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

                LinkedListNode<int> nextNode = nodes[originNodeIndex].Previous;
                if (nextNode != null) nextNode.Value = data;

                if(nativeLinkedList.TryGetPrev(nativeNodes[originNodeIndex], out int prevNodeIndex))
                {
                    nativeLinkedList.SetNodeData(prevNodeIndex, data);
                }
                opCount++;
            }
        }
        static void RemoveHeadAndTail(LinkedList<int> linkedList, DLinkedList<int> nativeLinkedList,
            List<LinkedListNode<int>> nodes)
        {
            if (nodes.Count < 2) return;
            linkedList.RemoveFirst();
            linkedList.RemoveLast();
            nativeLinkedList.GetFirst(out int head);
            nativeLinkedList.GetLast(out int tail);
            nativeLinkedList.RemoveUnchecked(head);
            nativeLinkedList.RemoveUnchecked(tail);
        }
        static bool CheckContent(LinkedList<int> linkedList, DLinkedList<int> nativeLinkedList, StringBuilder stringBuilder)
        {
            bool success = true;
            NativeList<int> linkedListContent = new NativeList<int>(Allocator.Temp);
            NativeList<int> nativeLinkedListContent = new NativeList<int>(Allocator.Temp);
            NativeList<int> nativeLinkedListContentReverse = new NativeList<int>(Allocator.Temp);
            LinkedList<int>.Enumerator linkedListEnumerator = linkedList.GetEnumerator();
            while(linkedListEnumerator.MoveNext())
            {
                linkedListContent.Add(linkedListEnumerator.Current);
            }
            nativeLinkedList.ToNativeList(nativeLinkedListContent);
            nativeLinkedList.ToNativeListReverse(nativeLinkedListContentReverse);

            if(linkedListContent.Length != nativeLinkedListContent.Length)
            {
                stringBuilder.AppendLine($"linked list does not have expected size from head to tail. Expected: {linkedListContent.Length}, Result: {nativeLinkedListContent.Length}");
                success = false;
            }

            if(linkedListContent.Length != nativeLinkedListContentReverse.Length)
            {
                stringBuilder.AppendLine($"linked list does not have expected size from tail to head. Expected: {linkedListContent.Length}, Result: {nativeLinkedListContentReverse.Length}");
                success = false;
            }

            for(int i = 0; i < linkedListContent.Length; i++)
            {
                if (linkedListContent[i] != nativeLinkedListContent[i])
                {
                    stringBuilder.AppendLine("linked list content does not match");
                    success = false;
                    break;
                }
                if (linkedListContent[i] != nativeLinkedListContentReverse[nativeLinkedListContentReverse.Length - 1 - i])
                {
                    stringBuilder.AppendLine("linked list content does not match");
                    success = false;
                    break;
                }
            }

            stringBuilder.AppendLine();
            stringBuilder.Append("Expected Content: ");
            for (int i = 0; i < linkedListContent.Length; i++)
            {
                stringBuilder.Append(linkedListContent[i]);
                stringBuilder.Append(',');
                stringBuilder.Append(' ');
            }
            stringBuilder.AppendLine();
            stringBuilder.Append("Result: ");
            for (int i = 0; i < nativeLinkedListContent.Length; i++)
            {
                stringBuilder.Append(nativeLinkedListContent[i]);
                stringBuilder.Append(',');
                stringBuilder.Append(' ');
            }

            stringBuilder.AppendLine();
            stringBuilder.Append("Result Reverse: ");
            for (int i = 0; i < nativeLinkedListContentReverse.Length; i++)
            {
                stringBuilder.Append(nativeLinkedListContentReverse[i]);
                stringBuilder.Append(',');
                stringBuilder.Append(' ');
            }
            return success;
        }
    }
}
