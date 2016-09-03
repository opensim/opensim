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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Tools;

namespace OpenSim.Region.ScriptEngine.Shared.CodeTools
{
    public class LSL2CSCodeTransformer
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private SYMBOL m_astRoot = null;
        private static Dictionary<string, string> m_datatypeLSL2OpenSim = null;

        /// <summary>
        /// Pass the new CodeTranformer an abstract syntax tree.
        /// </summary>
        /// <param name="astRoot">The root node of the AST.</param>
        public LSL2CSCodeTransformer(SYMBOL astRoot)
        {
            m_astRoot = astRoot;

            // let's populate the dictionary
            if (null == m_datatypeLSL2OpenSim)
            {
                m_datatypeLSL2OpenSim = new Dictionary<string, string>();
                m_datatypeLSL2OpenSim.Add("integer", "LSL_Types.LSLInteger");
                m_datatypeLSL2OpenSim.Add("float", "LSL_Types.LSLFloat");
                //m_datatypeLSL2OpenSim.Add("key", "LSL_Types.key"); // key doesn't seem to be used
                m_datatypeLSL2OpenSim.Add("key", "LSL_Types.LSLString");
                m_datatypeLSL2OpenSim.Add("string", "LSL_Types.LSLString");
                m_datatypeLSL2OpenSim.Add("vector", "LSL_Types.Vector3");
                m_datatypeLSL2OpenSim.Add("rotation", "LSL_Types.Quaternion");
                m_datatypeLSL2OpenSim.Add("list", "LSL_Types.list");
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
//            m_log.DebugFormat("[LSL2CSCODETRANSFORMER]: Tranforming node {0}", s);

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

            for (int i = 0; i < s.kids.Count; i++)
            {
                // It's possible that a child is null, for instance when the
                // assignment part in a for-loop is left out, ie:
                //
                //     for (; i < 10; i++)
                //     {
                //         ...
                //     }
                //
                // We need to check for that here.
                if (null != s.kids[i])
                {
//                    m_log.Debug("[LSL2CSCODETRANSFORMER]: Moving down level");

                    if (!(s is Assignment || s is ArgumentDeclarationList) && s.kids[i] is Declaration)
                        AddImplicitInitialization(s, i);

                    TransformNode((SYMBOL) s.kids[i]);

//                    m_log.Debug("[LSL2CSCODETRANSFORMER]: Moving up level");
                }
            }
        }

        /// <summary>
        /// Replaces an instance of the node at s.kids[didx] with an assignment
        /// node. The assignment node has the Declaration node on the left hand
        /// side and a default initializer on the right hand side.
        /// </summary>
        /// <param name="s">
        /// The node containing the Declaration node that needs replacing.
        /// </param>
        /// <param name="didx">Index of the Declaration node to replace.</param>
        private void AddImplicitInitialization(SYMBOL s, int didx)
        {
            // We take the kids for a while to play with them.
            int sKidSize = s.kids.Count;
            object [] sKids = new object[sKidSize];
            for (int i = 0; i < sKidSize; i++)
                sKids[i] = s.kids.Pop();

            // The child to be changed.
            Declaration currentDeclaration = (Declaration) sKids[didx];

            // We need an assignment node.
            Assignment newAssignment = new Assignment(currentDeclaration.yyps,
                                                      currentDeclaration,
                                                      GetZeroConstant(currentDeclaration.yyps, currentDeclaration.Datatype),
                                                      "=");
            sKids[didx] = newAssignment;

            // Put the kids back where they belong.
            for (int i = 0; i < sKidSize; i++)
                s.kids.Add(sKids[i]);
        }

        /// <summary>
        /// Generates the node structure required to generate a default
        /// initialization.
        /// </summary>
        /// <param name="p">
        /// Tools.Parser instance to use when instantiating nodes.
        /// </param>
        /// <param name="constantType">String describing the datatype.</param>
        /// <returns>
        /// A SYMBOL node conaining the appropriate structure for intializing a
        /// constantType.
        /// </returns>
        private SYMBOL GetZeroConstant(Parser p, string constantType)
        {
            switch (constantType)
            {
            case "integer":
                return new Constant(p, constantType, "0");
            case "float":
                return new Constant(p, constantType, "0.0");
            case "string":
            case "key":
                return new Constant(p, constantType, "");
            case "list":
                ArgumentList al = new ArgumentList(p);
                return new ListConstant(p, al);
            case "vector":
                Constant vca = new Constant(p, "float", "0.0");
                Constant vcb = new Constant(p, "float", "0.0");
                Constant vcc = new Constant(p, "float", "0.0");
                ConstantExpression vcea = new ConstantExpression(p, vca);
                ConstantExpression vceb = new ConstantExpression(p, vcb);
                ConstantExpression vcec = new ConstantExpression(p, vcc);
                return new VectorConstant(p, vcea, vceb, vcec);
            case "rotation":
                Constant rca = new Constant(p, "float", "0.0");
                Constant rcb = new Constant(p, "float", "0.0");
                Constant rcc = new Constant(p, "float", "0.0");
                Constant rcd = new Constant(p, "float", "1.0");
                ConstantExpression rcea = new ConstantExpression(p, rca);
                ConstantExpression rceb = new ConstantExpression(p, rcb);
                ConstantExpression rcec = new ConstantExpression(p, rcc);
                ConstantExpression rced = new ConstantExpression(p, rcd);
                return new RotationConstant(p, rcea, rceb, rcec, rced);
            default:
                return null; // this will probably break stuff
            }
        }
    }
}
