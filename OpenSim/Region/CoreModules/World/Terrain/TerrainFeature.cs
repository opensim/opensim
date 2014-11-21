/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Reflection;

using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.World.Terrain
{
    public abstract class TerrainFeature : ITerrainFeature
    {
        protected ITerrainModule m_module;

        protected TerrainFeature(ITerrainModule module)
        {
            m_module = module;
        }

        public abstract string CreateFeature(ITerrainChannel map, string[] args);

        public abstract string GetUsage();

        protected string parseFloat(String s, out float f)
        {
            string result;
            double d;
            if (Double.TryParse(s, out d))
            {
                try
                {
                    f = (float)d;
                    result = String.Empty;
                }
                catch(InvalidCastException)
                {
                    result = String.Format("{0} is invalid", s);
                    f = -1.0f;
                }
            }
            else
            {
                f = -1.0f;
                result = String.Format("{0} is invalid", s);
            }
            return result;
        }

        protected string parseInt(String s, out int i)
        {
            string result;
            if (Int32.TryParse(s, out i))
            {
                result = String.Empty;
            }
            else
            {
                result = String.Format("{0} is invalid", s);
            }
            return result;
        }

    }

}

