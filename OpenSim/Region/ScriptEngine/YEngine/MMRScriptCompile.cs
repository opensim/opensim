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

/**
 * @brief Compile a script to produce a ScriptObjCode object
 */

using System;
using System.IO;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public partial class XMRInstance
    {
        /**
         * @brief Compile a script to produce a ScriptObjCode object
         * @returns object code pointer or null if compile error
         *          also can throw compile error exception
         */
        public ScriptObjCode Compile()
        {
            Stream objFileStream = null;
            StreamWriter asmFileWriter = null;
            string sourceHash = null;
            TextWriter saveSource = null;

            string objFileName = GetScriptFileName (m_ScriptObjCodeKey + ".yobj");
            string tmpFileName = GetScriptFileName (m_ScriptObjCodeKey + ".ytmp");

             // If we already have an object file, don't bother compiling.
            if (!m_ForceRecomp && File.Exists(objFileName))
            {
                objFileStream = File.OpenRead (objFileName);
            }
            else
            {
                 // If source file empty, try to read from asset server.
                if (EmptySource (m_SourceCode))
                    m_SourceCode = FetchSource (m_CameFrom);

                 // Maybe write script source to a file for debugging.
                if (m_Engine.m_ScriptDebugSaveSource)
                {
                    string lslFileName = GetScriptFileName (m_ScriptObjCodeKey + ".lsl");           
//                    m_log.Debug ("[YEngine]: MMRScriptCompileSaveSource: saving to " + lslFileName);
                    saveSource = File.CreateText (lslFileName);
                }

                 // Parse source string into tokens.
                TokenBegin tokenBegin;
                try
                {
                    tokenBegin = TokenBegin.Construct(m_CameFrom, saveSource, ErrorHandler, m_SourceCode, out sourceHash);
                }
                finally
                {
                    if (saveSource != null)
                        saveSource.Close ();
                }
                if (tokenBegin == null)
                {
                    m_log.Debug ("[YEngine]: parsing errors on " + m_ScriptObjCodeKey);
                    return null;
                }

                 // Create object file one way or another.
                try
                {
                     // Create abstract syntax tree from raw tokens.
                    TokenScript tokenScript = ScriptReduce.Reduce(tokenBegin);
                    if (tokenScript == null)
                    {
                        m_log.Warn ("[YEngine]: reduction errors on " + m_ScriptObjCodeKey + " (" + m_CameFrom + ")");
                        PrintCompilerErrors();
                        return null;
                    }

                     // Compile abstract syntax tree to write object file.
                    using(BinaryWriter objFileWriter = new BinaryWriter(File.Create(tmpFileName)))
                    {
                        bool ok = ScriptCodeGen.CodeGen(tokenScript, objFileWriter, sourceHash);
                        if (!ok)
                        {
                            m_log.Warn ("[YEngine]: compile error on " + m_ScriptObjCodeKey + " (" + m_CameFrom + ")");
                            PrintCompilerErrors ();
                            return null;
                        }
                    }

                     // File has been completely written.
                     // If there is an old one laying around, delete it now.
                     // Then re-open the new file for reading from the beginning.
                    if (File.Exists(objFileName))
                        File.Replace(tmpFileName, objFileName, null);
                    else
                        File.Move(tmpFileName, objFileName);

                    objFileStream = File.OpenRead(objFileName);
                }
                finally
                {
                     // In case something went wrong writing temp file, delete it.
                     File.Delete (tmpFileName);
                }

                 // Since we just wrote the .xmrobj file, maybe save disassembly.
                if (m_Engine.m_ScriptDebugSaveIL)
                {
                    string asmFileName = GetScriptILFileName(m_ScriptObjCodeKey + ".yasm");
//                    m_log.Debug ("[YEngine]: MMRScriptCompileSaveILGen: saving to " + asmFileName);
                    asmFileWriter = File.CreateText (asmFileName);
                }
            }

             // Read object file to create ScriptObjCode object.
             // Maybe also write disassembly to a file for debugging.
            BinaryReader objFileReader = new BinaryReader (objFileStream);
            ScriptObjCode scriptObjCode = null;
            try
            {
                scriptObjCode = new ScriptObjCode (objFileReader, asmFileWriter, null);
            }
            finally
            {
                objFileReader.Close ();
                if (asmFileWriter != null)
                 {
                    asmFileWriter.Flush ();
                    asmFileWriter.Close ();
                }
            }

            return scriptObjCode;
        }

        private void PrintCompilerErrors ()
        {
            m_log.Info ("[YEngine]: - " + m_Part.GetWorldPosition () + " " + m_DescName);
            foreach (string error in m_CompilerErrors)
            {
                m_log.Info ("[YEngine]: - " + error);
            }
        }

        /**
         * @brief Check for empty source, allowing for a first line of //... script engine selector.
         */
        public static bool EmptySource (string source)
        {
            int len = source.Length;
            bool skipeol = false;
            for (int i = 0; i < len; i ++)
            {
                char c = source[i];
                skipeol &= c != '\n';
                skipeol |= (c == '/') && (i + 1 < len) && (source[i+1] == '/');
                if ((c > ' ') && !skipeol)
                    return false;
            }
            return true;
        }
    }
}
