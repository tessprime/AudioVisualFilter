namespace AudioVisualFilter.DataStructures
{
    class RingBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head = 0;
        private int _count = 0;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public RingBuffer(int capacity)
        {
            _buffer = new T[capacity];
        }

        public void Write(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException();
                int start = _count < Capacity ? 0 : _head;
                return _buffer[(start + index) % Capacity];
            }
        }
    }
}
