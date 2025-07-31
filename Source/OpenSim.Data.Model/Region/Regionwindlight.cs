using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class Regionwindlight
{
    public string RegionId { get; set; }

    public float WaterColorR { get; set; }

    public float WaterColorG { get; set; }

    public float WaterColorB { get; set; }

    public float WaterFogDensityExponent { get; set; }

    public float UnderwaterFogModifier { get; set; }

    public float ReflectionWaveletScale1 { get; set; }

    public float ReflectionWaveletScale2 { get; set; }

    public float ReflectionWaveletScale3 { get; set; }

    public float FresnelScale { get; set; }

    public float FresnelOffset { get; set; }

    public float RefractScaleAbove { get; set; }

    public float RefractScaleBelow { get; set; }

    public float BlurMultiplier { get; set; }

    public float BigWaveDirectionX { get; set; }

    public float BigWaveDirectionY { get; set; }

    public float LittleWaveDirectionX { get; set; }

    public float LittleWaveDirectionY { get; set; }

    public string NormalMapTexture { get; set; }

    public float HorizonR { get; set; }

    public float HorizonG { get; set; }

    public float HorizonB { get; set; }

    public float HorizonI { get; set; }

    public float HazeHorizon { get; set; }

    public float BlueDensityR { get; set; }

    public float BlueDensityG { get; set; }

    public float BlueDensityB { get; set; }

    public float BlueDensityI { get; set; }

    public float HazeDensity { get; set; }

    public float DensityMultiplier { get; set; }

    public float DistanceMultiplier { get; set; }

    public uint MaxAltitude { get; set; }

    public float SunMoonColorR { get; set; }

    public float SunMoonColorG { get; set; }

    public float SunMoonColorB { get; set; }

    public float SunMoonColorI { get; set; }

    public float SunMoonPosition { get; set; }

    public float AmbientR { get; set; }

    public float AmbientG { get; set; }

    public float AmbientB { get; set; }

    public float AmbientI { get; set; }

    public float EastAngle { get; set; }

    public float SunGlowFocus { get; set; }

    public float SunGlowSize { get; set; }

    public float SceneGamma { get; set; }

    public float StarBrightness { get; set; }

    public float CloudColorR { get; set; }

    public float CloudColorG { get; set; }

    public float CloudColorB { get; set; }

    public float CloudColorI { get; set; }

    public float CloudX { get; set; }

    public float CloudY { get; set; }

    public float CloudDensity { get; set; }

    public float CloudCoverage { get; set; }

    public float CloudScale { get; set; }

    public float CloudDetailX { get; set; }

    public float CloudDetailY { get; set; }

    public float CloudDetailDensity { get; set; }

    public float CloudScrollX { get; set; }

    public byte CloudScrollXLock { get; set; }

    public float CloudScrollY { get; set; }

    public byte CloudScrollYLock { get; set; }

    public byte DrawClassicClouds { get; set; }
}
