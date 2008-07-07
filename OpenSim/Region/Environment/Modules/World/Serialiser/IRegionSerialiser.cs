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
 *     * Neither the name of the OpenSim Project nor the
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

using libsecondlife;
using System.Collections.Generic;
using System.IO;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.Serialiser
{
    public interface IRegionSerialiser
    {
        List<string> SerialiseRegion(Scene scene, string saveDir);

        /// <summary>
        /// Load prims from the xml format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fileName"></param>
        /// <param name="newIDS"></param>
        /// <param name="loadOffset"></param>
        void LoadPrimsFromXml(Scene scene, string fileName, bool newIDS, LLVector3 loadOffset);

        /// <summary>
        /// Save prims in the xml format
        /// </summary>
        /// <param name="scene"> </param>
        /// <param name="fileName"></param>
        void SavePrimsToXml(Scene scene, string fileName);

        /// <summary>
        /// Load prims from the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fileName"></param>
        void LoadPrimsFromXml2(Scene scene, string fileName);

        /// <summary>
        /// Load prims from the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="reader"></param>
        void LoadPrimsFromXml2(Scene scene, TextReader reader);

        /// <summary>
        /// Save prims in the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fileName"></param>
        void SavePrimsToXml2(Scene scene, string fileName);

        /// <summary>
        /// Save a set of prims in the xml2 format
        /// </summary>
        /// <param name="entityList"></param>
        /// <param name="fileName"></param>
        void SavePrimListToXml2(List<EntityBase> entityList, string fileName);

        /// <summary>
        /// Load an individual scene object from the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="xmlString"></param>
        /// <returns>The scene object created</returns>
        SceneObjectGroup LoadGroupFromXml2(Scene scene, string xmlString);

        /// <summary>
        /// Serialize an individual scene object into the xml2 format
        /// </summary>
        /// <param name="grp"></param>
        /// <returns></returns>
        string SaveGroupToXml2(SceneObjectGroup grp);
    }
}
