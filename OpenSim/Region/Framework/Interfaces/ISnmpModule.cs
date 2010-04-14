///////////////////////////////////////////////////////////////////
//
// (c) Careminster LImited, Melanie Thielker and the Meta7 Team
//
// This file is not open source. All rights reserved
// Mod 2

using OpenSim.Region.Framework.Scenes;

public interface ISnmpModule
{
    void Trap(int code, string Message, Scene scene);
    void Critical(string Message, Scene scene);
    void Warning(string Message, Scene scene);
    void Major(string Message, Scene scene);
    void ColdStart(int step , Scene scene);
    void Shutdown(int step , Scene scene);
}
