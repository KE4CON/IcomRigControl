using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

public class WaterfallFrequencyMapperTests
{
    [Fact]
    public void PixelToFrequency_CenterPixel_ReturnsCenterFrequency()
    {
        // Center frequency 14074000 Hz, 50 kHz span, 475 data points
        long result = WaterfallFrequencyMapper.PixelToFrequency(
            pixelX: 237, // roughly the middle of 0-474
            dataPointCount: 475,
            centerFrequencyHz: 14_074_000,
            spanHz: 50_000);

        Assert.InRange(result, 14_073_950, 14_074_050);
    }

    [Fact]
    public void PixelToFrequency_LeftEdge_ReturnsLowestFrequency()
    {
        long result = WaterfallFrequencyMapper.PixelToFrequency(
            pixelX: 0,
            dataPointCount: 475,
            centerFrequencyHz: 14_074_000,
            spanHz: 50_000);

        // Left edge should be roughly centerFreq - span/2
        Assert.InRange(result, 14_048_900, 14_049_100);
    }

    [Fact]
    public void PixelToFrequency_RightEdge_ReturnsHighestFrequency()
    {
        long result = WaterfallFrequencyMapper.PixelToFrequency(
            pixelX: 474,
            dataPointCount: 475,
            centerFrequencyHz: 14_074_000,
            spanHz: 50_000);

        // Right edge should be roughly centerFreq + span/2
        Assert.InRange(result, 14_098_900, 14_099_100);
    }

    [Fact]
    public void GenerateAxisLabels_ReturnsCorrectCount()
    {
        var labels = WaterfallFrequencyMapper.GenerateAxisLabels(
            centerFrequencyHz: 14_074_000,
            spanHz: 50_000,
            labelCount: 5);

        Assert.Equal(5, labels.Count);
    }

    [Fact]
    public void GenerateAxisLabels_MiddleLabelIsCenterFrequency()
    {
        var labels = WaterfallFrequencyMapper.GenerateAxisLabels(
            centerFrequencyHz: 14_074_000,
            spanHz: 50_000,
            labelCount: 5);

        // Middle label (index 2 of 5) should be the center frequency
        Assert.InRange(labels[2].FrequencyHz, 14_073_900, 14_074_100);
    }

    [Fact]
    public void GenerateAxisLabels_LabelsAreEvenlySpaced()
    {
        var labels = WaterfallFrequencyMapper.GenerateAxisLabels(
            centerFrequencyHz: 14_074_000,
            spanHz: 50_000,
            labelCount: 5);

        long spacing1 = labels[1].FrequencyHz - labels[0].FrequencyHz;
        long spacing2 = labels[2].FrequencyHz - labels[1].FrequencyHz;

        Assert.InRange(Math.Abs(spacing1 - spacing2), 0, 100);
    }

    [Fact]
    public void FormatFrequencyLabel_FormatsAsMHz()
    {
        string label = WaterfallFrequencyMapper.FormatFrequencyLabel(14_074_000);
        Assert.Contains("14.074", label);
    }
}