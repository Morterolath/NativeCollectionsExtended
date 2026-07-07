using Unity.Mathematics;

namespace NativeCollectionsExtended.UnitTest
{
    internal static class  TestUtils
    {
        internal static Unity.Mathematics.Random GetRandom(uint seed1, uint seed2, uint seed3)
        {
            Unity.Mathematics.Random rng_iterationCount = new Unity.Mathematics.Random(seed1);
            Unity.Mathematics.Random rng_maxIterationCount = new Unity.Mathematics.Random(seed2);
            int iterationCount1 = rng_maxIterationCount.NextInt(0, 1000);
            int iterationCount2 = rng_maxIterationCount.NextInt(0, 1000);
            int minIterationCount = math.min(iterationCount1, iterationCount2);
            int maxIterationCount = math.max(iterationCount1, iterationCount2);
            int iterationCount = rng_iterationCount.NextInt(minIterationCount, maxIterationCount);
            uint outputSeed = 1;
            Unity.Mathematics.Random rng_outputSeed = new Unity.Mathematics.Random(outputSeed);
            for (int i = 0; i < iterationCount; i++)
            {
                outputSeed = rng_outputSeed.NextUInt();
            }
            return new Unity.Mathematics.Random(outputSeed);
        }
        internal static void SetMinMax(int v1, int v2, out int min, out int max)
        {
            min = math.min(v1, v2);
            max = math.max(v1, v2);
        }
    }
}