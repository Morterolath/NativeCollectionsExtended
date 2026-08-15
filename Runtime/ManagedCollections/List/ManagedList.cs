using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace ManagedCollectionsExtended
{
    public class ManagedList<T>
    {
        public ref struct Span
        {
            internal Span<T> m_Data;
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal ulong m_VersionSnapshot_Dbg;
            internal ManagedList<T> m_VersionOriginal_Dbg;
#endif

            internal Span(ManagedList<T> list)
            {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.SnapshotVersion(list, out m_VersionSnapshot_Dbg, out m_VersionOriginal_Dbg);
#endif
                m_Data = new Span<T>(list.m_Data, 0, list.m_Length);
            }
            public int Length
            {
                get
                {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.VersionMustBeSame(m_VersionSnapshot_Dbg, m_VersionOriginal_Dbg);
#endif
                    return m_Data.Length;
                }
            }
            public T this[int index]
            {
                get
                {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.VersionMustBeSame(m_VersionSnapshot_Dbg, m_VersionOriginal_Dbg);
                    SafetyCheckHelper.IndexMustBeWithinBounds(index, m_Data.Length);
#endif
                    return m_Data[index];
                }
                set
                {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.VersionMustBeSame(m_VersionSnapshot_Dbg, m_VersionOriginal_Dbg);
                    SafetyCheckHelper.IndexMustBeWithinBounds(index, m_Data.Length);
#endif
                    m_Data[index] = value;
                }
            }
        }
        public ref struct ReadOnlySpan
        {
            internal ReadOnlySpan<T> m_Data;
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            internal ulong m_VersionSnapshot_Dbg;
            internal ManagedList<T> m_VersionOriginal_Dbg;
#endif

            internal ReadOnlySpan(ManagedList<T> list)
            {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.SnapshotVersion(list, out m_VersionSnapshot_Dbg, out m_VersionOriginal_Dbg);
#endif
                m_Data = new ReadOnlySpan<T>(list.m_Data, 0, list.m_Length);
            }
            public int Length
            {
                get
                {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.VersionMustBeSame(m_VersionSnapshot_Dbg, m_VersionOriginal_Dbg);
#endif
                    return m_Data.Length;
                }
            }
            public T this[int index]
            {
                get
                {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                    SafetyCheckHelper.VersionMustBeSame(m_VersionSnapshot_Dbg, m_VersionOriginal_Dbg);
                    SafetyCheckHelper.IndexMustBeWithinBounds(index, m_Data.Length);
#endif
                    return m_Data[index];
                }
            }
        }

        internal T[] m_Data;
        internal int m_Length;
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
        internal ulong m_Version_Dbg;
#endif

        public ManagedList(int capacity = 32)
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.CapacityMustBeNonNegative(capacity);
            SafetyCheckHelper.CreateVersion(out m_Version_Dbg);
#endif
            capacity = math.ceilpow2(capacity);

            m_Data = new T[capacity];
            m_Length = 0;
        }

        public T this[int index]
        {
            get
            {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.IndexMustBeWithinBounds(index, m_Length);
#endif
                return m_Data[index]; 
            }
            set
            {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
                SafetyCheckHelper.IndexMustBeWithinBounds(index, m_Length);
#endif
                m_Data[index] = value;
            }
        }
        public int Length
        {
            get
            {
                return m_Length; 
            }
        }
        public void Resize(int length)
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.LengthMustBeNonNegative(length);
            SafetyCheckHelper.IncVersion(ref m_Version_Dbg);
#endif
            if (length < m_Length)
            {
                m_Length = length;
                return;
            }
            if(length > m_Data.Length)
            {
                GrowTo(length);
            }
            for(int i = m_Length; i < length; i++)
                m_Data[i] = default;
            m_Length = length;
        }
        public void ResizeUninitialized(int length)
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.LengthMustBeNonNegative(length);
            SafetyCheckHelper.IncVersion(ref m_Version_Dbg);
#endif
            if (length < m_Length)
            {
                m_Length = length;
                return;
            }
            if (length > m_Data.Length)
            {
                GrowTo(length);
            }
            m_Length = length;
        }
        public void SetRange(int start, int length, T value)
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.RangeMustBeWithinBounds(start, length, m_Length);
#endif
            for (int i = start; i < length; i++)
                m_Data[i] = value;
        }
        public void Add(T value)
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.IncVersion(ref m_Version_Dbg);
#endif
            if (m_Data.Length == m_Length)
                Grow();
            m_Data[m_Length] = value;
            m_Length++;
        }
        public void RemoveAtSwapBack(int index)
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.IndexMustBeWithinBounds(index, m_Length);
            SafetyCheckHelper.IncVersion(ref m_Version_Dbg);
#endif
            m_Data[index] = m_Data[m_Length - 1];
            m_Length--;
        }
        public void Clear()
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.IncVersion(ref m_Version_Dbg);
#endif
            m_Length = 0;
        }
        public ReadOnlySpan AsReadOnlySpan()
        {
            return new ReadOnlySpan(this);
        }
        public Span AsSpan()
        {
            return new Span(this);
        }
        void Grow()
        {
            int capacity = m_Data.Length;
            int newCapacity = math.select(capacity * 2, 1, capacity == 0);
            T[] newData = new T[newCapacity];
            for (int i = 0; i < m_Length; i++)
                newData[i] = m_Data[i];
            m_Data = newData;
        }

        void GrowTo(int newCapacity)
        {
#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
            SafetyCheckHelper.NewCapacityMustBeGreater(newCapacity, m_Data);
#endif
            int capacity = m_Data.Length;
            newCapacity = math.ceilpow2(newCapacity);
            T[] newData = new T[newCapacity];
            for (int i = 0; i < m_Length; i++)
                newData[i] = m_Data[i];
            m_Data = newData;
        }

#if NATIVE_COLLECTIONS_EXTENDED_DEBUG
        struct SafetyCheckHelper
        {
            internal static void IndexMustBeWithinBounds(int index, int length)
            {
                if(index < 0 | index >= length)
                {
                    throw new Exception($"Index {index} must be within bounds (0, {length})");
                }
            }
            internal static void RangeMustBeWithinBounds(int start, int length, int listLength)
            {
                if(length < 0)
                {
                    throw new Exception($"Range length ({length}) can not be <0");
                }
                if(start < 0)
                {
                    throw new Exception($"Range start ({start}) can not be  <0");
                }
                if((start + length) > listLength)
                {
                    throw new Exception($"Range ({start}, {length}) must be withing bounds (0, {listLength})");
                }
            }
            internal static void NewCapacityMustBeGreater(int newCapacity, T[] data)
            {
                if(newCapacity <= data.Length)
                {
                    throw new Exception($"New Capacity {newCapacity} must be greater than capacity {data.Length}");
                }
            }
            internal static void LengthMustBeNonNegative(int length)
            {
                if(length < 0)
                {
                    throw new Exception($"Length {length} can not be negative");
                }
            }
            internal static void CapacityMustBeNonNegative(int capacaity)
            {
                if(capacaity < 0)
                {
                    throw new Exception($"Capacity {capacaity} can not be negative");
                }
            }
            internal static void IncVersion(ref ulong version)
            {
                version++;
            }
            internal static void VersionMustBeSame(ulong version, ManagedList<T> original)
            {
                if(version != original.m_Version_Dbg)
                {
                    throw new Exception($"Due to interactions with original data structure {nameof(ManagedList<T>)} this view is invalidated");
                }
            }
            internal static void CreateVersion(out ulong version)
            {
                version = 0;
            }
            internal static void SnapshotVersion(ManagedList<T> list, out ulong snapshotVersion, out ManagedList<T> originalVersion)
            {
                snapshotVersion = list.m_Version_Dbg;
                originalVersion = list;
            }
        }
#endif
    }
}
