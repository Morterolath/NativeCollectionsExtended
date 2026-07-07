using Unity.Collections;
using Unity.Mathematics;

namespace NativeCollectionsExtended
{
    public struct LinearQueue<T>
        where T : unmanaged
    {
        public struct Writer
        {
            NativeReference<int> _originalStartIndex;
            NativeList<T> _originalQueueData;
            int _startIndex;
            internal Writer(NativeList<T> queueData, NativeReference<int> startIndex)
            {
                _originalQueueData = queueData;
                _originalStartIndex = startIndex;
                _startIndex = startIndex.Value;
            }
            public void Enqueue(T value)
            {
                _originalQueueData.Add(value);
            }
            public T Dequeue()
            {
                if (_startIndex == _originalQueueData.Length) return default;
                T data = _originalQueueData[_startIndex];
                _startIndex++;
                return data;
            }
            public bool IsEmpty()
            {
                return _startIndex == _originalQueueData.Length;
            }
            public void Clear()
            {
                _originalQueueData.Clear();
                _startIndex = 0;
            }
            public void SubmitChanges()
            {
                _originalStartIndex.Value = _startIndex;
            }

        }
        NativeReference<int> _startIndex;
        NativeList<T> _queueData;

        public LinearQueue(Allocator allocator)
        {
            _queueData = new NativeList<T>(allocator);
            _startIndex = new NativeReference<int>(0, allocator);
        }
        public void Enqueue(T value)
        {
            _queueData.Add(value);
        }
        public T Dequeue()
        {
            int start = _startIndex.Value;
            if (start == _queueData.Length) return default;
            T data = _queueData[start];
            _startIndex.Value = start + 1;
            return data;
        }
        public bool IsEmpty()
        {
            return _startIndex.Value >= _queueData.Length;
        }
        public void Clear()
        {
            _queueData.Clear();
            _startIndex.Value = 0;
        }
        public Writer AsWriter()
        {
            return new Writer(_queueData, _startIndex);
        }
    }
}
