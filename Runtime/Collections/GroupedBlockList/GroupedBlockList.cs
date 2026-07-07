using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace NativeCollectionsExtended
{
    internal struct GroupedBlockList<T>
        where T : unmanaged
    {
        struct Group
        {
            internal int FirstBlockIndex;
            internal int LastBlockIndex;
        }
        public readonly int BlockSize;
        NativeList<Group> _groups;
        NativeList<T> _blockData;
        NativeList<int> _nextBlockIndexEachBlock;
        NativeList<int> _unusedBlockIndicies;

        //Min block size can not be less than 2
        const int MIN_BLOCK_SIZE = 16;
        public GroupedBlockList(int blockSize, Allocator allocator)
        {
            _groups = new NativeList<Group>(allocator);
            _blockData = new NativeList<T>(allocator);
            _nextBlockIndexEachBlock = new NativeList<int>(allocator);
            _unusedBlockIndicies = new NativeList<int>(allocator);

            blockSize = math.max(MIN_BLOCK_SIZE, blockSize);
            BlockSize = blockSize;
            _blockData.Length += blockSize;
            _nextBlockIndexEachBlock.Length += 1;
        }

        public int GroupLength()
        {
            return _groups.Length;
        }
        public int BlockCount()
        {
            return _blockData.Length / BlockSize;
        }
        public void AddGroup()
        {
            _groups.Add(default);
        }
        public int AddBlock(int groupIndex)
        {
            Group gr = _groups[groupIndex];
            gr = AllocateNextBlockFor(gr);
            InitializeBlock(gr.LastBlockIndex);
            _groups[groupIndex] = gr;
            return gr.LastBlockIndex;
        }
        public void RemoveGroup(int groupIndex)
        {
            Group group = _groups[groupIndex];
            _groups[groupIndex] = default;
            DeallocateGroup(group);
        }
        void InitializeBlock(int blockIndex)
        {
            for (int i = blockIndex * BlockSize; i < blockIndex * BlockSize + BlockSize; i++)
            {
                _blockData[i] = default;
            }
        }
        Group AllocateNextBlockFor(Group group)
        {
            bool lastBlockDoesNotExist = group.LastBlockIndex == 0;
            int newBlockIndex = AllocateBlock();
            _nextBlockIndexEachBlock[group.LastBlockIndex] = math.select(newBlockIndex, 0, lastBlockDoesNotExist);
            group.FirstBlockIndex = math.select(group.FirstBlockIndex, newBlockIndex, lastBlockDoesNotExist);
            group.LastBlockIndex = newBlockIndex;
            return group;
        }
        int AllocateBlock()
        {
            if (_unusedBlockIndicies.IsEmpty)
            {
                int newBlockIndex = _blockData.Length / BlockSize;
                _blockData.Length += BlockSize;
                _nextBlockIndexEachBlock.Add(0);
                return newBlockIndex;
            }
            else
            {
                int unusedListLastIndex = _unusedBlockIndicies.Length - 1;
                int newBlockIndex = _unusedBlockIndicies[unusedListLastIndex];
                int nextOfNewBlock = _nextBlockIndexEachBlock[newBlockIndex];
                _nextBlockIndexEachBlock[newBlockIndex] = 0;
                _unusedBlockIndicies[unusedListLastIndex] = nextOfNewBlock;
                _unusedBlockIndicies.Length -= math.select(0, 1, nextOfNewBlock == 0);
                return newBlockIndex;
            }
        }
        void DeallocateGroup(Group group)
        {
            _unusedBlockIndicies.Add(group.FirstBlockIndex);
            _unusedBlockIndicies.Length -= math.select(0, 1, group.FirstBlockIndex == 0);
        }
    }
}
