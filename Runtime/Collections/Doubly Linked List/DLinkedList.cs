using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct DLinkedList<T> where T : unmanaged
    {
        public struct Node
        {
            internal int Next;
            internal int Prev;
            internal T Data;
        }
        internal struct HeadAndTail
        {
            internal int Head;
            internal int Tail;
        }
        public const int INVALID_NODE_INDEX = -1;

        NativeReference<HeadAndTail> _headAndTail;
        NativeList<Node> _nodePool;
        NativeList<int> _freeNodeIndicies;

        public DLinkedList(Allocator allocator)
        {
            _headAndTail = new NativeReference<HeadAndTail>(new HeadAndTail { Head = INVALID_NODE_INDEX, Tail = INVALID_NODE_INDEX }, allocator);
            _nodePool = new NativeList<Node>(allocator);
            _freeNodeIndicies = new NativeList<int>(allocator);
        }
        public void Dispose()
        {
            _headAndTail.Dispose();
            _nodePool.Dispose();
            _freeNodeIndicies.Dispose();
        }
        public bool GetFirst(out int head)
        {
            head = _headAndTail.Value.Head;
            return head != INVALID_NODE_INDEX;
        }
        public bool GetLast(out int tail)
        {
            tail = _headAndTail.Value.Tail;
            return tail != INVALID_NODE_INDEX;
        }
        public int AddLast(T data)
        {
            HeadAndTail ht = _headAndTail.Value;
            int nodeIndex = AllocNewNode();
            Node node = new Node { Data = data, Next = INVALID_NODE_INDEX };

            if(ht.Tail == INVALID_NODE_INDEX)
            {
                node.Prev = INVALID_NODE_INDEX;
                ht.Tail = nodeIndex;
                ht.Head = nodeIndex;
            }
            else
            {
                Node tailNode = _nodePool[ht.Tail];
                tailNode.Next = nodeIndex;
                _nodePool[ht.Tail] = tailNode;

                node.Prev = ht.Tail;
                ht.Tail = nodeIndex;
            }
            _headAndTail.Value = ht;
            _nodePool[nodeIndex] = node;
            return nodeIndex;
        }
        public int AddBeforeUnchecked(int originNodeIndex, T insertedData)
        {
            HeadAndTail ht = _headAndTail.Value;
            Node originNode = _nodePool[originNodeIndex];

            int newNodeIndex = AllocNewNode();
            Node newNode = new Node { Data = insertedData, Prev = originNode.Prev, Next = originNodeIndex };
            _nodePool[newNodeIndex] = newNode;

            if (originNodeIndex == ht.Head)
            {
                ht.Head = newNodeIndex;
                _headAndTail.Value = ht;
            }
            if(originNode.Prev != INVALID_NODE_INDEX)
            {
                Node prevOfOriginal = _nodePool[originNode.Prev];
                prevOfOriginal.Next = newNodeIndex;
                _nodePool[originNode.Prev] = prevOfOriginal;
            }
            originNode.Prev = newNodeIndex;
            _nodePool[originNodeIndex] = originNode;
            return newNodeIndex;
        }

        //Removes node without checking if it is already being removed
        //Can implement a checked version. If bot next and prev are invalid, it means the node is either removed or only node in the list.
        //If it is only node in the list, it must also be both head and tail. If not, it is an already removed node
        public void RemoveUnchecked(int nodeIndex)
        {
            Node node = _nodePool[nodeIndex];
            HeadAndTail ht = _headAndTail.Value;

            ht.Head = math.select(ht.Head, node.Next, ht.Head == nodeIndex);
            ht.Tail = math.select(ht.Tail, node.Prev, ht.Tail == nodeIndex);
            _headAndTail.Value = ht;

            if(node.Next != INVALID_NODE_INDEX)
            {
                Node nextNode = _nodePool[node.Next];
                nextNode.Prev = node.Prev;
                _nodePool[node.Next] = nextNode;
            }
            if(node.Prev != INVALID_NODE_INDEX)
            {
                Node prevNode = _nodePool[node.Prev];
                prevNode.Next = node.Next;
                _nodePool[node.Prev] = prevNode;
            }
            node.Next = INVALID_NODE_INDEX;
            node.Prev = INVALID_NODE_INDEX;
            _nodePool[nodeIndex] = node;
            _freeNodeIndicies.Add(nodeIndex);
        }
        public T GetNodeData(int nodeIndex)
        {
            return _nodePool[nodeIndex].Data;
        }
        public void SetNodeData(int nodeIndex, T data)
        {
            Node node = _nodePool[nodeIndex];
            node.Data = data;
            _nodePool[nodeIndex] = node;
        }
        public bool TryGetNext(int nodeIndex, out int nextNodeIndex)
        {
            nextNodeIndex = _nodePool[nodeIndex].Next;
            return nextNodeIndex != INVALID_NODE_INDEX;
        }
        public bool TryGetNext(int nodeIndex, out T nextData)
        {
            nextData = default;

            int nextNodeIndex = _nodePool[nodeIndex].Next;
            if (nextNodeIndex == INVALID_NODE_INDEX) return false;

            nextData = _nodePool[nextNodeIndex].Data;
            return true;
        }
        public bool TryGetPrev(int nodeIndex, out int prevNodeIndex)
        {
            prevNodeIndex = _nodePool[nodeIndex].Prev;
            return prevNodeIndex != INVALID_NODE_INDEX;
        }
        public bool TryGetPrev(int nodeIndex, out T prevData)
        {
            prevData = default;

            int prevNodeIndex = _nodePool[nodeIndex].Prev;
            if (prevNodeIndex == INVALID_NODE_INDEX) return false;

            prevData = _nodePool[prevNodeIndex].Data;
            return true;
        }
        public void ToNativeList(NativeList<T> listOut)
        {
            int cur = _headAndTail.Value.Head;

            while(cur != INVALID_NODE_INDEX)
            {
                Node node = _nodePool[cur];
                listOut.Add(node.Data);
                cur = node.Next;
            }
        }
        public void ToNativeListReverse(NativeList<T> listOut)
        {
            int cur = _headAndTail.Value.Tail;

            while (cur != INVALID_NODE_INDEX)
            {
                Node node = _nodePool[cur];
                listOut.Add(node.Data);
                cur = node.Prev;
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
    }
}
