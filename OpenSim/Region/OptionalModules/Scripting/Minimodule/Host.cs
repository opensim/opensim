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

using System.Reflection;
using log4net;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class Host : System.MarshalByRefObject, IHost
    {
        private readonly IObject m_obj;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IGraphics m_graphics;
        private readonly IExtension m_extend;
        private readonly IMicrothreader m_threader;
        //private Scene m_scene;

        public Host(IObject m_obj, Scene m_scene, IExtension m_extend, IMicrothreader m_threader)
        {
            this.m_obj = m_obj;
            this.m_threader = m_threader;
            this.m_extend = m_extend;
            //this.m_scene = m_scene;

            m_graphics = new Graphics(m_scene);
        }

        public IObject Object
        {
            get { return m_obj; }
        }

        public ILog Console
        {
            get { return m_log; }
        }

        public IGraphics Graphics
        {
            get { return m_graphics; }
        }

        public IExtension Extensions
        {
            get { return m_extend; }
        }

        public IMicrothreader Microthreads
        {
            get { return m_threader; }
        }
    }
}
