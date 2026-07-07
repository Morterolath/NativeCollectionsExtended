using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace NativeCollectionsExtended
{
    public struct NativeMaxIntHeap<T> where T : unmanaged
    {
        internal NativeList<HeapElement<T>> _array;
        public T this[int index]
        {
            get
            {
                return _array[index].data;
            }
        }
        public bool IsEmpty
        {
            get
            {
                return _array.IsEmpty;
            }
        }
        public int Length
        {
            get { return _array.Length; }
        }
        public NativeMaxIntHeap(int size, Allocator allocator)
        {
            _array = new NativeList<HeapElement<T>>(size, allocator);
        }
        public void Clear()
        {
            _array.Clear();
        }
        public void Enqueue(T element, int pri)
        {
            int elementIndex = _array.Length;
            _array.Add(new HeapElement<T>(element, pri));
            if (elementIndex != 0)
            {
                HeapifyUp(elementIndex);
            }
        }
        public T GetMax() => _array[0].data;
        public int GetMaxPriority() => _array[0].pri;
        public T Dequeue()
        {
            T max = _array[0].data;
            HeapElement<T> last = _array[_array.Length - 1];
            _array[0] = last;
            _array.Length--;
            if (_array.Length > 1)
            {
                HeapifyDown(0);
            }
            return max;
        }
        public T Dequeue(out int priority)
        {
            HeapElement<T> maxElement = _array[0];
            T max = maxElement.data;
            HeapElement<T> last = _array[_array.Length - 1];
            _array[0] = last;
            _array.Length--;
            if (_array.Length > 1)
            {
                HeapifyDown(0);
            }
            priority = maxElement.pri;
            return max;
        }
        public void Dispose()
        {
            _array.Dispose();
        }
        public NativeArray<HeapElement<T>>.ReadOnly AsReaonly()
        {
            return _array.AsReadOnly();
        }
        void HeapifyUp(int startIndex)
        {
            int curIndex = startIndex;
            int parIndex = (curIndex - 1) / 2;
            HeapElement<T> cur = _array[startIndex];
            HeapElement<T> par = _array[parIndex];
            bool isCurBigger = cur.pri > par.pri;
            while (isCurBigger)
            {
                _array[parIndex] = cur;
                _array[curIndex] = par;
                curIndex = parIndex;
                parIndex = math.select((curIndex - 1) / 2, 0, curIndex == 0);
                par = _array[parIndex];
                isCurBigger = cur.pri > par.pri;
            }
        }
        void HeapifyDown(int startIndex)
        {
            int length = _array.Length;
            int curIndex = startIndex;
            int lcIndex = startIndex * 2 + 1;
            int rcIndex = lcIndex + 1;
            lcIndex = math.select(curIndex, lcIndex, lcIndex < length);
            rcIndex = math.select(curIndex, rcIndex, rcIndex < length);
            HeapElement<T> cur;
            HeapElement<T> lc;
            HeapElement<T> rc;
            while (lcIndex != curIndex)
            {
                cur = _array[curIndex];
                lc = _array[lcIndex];
                rc = _array[rcIndex];
                bool lcBiggerThanRc = lc.pri > rc.pri;
                bool lcBiggerThanCur = lc.pri > cur.pri;
                bool rcBiggerThanCur = rc.pri > cur.pri;

                if (lcBiggerThanRc && lcBiggerThanCur)
                {
                    _array[curIndex] = lc;
                    _array[lcIndex] = cur;
                    curIndex = lcIndex;
                    lcIndex = curIndex * 2 + 1;
                    rcIndex = lcIndex + 1;
                    lcIndex = math.select(lcIndex, curIndex, lcIndex >= length);
                    rcIndex = math.select(rcIndex, curIndex, rcIndex >= length);
                }
                else if (!lcBiggerThanRc && rcBiggerThanCur)
                {
                    _array[curIndex] = rc;
                    _array[rcIndex] = cur;
                    curIndex = rcIndex;
                    lcIndex = curIndex * 2 + 1;
                    rcIndex = lcIndex + 1;
                    lcIndex = math.select(lcIndex, curIndex, lcIndex >= length);
                    rcIndex = math.select(rcIndex, curIndex, rcIndex >= length);
                }
                else
                {
                    break;
                }
            }
        }
        public struct HeapElement<T> where T : unmanaged
        {
            public T data;
            public int pri;

            internal HeapElement(T data, int pri)
            {
                this.data = data;
                this.pri = pri;
            }
        }
    }
}
