////////////////////////////////////////////////////////////////
//
// (c) 2009, 2010 Careminster Limited and Melanie Thielker
//
// All rights reserved
//
using System;
using Nini.Config;

namespace OpenSim.Services.Interfaces
{
    public interface IBakedTextureService
    {
        string Get(string id);
        void Store(string id, string data);
    }
}
