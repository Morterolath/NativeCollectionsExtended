using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct ListOfUniformSparseGrids<T>
        where T : unmanaged
    {
        internal struct SubGridPtr
        {
            internal static readonly SubGridPtr NULL = new SubGridPtr { Start = -1 };
            internal int Start;

            internal bool Equals(SubGridPtr other)
            {
                return Start == other.Start;
            }
        }
        public readonly int SubGridRowColAmount;
        public readonly int SubGridTileAmount;
        public readonly int SuperGridRowAmount;
        public readonly int SuperGridColAmount;
        public readonly int SuperGridTileAmount;

        internal NativeList<SubGridPtr> SubGridPtrs;
        internal NativeList<T> SubGridData;
        internal NativeList<int> UnusedSubGridStartIndicies;

        public ListOfUniformSparseGrids(Allocator allocator, int gridColAmount, int gridRowAmount, int subGridRowColAmount)
        {
            gridColAmount = math.max(gridColAmount, 1);
            gridRowAmount = math.max(gridRowAmount, 1);
            subGridRowColAmount = math.max(subGridRowColAmount, 1);

            SubGridRowColAmount = subGridRowColAmount;
            SubGridTileAmount = subGridRowColAmount * subGridRowColAmount;
            SuperGridRowAmount = gridRowAmount / subGridRowColAmount + math.select(1, 0, gridRowAmount % subGridRowColAmount == 0);
            SuperGridColAmount = gridColAmount / subGridRowColAmount + math.select(1, 0, gridColAmount % subGridRowColAmount == 0);
            SuperGridTileAmount = SuperGridRowAmount * SuperGridColAmount;
            SubGridPtrs = new NativeList<SubGridPtr>(allocator);
            SubGridData = new NativeList<T>(allocator);
            UnusedSubGridStartIndicies = new NativeList<int>(allocator);
        }
        public void Dispose()
        {
            SubGridPtrs.Dispose();
            SubGridData.Dispose();
            UnusedSubGridStartIndicies.Dispose();
        }
        public int Length => SubGridPtrs.Length / SuperGridTileAmount;

        public void AddGrid()
        {
            SubGridPtrs.AddReplicate(SubGridPtr.NULL, SuperGridTileAmount);
        }
        public void DisposeGrid(int gridIndex)
        {
            int superGridStartInc = gridIndex * SuperGridTileAmount;
            int superGridEndExc = superGridStartInc + SuperGridTileAmount;
            for(int i = superGridStartInc; i < superGridEndExc; i++)
            {
                SubGridPtr subGridPtr = SubGridPtrs[i];
                SubGridPtrs[i] = SubGridPtr.NULL;
                if (subGridPtr.Equals(SubGridPtr.NULL)) continue;
                DeallocateSubGrid(subGridPtr);
            }
        }
        public T Read(int gridIndex, int2 tileIndex)
        {
            GetSparseGridIndex(tileIndex, SubGridRowColAmount, SuperGridColAmount,
                out int superGridIndex1d, out int subGridIndex1d);

            int subGridPtrIndex = gridIndex * SuperGridTileAmount + superGridIndex1d;
            SubGridPtr subGridPtr = SubGridPtrs[subGridPtrIndex];
            
            if (subGridPtr.Equals(SubGridPtr.NULL))
                return default;

            int dataIndex = subGridPtr.Start + subGridIndex1d;
            return SubGridData[dataIndex];
        }
        public void Write(int gridIndex, int2 tileIndex, T data)
        {
            GetSparseGridIndex(tileIndex, SubGridRowColAmount, SuperGridColAmount,
                out int superGridIndex1d, out int subGridIndex1d);

            int subGridPtrIndex = gridIndex * SuperGridTileAmount + superGridIndex1d;
            SubGridPtr subGridPtr = SubGridPtrs[subGridPtrIndex];

            if (subGridPtr.Equals(SubGridPtr.NULL))
            {
                subGridPtr = AllocateSubGrid();
                InitSubGrid(subGridPtr);
                SubGridPtrs[subGridPtrIndex] = subGridPtr;
            }

            int dataIndex = subGridPtr.Start + subGridIndex1d;
            SubGridData[dataIndex] = data;
        }
        public bool TryAllocateSubGrid(int gridIndex, int2 superGridTileIndex)
        {
            int superGridTileIndex_1d = superGridTileIndex.y * SuperGridColAmount + superGridTileIndex.x;
            int subGridPtrIndex = gridIndex * SuperGridTileAmount + superGridTileIndex_1d;
            SubGridPtr subGridPtr = SubGridPtrs[subGridPtrIndex];
            if (subGridPtr.Equals(SubGridPtr.NULL))
            {
                subGridPtr = AllocateSubGrid();
                InitSubGrid(subGridPtr);
                SubGridPtrs[subGridPtrIndex] = subGridPtr;
                return true;
            }
            return false;
        }
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(SubGridPtrs.AsArray(), SubGridData.AsArray(), SubGridRowColAmount, SubGridTileAmount, SuperGridRowAmount,
                SuperGridColAmount, SuperGridTileAmount);
        }
        public ParallelWriter AsDeferredParallelWriter()
        {
            return new ParallelWriter(SubGridPtrs.AsDeferredJobArray(), SubGridData.AsDeferredJobArray(), SubGridRowColAmount, 
                SubGridTileAmount, SuperGridRowAmount, SuperGridColAmount, SuperGridTileAmount);
        }
        static void GetSparseGridIndex(int2 tileIndex, int subGridRowColAmount, int superGridColAmount,
            out int supGridIndex1d, out int subGridIndex1d)
        {
            int2 supGridIndex2d = tileIndex / subGridRowColAmount;
            int2 subGridIndex2d = tileIndex % subGridRowColAmount;
            supGridIndex1d = supGridIndex2d.y * superGridColAmount + supGridIndex2d.x;
            subGridIndex1d = subGridIndex2d.y * subGridRowColAmount + subGridIndex2d.x;
        }
        void InitSubGrid(SubGridPtr subGridPtr)
        {
            int fromInc = subGridPtr.Start;
            int toExc = subGridPtr.Start + SubGridTileAmount;
            for (int i = fromInc; i < toExc; i++)
                SubGridData[i] = default;
        }
        SubGridPtr AllocateSubGrid()
        {
            int lastUnusedIndex = UnusedSubGridStartIndicies.Length - 1;
            if (lastUnusedIndex >= 0)
            {
                int subGridStartIndex = UnusedSubGridStartIndicies[lastUnusedIndex];
                UnusedSubGridStartIndicies.Length -= 1;
                SubGridPtr ptr = new SubGridPtr { Start = subGridStartIndex };
                return ptr;
            }
            else
            {
                SubGridPtr ptr = new SubGridPtr { Start = SubGridData.Length };
                SubGridData.Length += SubGridTileAmount;
                return ptr;
            }
        }
        void DeallocateSubGrid(SubGridPtr clusterPtr)
        {
            UnusedSubGridStartIndicies.Add(clusterPtr.Start);
        }


        public struct ParallelWriter
        {
            public readonly int SubGridRowColAmount;
            public readonly int SubGridTileAmount;
            public readonly int SuperGridRowAmount;
            public readonly int SuperGridColAmount;
            public readonly int SuperGridTileAmount;

            internal NativeArray<SubGridPtr> SubGridPtrs;
            internal NativeArray<T> SubGridData;

            internal ParallelWriter(NativeArray<SubGridPtr> subGridPtrs, NativeArray<T> subGridData,
                int subGridRowColAmount, int subGridTileAmount, int superGridRowAmount, int superGridColAmount,
                int superGridTileAmount)
            {
                SubGridRowColAmount = subGridRowColAmount;
                SubGridTileAmount = subGridTileAmount;
                SuperGridRowAmount = superGridRowAmount;
                SuperGridColAmount = superGridColAmount;
                SuperGridTileAmount = superGridTileAmount;
                SubGridPtrs = subGridPtrs;
                SubGridData = subGridData;
            }

            public T Read(int gridIndex, int2 tileIndex)
            {
                GetSparseGridIndex(tileIndex, SubGridRowColAmount, SuperGridColAmount,
                    out int superGridIndex1d, out int subGridIndex1d);

                int subGridPtrIndex = gridIndex * SuperGridTileAmount + superGridIndex1d;
                SubGridPtr subGridPtr = SubGridPtrs[subGridPtrIndex];

                if (subGridPtr.Equals(SubGridPtr.NULL))
                    return default;

                int dataIndex = subGridPtr.Start + subGridIndex1d;
                return SubGridData[dataIndex];
            }
            public void WriteNoAlloc(int gridIndex, int2 tileIndex, T data)
            {
                GetSparseGridIndex(tileIndex, SubGridRowColAmount, SuperGridColAmount,
                    out int superGridIndex1d, out int subGridIndex1d);

                int subGridPtrIndex = gridIndex * SuperGridTileAmount + superGridIndex1d;
                SubGridPtr subGridPtr = SubGridPtrs[subGridPtrIndex];

                if (subGridPtr.Equals(SubGridPtr.NULL)) return;

                int dataIndex = subGridPtr.Start + subGridIndex1d;
                SubGridData[dataIndex] = data;
            }
        }
    }
}