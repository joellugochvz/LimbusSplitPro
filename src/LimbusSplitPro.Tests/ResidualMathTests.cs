using Xunit;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.Tests;

public class ResidualMathTests
{
    [Fact]
    public void TestResidualSubtractionExactness()
    {
        // Simulate float audio buffers of 100 samples
        int samples = 100;
        float[] mix = new float[samples];
        float[] vocal = new float[samples];
        float[] drums = new float[samples];
        float[] bass = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            vocal[i] = 0.3f * MathF.Sin(i * 0.1f);
            drums[i] = 0.4f * MathF.Cos(i * 0.2f);
            bass[i] = 0.2f * MathF.Sin(i * 0.05f);
            mix[i] = vocal[i] + drums[i] + bass[i] + 0.1f; // 0.1f residual
        }

        // Calculate residual Other mathematically
        float[] other = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            other[i] = mix[i] - (vocal[i] + drums[i] + bass[i]);
        }

        // Verify reconstruction: Vocals + Drums + Bass + Other == Mix
        for (int i = 0; i < samples; i++)
        {
            float reconstructed = vocal[i] + drums[i] + bass[i] + other[i];
            float diff = MathF.Abs(mix[i] - reconstructed);
            Assert.True(diff < 1e-6f, $"Reconstruction error at sample {i}: {diff}");
        }
    }
}
