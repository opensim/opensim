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

using System;
using System.Collections.Generic;
using Tools;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    public class LSL2CSCodeTransformer
    {
        private SYMBOL m_astRoot = null;
        private static Dictionary<string, string> m_datatypeLSL2OpenSim = new Dictionary<string, string>();

        /// <summary>
        /// Pass the new CodeTranformer an abstract syntax tree.
        /// </summary>
        /// <param name="astRoot">The root node of the AST.</param>
        public LSL2CSCodeTransformer(SYMBOL astRoot)
        {
            m_astRoot = astRoot;

            // let's populate the dictionary
            try
            {
                m_datatypeLSL2OpenSim.Add("integer", "LSL_Types.LSLInteger");
                m_datatypeLSL2OpenSim.Add("float", "LSL_Types.LSLFloat");
                //m_datatypeLSL2OpenSim.Add("key", "LSL_Types.key"); // key doesn't seem to be used
                m_datatypeLSL2OpenSim.Add("key", "LSL_Types.LSLString");
                m_datatypeLSL2OpenSim.Add("string", "LSL_Types.LSLString");
                m_datatypeLSL2OpenSim.Add("vector", "LSL_Types.Vector3");
                m_datatypeLSL2OpenSim.Add("rotation", "LSL_Types.Quaternion");
                m_datatypeLSL2OpenSim.Add("list", "LSL_Types.list");
            }
            catch
            {
                // temporary workaround since we are adding to a static datatype
            }
        }

        /// <summary>
        /// Transform the code in the AST we have.
        /// </summary>
        /// <returns>The root node of the transformed AST</returns>
        public SYMBOL Transform()
        {
            foreach (SYMBOL s in m_astRoot.kids)
                TransformNode(s);

            return m_astRoot;
        }

        /// <summary>
        /// Recursively called to transform each type of node. Will transform this
        /// node, then all it's children.
        /// </summary>
        /// <param name="s">The current node to transform.</param>
        private void TransformNode(SYMBOL s)
        {
            // make sure to put type lower in the inheritance hierarchy first
            // ie: since IdentConstant and StringConstant inherit from Constant,
            // put IdentConstant and StringConstant before Constant
            if (s is Declaration)
                ((Declaration) s).Datatype = m_datatypeLSL2OpenSim[((Declaration) s).Datatype];
            else if (s is Constant)
                ((Constant) s).Type = m_datatypeLSL2OpenSim[((Constant) s).Type];
            else if (s is TypecastExpression)
                ((TypecastExpression) s).TypecastType = m_datatypeLSL2OpenSim[((TypecastExpression) s).TypecastType];
            else if (s is GlobalFunctionDefinition && "void" != ((GlobalFunctionDefinition) s).ReturnType) // we don't need to translate "void"
                ((GlobalFunctionDefinition) s).ReturnType = m_datatypeLSL2OpenSim[((GlobalFunctionDefinition) s).ReturnType];

            foreach (SYMBOL kid in s.kids)
                TransformNode(kid);
        }
    }
}
