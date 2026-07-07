using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    //list handle bug: You can submit an outdated list handle back, because it is not checked. Id is not enough.
    public struct ListOfChunkedLists<T>
        where T : unmanaged
    {
        internal const int INVALID_CHUNK_IDX = -1;
        internal const int INVALID_DATA_IDX = -1;
        internal NativeList<ListInfo> ListInfoBuffer;
        internal NativeList<T> DataBuffer;
        internal NativeList<Chunk> ChunkBuffer;
        internal NativeList<int> FreeChunkBuffer;
        internal readonly int ChunkSize;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
        internal NativeReference<DebugInfo> DbgInfo;
#endif

        public ListOfChunkedLists(int chunkSize, Allocator allocator)
        {
            ListInfoBuffer = new NativeList<ListInfo>(allocator);
            DataBuffer = new NativeList<T>(allocator);
            ChunkBuffer = new NativeList<Chunk>(allocator);
            FreeChunkBuffer = new NativeList<int>(allocator);
            ChunkSize = math.max(chunkSize, 4);
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            DbgInfo = new NativeReference<DebugInfo>(allocator);
#endif
        }

        public void Dispose()
        {
            ListInfoBuffer.Dispose();
            DataBuffer.Dispose();
            ChunkBuffer.Dispose();
            FreeChunkBuffer.Dispose();
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            DbgInfo.Dispose();
#endif
        }
        public int GetListCount()
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
#endif
            return ListInfoBuffer.Length;
        }
        public void SetListCount(int count)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            IncListCountVersion(DbgInfo);
#endif
            int newCount = count;
            int oldCount = ListInfoBuffer.Length;

            for (int i = oldCount; i < newCount; i++)
                AddList();
            for (int i = newCount; i < oldCount; i++)
                DisposeList(i);
            ListInfoBuffer.Length = newCount;
        }
        public void AddList()
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            IncListCountVersion(DbgInfo);
#endif
            ListInfo info = ListInfo.DEFAULT;

#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            ReturnIncListId(DbgInfo, ref info.Version_Dbg);
#endif

            ListWriteHelper.AllocateChunkToEnd(ref info, ChunkSize, DataBuffer, ChunkBuffer, FreeChunkBuffer);
            info.NextDataIdx = info.FirstChunkIdx * ChunkSize;
            ListInfoBuffer.Add(info);
        }
        public void DisposeList(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
            IncListCountVersion(DbgInfo);
#endif
            ListInfo info = ListInfoBuffer[listIndex];

#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            ReturnIncListId(DbgInfo, ref info.Version_Dbg);
#endif

            ListWriteHelper.DisposeList(ref info, ChunkSize, ChunkBuffer, FreeChunkBuffer);
            ListInfoBuffer[listIndex] = info;
        }
        public int GetListCapacity(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
#endif
            return ListInfoBuffer[listIndex].Capacity;
        }
        public void SetListCapacityGreater(int listIndex, int greaterCapacity)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
            IncListAllocVersion(DbgInfo);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.SetListCapacityGreater(ref info, ChunkSize, greaterCapacity, DataBuffer, ChunkBuffer, FreeChunkBuffer);
            ListInfoBuffer[listIndex] = info;
        }
        public int GetListLength(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
#endif
            return ListInfoBuffer[listIndex].Length;
        }
        public void ClearList(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.ClearList(ref info, ChunkSize);
            ListInfoBuffer[listIndex] = info;
        }
        public void AddToList(int listIndex, T data)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
            IncListAllocVersion(DbgInfo);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.AddToList(ref info, data, ChunkSize, DataBuffer, ChunkBuffer, FreeChunkBuffer);
            ListInfoBuffer[listIndex] = info;
        }
        public void AddToList(int listIndex, T data, out DataIndex dataIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
            IncListAllocVersion(DbgInfo);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            dataIndex = new DataIndex
            {
                Index = info.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                ListIndex_Dbg = listIndex,
                ListVersion_Dbg = info.Version_Dbg,
#endif
            };
            ListWriteHelper.AddToList(ref info, data, ChunkSize, DataBuffer, ChunkBuffer, FreeChunkBuffer);
            ListInfoBuffer[listIndex] = info;
        }
        public void AddToListNoResize(int listIndex, T data)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
            CheckNoResize(ListInfoBuffer[listIndex]);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
            ListInfoBuffer[listIndex] = info;
        }
        public void AddToListNoResize(int listIndex, T data, out DataIndex dataIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
            CheckNoResize(ListInfoBuffer[listIndex]);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            dataIndex = new DataIndex
            {
                Index = info.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                ListIndex_Dbg = listIndex,
                ListVersion_Dbg = info.Version_Dbg,
#endif
            };
            ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
            ListInfoBuffer[listIndex] = info;
        }
        public T ReadData(DataIndex dataIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckDataIndex(ListInfoBuffer, ChunkBuffer, DataBuffer, ChunkSize, dataIndex);
#endif
            return DataBuffer[dataIndex.Index];
        }
        public void WriteData(DataIndex dataIndex, T data)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckDataIndex(ListInfoBuffer, ChunkBuffer, DataBuffer, ChunkSize, dataIndex);
#endif
            DataBuffer[dataIndex.Index] = data;
        }
        public int GetDataBufferLength()
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
#endif
            return DataBuffer.Length;
        }
        public RWEnumerator GetEnumerator(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
            CheckListIndex(ListInfoBuffer, listIndex);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            return new RWEnumerator
            {
                EnumData = new EnumData
                {
                    DataBuffer = DataBuffer.AsArray(),
                    ChunkBuffer = ChunkBuffer.AsArray(),
                    Length = info.Length,
                    FirstChunkIdx = info.FirstChunkIdx,
                    NewDataChunkIdx = info.NextDataIdx / ChunkSize,
                    ChunkSize = ChunkSize,
                    CurrentChunkIdx = info.FirstChunkIdx,
                },
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
                ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                ListVersion_Dbg = info.Version_Dbg,
                ListIndex_Dbg = listIndex,
#endif
            };
        }
        public ListWriter AsListWriter()
        {
            return new ListWriter
            {
                DataBuffer = DataBuffer,
                ChunkSize = ChunkSize,
                ChunkBuffer = ChunkBuffer,
                FreeChunkBuffer = FreeChunkBuffer,
                ListInfoBuffer = ListInfoBuffer.AsArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
#endif
            };
        }
        public ListWriter AsDeferredListWriter()
        {
            return new ListWriter
            {
                DataBuffer = DataBuffer,
                ChunkSize = ChunkSize,
                ChunkBuffer = ChunkBuffer,
                FreeChunkBuffer = FreeChunkBuffer,
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersionSnapshot = DebugInfo.DEFERRED_VERSION,
#endif
            };
        }
        public ListWriterNoResize AsListWriterNoResize()
        {
            return new ListWriterNoResize
            {
                DataBuffer = DataBuffer.AsArray(),
                ChunkSize = ChunkSize,
                ChunkBuffer = ChunkBuffer.AsArray(),
                ListInfoBuffer = ListInfoBuffer.AsArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
#endif
            };
        }
        public ListWriterNoResize AsDeferredListWriterNoResize()
        {
            return new ListWriterNoResize
            {
                DataBuffer = DataBuffer.AsDeferredJobArray(),
                ChunkSize = ChunkSize,
                ChunkBuffer = ChunkBuffer.AsDeferredJobArray(),
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersionSnapshot = DebugInfo.DEFERRED_VERSION,
                ListAllocVersionSnapshot = DebugInfo.DEFERRED_VERSION,
#endif
            };
        }
        public ReadOnly AsReadOnly()
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
#endif
            return new ReadOnly
            {
                DataBuffer = DataBuffer.AsArray(),
                ChunkBuffer = ChunkBuffer.AsArray(),
                ListInfoBuffer = ListInfoBuffer.AsArray(),
                ChunkSize = ChunkSize,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
#endif
            };
        }
        public ReadOnly AsDeferredReadOnly()
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
#endif
            return new ReadOnly
            {
                DataBuffer = DataBuffer.AsDeferredJobArray(),
                ChunkBuffer = ChunkBuffer.AsDeferredJobArray(),
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
                ChunkSize = ChunkSize,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersionSnapshot = DebugInfo.DEFERRED_VERSION,
                ListAllocVersionSnapshot = DebugInfo.DEFERRED_VERSION,
#endif
            };
        }
        public ParallelWriter AsParallelWriter()
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            IsUsable(this);
#endif
            return new ParallelWriter
            {
                DataBuffer = DataBuffer.AsDeferredJobArray(),
                ChunkBuffer = ChunkBuffer.AsDeferredJobArray(),
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
                ChunkSize = ChunkSize,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
#endif
            };
        }
        public struct ListWriter
        {
            internal NativeArray<ListInfo> ListInfoBuffer;
            internal NativeList<T> DataBuffer;
            internal NativeList<Chunk> ChunkBuffer;
            internal NativeList<int> FreeChunkBuffer;
            internal int ChunkSize;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal ulong ListCountVersionSnapshot;
#endif
            public int GetListCount()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfoBuffer.Length;
            }
            public void DisposeList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];

#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                ReturnIncListId(DbgInfo, ref info.Version_Dbg);
#endif

                ListWriteHelper.DisposeList(ref info, ChunkSize, ChunkBuffer, FreeChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public int GetListCapacity(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public void SetListCapacityGreater(int listIndex, int greaterCapacity)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.SetListCapacityGreater(ref info, ChunkSize, greaterCapacity, DataBuffer, ChunkBuffer, FreeChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public void ClearList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.ClearList(ref info, ChunkSize);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToList(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToList(ref info, data, ChunkSize, DataBuffer, ChunkBuffer, FreeChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToList(int listIndex, T data, out DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                dataIndex = new DataIndex
                {
                    Index = info.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = listIndex,
                    ListVersion_Dbg = info.Version_Dbg,
#endif
                };
                ListWriteHelper.AddToList(ref info, data, ChunkSize, DataBuffer, ChunkBuffer, FreeChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToListNoResize(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                CheckNoResize(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToListNoResize(int listIndex, T data, out DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                CheckNoResize(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                dataIndex = new DataIndex
                {
                    Index = info.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = listIndex,
                    ListVersion_Dbg = info.Version_Dbg,
#endif
                };
                ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public T ReadData(DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckDataIndex(ListInfoBuffer, ChunkBuffer, DataBuffer, ChunkSize, dataIndex);
#endif
                return DataBuffer[dataIndex.Index];
            }
            public void WriteData(DataIndex dataIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckDataIndex(ListInfoBuffer, ChunkBuffer, DataBuffer, ChunkSize, dataIndex);
#endif
                DataBuffer[dataIndex.Index] = data;
            }
            public int GetDataBufferLength()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return DataBuffer.Length;
            }
            public ListHandle GetListHandle(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                Lock(DbgInfo);
#endif
                return new ListHandle
                {
                    DataBuffer = DataBuffer,
                    ChunkSize = ChunkSize,
                    ChunkBuffer = ChunkBuffer,
                    FreeChunkBuffer = FreeChunkBuffer,
                    ListIndex = listIndex,
                    ListInfo = ListInfoBuffer[listIndex],
                    ListInfoBuffer = ListInfoBuffer,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    HandleId = DbgInfo.Value.HandleId,
#endif
                };
            }
            public RWEnumerator GetEnumerator(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return new RWEnumerator
                {
                    EnumData = new EnumData
                    {
                        DataBuffer = DataBuffer.AsArray(),
                        ChunkBuffer = ChunkBuffer.AsArray(),
                        Length = info.Length,
                        FirstChunkIdx = info.FirstChunkIdx,
                        NewDataChunkIdx = info.NextDataIdx / ChunkSize,
                        ChunkSize = ChunkSize,
                        CurrentChunkIdx = info.FirstChunkIdx,
                    },
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
                    ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                    ListVersion_Dbg = info.Version_Dbg,
                    ListIndex_Dbg = listIndex,
#endif
                };
            }
        }
        public struct ListWriterNoResize
        {
            internal NativeArray<ListInfo> ListInfoBuffer;
            internal NativeArray<T> DataBuffer;
            internal NativeArray<Chunk> ChunkBuffer;
            internal int ChunkSize;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal ulong ListCountVersionSnapshot;
            internal ulong ListAllocVersionSnapshot;
#endif
            public int GetListCount()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfoBuffer.Length;
            }
            public int GetListCapacity(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public void ClearList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.ClearList(ref info, ChunkSize);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToListNoResize(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                CheckNoResize(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToListNoResize(int listIndex, T data, out DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                CheckNoResize(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                dataIndex = new DataIndex
                {
                    Index = info.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = listIndex,
                    ListVersion_Dbg = info.Version_Dbg,
#endif
                };
                ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public T ReadData(DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckDataIndex(ListInfoBuffer, ChunkBuffer, DataBuffer, ChunkSize, dataIndex);
#endif
                return DataBuffer[dataIndex.Index];
            }
            public void WriteData(DataIndex dataIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckDataIndex(ListInfoBuffer, ChunkBuffer, DataBuffer, ChunkSize, dataIndex);
#endif
                DataBuffer[dataIndex.Index] = data;
            }
            public int GetDataBufferLength()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return DataBuffer.Length;
            }
            public ListHandleNoResize GetListHandle(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                Lock(DbgInfo);
#endif
                return new ListHandleNoResize
                {
                    DataBuffer = DataBuffer,
                    ChunkSize = ChunkSize,
                    ChunkBuffer = ChunkBuffer,
                    ListIndex = listIndex,
                    ListInfo = ListInfoBuffer[listIndex],
                    ListInfoBuffer = ListInfoBuffer,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    HandleId = DbgInfo.Value.HandleId,
#endif
                };
            }
            public RWEnumerator GetEnumerator(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return new RWEnumerator
                {
                    EnumData = new EnumData
                    {
                        DataBuffer = DataBuffer,
                        ChunkBuffer = ChunkBuffer,
                        Length = info.Length,
                        FirstChunkIdx = info.FirstChunkIdx,
                        NewDataChunkIdx = info.NextDataIdx / ChunkSize,
                        ChunkSize = ChunkSize,
                        CurrentChunkIdx = info.FirstChunkIdx,
                    },
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
                    ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                    ListVersion_Dbg = info.Version_Dbg,
                    ListIndex_Dbg = listIndex,
#endif
                };
            }
        }
        public struct ParallelWriter
        {
            internal NativeArray<ListInfo> ListInfoBuffer;
            [NativeDisableParallelForRestriction] internal NativeArray<T> DataBuffer;
            [NativeDisableParallelForRestriction] internal NativeArray<Chunk> ChunkBuffer;
            internal int ChunkSize;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
#endif
            public int GetListCapacity(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public void ClearList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.ClearList(ref info, ChunkSize);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToListNoResize(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                CheckNoResize(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToListNoResize(int listIndex, T data, out DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
                CheckNoResize(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                dataIndex = new DataIndex
                {
                    Index = info.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = listIndex,
                    ListVersion_Dbg = info.Version_Dbg,
#endif
                };
                ListWriteHelper.AddToListNoResize(ref info, data, ChunkSize, DataBuffer, ChunkBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public RWEnumerator GetEnumerator(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return new RWEnumerator
                {
                    EnumData = new EnumData
                    {
                        DataBuffer = DataBuffer,
                        ChunkBuffer = ChunkBuffer,
                        Length = info.Length,
                        FirstChunkIdx = info.FirstChunkIdx,
                        NewDataChunkIdx = info.NextDataIdx / ChunkSize,
                        ChunkSize = ChunkSize,
                        CurrentChunkIdx = info.FirstChunkIdx,
                    },
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
                    ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                    ListVersion_Dbg = info.Version_Dbg,
                    ListIndex_Dbg = listIndex,
#endif
                };
            }
        }
        public struct ReadOnly
        {
            internal NativeArray<ListInfo> ListInfoBuffer;
            internal NativeArray<T> DataBuffer;
            internal NativeArray<Chunk> ChunkBuffer;
            internal int ChunkSize;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
            internal ulong ListCountVersionSnapshot;
            internal ulong ListAllocVersionSnapshot;
#endif
            public int GetListCount()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfoBuffer.Length;
            }
            public int GetListCapacity(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public T ReadData(DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckDataIndex(ListInfoBuffer, ChunkBuffer, DataBuffer, ChunkSize, dataIndex);
#endif
                return DataBuffer[dataIndex.Index];
            }
            public ROEnumerator GetEnumerator(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return new ROEnumerator
                {
                    EnumData = new EnumData
                    {
                        DataBuffer = DataBuffer,
                        ChunkBuffer = ChunkBuffer,
                        Length = info.Length,
                        FirstChunkIdx = info.FirstChunkIdx,
                        NewDataChunkIdx = info.NextDataIdx / ChunkSize,
                        ChunkSize = ChunkSize,
                        CurrentChunkIdx = info.FirstChunkIdx,
                    },
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                    ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
                    ListIndex_Dbg = listIndex,
                    ListVersion_Dbg = info.Version_Dbg,
#endif
                };
            }
        }
        public struct ListHandle : IDisposable
        {
            internal ListInfo ListInfo;
            internal int ListIndex;
            internal NativeArray<ListInfo> ListInfoBuffer;
            internal NativeList<T> DataBuffer;
            internal NativeList<Chunk> ChunkBuffer;
            internal NativeList<int> FreeChunkBuffer;
            internal int ChunkSize;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal ulong HandleId;
#endif
            public void DisposeList()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                IncListAllocVersion(DbgInfo);
                ReturnIncListId(DbgInfo, ref ListInfo.Version_Dbg);
#endif
                ListWriteHelper.DisposeList(ref ListInfo, ChunkSize, ChunkBuffer, FreeChunkBuffer);
            }
            public int GetListCapacity()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfo.Capacity;
            }
            public void SetListCapacityGreater(int greaterCapacity)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                IncListAllocVersion(DbgInfo);
#endif
                ListWriteHelper.SetListCapacityGreater(ref ListInfo, ChunkSize, greaterCapacity, DataBuffer, ChunkBuffer, FreeChunkBuffer);
            }
            public int GetListLength()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfo.Length;
            }
            public void ClearList()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                ListWriteHelper.ClearList(ref ListInfo, ChunkSize);
            }
            public void AddToList(T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                IncListAllocVersion(DbgInfo);
#endif
                ListWriteHelper.AddToList(ref ListInfo, data, ChunkSize, DataBuffer, ChunkBuffer, FreeChunkBuffer);
            }
            public void AddToList(T data, out DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                IncListAllocVersion(DbgInfo);
#endif
                dataIndex = new DataIndex
                {
                    Index = ListInfo.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = ListIndex,
                    ListVersion_Dbg = ListInfo.Version_Dbg,
#endif
                };
                ListWriteHelper.AddToList(ref ListInfo, data, ChunkSize, DataBuffer, ChunkBuffer, FreeChunkBuffer);
            }
            public void AddToListNoResize(T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckNoResize(ListInfo);
#endif
                ListWriteHelper.AddToListNoResize(ref ListInfo, data, ChunkSize, DataBuffer, ChunkBuffer);
            }
            public void AddToListNoResize(T data, out DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckNoResize(ListInfo);
#endif
                dataIndex = new DataIndex
                {
                    Index = ListInfo.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = ListIndex,
                    ListVersion_Dbg = ListInfo.Version_Dbg,
#endif
                };
                ListWriteHelper.AddToListNoResize(ref ListInfo, data, ChunkSize, DataBuffer, ChunkBuffer);
            }
            public void Dispose()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                IncHandleId(DbgInfo);
                Unlock(DbgInfo);
#endif
                ListInfoBuffer[ListIndex] = ListInfo;
            }
        }
        public struct ListHandleNoResize : IDisposable
        {
            internal ListInfo ListInfo;
            internal int ListIndex;
            internal NativeArray<ListInfo> ListInfoBuffer;
            internal NativeArray<T> DataBuffer;
            internal NativeArray<Chunk> ChunkBuffer;
            internal int ChunkSize;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal ulong HandleId;
#endif
            public int GetListCapacity()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfo.Capacity;
            }
            public int GetListLength()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                return ListInfo.Length;
            }
            public void ClearList()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                ListWriteHelper.ClearList(ref ListInfo, ChunkSize);
            }
            public void AddToListNoResize(T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckNoResize(ListInfo);
#endif
                ListWriteHelper.AddToListNoResize(ref ListInfo, data, ChunkSize, DataBuffer, ChunkBuffer);
            }
            public void AddToListNoResize(T data, out DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckNoResize(ListInfo);
#endif
                dataIndex = new DataIndex
                {
                    Index = ListInfo.NextDataIdx,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = ListIndex,
                    ListVersion_Dbg = ListInfo.Version_Dbg,
#endif
                };
                ListWriteHelper.AddToListNoResize(ref ListInfo, data, ChunkSize, DataBuffer, ChunkBuffer);
            }
            public void Dispose()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                IncHandleId(DbgInfo);
                Unlock(DbgInfo);
#endif
                ListInfoBuffer[ListIndex] = ListInfo;
            }
        }
        public struct RWEnumerator
        {
            internal EnumData EnumData;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
            internal ulong ListCountVersionSnapshot;
            internal ulong ListAllocVersionSnapshot;
            internal ulong ListVersion_Dbg;
            internal int ListIndex_Dbg;
#endif
            public bool MoveNext(out RWChunk chunk)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                bool valid = EnumeratorHelper.MoveNext(ref EnumData, out NativeSlice<T> chunkSlice, out int chunkStart);
                chunk = new RWChunk
                {
                    ChunkSlice = chunkSlice,
                    ChunkStart = chunkStart,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
                    ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                    ListVersion = ListVersion_Dbg,
                    ListIndex = ListIndex_Dbg,
#endif
                };
                return valid;
            }
            public void Reset()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                EnumeratorHelper.Reset(ref EnumData);
            }
            public int JumpReturnLocalIndex(DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckDataIndex(this, dataIndex);
#endif
                return EnumeratorHelper.JumpReturnLocalIndex(ref EnumData, dataIndex);
            }
        }
        public struct ROEnumerator
        {
            internal EnumData EnumData;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
            internal ulong ListCountVersionSnapshot;
            internal ulong ListAllocVersionSnapshot;
            internal ulong ListVersion_Dbg;
            internal int ListIndex_Dbg;
#endif
            public bool MoveNext(out ROChunk chunk)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                bool valid = EnumeratorHelper.MoveNext(ref EnumData, out NativeSlice<T> chunkSlice, out int chunkStart);
                chunk = new ROChunk
                {
                    ChunkSlice = chunkSlice,
                    ChunkStart = chunkStart,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersionSnapshot = DbgInfo.Value.ListAllocVersion,
                    ListCountVersionSnapshot = DbgInfo.Value.ListCountVersion,
                    ListIndex = ListIndex_Dbg,
                    ListVersion = ListVersion_Dbg,
#endif
                };
                return valid;
            }
            public void Reset()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
#endif
                EnumeratorHelper.Reset(ref EnumData);
            }
            public int JumpReturnLocalIndex(DataIndex dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckDataIndex(this, dataIndex);
#endif
                return EnumeratorHelper.JumpReturnLocalIndex(ref EnumData, dataIndex);
            }
        }
        public struct RWChunk
        {
            internal NativeSlice<T> ChunkSlice;
            internal int ChunkStart;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
            internal ulong ListCountVersionSnapshot;
            internal ulong ListAllocVersionSnapshot;
            internal ulong ListVersion;
            internal int ListIndex;
#endif
            public int Length
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    IsUsable(this);
#endif
                    return ChunkSlice.Length;
                }
            }
            public T this[int index]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    IsUsable(this);
                    CheckChunkLocalIndex(ChunkSlice, index);
#endif
                    return ChunkSlice[index];
                }
                set
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    IsUsable(this);
                    CheckChunkLocalIndex(ChunkSlice, index);
#endif
                    ChunkSlice[index] = value; 
                }
            }
            public DataIndex GetDataIndex(int index)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckChunkLocalIndex(ChunkSlice, index);
#endif
                return new DataIndex
                {
                    Index = ChunkStart + index,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = ListIndex,
                    ListVersion_Dbg = ListVersion,
#endif
                };
            }
        }
        public struct ROChunk
        {
            internal NativeSlice<T> ChunkSlice;
            internal int ChunkStart;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
            internal ulong ListCountVersionSnapshot;
            internal ulong ListAllocVersionSnapshot;
            internal ulong ListVersion;
            internal int ListIndex;
#endif
            public int Length
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    IsUsable(this);
#endif
                    return ChunkSlice.Length;
                }
            }
            public T this[int index]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    IsUsable(this);
                    CheckChunkLocalIndex(ChunkSlice, index);
#endif
                    return ChunkSlice[index];
                }
            }
            public DataIndex GetDataIndex(int index)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                IsUsable(this);
                CheckChunkLocalIndex(ChunkSlice, index);
#endif
                return new DataIndex
                {
                    Index = ChunkStart + index,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    ListIndex_Dbg = ListIndex,
                    ListVersion_Dbg = ListVersion,
#endif
                };
            }
        }
        public struct DataIndex
        {
            internal int Index;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal int ListIndex_Dbg;
            internal ulong ListVersion_Dbg;
#endif
        }
        internal struct ListWriteHelper
        {
            internal static void SetListCapacityGreater(ref ListInfo info, int chunkSize, int greaterCapacity, NativeList<T> dataBuffer, NativeList<Chunk> chunkBuffer, NativeList<int> freeChunkBuffer)
            {
                int chunkCount = info.Capacity / chunkSize;
                int newChunkCount = greaterCapacity / chunkSize + math.select(1, 0, greaterCapacity % chunkSize == 0);

                int chunksNeeded = math.max(0, newChunkCount - chunkCount);

                for (int i = 0; i < chunksNeeded; i++)
                {
                    AllocateChunkToEnd(ref info, chunkSize, dataBuffer, chunkBuffer, freeChunkBuffer);
                }
            }
            internal static void AllocateChunkToEnd(ref ListInfo info, int chunkSize, NativeList<T> dataBuffer, NativeList<Chunk> chunkBuffer, NativeList<int> freeChunkBuffer)
            {
                int newChunkIdx;
                int freeBufferLast = freeChunkBuffer.Length - 1;
                if (freeBufferLast < 0)
                {
                    dataBuffer.Length += chunkSize;
                    newChunkIdx = chunkBuffer.Length;
                    Chunk newChunk = new Chunk 
                    { 
                        NextIdx = INVALID_CHUNK_IDX,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                        ChunkStartLength_Dbg = info.Capacity,
#endif
                    };
                    chunkBuffer.Add(newChunk);

                    if (info.LastChunkIdx != INVALID_CHUNK_IDX)
                    {
                        Chunk lastChunk = chunkBuffer[info.LastChunkIdx];
                        lastChunk.NextIdx = newChunkIdx;
                        chunkBuffer[info.LastChunkIdx] = lastChunk;
                    }
                }
                else
                {
                    newChunkIdx = freeChunkBuffer[freeBufferLast];
                    Chunk newChunk = chunkBuffer[newChunkIdx];
                    freeChunkBuffer[freeBufferLast] = newChunk.NextIdx;
                    freeChunkBuffer.ResizeUninitialized(math.select(freeBufferLast + 1, freeBufferLast, newChunk.NextIdx == INVALID_CHUNK_IDX));
                    newChunk = new Chunk 
                    { 
                        NextIdx = INVALID_CHUNK_IDX,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                        ChunkStartLength_Dbg = info.Capacity,
#endif
                    };
                    chunkBuffer[newChunkIdx] = newChunk;

                    if (info.LastChunkIdx != INVALID_CHUNK_IDX)
                    {
                        Chunk lastChunk = chunkBuffer[info.LastChunkIdx];
                        lastChunk.NextIdx = newChunkIdx;
                        chunkBuffer[info.LastChunkIdx] = lastChunk;
                    }
                }

                info.Capacity += chunkSize;
                info.LastChunkIdx = newChunkIdx;
                info.FirstChunkIdx = math.select(info.FirstChunkIdx, newChunkIdx, info.FirstChunkIdx == INVALID_CHUNK_IDX);
            }
            internal static void ClearList(ref ListInfo info, int chunkSize)
            {
                info.Length = 0;
                info.NextDataIdx = info.FirstChunkIdx * chunkSize;
            }
            internal static void AddToList(ref ListInfo info, T data, int chunkSize, NativeList<T> dataBuffer, NativeList<Chunk> chunkBuffer, NativeList<int> freeChunkBuffer)
            {
                int dataIndex = info.NextDataIdx;
                dataBuffer[dataIndex] = data;

                info.NextDataIdx++;
                info.Length++;
                if (info.NextDataIdx % chunkSize == 0)
                {
                    if (info.Length == info.Capacity)
                    {
                        AllocateChunkToEnd(ref info, chunkSize, dataBuffer, chunkBuffer, freeChunkBuffer);
                        info.NextDataIdx = info.LastChunkIdx * chunkSize;
                    }
                    else
                    {
                        int lastDataChunkIdx = (info.NextDataIdx - 1) / chunkSize;
                        int nextChunkIdx = chunkBuffer[lastDataChunkIdx].NextIdx;
                        info.NextDataIdx = nextChunkIdx * chunkSize;
                    }
                }
            }
            internal static void AddToListNoResize(ref ListInfo info, T data, int chunkSize, NativeList<T> dataBuffer, NativeList<Chunk> chunkBuffer)
            {
                int dataIndex = info.NextDataIdx;
                dataBuffer[dataIndex] = data;

                info.NextDataIdx++;
                info.Length++;
                if (info.NextDataIdx % chunkSize == 0)
                {
                    int lastDataChunkIdx = (info.NextDataIdx - 1) / chunkSize;
                    int nextChunkIdx = chunkBuffer[lastDataChunkIdx].NextIdx;
                    info.NextDataIdx = nextChunkIdx * chunkSize;
                }
            }
            internal static void AddToListNoResize(ref ListInfo info, T data, int chunkSize, NativeArray<T> dataBuffer, NativeArray<Chunk> chunkBuffer)
            {
                int dataIndex = info.NextDataIdx;
                dataBuffer[dataIndex] = data;

                info.NextDataIdx++;
                info.Length++;
                if (info.NextDataIdx % chunkSize == 0)
                {
                    int lastDataChunkIdx = (info.NextDataIdx - 1) / chunkSize;
                    int nextChunkIdx = chunkBuffer[lastDataChunkIdx].NextIdx;
                    info.NextDataIdx = nextChunkIdx * chunkSize;
                }
            }
            internal static void DisposeList(ref ListInfo info, int chunkSize, NativeList<Chunk> chunkBuffer, NativeList<int> freeChunkBuffer)
            {
                info.LastChunkIdx = info.FirstChunkIdx;
                info.Length = 0;
                info.NextDataIdx = info.FirstChunkIdx * chunkSize;
                info.Capacity = chunkSize;

                Chunk firstChunk = chunkBuffer[info.FirstChunkIdx];
                int secondChunkIdx = firstChunk.NextIdx;
                firstChunk.NextIdx = INVALID_CHUNK_IDX;
                chunkBuffer[info.FirstChunkIdx] = firstChunk;

                if (secondChunkIdx != INVALID_CHUNK_IDX)
                    freeChunkBuffer.Add(secondChunkIdx);
            }
        }
        internal struct EnumData
        {
            internal NativeArray<T> DataBuffer;
            internal NativeArray<Chunk> ChunkBuffer;
            internal int Length;
            internal int FirstChunkIdx;
            internal int NewDataChunkIdx;
            internal int ChunkSize;
            internal int CurrentChunkIdx;
        }
        internal struct EnumeratorHelper
        {
            internal static bool MoveNext(ref EnumData enumData, out NativeSlice<T> chunk, out int chunkStart)
            {
                bool chunkInvalid = enumData.CurrentChunkIdx == INVALID_CHUNK_IDX;
                bool lastChunk = enumData.CurrentChunkIdx == enumData.NewDataChunkIdx;
                int lastChunkSize = enumData.Length % enumData.ChunkSize;
                bool lastChunkEmpty = lastChunkSize == 0;
                if (chunkInvalid | (lastChunk & lastChunkEmpty))
                {
                    chunk = default;
                    chunkStart = 0;
                    return false;
                }
                int chunkSize = math.select(enumData.ChunkSize, lastChunkSize, lastChunk);
                chunkStart = enumData.CurrentChunkIdx * enumData.ChunkSize;
                chunk = enumData.DataBuffer.Slice(chunkStart, chunkSize);
                enumData.CurrentChunkIdx = math.select(enumData.ChunkBuffer[enumData.CurrentChunkIdx].NextIdx, INVALID_CHUNK_IDX, lastChunk);
                return true;
            }
            internal static void Reset(ref EnumData enumData)
            {
                enumData.CurrentChunkIdx = enumData.FirstChunkIdx;
            }
            internal static int JumpReturnLocalIndex(ref EnumData enumData, DataIndex dataIndex)
            {
                enumData.CurrentChunkIdx = dataIndex.Index / enumData.ChunkSize;
                return dataIndex.Index % enumData.ChunkSize;
            }
        }
        internal struct ListInfo
        {
            internal static readonly ListInfo DEFAULT = new ListInfo
            {
                FirstChunkIdx = INVALID_CHUNK_IDX,
                LastChunkIdx = INVALID_CHUNK_IDX,
                Capacity = 0,
                Length = 0,
                NextDataIdx = INVALID_DATA_IDX,
            };

            internal int Length;
            internal int Capacity;
            internal int NextDataIdx;
            internal int FirstChunkIdx;
            internal int LastChunkIdx;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal ulong Version_Dbg;
#endif
        }
        internal struct Chunk
        {
            internal int NextIdx;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal int ChunkStartLength_Dbg;
#endif

        }
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
        internal struct DebugInfo
        {
            internal const ulong DEFERRED_VERSION = ulong.MaxValue;
            internal ulong ListCountVersion;
            internal ulong ListAllocVersion;
            internal ulong HandleId;
            internal ulong ListId;
            internal bool Locked;
        }
        internal static void IncListCountVersion(NativeReference<DebugInfo> info)
        {
            DebugInfo val = info.Value;
            val.ListCountVersion++;
            info.Value = val;
        }
        internal static void IncListAllocVersion(NativeReference<DebugInfo> info)
        {
            DebugInfo val = info.Value;
            val.ListAllocVersion++;
            info.Value = val;
        }
        internal static void IncHandleId(NativeReference<DebugInfo> info)
        {
            DebugInfo val = info.Value;
            val.HandleId++;
            info.Value = val;
        }
        internal static void ReturnIncListId(NativeReference<DebugInfo> info, ref ulong id)
        {
            DebugInfo val = info.Value;
            id = val.ListId;
            val.ListId++;
            info.Value = val;
        }
        internal static void Lock(NativeReference<DebugInfo> info)
        {
            DebugInfo val = info.Value;
            val.Locked = true;
            info.Value = val;
        }
        internal static void Unlock(NativeReference<DebugInfo> info)
        {
            DebugInfo val = info.Value;
            val.Locked = false;
            info.Value = val;
        }
        internal static void IsUsable(ListOfChunkedLists<T> view)
        {
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }
        }
        internal static void IsUsable(ListWriter view)
        {
            if(view.DbgInfo.Value.ListCountVersion != view.ListCountVersionSnapshot & view.ListCountVersionSnapshot != DebugInfo.DEFERRED_VERSION)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ListWriter);
                throw new Exception($"{type_n}.{view_n} is invalidated due to the changes made to list count");
            }
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }
        }
        internal static void IsUsable(ListWriterNoResize view)
        {
            if (view.DbgInfo.Value.ListCountVersion != view.ListCountVersionSnapshot & view.ListCountVersionSnapshot != DebugInfo.DEFERRED_VERSION)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ListWriterNoResize);
                throw new Exception($"{type_n}.{view_n} is invalidated due to the changes made to list count");
            }
            if (view.DbgInfo.Value.ListAllocVersion != view.ListAllocVersionSnapshot & view.ListAllocVersionSnapshot != DebugInfo.DEFERRED_VERSION)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ListWriterNoResize);
                throw new Exception($"{type_n}.{view_n} is invalidated due to changes made to lists");
            }
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }
        }
        internal static void IsUsable(ParallelWriter view)
        {
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }
        }
        internal static void IsUsable(ReadOnly view)
        {
            if (view.DbgInfo.Value.ListCountVersion != view.ListCountVersionSnapshot & view.ListCountVersionSnapshot != DebugInfo.DEFERRED_VERSION)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ReadOnly);
                throw new Exception($"{type_n}.{view_n} is invalidated due to the changes made to list count");
            }
            if (view.DbgInfo.Value.ListAllocVersion != view.ListAllocVersionSnapshot & view.ListAllocVersionSnapshot != DebugInfo.DEFERRED_VERSION)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ReadOnly);
                throw new Exception($"{type_n}.{view_n} is invalidated due to changes made to lists");
            }
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }
        }
        internal static void IsUsable(ListHandle view)
        {
            if (view.DbgInfo.Value.HandleId != view.HandleId)
            {
                FixedString64Bytes view_n = nameof(ListHandle);
                throw new Exception($"{view_n} is disposed");
            }
        }
        internal static void IsUsable(ListHandleNoResize view)
        {
            if (view.DbgInfo.Value.HandleId != view.HandleId)
            {
                FixedString64Bytes view_n = nameof(ListHandleNoResize);
                throw new Exception($"{view_n} is disposed");
            }

        }
        internal static void IsUsable(RWEnumerator view)
        {
            if (view.DbgInfo.Value.ListCountVersion != view.ListCountVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(RWEnumerator);
                throw new Exception($"{type_n}.{view_n} is invalidated due to the changes made to list count");
            }
            if (view.DbgInfo.Value.ListAllocVersion != view.ListAllocVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(RWEnumerator);
                throw new Exception($"{type_n}.{view_n} is invalidated due to changes made to lists");
            }
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }

        }
        internal static void IsUsable(ROEnumerator view)
        {
            if (view.DbgInfo.Value.ListCountVersion != view.ListCountVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ROEnumerator);
                throw new Exception($"{type_n}.{view_n} is invalidated due to the changes made to list count");
            }
            if (view.DbgInfo.Value.ListAllocVersion != view.ListAllocVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ROEnumerator);
                throw new Exception($"{type_n}.{view_n} is invalidated due to changes made to lists");
            }
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }

        }
        internal static void IsUsable(RWChunk view)
        {
            if (view.DbgInfo.Value.ListCountVersion != view.ListCountVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(RWChunk);
                throw new Exception($"{type_n}.{view_n} is invalidated due to the changes made to list count");
            }
            if (view.DbgInfo.Value.ListAllocVersion != view.ListAllocVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(RWChunk);
                throw new Exception($"{type_n}.{view_n} is invalidated due to changes made to lists");
            }
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }
        }
        internal static void IsUsable(ROChunk view)
        {
            if (view.DbgInfo.Value.ListCountVersion != view.ListCountVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ROChunk);
                throw new Exception($"{type_n}.{view_n} is invalidated due to the changes made to list count");
            }
            if (view.DbgInfo.Value.ListAllocVersion != view.ListAllocVersionSnapshot)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes view_n = nameof(ROChunk);
                throw new Exception($"{type_n}.{view_n} is invalidated due to changes made to lists");
            }
            if (view.DbgInfo.Value.Locked)
            {
                FixedString64Bytes type_n = nameof(ListOfChunkedLists<T>);
                FixedString64Bytes handle1_n = nameof(ListHandle);
                FixedString64Bytes handle2_n = nameof(ListHandleNoResize);
                throw new Exception($"{type_n} is locked because a created {handle1_n} or {handle2_n} is not disposed");
            }
        }
        internal static void CheckListIndex(NativeList<ListInfo> listInfoBuffer, int listIndex)
        {
            if(listIndex < 0 | listIndex >= listInfoBuffer.Length)
            {
                throw new Exception($"List index ({listIndex}) is out of bounds [0, {listInfoBuffer.Length})");
            }
        }
        internal static void CheckListIndex(NativeArray<ListInfo> listInfoBuffer, int listIndex)
        {
            if (listIndex < 0 | listIndex >= listInfoBuffer.Length)
            {
                throw new Exception($"List index ({listIndex}) is out of bounds [0, {listInfoBuffer.Length})");
            }
        }
        internal static void CheckNoResize(ListInfo info)
        {
            if(info.Length == info.Capacity)
            {
                throw new Exception("List is full");
            }
        }
        internal static void CheckDataIndex(NativeList<ListInfo> listInfoBuffer, NativeList<Chunk> chunkBuffer, NativeList<T> dataBuffer, int chunkSize, DataIndex dataIndex)
        {
            if(dataIndex.Index < 0 | dataIndex.Index >= dataBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is out of bounds");
            }
            if(dataIndex.ListIndex_Dbg < 0 | dataIndex.ListIndex_Dbg >= listInfoBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to data of a disposed list. (DataIndex.ListIndex: {dataIndex.ListIndex_Dbg}, List Count: {listInfoBuffer.Length})");
            }
            ListInfo listInfo = listInfoBuffer[dataIndex.ListIndex_Dbg];
            if (dataIndex.ListVersion_Dbg != listInfo.Version_Dbg)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is disposed (Chunk no more belongs to the list that {type_n} is acquired from)");
            }
            Chunk chunk = chunkBuffer[dataIndex.Index / chunkSize];
            if (chunk.ChunkStartLength_Dbg + (dataIndex.Index % chunkSize) >= listInfo.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is not within the length of the list anymore.");
            }
        }
        internal static void CheckDataIndex(NativeArray<ListInfo> listInfoBuffer, NativeList<Chunk> chunkBuffer, NativeList<T> dataBuffer, int chunkSize, DataIndex dataIndex)
        {
            if(dataIndex.Index < 0 | dataIndex.Index >= dataBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is out of bounds");
            }
            if(dataIndex.ListIndex_Dbg < 0 | dataIndex.ListIndex_Dbg >= listInfoBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to data of a disposed list. (DataIndex.ListIndex: {dataIndex.ListIndex_Dbg}, List Count: {listInfoBuffer.Length})");
            }
            ListInfo listInfo = listInfoBuffer[dataIndex.ListIndex_Dbg];
            if (dataIndex.ListVersion_Dbg != listInfo.Version_Dbg)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is disposed (Chunk no more belongs to the list that {type_n} is acquired from)");
            }
            Chunk chunk = chunkBuffer[dataIndex.Index / chunkSize];
            if (chunk.ChunkStartLength_Dbg + (dataIndex.Index % chunkSize) >= listInfo.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is not within the length of the list anymore.");
            }
        }
        internal static void CheckDataIndex(NativeArray<ListInfo> listInfoBuffer, NativeArray<Chunk> chunkBuffer, NativeArray<T> dataBuffer, int chunkSize, DataIndex dataIndex)
        {
            if(dataIndex.Index < 0 | dataIndex.Index >= dataBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is out of bounds");
            }
            if(dataIndex.ListIndex_Dbg < 0 | dataIndex.ListIndex_Dbg >= listInfoBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to data of a disposed list. (DataIndex.ListIndex: {dataIndex.ListIndex_Dbg}, List Count: {listInfoBuffer.Length})");
            }
            ListInfo listInfo = listInfoBuffer[dataIndex.ListIndex_Dbg];
            if (dataIndex.ListVersion_Dbg != listInfo.Version_Dbg)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is disposed (Chunk no more belongs to the list that {type_n} is acquired from)");
            }
            Chunk chunk = chunkBuffer[dataIndex.Index / chunkSize];
            if (chunk.ChunkStartLength_Dbg + (dataIndex.Index % chunkSize) >= listInfo.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is not within the length of the list anymore.");
            }
        }

        internal static void CheckDataIndex(ROEnumerator enumerator, DataIndex dataIndex)
        {
            NativeArray<Chunk> chunkBuffer = enumerator.EnumData.ChunkBuffer;
            NativeArray<T> dataBuffer = enumerator.EnumData.DataBuffer; 
            int chunkSize = enumerator.EnumData.ChunkSize;
            int listIndex = enumerator.ListIndex_Dbg;
            ulong listVersion = enumerator.ListVersion_Dbg;
            int listLength = enumerator.EnumData.Length;

            if (dataIndex.Index < 0 | dataIndex.Index >= dataBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is out of bounds");
            }
            if(dataIndex.ListIndex_Dbg != listIndex | dataIndex.ListVersion_Dbg != listVersion)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                FixedString64Bytes enumerator_n = nameof(ROEnumerator);
                throw new Exception($"{type_n} does not point to a data of the list that {enumerator_n} enumerates");
            }
            Chunk chunk = chunkBuffer[dataIndex.Index / chunkSize];
            if (chunk.ChunkStartLength_Dbg + (dataIndex.Index % chunkSize) >= listIndex)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                FixedString64Bytes enumerator_n = nameof(ROEnumerator);
                throw new Exception($"{type_n} points to a data that is not within range of the {enumerator_n}");
            }
        }

        internal static void CheckDataIndex(RWEnumerator enumerator, DataIndex dataIndex)
        {
            NativeArray<Chunk> chunkBuffer = enumerator.EnumData.ChunkBuffer;
            NativeArray<T> dataBuffer = enumerator.EnumData.DataBuffer; 
            int chunkSize = enumerator.EnumData.ChunkSize;
            int listIndex = enumerator.ListIndex_Dbg;
            ulong listVersion = enumerator.ListVersion_Dbg;
            int listLength = enumerator.EnumData.Length;

            if (dataIndex.Index < 0 | dataIndex.Index >= dataBuffer.Length)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                throw new Exception($"{type_n} is pointing to a chunk that is out of bounds");
            }
            if(dataIndex.ListIndex_Dbg != listIndex | dataIndex.ListVersion_Dbg != listVersion)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                FixedString64Bytes enumerator_n = nameof(RWEnumerator);
                throw new Exception($"{type_n} does not point to a data of the list that {enumerator_n} enumerates");
            }
            Chunk chunk = chunkBuffer[dataIndex.Index / chunkSize];
            if (chunk.ChunkStartLength_Dbg + (dataIndex.Index % chunkSize) >= listIndex)
            {
                FixedString64Bytes type_n = nameof(DataIndex);
                FixedString64Bytes enumerator_n = nameof(RWEnumerator);
                throw new Exception($"{type_n} points to a data that is not within range of the {enumerator_n}");
            }
        }
        internal static void CheckChunkLocalIndex(NativeSlice<T> chunkSlice, int localIdx)
        {
            if (localIdx < 0 | localIdx >= chunkSlice.Length)
            {
                throw new Exception($"Chunk local index ({localIdx}) is out of bounds [0, {chunkSlice.Length})");
            }
        }
#endif
    }
}
