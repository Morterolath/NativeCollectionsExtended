using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    internal struct NativeLinkedList<T> where T : unmanaged
    {
        internal const int NULL_PTR = -1;
        NativeList<Node<T>> _mem;
        NativeList<int> _freeMemIndicies;
        NativeReference<int> _head;
        NativeReference<int> _tail;
        internal NativeLinkedList(Allocator allocator)
        {
            _mem = new NativeList<Node<T>>(allocator);
            _freeMemIndicies = new NativeList<int>(allocator);
            _head = new NativeReference<int>(NULL_PTR, allocator);
            _tail = new NativeReference<int>(NULL_PTR, allocator);
        }
        internal void Dispose()
        {
            _mem.Dispose();
            _freeMemIndicies.Dispose();
            _head.Dispose();
            _tail.Dispose();
        }
        internal int AddToTail(T data)
        {
            int tail = _tail.Value;
            int newNodeIndex = AllocateNewNode();
            if (IsEmpty())
            {
                _head.Value = newNodeIndex;
                _tail.Value = newNodeIndex;
                _mem[newNodeIndex] = new Node<T>()
                {
                    Data = data,
                    Next = NULL_PTR,
                    Previous = NULL_PTR,
                };
                return newNodeIndex;
            }
            Node<T> tailNode = _mem[tail];
            tailNode.Next = newNodeIndex;
            _mem[tail] = tailNode;

            _mem[newNodeIndex] = new Node<T>()
            {
                Data = data,
                Next = NULL_PTR,
                Previous = tail,
            };
            _tail.Value = newNodeIndex;
            return newNodeIndex;
        }
        internal int AddToHead(T data)
        {
            int head = _head.Value;
            int newNodeIndex = AllocateNewNode();
            if (IsEmpty())
            {
                _head.Value = newNodeIndex;
                _tail.Value = newNodeIndex;
                _mem[newNodeIndex] = new Node<T>()
                {
                    Data = data,
                    Next = NULL_PTR,
                    Previous = NULL_PTR,
                };
                return newNodeIndex;
            }
            Node<T> headNode = _mem[head];
            headNode.Previous = newNodeIndex;
            _mem[head] = headNode;

            _mem[newNodeIndex] = new Node<T>()
            {
                Data = data,
                Next = head,
                Previous = NULL_PTR,
            };
            _head.Value = newNodeIndex;
            return newNodeIndex;
        }
        internal bool TryRemove(int index, FreedMemoryArgument freedMemoryArgument)
        {
            Node<T> node = _mem[index];
            if (node.IsNull()) { return false; }
            _mem[index] = Node<T>.NULL;
            bool isTail = node.Next == NULL_PTR;
            bool isHead = node.Previous == NULL_PTR;
            if (isHead && isTail)
            {
                _tail.Value = NULL_PTR;
                _head.Value = NULL_PTR;
            }
            else if (isHead)
            {
                Node<T> nextNode = _mem[node.Next];
                nextNode.Previous = NULL_PTR;
                _mem[node.Next] = nextNode;
                _head.Value = node.Next;
            }
            else if (isTail)
            {
                Node<T> prevNode = _mem[node.Previous];
                prevNode.Next = NULL_PTR;
                _mem[node.Previous] = prevNode;
                _tail.Value = node.Previous;
            }
            else
            {
                Node<T> nextNode = _mem[node.Next];
                nextNode.Previous = node.Previous;
                _mem[node.Next] = nextNode;

                Node<T> prevNode = _mem[node.Previous];
                prevNode.Next = node.Next;
                _mem[node.Previous] = prevNode;
            }

            if(freedMemoryArgument == FreedMemoryArgument.ReuseFreedMemroy)
            {
                _freeMemIndicies.Add(index);
            }
            return true;
        }
        internal bool FreeMemoryIfNull(int index)
        {
            Node<T> node = _mem[index];
            if (node.IsNull()) { _freeMemIndicies.Add(index); return true; }
            return false;
        }
        internal void SetData(int index, T data)
        {
            Node<T> node = _mem[index];
            if (node.IsNull()) { return; }
            node.Data = data;
            _mem[index] = node;
        }
        internal T GetData(int index)
        {
            return _mem[index].Data;
        }
        internal int InsertNext(int index, T data)
        {
            Node<T> curNode = _mem[index];
            bool isTail = curNode.Next == NULL_PTR;
            if (isTail) { return AddToTail(data); }
            int newNodeIndex = AllocateNewNode();
            Node<T> newNode = new Node<T>()
            {
                Data = data,
                Next = curNode.Next,
                Previous = index,
            };
            _mem[newNodeIndex] = newNode;

            Node<T> nextNode = _mem[curNode.Next];
            nextNode.Previous = newNodeIndex;
            _mem[curNode.Next] = nextNode;

            curNode.Next = newNodeIndex;
            _mem[index] = curNode;

            return newNodeIndex;
        }
        internal int InsertPrevious(int index, T data)
        {
            Node<T> curNode = _mem[index];
            bool isHead = curNode.Previous == NULL_PTR;
            if (isHead) { return AddToHead(data); }
            int newNodeIndex = AllocateNewNode();
            Node<T> newNode = new Node<T>()
            {
                Data = data,
                Next = index,
                Previous = curNode.Previous,
            };
            _mem[newNodeIndex] = newNode;

            Node<T> prevNode = _mem[curNode.Previous];
            prevNode.Next = newNodeIndex;
            _mem[curNode.Previous] = prevNode;

            curNode.Previous = newNodeIndex;
            _mem[index] = curNode;

            return newNodeIndex;
        }
        internal bool TryGetHeadIndex(out int index)
        {
            index = _head.Value;
            return index != NULL_PTR;
        }
        internal bool TryGetPreviousIndex(int index, out int previousIndex)
        {
            Node<T> node = _mem[index];
            previousIndex = node.Previous;
            return node.Previous != NULL_PTR;
        }
        internal bool TryGetNextIndex(int index, out int nextIndex)
        {
            Node<T> node = _mem[index];
            nextIndex = node.Next;
            return node.Next != NULL_PTR;
        }
        internal bool IsEmpty()
        {
            return _head.Value == NULL_PTR;
        }
        int AllocateNewNode()
        {
            if (_freeMemIndicies.IsEmpty)
            {
                int allocated = _mem.Length;
                _mem.Length++;
                return allocated;
            }
            else
            {
                int allocated = _freeMemIndicies[_freeMemIndicies.Length - 1];
                _freeMemIndicies.Length--;
                return allocated;
            }
        }
        internal NativeArray<T> ToArray(Allocator allocator)
        {
            NativeList<T> array = new NativeList<T>(allocator);
            int head = _head.Value;
            int curIndex = head;
            while(curIndex != NULL_PTR)
            {
                Node<T> curNode = _mem[curIndex];
                array.Add(curNode.Data);
                curIndex = curNode.Next;
            }
            return array.AsArray();
        }
    }
    internal enum FreedMemoryArgument : byte
    {
        ReuseFreedMemroy = 0,
        DoNotReuseFreedMemory = 1,
    }
    struct Node<T> where T : unmanaged
    {
        internal static Node<T> NULL { get { return default(Node<T>); } }
        internal T Data;
        internal int Next;
        internal int Previous;

        internal bool IsNull()
        {
            return Next == default(int) && Previous == default(int);
        }
    }
}
