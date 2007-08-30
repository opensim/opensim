using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ITerrain
    {
        bool Tainted();
        bool Tainted(int x, int y);
        void ResetTaint();
        void ModifyTerrain(float height, float seconds, byte brushsize, byte action, float north, float west, IClientAPI remoteUser);
        void CheckHeightValues();
        float[] GetHeights1D();
        float[,] GetHeights2D();
        double[,] GetHeights2DD();
        void GetHeights1D(float[] heights);
        void SetHeights2D(float[,] heights);
        void SetHeights2D(double[,] heights);
        void SwapRevertMaps();
        void SaveRevertMap();
        bool RunTerrainCmd(string[] args, ref string resultText, string simName);
        void SetRange(float min, float max);
        void LoadFromFileF64(string filename);
        void LoadFromFileF32(string filename);
        void LoadFromFileF32(string filename, int dimensionX, int dimensionY, int lowerboundX, int lowerboundY);
        void LoadFromFileIMG(string filename, int dimensionX, int dimensionY, int lowerboundX, int lowerboundY);
        void LoadFromFileSLRAW(string filename);
        void WriteToFileF64(string filename);
        void WriteToFileF32(string filename);
        void WriteToFileRAW(string filename);
        void WriteToFileHiRAW(string filename);
        void SetSeed(int val);
        void RaiseTerrain(double rx, double ry, double size, double amount);
        void LowerTerrain(double rx, double ry, double size, double amount);
        void FlattenTerrain(double rx, double ry, double size, double amount);
        void NoiseTerrain(double rx, double ry, double size, double amount);
        void RevertTerrain(double rx, double ry, double size, double amount);
        void SmoothTerrain(double rx, double ry, double size, double amount);
        void HillsGenerator();
        double GetHeight(int x, int y);
        void ExportImage(string filename, string gradientmap);
        byte[] ExportJpegImage(string gradientmap);
    }
}
