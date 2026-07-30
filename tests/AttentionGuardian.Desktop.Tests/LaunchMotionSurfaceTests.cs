using AttentionGuardian.Desktop.Views;

namespace AttentionGuardian.Desktop.Tests;

public sealed class LaunchMotionSurfaceTests
{
    [Fact]
    public void LaunchShader_Compiles()
    {
        Assert.Null(LaunchMotionSurface.GetShaderCompilationError());
    }

    [Fact]
    public void GuardianLettering_LoadsTheImmutableFinalOutline()
    {
        Assert.Equal(1, LaunchMotionSurface.GetLetteringGeometryCount());
        Assert.True(LaunchMotionSurface.GetLetteringContourCount() > 1);
    }

    [Fact]
    public void GuardianLettering_PreservesTheFinalSvgBytes()
    {
        Assert.Equal(
            "7140DE6A319D7AE59599E16E28B78AC1586BC4045190137692D0C0A9042ABE72",
            LaunchMotionSurface.GetLetteringResourceSha256());
    }

    [Fact]
    public void GuardianLettering_UsesEightReferenceDrawingStages()
    {
        Assert.Equal(8, LaunchMotionSurface.GetLetteringStageCount());
    }

    [Fact]
    public void GuardianLettering_FirstGGestureIsContinuousAndComplete()
    {
        Assert.InRange(
            LaunchMotionSurface.GetLetteringStageMaximumJump(0),
            0,
            12);
        Assert.InRange(
            LaunchMotionSurface.GetLetteringOutlineCoverageRatio(),
            0.9999,
            1.0001);
    }

    [Fact]
    public void GuardianLettering_IsCenteredWithinTheWindow()
    {
        var mappedBounds =
            LaunchMotionSurface.GetLetteringMappedBounds(1280, 800);

        Assert.InRange(Math.Abs(mappedBounds.Center.X - 640), 0, 0.01);
        Assert.InRange(Math.Abs(mappedBounds.Center.Y - 400), 0, 0.01);
    }
}
