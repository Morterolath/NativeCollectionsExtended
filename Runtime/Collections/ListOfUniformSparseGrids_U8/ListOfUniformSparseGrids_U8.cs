using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct ListOfUniformSparseGrids_U8<T>
        where T : unmanaged
    {
        internal NativeList<int> SparseGridStartBuffer;
        internal NativeList<LengthAndCap> SparseGridLengthAndCapBuffer;
        internal NativeList<int> SparseGridAllocNodeIndexBuffer;
        internal NativeList<byte> SubGridPtrBuffer;
        internal TLSFAllocator<T> TileAllocator;
        
        internal readonly int SubGridTileCnt;
        internal readonly int SubGridRowColCnt;

        internal readonly int SuperGridTileCnt;
        internal readonly int SuperGridColCnt;

        internal readonly int BaseGridColCnt;

        internal const int MAX_SUPER_GIRD_TILE_CNT = byte.MaxValue;
        internal const int INVALID_SPARSE_GRID_START_INDEX = -1;
        internal const int INVALID_SUB_GRID_PTR = byte.MaxValue;
        public void Dispose()
        {
            SparseGridStartBuffer.Dispose();
            SparseGridLengthAndCapBuffer.Dispose();
            SparseGridAllocNodeIndexBuffer.Dispose();
            SubGridPtrBuffer.Dispose();
            TileAllocator.Dispose();
        }
        public int Count
        {
            get => SparseGridAllocNodeIndexBuffer.Length;
            set
            {
                int newCount = value;
                int oldCount = SparseGridAllocNodeIndexBuffer.Length;

                if(newCount > oldCount)
                {
                    int countToAdd = newCount - oldCount;
                    SubGridPtrBuffer.AddReplicate(INVALID_SUB_GRID_PTR, SuperGridTileCnt * countToAdd);
                    SparseGridAllocNodeIndexBuffer.AddReplicate(TLSFAllocator<T>.INVALID_ALLOC_NODE_INDEX, countToAdd);
                    SparseGridLengthAndCapBuffer.AddReplicate(default, countToAdd);
                    SparseGridStartBuffer.AddReplicate(0, countToAdd);
                }

                if(newCount < oldCount)
                {
                    for(int i = newCount; i < oldCount; i++)
                    {
                        TileAllocator.Deallocate(SparseGridAllocNodeIndexBuffer[i]);
                    }
                    SubGridPtrBuffer.Length = newCount * SuperGridTileCnt;
                    SparseGridAllocNodeIndexBuffer.Length = newCount;
                    SparseGridLengthAndCapBuffer.Length = newCount;
                    SparseGridStartBuffer.Length = newCount;
                }
            }
        }
        public void AddSparseGrid()
        {
            SubGridPtrBuffer.AddReplicate(INVALID_SUB_GRID_PTR, SuperGridTileCnt);
            SparseGridAllocNodeIndexBuffer.Add(TLSFAllocator<T>.INVALID_ALLOC_NODE_INDEX);
            SparseGridLengthAndCapBuffer.Add(default);
            SparseGridStartBuffer.Add(0);
        }
        public void Write(int sparseGridIndex, int baseGridTileIndex_1d, T data)
        {
            BaseToSuperAndSubGridTileIndex(baseGridTileIndex_1d, BaseGridColCnt, SubGridRowColCnt, SuperGridColCnt,
                out int superGridTileIndex_1d, out int subGridTileIndex_1d);

            int gridDataStart = SparseGridStartBuffer[sparseGridIndex];
            byte subGridPtr = SubGridPtrBuffer[sparseGridIndex * SuperGridTileCnt + superGridTileIndex_1d];

            if(subGridPtr == INVALID_SUB_GRID_PTR)
            {
                subGridPtr = AllocateSubGrid(sparseGridIndex);
                SubGridPtrBuffer[sparseGridIndex * SuperGridTileCnt + superGridTileIndex_1d] = subGridPtr;
                gridDataStart = SparseGridStartBuffer[sparseGridIndex];
            }

            TileAllocator.Data[gridDataStart + subGridPtr * SubGridTileCnt + subGridTileIndex_1d] = data;
        }
        public T Read(int sparseGridIndex, int baseGridTileIndex_1d)
        {
            BaseToSuperAndSubGridTileIndex(baseGridTileIndex_1d, BaseGridColCnt, SubGridRowColCnt, SuperGridColCnt,
                out int superGridTileIndex_1d, out int subGridTileIndex_1d);

            int gridDataStart = SparseGridStartBuffer[sparseGridIndex];
            byte subGridPtr = SubGridPtrBuffer[sparseGridIndex * SuperGridTileCnt + superGridTileIndex_1d];

            if(subGridPtr == INVALID_SUB_GRID_PTR)
            {
                return default;
            }

            return TileAllocator.Data[gridDataStart + subGridPtr * SubGridTileCnt + subGridTileIndex_1d];
        }
        public void DisposeSparseGrid(int sparseGridIndex)
        {
            TileAllocator.Deallocate(SparseGridAllocNodeIndexBuffer[sparseGridIndex]);

            SparseGridAllocNodeIndexBuffer[sparseGridIndex] = TLSFAllocator<int>.INVALID_ALLOC_NODE_INDEX;
            SparseGridLengthAndCapBuffer[sparseGridIndex] = default;
            SparseGridStartBuffer[sparseGridIndex] = 0;

            int from = sparseGridIndex * SuperGridTileCnt;
            int toExc = from + SuperGridTileCnt;
            for (int i = from; i < toExc; i++)
                SubGridPtrBuffer[i] = INVALID_SUB_GRID_PTR;
        }
        byte AllocateSubGrid(int sparseGridIndex)
        {
            LengthAndCap sparseGridLenAndCap = SparseGridLengthAndCapBuffer[sparseGridIndex];
            int sparseGridLength = sparseGridLenAndCap.Length;
            int sparseGridCapacity = sparseGridLenAndCap.Capacity;
            int newSparseGridLength = sparseGridLength + SubGridTileCnt;
            if(newSparseGridLength > sparseGridCapacity)
            {
                int sparseGridStart = SparseGridStartBuffer[sparseGridIndex];
                TileAllocator.Allocate(newSparseGridLength, out int newSparseGridStart, out int newSparseGridCapacity,
                    out int newAllocNodeIndex);
                NativeArray<T> dataAsArray = TileAllocator.DataAsArray();
                dataAsArray.Slice(newSparseGridStart, sparseGridLength).CopyFrom(dataAsArray.Slice(sparseGridStart, sparseGridLength));

                int clearStart = newSparseGridStart + sparseGridLength;
                int clearEndExcluding = newSparseGridStart + newSparseGridCapacity;
                for (int i = clearStart; i < clearEndExcluding; i++) dataAsArray[i] = default;

                TileAllocator.Deallocate(SparseGridAllocNodeIndexBuffer[sparseGridIndex]);
                SparseGridStartBuffer[sparseGridIndex] = newSparseGridStart;
                SparseGridLengthAndCapBuffer[sparseGridIndex] = new LengthAndCap { Length = newSparseGridLength, Capacity = newSparseGridCapacity };
                SparseGridAllocNodeIndexBuffer[sparseGridIndex] = newAllocNodeIndex;
            }
            return (byte)(sparseGridLength / SubGridTileCnt);
        }
        static void BaseToSuperAndSubGridTileIndex(int baseGridTileIndex_1d, int baseGridColCnt, int subGridRowColCnt,
            int superGridColCnt, out int superGridTileIndex_1d, out int subGridTileIndex_1d)
        {
            int2 baseGridTileIndex_2d = new int2(baseGridTileIndex_1d % baseGridColCnt, baseGridTileIndex_1d / baseGridColCnt);
            int2 superGridTileIndex_2d = baseGridTileIndex_2d / subGridRowColCnt;
            int2 subGridTileIndex_2d = baseGridTileIndex_2d % subGridRowColCnt;

            superGridTileIndex_1d = superGridTileIndex_2d.y * superGridColCnt + superGridTileIndex_2d.x;
            subGridTileIndex_1d = subGridTileIndex_2d.y * subGridRowColCnt + subGridTileIndex_2d.x;
        }
        internal struct LengthAndCap
        {
            internal int Length;
            internal int Capacity;
        }
    }
}
