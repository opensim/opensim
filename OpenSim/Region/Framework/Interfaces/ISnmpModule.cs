///////////////////////////////////////////////////////////////////
//
// (c) Careminster LImited, Melanie Thielker and the Meta7 Team
//
// This file is not open source. All rights reserved
// Mod 2
public interface ISnmpModule
{
    void Alert(string message);
    void  Trap(int code,string simname,string Message);
}
