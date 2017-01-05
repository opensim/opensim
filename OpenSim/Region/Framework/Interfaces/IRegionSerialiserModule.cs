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

using System.Collections.Generic;
using System.IO;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IRegionSerialiserModule
    {
        List<string> SerialiseRegion(Scene scene, string saveDir);

        /// <summary>
        /// Load prims from the xml format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fileName"></param>
        /// <param name="newIDS"></param>
        /// <param name="loadOffset"></param>
        void LoadPrimsFromXml(Scene scene, string fileName, bool newIDS, Vector3 loadOffset);

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
        /// <param name="startScripts"></param>
        void LoadPrimsFromXml2(Scene scene, TextReader reader, bool startScripts);

        /// <summary>
        /// Save prims in the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fileName"></param>
        void SavePrimsToXml2(Scene scene, string fileName);

        /// <summary>
        /// Save prims in the xml2 format, optionally specifying a bounding box for which
        /// prims should be saved.  If both min and max vectors are Vector3.Zero, then all prims
        /// are exported.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="stream"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        void SavePrimsToXml2(Scene scene, TextWriter stream, Vector3 min, Vector3 max);

        /// <summary>
        /// Save a set of prims in the xml2 format
        /// </summary>
        /// <param name="entityList"></param>
        /// <param name="fileName"></param>
        void SavePrimListToXml2(EntityBase[] entityList, string fileName);

        /// <summary>
        /// Save a set of prims in the xml2 format, optionally specifying a bounding box for which
        /// prims should be saved.  If both min and max vectors are Vector3.Zero, then all prims
        /// are exported.
        /// </summary>
        /// <param name="entityList"></param>
        /// <param name="stream"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        void SavePrimListToXml2(EntityBase[] entityList, TextWriter stream, Vector3 min, Vector3 max);

        void SaveNamedPrimsToXml2(Scene scene, string primName, string fileName);

        /// <summary>
        /// Deserializes a scene object from its xml2 representation.  This does not load the object into the scene.
        /// </summary>
        /// <param name="xmlString"></param>
        /// <returns>The scene object created</returns>
        SceneObjectGroup DeserializeGroupFromXml2(string xmlString);

        /// <summary>
        /// Serialize an individual scene object into the xml2 format
        /// </summary>
        /// <param name="grp"></param>
        /// <returns></returns>
        string SerializeGroupToXml2(SceneObjectGroup grp, Dictionary<string, object> options);
    }
}
