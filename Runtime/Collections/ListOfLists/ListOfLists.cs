using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct ListOfLists<T>
        where T : unmanaged
    {
        [NoAlias] internal NativeList<ListInfo> ListInfoBuffer;
        [NoAlias] internal SLSFAllocator<T> Allocator;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
        internal NativeReference<DebugInfo> DbgInfo;
#endif
        public ListOfLists(Allocator allocator)
        {
            ListInfoBuffer = new NativeList<ListInfo>(allocator);
            Allocator = new SLSFAllocator<T>(allocator);
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            DbgInfo = new NativeReference<DebugInfo>(allocator);
#endif
        }
        public void Dispose()
        {
            ListInfoBuffer.Dispose();
            Allocator.Dispose();
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            DbgInfo.Dispose();
#endif
        }
        public ListSlice this[int listIndex]
        {
            get
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif

                ListInfo info = ListInfoBuffer[listIndex];
                return new ListSlice
                {
                    InternalSlice = Allocator.DataBuffer.AsArray().Slice(info.Start, info.Length),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                    ListCountVersion = DbgInfo.Value.ListCountVersion,
                    ListLengthVersion = DbgInfo.Value.ListLengthVersion,
#endif
                };
            }
        }
        public T this[int listIndex, int dataIndex]
        {
            get
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif

                return Allocator.DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex];
            }
            set
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif

                Allocator.DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex] = value;
            }
        }
        public int GetListCount()
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
#endif

            return ListInfoBuffer.Length;
        }
        public void SetListCount(int newCount, int initCapacity = 32)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListCountVersion(DbgInfo);
            SafetyCheckHelper.CheckListCount(newCount);
#endif

            int oldCount = ListInfoBuffer.Length;
            if(oldCount < newCount)
            {
                ListInfoBuffer.Length = newCount;
                NativeArray<ListInfo> listInfoBuffer_array = ListInfoBuffer.AsArray();
                for (int i = oldCount; i < newCount; i++)
                {
                    ListInfo info = ListInfo.INVALID;
                    ListWriteHelper.SetCapacity(ref info, initCapacity, Allocator);
                    listInfoBuffer_array[i] = info;
                }
            }
            else if(oldCount > newCount)
            {
                NativeArray<ListInfo> listInfoBuffer_array = ListInfoBuffer.AsArray();
                for (int i = newCount; i < oldCount; i++)
                {
                    ListInfo info = listInfoBuffer_array[i];
                    if (ListWriteHelper.IsListAllocated(in info))
                        ListWriteHelper.DisposeListNoWriteBack(in info, Allocator);
                }
                ListInfoBuffer.Length = newCount;
            }
        }
        public void SetListCountNoAlloc(int newCount)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListCountVersion(DbgInfo);
            SafetyCheckHelper.CheckListCount(newCount);
#endif
            int oldCount = ListInfoBuffer.Length;
            if(oldCount < newCount)
            {
                ListInfoBuffer.Length = newCount;
                NativeArray<ListInfo> listInfoBuffer_array = ListInfoBuffer.AsArray();
                for (int i = oldCount; i < newCount; i++)
                    listInfoBuffer_array[i] = ListInfo.INVALID;
            }
            else if(oldCount > newCount)
            {
                NativeArray<ListInfo> listInfoBuffer_array = ListInfoBuffer.AsArray();
                for (int i = newCount; i < oldCount; i++)
                {
                    ListInfo info = listInfoBuffer_array[i];
                    if(ListWriteHelper.IsListAllocated(in info))
                        ListWriteHelper.DisposeListNoWriteBack(in info, Allocator);
                }
                ListInfoBuffer.Length = newCount;
            }
        }
        public int GetListLength(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
            return ListInfoBuffer[listIndex].Length;
        }
        public int GetListCapaciy(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
            return ListInfoBuffer[listIndex].Capacity;
        }
        public void SetListCapacityGreater(int listIndex, int greaterCapacity)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
            SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.SetListCapacityGreater(ref info, greaterCapacity, Allocator);
            ListInfoBuffer[listIndex] = info;
        }
        public void SetListLength(int listIndex, int newLength)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListAllocVersion(DbgInfo);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
            SafetyCheckHelper.CheckNewListLength(newLength);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.SetListLength(ref info, newLength, Allocator);
            ListInfoBuffer[listIndex] = info;
        }
        public void SetListLengthUninitialized(int listIndex, int newLength)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListAllocVersion(DbgInfo);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
            SafetyCheckHelper.CheckNewListLength(newLength);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.SetListLengthUninitialized(ref info, newLength, Allocator);
            ListInfoBuffer[listIndex] = info;
        }
        public void ClearList(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
            SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.ClearList(ref info);
            ListInfoBuffer[listIndex] = info;
        }
        public void AddList(int initCapacity = 32)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListCountVersion(DbgInfo);
#endif
            ListInfo info = ListInfo.INVALID;
            ListWriteHelper.SetCapacity(ref info, initCapacity, Allocator);
            ListInfoBuffer.Add(info);
        }
        public void AllocateList(int listIndex, int initCapacity = 32)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListAllocVersion(DbgInfo);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListDisposed(ListInfoBuffer[listIndex]);
#endif
            ListInfo info = ListInfo.INVALID;
            ListWriteHelper.SetCapacity(ref info, initCapacity, Allocator);
            ListInfoBuffer[listIndex] = info;
        }
        public void DisposeList(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListAllocVersion(DbgInfo);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.DisposeList(ref info, Allocator);
            ListInfoBuffer[listIndex] = info;
        }
        public bool IsListAllocated(int listIndex)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            return ListWriteHelper.IsListAllocated(in info);
        }
        public void AddToList(int listIndex, T data)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.IncListAllocVersion(DbgInfo);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.AddToList(ref info, Allocator, data);
            ListInfoBuffer[listIndex] = info;
        }
        public void AddToListNoResize(int listIndex, T data)
        {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CheckUsable(this);
            SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
            SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
            SafetyCheckHelper.CheckNoResize(ListInfoBuffer[listIndex]);
            SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
            ListInfo info = ListInfoBuffer[listIndex];
            ListWriteHelper.AddToListNoResize(ref info, Allocator.DataBuffer, data);
            ListInfoBuffer[listIndex] = info;
        }
        public ReadOnly AsReadOnly()
        {
            return new ReadOnly
            {
                ListInfoBuffer = ListInfoBuffer.AsArray(),
                DataBuffer = Allocator.DataBuffer.AsArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                ListCountVersion = DbgInfo.Value.ListCountVersion,
#endif
            };
        }
        public ReadOnly AsDeferredReadOnly()
        {
            return new ReadOnly
            {
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
                DataBuffer = Allocator.DataBuffer.AsDeferredJobArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListAllocVersion = DebugInfo.DEFERRED_VERSION,
                ListCountVersion = DebugInfo.DEFERRED_VERSION,
#endif
            };
        }
        public ListReadWrite AsListReadWrite()
        {
            return new ListReadWrite
            {
                ListInfoBuffer = ListInfoBuffer.AsArray(),
                Allocator = Allocator,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersion = DbgInfo.Value.ListCountVersion,
#endif
            };
        }
        public ListReadWrite AsDeferredListReadWrite()
        {
            return new ListReadWrite
            {
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
                Allocator = Allocator,
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersion = DebugInfo.DEFERRED_VERSION,
#endif
            };
        }
        public ListReadWriteNoResize AsListReadWriteNoResize()
        {
            return new ListReadWriteNoResize
            {
                ListInfoBuffer = ListInfoBuffer.AsArray(),
                DataBuffer = Allocator.DataBuffer.AsArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersion = DbgInfo.Value.ListCountVersion,
                ListAllocVersion = DbgInfo.Value.ListAllocVersion,
#endif
            };
        }
        public ListReadWriteNoResize AsDeferredListReadWriteNoResize()
        {
            return new ListReadWriteNoResize
            {
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
                DataBuffer = Allocator.DataBuffer.AsDeferredJobArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
                ListCountVersion = DebugInfo.DEFERRED_VERSION,
                ListAllocVersion = DebugInfo.DEFERRED_VERSION,
#endif
            };
        }
        public ParallelReadWrite AsParallelReadWrite()
        {
            return new ParallelReadWrite
            {
                DataBuffer = Allocator.DataBuffer.AsDeferredJobArray(),
                ListInfoBuffer = ListInfoBuffer.AsDeferredJobArray(),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                DbgInfo = DbgInfo,
#endif
            };
        }
        public struct ReadOnly
        {
            [NoAlias] internal NativeArray<ListInfo> ListInfoBuffer;
            [NoAlias] internal NativeArray<T> DataBuffer;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal uint ListCountVersion;
            internal uint ListAllocVersion;
#endif
            public T this[int listIndex, int dataIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                    SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif
                    return DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex];
                }
            }
            public ListSliceReadOnly this[int listIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                    ListInfo info = ListInfoBuffer[listIndex];
                    return new ListSliceReadOnly
                    {
                        InternalSlice = DataBuffer.Slice(info.Start, info.Length),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                        DbgInfo = DbgInfo,
                        ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                        ListCountVersion = DbgInfo.Value.ListCountVersion,
                        ListLengthVersion = DbgInfo.Value.ListLengthVersion,
#endif
                    };
                }
            }
            public int GetListCount()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfoBuffer.Length;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public int GetListCapaciy(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public bool IsListAllocated(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return ListWriteHelper.IsListAllocated(in info);
            }
        }
        public struct ListReadWrite
        {
            [NoAlias] internal NativeArray<ListInfo> ListInfoBuffer;
            [NoAlias] internal SLSFAllocator<T> Allocator;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal uint ListCountVersion;
#endif
            public ListSlice this[int listIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                    ListInfo info = ListInfoBuffer[listIndex];
                    return new ListSlice
                    {
                        InternalSlice = Allocator.DataBuffer.AsArray().Slice(info.Start, info.Length),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                        DbgInfo = DbgInfo,
                        ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                        ListCountVersion = DbgInfo.Value.ListCountVersion,
                        ListLengthVersion = DbgInfo.Value.ListLengthVersion,
#endif
                    };
                }
            }
            public T this[int listIndex, int dataIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                    SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif
                    return Allocator.DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex];
                }
                set
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                    SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif
                    Allocator.DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex] = value;
                }
            }
            public int GetListCount()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfoBuffer.Length;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public int GetListCapaciy(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public void SetListCapacityGreater(int listIndex, int greaterCapacity)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.SetListCapacityGreater(ref info, greaterCapacity, Allocator);
                ListInfoBuffer[listIndex] = info;
            }
            public void SetListLength(int listIndex, int newLength)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckNewListLength(newLength);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.SetListLength(ref info, newLength, Allocator);
                ListInfoBuffer[listIndex] = info;
            }
            public void SetListLengthUninitialized(int listIndex, int newLength)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckNewListLength(newLength);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.SetListLengthUninitialized(ref info, newLength, Allocator);
                ListInfoBuffer[listIndex] = info;
            }
            public void ClearList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.ClearList(ref info);
                ListInfoBuffer[listIndex] = info;
            }
            public void AllocateList(int listIndex, int initCapacity = 32)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListDisposed(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfo.INVALID;
                ListWriteHelper.SetCapacity(ref info, initCapacity, Allocator);
                ListInfoBuffer[listIndex] = info;
            }
            public void DisposeList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.DisposeList(ref info, Allocator);
                ListInfoBuffer[listIndex] = info;
            }
            public bool IsListAllocated(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return ListWriteHelper.IsListAllocated(in info);
            }
            public void AddToList(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToList(ref info, Allocator, data);
                ListInfoBuffer[listIndex] = info;
            }
            public void AddToListNoResize(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckNoResize(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToListNoResize(ref info, Allocator.DataBuffer, data);
                ListInfoBuffer[listIndex] = info;
            }
            public ListHandle GetListHandle(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.GetHandle(this);
#endif
                return new ListHandle
                {
                    Allocator = Allocator,
                    ListInfoBuffer = ListInfoBuffer,
                    ListIndex = listIndex,
                    ListInfo = ListInfoBuffer[listIndex],
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    HandleId = DbgInfo.Value.HandleId,
                    HandleVersion = DbgInfo.Value.HandleVersion,
#endif
                };
            }
        }
        public struct ListReadWriteNoResize
        {
            [NoAlias] internal NativeArray<ListInfo> ListInfoBuffer;
            [NoAlias] internal NativeArray<T> DataBuffer;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal uint ListCountVersion;
            internal uint ListAllocVersion;
#endif
            public ListSlice this[int listIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                    ListInfo info = ListInfoBuffer[listIndex];
                    return new ListSlice
                    {
                        InternalSlice = DataBuffer.Slice(info.Start, info.Length),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                        DbgInfo = DbgInfo,
                        ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                        ListCountVersion = DbgInfo.Value.ListCountVersion,
                        ListLengthVersion = DbgInfo.Value.ListLengthVersion,
#endif
                    };
                }
            }
            public T this[int listIndex, int dataIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                    SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif
                    return DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex];
                }
                set
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                    SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif
                    DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex] = value;
                }
            }
            public int GetListCount()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfoBuffer.Length;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public int GetListCapaciy(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public void ClearList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.ClearList(ref info);
                ListInfoBuffer[listIndex] = info;
            }
            public bool IsListAllocated(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return ListWriteHelper.IsListAllocated(in info);
            }
            public void AddToListNoResize(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckNoResize(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToListNoResize(ref info, DataBuffer, data);
                ListInfoBuffer[listIndex] = info;
            }
            public ListHandleNoResize GetListHandle(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.GetHandle(this);
#endif
                return new ListHandleNoResize
                {
                    DataBuffer = DataBuffer,
                    ListInfoBuffer = ListInfoBuffer,
                    ListIndex = listIndex,
                    ListInfo = ListInfoBuffer[listIndex],
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    HandleId = DbgInfo.Value.HandleId,
                    HandleVersion = DbgInfo.Value.HandleVersion,
#endif
                };
            }
        }
        public struct ParallelReadWrite
        {
            [NoAlias] internal NativeArray<ListInfo> ListInfoBuffer;
            [NoAlias, NativeDisableParallelForRestriction] internal NativeArray<T> DataBuffer;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
#endif
            public ListSlice this[int listIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                    ListInfo info = ListInfoBuffer[listIndex];
                    return new ListSlice
                    {
                        InternalSlice = DataBuffer.Slice(info.Start, info.Length),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                        DbgInfo = DbgInfo,
                        ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                        ListCountVersion = DbgInfo.Value.ListCountVersion,
                        ListLengthVersion = DbgInfo.Value.ListLengthVersion,
#endif
                    };
                }
            }
            public T this[int listIndex, int dataIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                    SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif
                    return DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex];
                }
                set
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                    SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                    SafetyCheckHelper.CheckDataIndex(ListInfoBuffer[listIndex], dataIndex);
#endif
                    DataBuffer[ListInfoBuffer[listIndex].Start + dataIndex] = value;
                }
            }
            public int GetListCount()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfoBuffer.Length;
            }
            public int GetListLength(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Length;
            }
            public int GetListCapaciy(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                return ListInfoBuffer[listIndex].Capacity;
            }
            public void SetListLengthNoResize(int listIndex, int newLength)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckNewListLength(newLength);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.SetListLengthNoResize(ref info, newLength, DataBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public void SetListLengthNoResizeUninitialized(int listIndex, int newLength)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckNewListLength(newLength);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.SetListLengthNoResizeUninitialized(ref info, newLength);
                ListInfoBuffer[listIndex] = info;
            }
            public void ClearList(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.ClearList(ref info);
                ListInfoBuffer[listIndex] = info;
            }
            public bool IsListAllocated(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                return ListWriteHelper.IsListAllocated(in info);
            }
            public void AddToListNoResize(int listIndex, T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckNoResize(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.AddToListNoResize(ref info, DataBuffer, data);
                ListInfoBuffer[listIndex] = info;
            }
            public void RemoveAtSwapBack(int listIndex, int dataIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.RemoveAtSwapBack(ref info, dataIndex, DataBuffer);
                ListInfoBuffer[listIndex] = info;
            }
            public void RemoveLast(int listIndex)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.CheckListIndex(ListInfoBuffer, listIndex);
                SafetyCheckHelper.CheckListAllocated(ListInfoBuffer[listIndex]);
                SafetyCheckHelper.CheckListNotEmpty(ListInfoBuffer[listIndex]);
#endif
                ListInfo info = ListInfoBuffer[listIndex];
                ListWriteHelper.RemoveLast(ref info);
                ListInfoBuffer[listIndex] = info;
            }
        }
        public struct ListHandle : IDisposable
        {
            internal ListInfo ListInfo;
            internal int ListIndex;
            [NoAlias] internal NativeArray<ListInfo> ListInfoBuffer;
            [NoAlias] internal SLSFAllocator<T> Allocator;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal uint HandleId;
            internal uint HandleVersion;
#endif
            public T this[int dataIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckDataIndex(ListInfo, dataIndex);
#endif
                    return Allocator.DataBuffer[ListInfo.Start + dataIndex];
                }
                set
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckDataIndex(ListInfo, dataIndex);
#endif
                    Allocator.DataBuffer[ListInfo.Start + dataIndex] = value;
                }
            }
            public ListSlice GetSlice()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return new ListSlice
                {
                    InternalSlice = Allocator.DataBuffer.AsArray().Slice(ListInfo.Start, ListInfo.Length),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                    ListCountVersion = DbgInfo.Value.ListCountVersion,
                    ListLengthVersion = DbgInfo.Value.ListLengthVersion,
#endif
                };
            }
            public int GetListLength()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfo.Length;
            }
            public int GetListCapaciy()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfo.Capacity;
            }
            public void SetListCapacityGreater(int greaterCapacity)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListWriteHelper.SetListCapacityGreater(ref ListInfo, greaterCapacity, Allocator);
            }
            public void SetListLength(int newLength)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListWriteHelper.SetListLength(ref ListInfo, newLength, Allocator);
            }
            public void SetListLengthUninitialized(int newLength)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListWriteHelper.SetListLengthUninitialized(ref ListInfo, newLength, Allocator);
            }
            public void ClearList()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListWriteHelper.ClearList(ref ListInfo);
            }
            public void AddToList(T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListAllocVersion(DbgInfo);
#endif
                ListWriteHelper.AddToList(ref ListInfo, Allocator, data);
            }
            public void AddToListNoResize(T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListWriteHelper.AddToListNoResize(ref ListInfo, Allocator.DataBuffer, data);
            }

            public void Dispose()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.ReleaseHandle(this);
#endif
                ListInfoBuffer[ListIndex] = ListInfo;
            }
        }
        public struct ListHandleNoResize : IDisposable
        {
            internal ListInfo ListInfo;
            internal int ListIndex;
            [NoAlias] internal NativeArray<ListInfo> ListInfoBuffer;
            [NoAlias] internal NativeArray<T> DataBuffer;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal uint HandleId;
            internal uint HandleVersion;
#endif
            public T this[int dataIndex]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckDataIndex(ListInfo, dataIndex);
#endif
                    return DataBuffer[ListInfo.Start + dataIndex];
                }
                set
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckDataIndex(ListInfo, dataIndex);
#endif
                    DataBuffer[ListInfo.Start + dataIndex] = value;
                }
            }
            public ListSlice GetSlice()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return new ListSlice
                {
                    InternalSlice = DataBuffer.Slice(ListInfo.Start, ListInfo.Length),
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    DbgInfo = DbgInfo,
                    ListAllocVersion = DbgInfo.Value.ListAllocVersion,
                    ListCountVersion = DbgInfo.Value.ListCountVersion,
                    ListLengthVersion = DbgInfo.Value.ListLengthVersion,
#endif
                };
            }
            public int GetListLength()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfo.Length;
            }
            public int GetListCapaciy()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return ListInfo.Capacity;
            }
            public void ClearList()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListWriteHelper.ClearList(ref ListInfo);
            }
            public void AddToListNoResize(T data)
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.IncHandleVersion(ref this);
                SafetyCheckHelper.IncListLengthVersion(DbgInfo);
#endif
                ListWriteHelper.AddToListNoResize(ref ListInfo, DataBuffer, data);
            }

            public void Dispose()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
                SafetyCheckHelper.ReleaseHandle(this);
#endif
                ListInfoBuffer[ListIndex] = ListInfo;
            }
        }
        public struct ListSlice
        {
            internal NativeSlice<T> InternalSlice;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo>.ReadOnly DbgInfo;
            internal uint ListCountVersion;
            internal uint ListAllocVersion;
            internal uint ListLengthVersion;
#endif
            public int Length
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
#endif
                    return InternalSlice.Length;
                }
            }
            public T this[int index]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckSliceLocalIndex(InternalSlice, index);
#endif
                    return InternalSlice[index];
                }
                set
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckSliceLocalIndex(InternalSlice, index);
#endif
                    InternalSlice[index] = value;
                }
            }
            public NativeSlice<T> GetInternalSlice()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return InternalSlice;
            }
        }
        public struct ListSliceReadOnly
        {
            internal NativeSlice<T> InternalSlice;
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal NativeReference<DebugInfo> DbgInfo;
            internal uint ListCountVersion;
            internal uint ListAllocVersion;
            internal uint ListLengthVersion;
#endif
            public int Length
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
#endif
                    return InternalSlice.Length;
                }
            }
            public T this[int index]
            {
                get
                {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.CheckUsable(this);
                    SafetyCheckHelper.CheckSliceLocalIndex(InternalSlice, index);
#endif
                    return InternalSlice[index];
                }
            }
            public NativeSliceReadOnly<T> GetInternalSlice()
            {
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.CheckUsable(this);
#endif
                return new NativeSliceReadOnly<T>(InternalSlice);
            }
        }
        internal struct ListWriteHelper
        {
            internal static void SetListCapacityGreater(ref ListInfo info, int greaterCapacity, SLSFAllocator<T> allocator)
            {
                if (info.Capacity >= greaterCapacity)
                    return;
                SetCapacity(ref info, greaterCapacity, allocator);
            }
            internal static void SetListLength(ref ListInfo info, int newLength, SLSFAllocator<T> allocator)
            {
                if(newLength <= info.Length)
                {
                    info.Length = newLength;
                    return;
                }

                if(newLength > info.Capacity)
                    SetCapacity(ref info, newLength, allocator);

                NativeSlice<T> data_array = allocator.DataBuffer.AsArray().Slice(info.Start, newLength);
                for (int i = info.Length; i < newLength; i++)
                    data_array[i] = default;
                info.Length = newLength;
            }
            internal static void SetListLengthUninitialized(ref ListInfo info, int newLength, SLSFAllocator<T> allocator)
            {
                if(newLength <= info.Length)
                {
                    info.Length = newLength;
                    return;
                }

                if(newLength > info.Capacity)
                    SetCapacity(ref info, newLength, allocator);
                info.Length = newLength;
            }
            internal static void SetListLengthNoResize(ref ListInfo info, int newLength, NativeArray<T> dataBuffer)
            {
                NativeSlice<T> data_array = dataBuffer.Slice(info.Start, newLength);
                for (int i = info.Length; i < newLength; i++)
                    data_array[i] = default;
                info.Length = newLength;
            }
            internal static void SetListLengthNoResizeUninitialized(ref ListInfo info, int newLength)
            {
                info.Length = newLength;
            }
            internal static void ClearList(ref ListInfo info)
            {
                info.Length = 0;
            }
            internal static void AddToList(ref ListInfo info, SLSFAllocator<T> allocator, T data)
            {
                if(info.Length == info.Capacity)
                {
                    SetCapacity(ref info, info.Capacity * 2, allocator);
                }
                allocator.DataBuffer[info.Start + info.Length] = data;
                info.Length++;
            }
            internal static void AddToListNoResize(ref ListInfo info, NativeList<T> dataBuffer, T data)
            {
                dataBuffer[info.Start + info.Length] = data;
                info.Length++;
            }
            internal static void AddToListNoResize(ref ListInfo info, NativeArray<T> dataBuffer, T data)
            {
                dataBuffer[info.Start + info.Length] = data;
                info.Length++;
            }
            internal static bool IsListAllocated(in ListInfo info)
            {
                return info.AllocId != SLSFAllocator<T>.INVALID_ALLOC_ID;
            }
            internal static void DisposeList(ref ListInfo info, SLSFAllocator<T> allocator)
            {
                allocator.Deallocate(info.AllocId);
                info = ListInfo.INVALID;
            }
            internal static void DisposeListNoWriteBack(in ListInfo info, SLSFAllocator<T> allocator)
            {
                allocator.Deallocate(info.AllocId);
            }
            internal static void SetCapacity(ref ListInfo info, int capacity, SLSFAllocator<T> allocator)
            {
                capacity = 1 << math.ceillog2(math.max(capacity, 2));
                allocator.Allocate(capacity, out int newStart, out int newCapacity, out int newAllocId);
                NativeSlice<T> oldSlice = allocator.DataBuffer.AsArray().Slice(info.Start, info.Length);
                NativeSlice<T> newSlice = allocator.DataBuffer.AsArray().Slice(newStart, info.Length);
                newSlice.CopyFrom(oldSlice);
                if (info.AllocId != SLSFAllocator<T>.INVALID_ALLOC_ID)
                    allocator.Deallocate(info.AllocId);
                info.Start = newStart;
                info.Capacity = newCapacity;
                info.AllocId = newAllocId;
            }
            internal static void RemoveAtSwapBack(ref ListInfo info, int index, NativeArray<T> dataBuffer)
            {
                dataBuffer[info.Start + index] = dataBuffer[info.Start + info.Length - 1];
                info.Length--;
            }
            internal static void RemoveLast(ref ListInfo info)
            {
                info.Length--;
            }
        }
        internal struct ListInfo
        {
            internal static readonly ListInfo INVALID = new ListInfo
            {
                AllocId = SLSFAllocator<T>.INVALID_ALLOC_ID,
                Start = 0,
                Capacity = 0,
                Length = 0,
            };
            internal int Start;
            internal int Length;
            internal int Capacity;
            internal int AllocId;
        }
#if DEBUG && NATIVE_COLLECTIONS_EXTENDED_DEBUG
        internal struct DebugInfo
        {
            internal const uint DEFERRED_VERSION = uint.MaxValue;
            internal uint ListCountVersion;
            internal uint ListAllocVersion;
            internal uint ListLengthVersion;
            internal uint HandleId;
            internal uint HandleVersion;
        }
        struct SafetyCheckHelper
        {
            internal static void CheckUsable(ListOfLists<T> view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if(info.HandleVersion != 0)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes handle_n = nameof(ListHandle);
                    throw new Exception($"{type_n} can not be used because an acquired {handle_n} is not disposed yet");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ReadOnly view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleVersion != 0)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ReadOnly);
                    FixedString64Bytes handle_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view_n} can not be used because an acquired {handle_n} is not disposed yet");
                }
                if(info.ListCountVersion != view.ListCountVersion & view.ListCountVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ReadOnly);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list count");
                }
                if(info.ListAllocVersion != view.ListAllocVersion & view.ListAllocVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ReadOnly);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list data");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ListReadWrite view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleVersion != 0)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListReadWrite);
                    FixedString64Bytes handle_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view_n} can not be used because an acquired {handle_n} is not disposed yet");
                }
                if (info.ListCountVersion != view.ListCountVersion & view.ListCountVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListReadWrite);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list count");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ListReadWriteNoResize view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleVersion != 0)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListReadWriteNoResize);
                    FixedString64Bytes handle_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view_n} can not be used because an acquired {handle_n} is not disposed yet");
                }
                if (info.ListCountVersion != view.ListCountVersion & view.ListCountVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListReadWriteNoResize);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list count");
                }
                if (info.ListAllocVersion != view.ListAllocVersion & view.ListAllocVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListReadWriteNoResize);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list data");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ParallelReadWrite view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleVersion != 0)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ParallelReadWrite);
                    FixedString64Bytes handle_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view_n} can not be used because an acquired {handle_n} is not disposed yet");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ListHandle view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleId != view.HandleId)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view} can not be used because it is disposed");
                }
                if (info.HandleVersion != view.HandleVersion)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view} can not be used because this instance of the struct is out-dated. (You made some changes using another instance of the struct)");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ListHandleNoResize view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleId != view.HandleId)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListHandleNoResize);
                    throw new Exception($"{type_n}.{view} can not be used because it is disposed");
                }
                if (info.HandleVersion != view.HandleVersion)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListHandleNoResize);
                    throw new Exception($"{type_n}.{view} can not be used because this instance of the struct is out-dated. (You made some changes using another instance of the struct)");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ListSlice view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleVersion != 0)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSlice);
                    FixedString64Bytes handle_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view_n} can not be used because an acquired {handle_n} is not disposed yet");
                }
                if (info.ListCountVersion != view.ListCountVersion & view.ListCountVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSlice);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list count");
                }
                if (info.ListAllocVersion != view.ListAllocVersion & view.ListAllocVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSlice);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list data");
                }
                if (info.ListLengthVersion != view.ListLengthVersion)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSlice);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list data");
                }
            }
            internal static void CheckUsable(ListOfLists<T>.ListSliceReadOnly view)
            {
                DebugInfo info = view.DbgInfo.Value;
                if (info.HandleVersion != 0)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSliceReadOnly);
                    FixedString64Bytes handle_n = nameof(ListHandle);
                    throw new Exception($"{type_n}.{view_n} can not be used because an acquired {handle_n} is not disposed yet");
                }
                if (info.ListCountVersion != view.ListCountVersion & view.ListCountVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSliceReadOnly);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list count");
                }
                if (info.ListAllocVersion != view.ListAllocVersion & view.ListAllocVersion != DebugInfo.DEFERRED_VERSION)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSliceReadOnly);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list data");
                }
                if (info.ListLengthVersion != view.ListLengthVersion)
                {
                    FixedString64Bytes type_n = nameof(ListOfLists<T>);
                    FixedString64Bytes view_n = nameof(ListSliceReadOnly);
                    throw new Exception($"{type_n}.{view} is invalidated to to the changes made to list data");
                }
            }
            internal static void IncListCountVersion(NativeReference<DebugInfo> dbgInfo)
            {
                DebugInfo info = dbgInfo.Value;
                info.ListCountVersion++;
                dbgInfo.Value = info;
            }
            internal static void IncListAllocVersion(NativeReference<DebugInfo> dbgInfo)
            {
                DebugInfo info = dbgInfo.Value;
                info.ListAllocVersion++;
                dbgInfo.Value = info;
            }
            internal static void IncListLengthVersion(NativeReference<DebugInfo> dbgInfo)
            {
                DebugInfo info = dbgInfo.Value;
                info.ListLengthVersion++;
                dbgInfo.Value = info;
            }
            internal static void GetHandle(ListOfLists<T>.ListReadWrite view)
            {
                DebugInfo info = view.DbgInfo.Value;
                info.HandleVersion++;
                view.DbgInfo.Value = info;
            }
            internal static void GetHandle(ListOfLists<T>.ListReadWriteNoResize view)
            {
                DebugInfo info = view.DbgInfo.Value;
                info.HandleVersion++;
                view.DbgInfo.Value = info;
            }
            internal static void ReleaseHandle(ListOfLists<T>.ListHandle view)
            {
                DebugInfo info = view.DbgInfo.Value;
                info.HandleId++;
                info.HandleVersion = 0;
                view.DbgInfo.Value = info;
            }
            internal static void ReleaseHandle(ListOfLists<T>.ListHandleNoResize view)
            {
                DebugInfo info = view.DbgInfo.Value;
                info.HandleId++;
                info.HandleVersion = 0;
                view.DbgInfo.Value = info;
            }
            internal static void IncHandleVersion(ref ListOfLists<T>.ListHandle view)
            {
                DebugInfo info = view.DbgInfo.Value;
                info.HandleVersion++;
                view.DbgInfo.Value = info;
                view.HandleVersion = info.HandleVersion;
            }
            internal static void IncHandleVersion(ref ListOfLists<T>.ListHandleNoResize view)
            {
                DebugInfo info = view.DbgInfo.Value;
                info.HandleVersion++;
                view.DbgInfo.Value = info;
                view.HandleVersion = info.HandleVersion;
            }
            internal static void CheckListIndex(NativeList<ListInfo> listInfoBuffer, int listIndex)
            {
                if(listIndex < 0 | listIndex >= listInfoBuffer.Length)
                {
                    throw new Exception($"List index ({listIndex}) is out of bounds (0, {listInfoBuffer.Length})");
                }
            }
            internal static void CheckListIndex(NativeArray<ListInfo> listInfoBuffer, int listIndex)
            {
                if (listIndex < 0 | listIndex >= listInfoBuffer.Length)
                {
                    throw new Exception($"List index ({listIndex}) is out of bounds (0, {listInfoBuffer.Length})");
                }
            }
            internal static void CheckDataIndex(ListInfo listInfo, int dataIndex)
            {
                if (dataIndex < 0 | dataIndex >= listInfo.Length)
                {
                    throw new Exception($"Data index ({dataIndex}) is out of bounds (0, {listInfo.Length})");
                }
            }
            internal static void CheckSliceLocalIndex(NativeSlice<T> slice, int localIndex)
            {
                if (localIndex < 0 | localIndex >= slice.Length)
                {
                    throw new Exception($"Index ({localIndex}) is out of bounds (0, {slice.Length})");
                }
            }
            internal static void CheckListCount(int listCount)
            {
                if(listCount < 0)
                {
                    throw new Exception($"List count ({listCount}) can not be less than zero");
                }
            }
            internal static void CheckNewListLength(int listLength)
            {
                if (listLength < 0)
                {
                    throw new Exception($"List length ({listLength}) can not be less than zero");
                }
            }
            internal static void CheckListAllocated(ListInfo info)
            {
                if (info.AllocId == SLSFAllocator<T>.INVALID_ALLOC_ID)
                {
                    throw new Exception($"List is not allocated");
                }
            }
            internal static void CheckListDisposed(ListInfo info)
            {
                if (info.AllocId != SLSFAllocator<T>.INVALID_ALLOC_ID)
                {
                    throw new Exception($"List is not disposed");
                }
            }
            internal static void CheckNoResize(ListInfo info)
            {
                if(info.Length == info.Capacity)
                {
                    throw new Exception($"List is full (Length: {info.Length}, Capacity: {info.Capacity})");
                }
            }
            internal static void CheckListNotEmpty(ListInfo info)
            {
                if(info.Length == 0)
                {
                    throw new Exception("List is empty");
                }
            }
        }
#endif
    }
}
