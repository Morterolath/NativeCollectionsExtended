using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct ListOfAppendOnlyUnrolledLinkedLists<T>
        where T : unmanaged
    {
        internal NativeList<ListPtr> ListPtrBuffer;
        internal NativeList<int> NextBlockIdxEachBlock;
        internal NativeList<T> NodeBuffer;
        internal readonly int BlockSize;

        internal const int MIN_BLOCK_SIZE = 8;
        internal const int INVALID_BLOCK_IDX = -1;
        public ListOfAppendOnlyUnrolledLinkedLists(Allocator allocator, int blockSize)
        {
            blockSize = math.max(blockSize, MIN_BLOCK_SIZE);

            ListPtrBuffer = new NativeList<ListPtr>(allocator);
            NextBlockIdxEachBlock = new NativeList<int>(allocator);
            NodeBuffer = new NativeList<T>(allocator);
            BlockSize = blockSize;
        }
        public void Dispose()
        {
            ListPtrBuffer.Dispose();
            NextBlockIdxEachBlock.Dispose();
            NodeBuffer.Dispose();
        }
        public bool IsCreated => ListPtrBuffer.IsCreated;
        public int Count => ListPtrBuffer.Length;
        public void AppendList()
        {
            int blockIdx = NodeBuffer.Length / BlockSize;
            NodeBuffer.Length += BlockSize;
            NextBlockIdxEachBlock.Add(INVALID_BLOCK_IDX);
            ListPtrBuffer.Add(new ListPtr { FirstBlockIdx = blockIdx, LastBlockIdx = blockIdx, TailLocalIdx = -1 });
        }
        public void Clear()
        {
            ListPtrBuffer.Clear();
            NodeBuffer.Clear();
            NextBlockIdxEachBlock.Clear();
        }
        public void Append(int listIndex, T data)
        {
            ListPtr listPtr = ListPtrBuffer[listIndex];
            listPtr.TailLocalIdx++;
            if(listPtr.TailLocalIdx == BlockSize)
            {
                int blockIdx = NodeBuffer.Length / BlockSize;
                NodeBuffer.Length += BlockSize;
                NextBlockIdxEachBlock.Add(INVALID_BLOCK_IDX);
                NextBlockIdxEachBlock[listPtr.LastBlockIdx] = blockIdx;
                listPtr.LastBlockIdx = blockIdx;
                listPtr.TailLocalIdx = 0;
            }
            ListPtrBuffer[listIndex] = listPtr;
            NodeBuffer[listPtr.LastBlockIdx * BlockSize + listPtr.TailLocalIdx] = data;
        }
        public Enumerator GetEnumerator(int listIndex)
        {
            return new Enumerator(in this, listIndex);
        }
        public ROSpan AsROSpan()
        {
            return new ROSpan(BlockSize)
            {
                ListPtrBuffer = ListPtrBuffer.AsArray(),
                NextBlockIdxEachBlock = NextBlockIdxEachBlock.AsArray(),
                NodeBuffer = NodeBuffer.AsArray(),
            };
        }
        public ROSpan AsROSpanDeferred()
        {
            return new ROSpan(BlockSize)
            {
                ListPtrBuffer = ListPtrBuffer.AsDeferredJobArray(),
                NextBlockIdxEachBlock = NextBlockIdxEachBlock.AsDeferredJobArray(),
                NodeBuffer = NodeBuffer.AsDeferredJobArray(),
            };
        }
        internal struct ListPtr
        {
            internal int FirstBlockIdx;
            internal int LastBlockIdx;
            internal int TailLocalIdx;
        }
        public struct Enumerator
        {
            internal readonly NativeArray<int> NextBlockIdxEachBlock;
            internal readonly NativeArray<T> NodeBuffer;
            internal readonly int StartBlockIdx;
            internal readonly int EndBlockIdx;
            internal readonly int EndBlockLength;
            internal readonly int BlockSize;
            internal int CurBlockIdx;

            public Enumerator(in ListOfAppendOnlyUnrolledLinkedLists<T> linkedList, int listIdx)
            {
                NextBlockIdxEachBlock = linkedList.NextBlockIdxEachBlock.AsArray();
                NodeBuffer = linkedList.NodeBuffer.AsArray();

                ListPtr listPtr = linkedList.ListPtrBuffer[listIdx];
                StartBlockIdx = listPtr.FirstBlockIdx;
                EndBlockIdx = listPtr.LastBlockIdx;
                EndBlockLength = math.select(listPtr.TailLocalIdx % linkedList.BlockSize + 1, 0, listPtr.TailLocalIdx == -1);
                BlockSize = linkedList.BlockSize;
                CurBlockIdx = StartBlockIdx;
            }
            public Enumerator(in ListOfAppendOnlyUnrolledLinkedLists<T>.ROSpan linkedList, int listIdx)
            {
                NextBlockIdxEachBlock = linkedList.NextBlockIdxEachBlock;
                NodeBuffer = linkedList.NodeBuffer;

                ListPtr listPtr = linkedList.ListPtrBuffer[listIdx];
                StartBlockIdx = listPtr.FirstBlockIdx;
                EndBlockIdx = listPtr.LastBlockIdx;
                EndBlockLength = math.select(listPtr.TailLocalIdx % linkedList.BlockSize + 1, 0, listPtr.TailLocalIdx == -1);
                BlockSize = linkedList.BlockSize;
                CurBlockIdx = StartBlockIdx;
            }
            public bool MoveNext(out NativeSliceReadOnly<T> block)
            {
                bool curBlockValid = CurBlockIdx != INVALID_BLOCK_IDX;
                int blockIdx = math.select(EndBlockIdx, CurBlockIdx, curBlockValid);
                int blockLength = math.select(BlockSize, EndBlockLength, blockIdx == EndBlockIdx);
                block = new NativeSliceReadOnly<T>(NodeBuffer.Slice(blockIdx * BlockSize, blockLength));
                CurBlockIdx = NextBlockIdxEachBlock[blockIdx];
                return curBlockValid;
            }
            public void Reset()
            {
                CurBlockIdx = StartBlockIdx;
            }
        }
        public struct ROSpan
        {
            internal NativeArray<ListPtr> ListPtrBuffer;
            internal NativeArray<int> NextBlockIdxEachBlock;
            internal NativeArray<T> NodeBuffer;
            internal readonly int BlockSize;

            internal ROSpan(int blockSize)
            {
                this = default;
                BlockSize = blockSize;
            }

            public bool IsCreated => ListPtrBuffer.IsCreated;
            public int Count => ListPtrBuffer.Length;
            public Enumerator GetEnumerator(int listIndex)
            {
                return new Enumerator(in this, listIndex);
            }
        }
    }
}
