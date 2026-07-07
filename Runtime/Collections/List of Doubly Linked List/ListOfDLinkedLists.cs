using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

namespace NativeCollectionsExtended
{
    public struct ListOfDLinkedLists<T> where T : unmanaged
    {
        public struct Node
        {
            internal T Data;
            internal int Next;
            internal int Prev;
        }
        internal struct HeadAndTail
        {
            internal int Head;
            internal int Tail;
        }
        public const int INVALID_INDEX = -1;
        static readonly Node INVALID_NODE = new Node { Data = default, Next = INVALID_INDEX, Prev = INVALID_INDEX };

        NativeList<HeadAndTail> _headAndTailEachList;
        NativeList<Node> _nodePool;
        NativeList<int> _freeNodeIndicies;

        public ListOfDLinkedLists(Allocator allocator)
        {
            _headAndTailEachList = new NativeList<HeadAndTail>(allocator);
            _nodePool = new NativeList<Node>(allocator);
            _freeNodeIndicies = new NativeList<int>(allocator);
        }
        public void Dispose()
        {
            _headAndTailEachList.Dispose();
            _nodePool.Dispose();
            _freeNodeIndicies.Dispose();
        }
        public void AddList()
        {
            _headAndTailEachList.Add(new HeadAndTail { Head = INVALID_INDEX, Tail = INVALID_INDEX });
        }
        public int ListCount()
        {
            return _headAndTailEachList.Length;
        }
        public int AddLast(int linkedListIndex, T data)
        {
            Node newNode = new Node { Data = data, Next = INVALID_INDEX, Prev = INVALID_INDEX };
            int newNodeIndex = AllocNewNode();

            HeadAndTail ht = _headAndTailEachList[linkedListIndex];
            if(ht.Tail == INVALID_INDEX)
            {
                ht.Head = newNodeIndex;
                ht.Tail = newNodeIndex;
                _headAndTailEachList[linkedListIndex] = ht;
                _nodePool[newNodeIndex] = newNode;
            }
            else
            {
                Node tailNode = _nodePool[ht.Tail];
                tailNode.Next = newNodeIndex;
                _nodePool[ht.Tail] = tailNode;

                newNode.Prev = ht.Tail;
                _nodePool[newNodeIndex] = newNode;

                ht.Tail = newNodeIndex;
                _headAndTailEachList[linkedListIndex] = ht;
            }
            return newNodeIndex;
        }
        public bool TryRemoveLast(int linkedListIndex, out T last)
        {
            last = default;

            HeadAndTail ht = _headAndTailEachList[linkedListIndex];
            if (ht.Tail == INVALID_INDEX) return false;

            if(ht.Tail == ht.Head)
            {
                last = _nodePool[ht.Tail].Data;
                _nodePool[ht.Tail] = INVALID_NODE;
                _freeNodeIndicies.Add(ht.Tail);

                ht.Tail = INVALID_INDEX;
                ht.Head = INVALID_INDEX;
                _headAndTailEachList[linkedListIndex] = ht;
            }
            else
            {
                _freeNodeIndicies.Add(ht.Tail);
                Node tailNode = _nodePool[ht.Tail];
                _nodePool[ht.Tail] = INVALID_NODE;
                last = tailNode.Data;

                Node prevNode = _nodePool[tailNode.Prev];
                prevNode.Next = INVALID_INDEX;
                _nodePool[tailNode.Prev] = prevNode;

                ht.Tail = tailNode.Prev;
                _headAndTailEachList[linkedListIndex] = ht;
            }
            return true;
        }
        //There is a problem: If node is head or tail of linked list, but input LinkedListIndex is worng, that just corrupts the node pool.
        //For checked version, I can check if node index is really tail or head of the given list:
        //bool isWongList = (node.next == invalid & ht.tail != nodeIndex) | (node.prev == invalid & ht.head != nodeIndex)
        public void RemoveUnchecked(int linkedListIndex, int nodeIndex)
        {
            Node node = _nodePool[nodeIndex];
            _nodePool[nodeIndex] = INVALID_NODE;

            HeadAndTail ht = _headAndTailEachList[linkedListIndex];
            ht.Head = math.select(ht.Head, node.Next, ht.Head == nodeIndex);
            ht.Tail = math.select(ht.Tail, node.Prev, ht.Tail == nodeIndex);
            _headAndTailEachList[linkedListIndex] = ht;

            if(node.Next != INVALID_INDEX)
            {
                Node nextNode = _nodePool[node.Next];
                nextNode.Prev = node.Prev;
                _nodePool[node.Next] = nextNode;
            }
            if(node.Prev != INVALID_INDEX)
            {
                Node prevNode = _nodePool[node.Prev];
                prevNode.Next = node.Next;
                _nodePool[node.Prev] = prevNode;
            }
            _freeNodeIndicies.Add(nodeIndex);
        }
        public void ToList(List<List<T>> lists)
        {
            for(int i = 0; i < _headAndTailEachList.Length; i++)
            {
                List<T> list = lists[i];
                int head = _headAndTailEachList[i].Head;
                int cur = head;
                while(cur != INVALID_INDEX)
                {
                    list.Add(_nodePool[cur].Data);
                    cur = _nodePool[cur].Next;
                }
            }
        }
        public void ToListReverse(List<List<T>> lists)
        {
            for(int i = 0; i < _headAndTailEachList.Length; i++)
            {
                List<T> list = lists[i];
                int tail = _headAndTailEachList[i].Tail;
                int cur = tail;
                while(cur != INVALID_INDEX)
                {
                    list.Add(_nodePool[cur].Data);
                    cur = _nodePool[cur].Prev;
                }
            }
        }
        int AllocNewNode()
        {
            if (_freeNodeIndicies.IsEmpty)
            {
                _nodePool.Length++;
                return _nodePool.Length - 1;
            }
            int nodeIndex = _freeNodeIndicies[_freeNodeIndicies.Length - 1];
            _freeNodeIndicies.Length--;
            return nodeIndex;
        }

        //try remove last - also returns data
        //add last
        //remove
    }
}
