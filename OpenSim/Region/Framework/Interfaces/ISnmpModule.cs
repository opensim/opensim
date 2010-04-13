///////////////////////////////////////////////////////////////////
//
// (c) Careminster LImited, Melanie Thielker and the Meta7 Team
//
// This file is not open source. All rights reserved
// Mod 2
public interface ISnmpModule
{
    void Trap(int code,string simname,string Message);
    void ColdStart(int step , string simname);
}
