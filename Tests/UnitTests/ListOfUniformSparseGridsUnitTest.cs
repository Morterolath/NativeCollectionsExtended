using NativeCollectionsExtended;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

namespace NativeCollectionsExtended.UnitTest
{
    public class ListOfUniformSparseGridsUnitTest : MonoBehaviour
    {
        public bool Run;
        public bool Log;
        public int MinGridColAmount;
        public int MaxGridColAmount;
        public int MinGridRowAmount;
        public int MaxGridRowAmount;
        public int MinSubGridRowColAmount;
        public int MaxSubGridRowColAmount;
        public int MinAddCnt;
        public int MaxAddCnt;
        public int MinWriteCnt;
        public int MaxWriteCnt;
        public int MinDisposeCnt;
        public int MaxDisposeCnt;
        public int MinIterCnt;
        public int MaxIterCnt;

        private void Update()
        {
            MinGridColAmount = math.clamp(MinGridColAmount, 1, 10000);
            MaxGridColAmount = math.clamp(MaxGridColAmount, MinGridColAmount, 10000);
            MinGridRowAmount = math.clamp(MinGridRowAmount, 1, 10000);
            MaxGridRowAmount = math.clamp(MaxGridRowAmount, MinGridRowAmount, 10000);
            MinSubGridRowColAmount = math.clamp(MinSubGridRowColAmount, 1, 10000);
            MaxSubGridRowColAmount = math.clamp(MaxSubGridRowColAmount, MinSubGridRowColAmount, 10000);
            MinAddCnt = math.clamp(MinAddCnt, 0, 10000);
            MaxAddCnt = math.clamp(MaxAddCnt, MinAddCnt, 10000);
            MinWriteCnt = math.clamp(MinWriteCnt, 0, 10000);
            MaxWriteCnt = math.clamp(MaxWriteCnt, MinWriteCnt, 10000);
            MinDisposeCnt = math.clamp(MinDisposeCnt, 0, 10000);
            MaxDisposeCnt = math.clamp(MaxDisposeCnt, MinDisposeCnt, 10000);
            MinIterCnt = math.clamp(MinIterCnt, 0, 10000);
            MaxIterCnt = math.clamp(MaxIterCnt, MinIterCnt, 10000);

            if (!Run) return;
            Test(MinGridColAmount, MaxGridColAmount, MinGridRowAmount, MaxGridRowAmount, MinSubGridRowColAmount, MaxSubGridRowColAmount,
                MinAddCnt, MaxAddCnt, MinWriteCnt, MaxWriteCnt, MinDisposeCnt, MaxDisposeCnt, MinIterCnt, MaxIterCnt, Log);
        }


        static void Test(
            int minGridColAmount,
            int maxGridColAmount,
            int minGridRowAmount,
            int maxGridRowAmount,
            int minSubGridRowColAmount,
            int maxSubGridRowColAmount,
            int minAddCnt,
            int maxAddCnt,
            int minWriteCnt,
            int maxWriteCnt,
            int minDisposeCnt,
            int maxDisposeCnt,
            int minIterCnt,
            int maxIterCnt,
            bool log)
        {
            StringBuilder sb = new StringBuilder();
            int iterCnt = UnityEngine.Random.Range(minIterCnt, maxIterCnt);
            int gridColAmount = UnityEngine.Random.Range(minGridColAmount, maxGridColAmount);
            int gridRowAmount = UnityEngine.Random.Range(minGridRowAmount, maxGridRowAmount);
            int subGridRowColAmount = UnityEngine.Random.Range(minSubGridRowColAmount, maxSubGridRowColAmount);
            ListOfUniformSparseGrids<int> grids = new ListOfUniformSparseGrids<int>(Allocator.Temp, gridColAmount, gridRowAmount, subGridRowColAmount);
            List<int[]> grids_reference = new List<int[]>();

            sb.AppendLine("grid col amount: " + gridColAmount);
            sb.AppendLine("grid row amount: " + gridRowAmount);
            sb.AppendLine("iteration cnt: " + iterCnt);
            sb.AppendLine("subGridRowColAmount: " + subGridRowColAmount);
            for(int i = 0; i < iterCnt; i++)
            {
                AddGrids(grids, grids_reference, gridColAmount, gridRowAmount, minAddCnt, maxAddCnt, sb);
                WriteData(grids, grids_reference, gridColAmount, gridRowAmount, minWriteCnt, maxWriteCnt, sb);
                Dispose(grids, grids_reference, minDisposeCnt, maxDisposeCnt, sb);
            }

            bool success = TestResult(grids, grids_reference, gridColAmount, gridRowAmount, sb);

            if (!success)
                UnityEngine.Debug.Log("test failed");
            if (log)
                UnityEngine.Debug.Log(sb.ToString());
        }
        static void AddGrids(ListOfUniformSparseGrids<int> grids, List<int[]> grids_reference, int gridColAmount, int gridRowAmount,
            int minAddCount, int maxAddCount, StringBuilder sb)
        {
            int addCount = UnityEngine.Random.Range(minAddCount, maxAddCount);
            for (int i = 0; i < addCount; i++)
            {
                grids.AddGrid();
                grids_reference.Add(new int[gridRowAmount * gridColAmount]);
            }
            sb.AppendLine("grids added: " + addCount);
        }
        static void WriteData(ListOfUniformSparseGrids<int> grids, List<int[]> grids_reference, int gridColAmount, int gridRowAmount, 
            int minWriteCount, int maxWriteCount, StringBuilder sb)
        {
            if (grids.Length == 0) return;

            int writeCount = UnityEngine.Random.Range(minWriteCount, maxWriteCount);
            for(int i = 0; i < writeCount; i++)
            {
                int2 tile = new int2(UnityEngine.Random.Range(0, gridColAmount), UnityEngine.Random.Range(0, gridRowAmount));
                int gridIndex = UnityEngine.Random.Range(0, grids.Length);
                int value = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                grids.Write(gridIndex, tile, value);
                grids_reference[gridIndex][tile.y * gridColAmount + tile.x] = value;
            }
            sb.AppendLine("data written: " + writeCount);
        }
        static void Dispose(ListOfUniformSparseGrids<int> grids, List<int[]> grids_reference, int minDisposeCnt, int maxDisposeCnt, StringBuilder sb)
        {
            if (grids.Length == 0) return;

            int disposeCnt = UnityEngine.Random.Range(minDisposeCnt, maxDisposeCnt);
            for (int i = 0; i < disposeCnt; i++)
            {
                int gridIndex = UnityEngine.Random.Range(0, grids.Length);
                grids.DisposeGrid(gridIndex);
                int[] grid = grids_reference[gridIndex];
                for (int j = 0; j < grid.Length; j++) grid[j] = default;
            }
            sb.AppendLine("disposed: " + disposeCnt);
        }
        static bool TestResult(ListOfUniformSparseGrids<int> grids, List<int[]> grids_reference, int gridColAmount, int gridRowAmount,
            StringBuilder sb)
        {
            if (grids.Length != grids_reference.Count)
            {
                sb.AppendLine("grid count does not match");
                return false;
            }

            for(int i = 0; i < grids.Length; i++)
            {
                for(int y = 0; y < gridRowAmount; y++)
                {
                    for(int x = 0; x < gridColAmount; x++)
                    {
                        int2 tile = new int2(x, y);
                        int data = grids.Read(i, tile);
                        int data_reference = grids_reference[i][tile.y * gridColAmount + tile.x];
                        if(data != data_reference)
                        {
                            sb.AppendLine("content does not match");
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}