namespace NativeCollectionsExtended
{
    internal struct Block
    {
        internal int BlockStart;
        internal int BlockLength;
        internal bool IsFree;

        internal Block(int blockStart, int blockLength, bool isFree)
        {
            BlockStart = blockStart;
            BlockLength = blockLength;
            IsFree = isFree;
        }
        internal bool Equals(Block rhs)
        {
            return BlockStart == rhs.BlockStart && BlockLength == rhs.BlockLength && IsFree == rhs.IsFree;
        }
    }
}
