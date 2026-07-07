namespace NativeCollectionsExtended
{
    internal struct ListPointer
    {
        internal static ListPointer NULL { get { return default(ListPointer); } }
        internal int StartIndex;
        internal int Length;
        internal int Capacity;

        internal ListPointer(int startIndex, int length, int capacity)
        {
            StartIndex = startIndex;
            Length = length;
            Capacity = capacity;
        }
        internal bool IsNull()
        {
            return Capacity == NULL.Capacity;
        }
        internal bool Equals(ListPointer rhs)
        {
            return StartIndex == rhs.StartIndex && Length == rhs.Length && Capacity == rhs.Capacity;
        }
    }
}
