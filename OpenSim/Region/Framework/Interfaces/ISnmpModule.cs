///////////////////////////////////////////////////////////////////
//
// (c) Careminster LImited, Melanie Thielker and the Meta7 Team
//
// This file is not open source. All rights reserved
//
public interface ISnmpModule
{
    void Alert(string message);
    void Trap(string message);
}
