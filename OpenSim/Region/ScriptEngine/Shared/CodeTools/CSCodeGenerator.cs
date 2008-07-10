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
using System.IO;
using System.Collections.Generic;
using Tools;

namespace OpenSim.Region.ScriptEngine.Shared.CodeTools
{
    public class CSCodeGenerator
    {
        private SYMBOL m_astRoot = null;
        private int m_braceCount;   // for indentation

        /// <summary>
        /// Pass the new CodeGenerator a string containing the LSL source.
        /// </summary>
        /// <param name="script">String containing LSL source.</param>
        public CSCodeGenerator(string script)
        {
            Parser p = new LSLSyntax(new yyLSLSyntax(), new ErrorHandler(true));
            // Obviously this needs to be in a try/except block.
            LSL2CSCodeTransformer codeTransformer = new LSL2CSCodeTransformer(p.Parse(script));
            m_astRoot = codeTransformer.Transform();
        }

        /// <summary>
        /// Pass the new CodeGenerator an abstract syntax tree.
        /// </summary>
        /// <param name="astRoot">The root node of the AST.</param>
        public CSCodeGenerator(SYMBOL astRoot)
        {
            m_braceCount = 0;
            m_astRoot = astRoot;
        }

        /// <summary>
        /// Generate the code from the AST we have.
        /// </summary>
        /// <returns>String containing the generated C# code.</returns>
        public string Generate()
        {
            string retstr = String.Empty;

            // standard preamble
            //retstr = "using OpenSim.Region.ScriptEngine.Common;\n";
            //retstr += "using System.Collections.Generic;\n\n";
            //retstr += "namespace SecondLife\n";
            //retstr += "{\n";
            //retstr += "    public class Script : OpenSim.Region.ScriptEngine.Common\n";
            //retstr += "    {\n";

            // here's the payload
            m_braceCount += 2;
            retstr += "\n";
            foreach (SYMBOL s in m_astRoot.kids)
                retstr += GenerateNode(s);

            // close braces!
            //retstr += "    }\n";
            //retstr += "}\n";
            m_braceCount -= 2;

            return retstr;
        }

        /// <summary>
        /// Recursively called to generate each type of node. Will generate this
        /// node, then all it's children.
        /// </summary>
        /// <param name="s">The current node to generate code for.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateNode(SYMBOL s)
        {
            string retstr = String.Empty;

            // make sure to put type lower in the inheritance hierarchy first
            // ie: since IdentArgument and ExpressionArgument inherit from
            // Argument, put IdentArgument and ExpressionArgument before Argument
            if (s is GlobalFunctionDefinition)
                retstr += GenerateGlobalFunctionDefinition((GlobalFunctionDefinition) s);
            else if (s is GlobalVariableDeclaration)
                retstr += GenerateGlobalVariableDeclaration((GlobalVariableDeclaration) s);
            else if (s is State)
                retstr += GenerateState((State) s);
            else if (s is CompoundStatement)
                retstr += GenerateCompoundStatement((CompoundStatement) s);
            else if (s is Declaration)
                retstr += GenerateDeclaration((Declaration) s);
            else if (s is Statement)
                retstr += GenerateStatement((Statement) s);
            else if (s is ReturnStatement)
                retstr += GenerateReturnStatement((ReturnStatement) s);
            else if (s is JumpLabel)
                retstr += GenerateJumpLabel((JumpLabel) s);
            else if (s is JumpStatement)
                retstr += GenerateJumpStatement((JumpStatement) s);
            else if (s is StateChange)
                retstr += GenerateStateChange((StateChange) s);
            else if (s is IfStatement)
                retstr += GenerateIfStatement((IfStatement) s);
            else if (s is WhileStatement)
                retstr += GenerateWhileStatement((WhileStatement) s);
            else if (s is DoWhileStatement)
                retstr += GenerateDoWhileStatement((DoWhileStatement) s);
            else if (s is ForLoop)
                retstr += GenerateForLoop((ForLoop) s);
            else if (s is ArgumentList)
                retstr += GenerateArgumentList((ArgumentList) s);
            else if (s is Assignment)
                retstr += GenerateAssignment((Assignment) s);
            else if (s is BinaryExpression)
                retstr += GenerateBinaryExpression((BinaryExpression) s);
            else if (s is ParenthesisExpression)
                retstr += GenerateParenthesisExpression((ParenthesisExpression) s);
            else if (s is UnaryExpression)
                retstr += GenerateUnaryExpression((UnaryExpression) s);
            else if (s is IncrementDecrementExpression)
                retstr += GenerateIncrementDecrementExpression((IncrementDecrementExpression) s);
            else if (s is TypecastExpression)
                retstr += GenerateTypecastExpression((TypecastExpression) s);
            else if (s is FunctionCall)
                retstr += GenerateFunctionCall((FunctionCall) s);
            else if (s is VectorConstant)
                retstr += GenerateVectorConstant((VectorConstant) s);
            else if (s is RotationConstant)
                retstr += GenerateRotationConstant((RotationConstant) s);
            else if (s is ListConstant)
                retstr += GenerateListConstant((ListConstant) s);
            else if (s is Constant)
                retstr += GenerateConstant((Constant) s);
            else if (s is IdentDotExpression)
                retstr += ((IdentDotExpression) s).Name + "." + ((IdentDotExpression) s).Member;
            else if (s is IdentExpression)
                retstr += ((IdentExpression) s).Name;
            else if (s is IDENT)
                retstr += ((TOKEN) s).yytext;
            else
            {
                foreach (SYMBOL kid in s.kids)
                    retstr += GenerateNode(kid);
            }

            return retstr;
        }

        /// <summary>
        /// Generates the code for a GlobalFunctionDefinition node.
        /// </summary>
        /// <param name="gf">The GlobalFunctionDefinition node.</param>
        /// <returns>String containing C# code for GlobalFunctionDefinition gf.</returns>
        private string GenerateGlobalFunctionDefinition(GlobalFunctionDefinition gf)
        {
            string retstr = String.Empty;

            // we need to separate the argument declaration list from other kids
            List<SYMBOL> argumentDeclarationListKids = new List<SYMBOL>();
            List<SYMBOL> remainingKids = new List<SYMBOL>();

            foreach (SYMBOL kid in gf.kids)
                if (kid is ArgumentDeclarationList)
                    argumentDeclarationListKids.Add(kid);
                else
                    remainingKids.Add(kid);

            retstr += WriteIndented(String.Format("{0} {1}(", gf.ReturnType, gf.Name));

            // print the state arguments, if any
            foreach (SYMBOL kid in argumentDeclarationListKids)
                retstr += GenerateArgumentDeclarationList((ArgumentDeclarationList) kid);

            retstr += ")\n";

            foreach (SYMBOL kid in remainingKids)
                retstr += GenerateNode(kid);

            return retstr;
        }

        /// <summary>
        /// Generates the code for a GlobalVariableDeclaration node.
        /// </summary>
        /// <param name="gv">The GlobalVariableDeclaration node.</param>
        /// <returns>String containing C# code for GlobalVariableDeclaration gv.</returns>
        private string GenerateGlobalVariableDeclaration(GlobalVariableDeclaration gv)
        {
            string retstr = String.Empty;

            foreach (SYMBOL s in gv.kids)
            {
                retstr += Indent();
                retstr += GenerateNode(s);
                retstr += ";\n";
            }

            return retstr;
        }

        /// <summary>
        /// Generates the code for a State node.
        /// </summary>
        /// <param name="s">The State node.</param>
        /// <returns>String containing C# code for State s.</returns>
        private string GenerateState(State s)
        {
            string retstr = String.Empty;

            foreach (SYMBOL kid in s.kids)
                if (kid is StateEvent)
                    retstr += GenerateStateEvent((StateEvent) kid, s.Name);
                else
                    retstr += String.Format("ERROR: State '{0}' contains a '{1}\n", s.Name, kid.GetType());

            return retstr;
        }

        /// <summary>
        /// Generates the code for a StateEvent node.
        /// </summary>
        /// <param name="se">The StateEvent node.</param>
        /// <param name="parentStateName">The name of the parent state.</param>
        /// <returns>String containing C# code for StateEvent se.</returns>
        private string GenerateStateEvent(StateEvent se, string parentStateName)
        {
            string retstr = String.Empty;

            // we need to separate the argument declaration list from other kids
            List<SYMBOL> argumentDeclarationListKids = new List<SYMBOL>();
            List<SYMBOL> remainingKids = new List<SYMBOL>();

            foreach (SYMBOL kid in se.kids)
                if (kid is ArgumentDeclarationList)
                    argumentDeclarationListKids.Add(kid);
                else
                    remainingKids.Add(kid);

            // "state" (function) declaration
            retstr += WriteIndented(String.Format("public void {0}_event_{1}(", parentStateName, se.Name));

            // print the state arguments, if any
            foreach (SYMBOL kid in argumentDeclarationListKids)
                retstr += GenerateArgumentDeclarationList((ArgumentDeclarationList) kid);

            retstr += ")\n";

            foreach (SYMBOL kid in remainingKids)
                retstr += GenerateNode(kid);

            return retstr;
        }

        /// <summary>
        /// Generates the code for an ArgumentDeclarationList node.
        /// </summary>
        /// <param name="adl">The ArgumentDeclarationList node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateArgumentDeclarationList(ArgumentDeclarationList adl)
        {
            string retstr = String.Empty;

            int comma = adl.kids.Count - 1; // tells us whether to print a comma

            foreach (Declaration d in adl.kids)
            {
                retstr += String.Format("{0} {1}", d.Datatype, d.Id);
                if (0 < comma--)
                    retstr += ", ";
            }

            return retstr;
        }

        /// <summary>
        /// Generates the code for an ArgumentList node.
        /// </summary>
        /// <param name="al">The ArgumentList node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateArgumentList(ArgumentList al)
        {
            string retstr = String.Empty;

            int comma = al.kids.Count - 1;  // tells us whether to print a comma

            foreach (SYMBOL s in al.kids)
            {
                retstr += GenerateNode(s);
                if (0 < comma--)
                    retstr += ", ";
            }

            return retstr;
        }

        /// <summary>
        /// Generates the code for a CompoundStatement node.
        /// </summary>
        /// <param name="cs">The CompoundStatement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateCompoundStatement(CompoundStatement cs)
        {
            string retstr = String.Empty;

            // opening brace
            retstr += WriteIndentedLine("{");
            m_braceCount++;

            foreach (SYMBOL kid in cs.kids)
                retstr += GenerateNode(kid);

            // closing brace
            m_braceCount--;
            retstr += WriteIndentedLine("}");

            return retstr;
        }

        /// <summary>
        /// Generates the code for a Declaration node.
        /// </summary>
        /// <param name="d">The Declaration node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateDeclaration(Declaration d)
        {
            return String.Format("{0} {1}", d.Datatype, d.Id);
        }

        /// <summary>
        /// Generates the code for a Statement node.
        /// </summary>
        /// <param name="s">The Statement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateStatement(Statement s)
        {
            string retstr = String.Empty;

            // Jump label prints its own colon, we don't need a semicolon.
            bool printSemicolon = !(s.kids.Top is JumpLabel);

            retstr += Indent();

            foreach (SYMBOL kid in s.kids)
                retstr += GenerateNode(kid);

            if (printSemicolon)
                retstr += ";\n";

            return retstr;
        }

        /// <summary>
        /// Generates the code for an Assignment node.
        /// </summary>
        /// <param name="a">The Assignment node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateAssignment(Assignment a)
        {
            string retstr = String.Empty;

            retstr += GenerateNode((SYMBOL) a.kids.Pop());
            retstr +=String.Format(" {0} ", a.AssignmentType);
            foreach (SYMBOL kid in a.kids)
                retstr += GenerateNode(kid);

            return retstr;
        }

        /// <summary>
        /// Generates the code for a ReturnStatement node.
        /// </summary>
        /// <param name="rs">The ReturnStatement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateReturnStatement(ReturnStatement rs)
        {
            string retstr = String.Empty;

            retstr += "return ";

            foreach (SYMBOL kid in rs.kids)
                retstr += GenerateNode(kid);

            return retstr;
        }

        /// <summary>
        /// Generates the code for a JumpLabel node.
        /// </summary>
        /// <param name="jl">The JumpLabel node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateJumpLabel(JumpLabel jl)
        {
            return String.Format("{0}:\n", jl.LabelName);
        }

        /// <summary>
        /// Generates the code for a JumpStatement node.
        /// </summary>
        /// <param name="js">The JumpStatement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateJumpStatement(JumpStatement js)
        {
            return String.Format("goto {0}", js.TargetName);
        }

        /// <summary>
        /// Generates the code for a IfStatement node.
        /// </summary>
        /// <param name="ifs">The IfStatement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateIfStatement(IfStatement ifs)
        {
            string retstr = String.Empty;

            retstr += WriteIndented("if (");
            retstr += GenerateNode((SYMBOL) ifs.kids.Pop());
            retstr += ")\n";

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.
            bool indentHere = ifs.kids.Top is Statement;
            if (indentHere) m_braceCount++;
            retstr += GenerateNode((SYMBOL) ifs.kids.Pop());
            if (indentHere) m_braceCount--;

            if (0 < ifs.kids.Count) // do it again for an else
            {
                retstr += WriteIndentedLine("else");

                indentHere = ifs.kids.Top is Statement;
                if (indentHere) m_braceCount++;
                retstr += GenerateNode((SYMBOL) ifs.kids.Pop());
                if (indentHere) m_braceCount--;
            }

            return retstr;
        }

        /// <summary>
        /// Generates the code for a StateChange node.
        /// </summary>
        /// <param name="sc">The StateChange node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateStateChange(StateChange sc)
        {
            return String.Format("state(\"{0}\")", sc.NewState);
        }

        /// <summary>
        /// Generates the code for a WhileStatement node.
        /// </summary>
        /// <param name="ws">The WhileStatement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateWhileStatement(WhileStatement ws)
        {
            string retstr = String.Empty;

            retstr += WriteIndented("while (");
            retstr += GenerateNode((SYMBOL) ws.kids.Pop());
            retstr += ")\n";

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.
            bool indentHere = ws.kids.Top is Statement;
            if (indentHere) m_braceCount++;
            retstr += GenerateNode((SYMBOL) ws.kids.Pop());
            if (indentHere) m_braceCount--;

            return retstr;
        }

        /// <summary>
        /// Generates the code for a DoWhileStatement node.
        /// </summary>
        /// <param name="dws">The DoWhileStatement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateDoWhileStatement(DoWhileStatement dws)
        {
            string retstr = String.Empty;

            retstr += WriteIndentedLine("do");

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.
            bool indentHere = dws.kids.Top is Statement;
            if (indentHere) m_braceCount++;
            retstr += GenerateNode((SYMBOL) dws.kids.Pop());
            if (indentHere) m_braceCount--;

            retstr += WriteIndented("while (");
            retstr += GenerateNode((SYMBOL) dws.kids.Pop());
            retstr += ");\n";

            return retstr;
        }

        /// <summary>
        /// Generates the code for a ForLoop node.
        /// </summary>
        /// <param name="fl">The ForLoop node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateForLoop(ForLoop fl)
        {
            string retstr = String.Empty;

            retstr += WriteIndented("for (");

            // for ( x = 0 ; x < 10 ; x++ )
            //       ^^^^^^^
            retstr += GenerateForLoopStatement((ForLoopStatement) fl.kids.Pop());
            retstr += "; ";
            // for ( x = 0 ; x < 10 ; x++ )
            //               ^^^^^^^^
            retstr += GenerateNode((SYMBOL) fl.kids.Pop());
            retstr += "; ";
            // for ( x = 0 ; x < 10 ; x++ )
            //                        ^^^^^
            retstr += GenerateForLoopStatement((ForLoopStatement) fl.kids.Pop());
            retstr += ")\n";

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.
            bool indentHere = fl.kids.Top is Statement;
            if (indentHere) m_braceCount++;
            retstr += GenerateNode((SYMBOL) fl.kids.Pop());
            if (indentHere) m_braceCount--;

            return retstr;
        }

        /// <summary>
        /// Generates the code for a ForLoopStatement node.
        /// </summary>
        /// <param name="fls">The ForLoopStatement node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateForLoopStatement(ForLoopStatement fls)
        {
            string retstr = String.Empty;

            int comma = fls.kids.Count - 1;  // tells us whether to print a comma

            foreach (SYMBOL s in fls.kids)
            {
                retstr += GenerateNode(s);
                if (0 < comma--)
                    retstr += ", ";
            }

            return retstr;
        }

        /// <summary>
        /// Generates the code for a BinaryExpression node.
        /// </summary>
        /// <param name="be">The BinaryExpression node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateBinaryExpression(BinaryExpression be)
        {
            string retstr = String.Empty;

            retstr += GenerateNode((SYMBOL) be.kids.Pop());
            retstr += String.Format(" {0} ", be.ExpressionSymbol);
            foreach (SYMBOL kid in be.kids)
                retstr += GenerateNode(kid);

            return retstr;
        }

        /// <summary>
        /// Generates the code for a UnaryExpression node.
        /// </summary>
        /// <param name="ue">The UnaryExpression node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateUnaryExpression(UnaryExpression ue)
        {
            string retstr = String.Empty;

            retstr += ue.UnarySymbol;
            retstr += GenerateNode((SYMBOL) ue.kids.Pop());

            return retstr;
        }

        /// <summary>
        /// Generates the code for a ParenthesisExpression node.
        /// </summary>
        /// <param name="pe">The ParenthesisExpression node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateParenthesisExpression(ParenthesisExpression pe)
        {
            string retstr = String.Empty;

            retstr += "(";
            foreach (SYMBOL kid in pe.kids)
                retstr += GenerateNode(kid);
            retstr += ")";

            return retstr;
        }

        /// <summary>
        /// Generates the code for a IncrementDecrementExpression node.
        /// </summary>
        /// <param name="ide">The IncrementDecrementExpression node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateIncrementDecrementExpression(IncrementDecrementExpression ide)
        {
            string retstr = String.Empty;

            if (0 < ide.kids.Count)
            {
                IdentDotExpression dot = (IdentDotExpression) ide.kids.Top;
                retstr += String.Format("{0}", ide.PostOperation ? dot.Name + "." + dot.Member + ide.Operation : ide.Operation + dot.Name + "." + dot.Member);
            }
            else
                retstr += String.Format("{0}", ide.PostOperation ? ide.Name + ide.Operation : ide.Operation + ide.Name);

            return retstr;
        }

        /// <summary>
        /// Generates the code for a TypecastExpression node.
        /// </summary>
        /// <param name="te">The TypecastExpression node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateTypecastExpression(TypecastExpression te)
        {
            string retstr = String.Empty;

            // we wrap all typecasted statements in parentheses 
            retstr += String.Format("({0}) (", te.TypecastType);
            retstr += GenerateNode((SYMBOL) te.kids.Pop());
            retstr += ")";

            return retstr;
        }

        /// <summary>
        /// Generates the code for a FunctionCall node.
        /// </summary>
        /// <param name="fc">The FunctionCall node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateFunctionCall(FunctionCall fc)
        {
            string retstr = String.Empty;

            retstr += String.Format("{0}(", fc.Id);

            foreach (SYMBOL kid in fc.kids)
                retstr += GenerateNode(kid);

            retstr += ")";

            return retstr;
        }

        /// <summary>
        /// Generates the code for a Constant node.
        /// </summary>
        /// <param name="c">The Constant node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateConstant(Constant c)
        {
            string retstr = String.Empty;

            // Supprt LSL's weird acceptance of floats with no trailing digits
            // after the period. Turn float x = 10.; into float x = 10.0;
            if ("LSL_Types.LSLFloat" == c.Type)
            {
                int dotIndex = c.Value.IndexOf('.') + 1;
                if (0 < dotIndex && (dotIndex == c.Value.Length || !Char.IsDigit(c.Value[dotIndex])))
                    c.Value = c.Value.Insert(dotIndex, "0");
            }

            // need to quote strings
            if ("LSL_Types.LSLString" == c.Type)
                retstr += "\"";
            retstr += c.Value;
            if ("LSL_Types.LSLString" == c.Type)
                retstr += "\"";

            return retstr;
        }

        /// <summary>
        /// Generates the code for a VectorConstant node.
        /// </summary>
        /// <param name="vc">The VectorConstant node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateVectorConstant(VectorConstant vc)
        {
            string retstr = String.Empty;

            retstr += String.Format("new {0}(", vc.Type);
            retstr += GenerateNode((SYMBOL) vc.kids.Pop());
            retstr += ", ";
            retstr += GenerateNode((SYMBOL) vc.kids.Pop());
            retstr += ", ";
            retstr += GenerateNode((SYMBOL) vc.kids.Pop());
            retstr += ")";

            return retstr;
        }

        /// <summary>
        /// Generates the code for a RotationConstant node.
        /// </summary>
        /// <param name="rc">The RotationConstant node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateRotationConstant(RotationConstant rc)
        {
            string retstr = String.Empty;

            retstr += String.Format("new {0}(", rc.Type);
            retstr += GenerateNode((SYMBOL) rc.kids.Pop());
            retstr += ", ";
            retstr += GenerateNode((SYMBOL) rc.kids.Pop());
            retstr += ", ";
            retstr += GenerateNode((SYMBOL) rc.kids.Pop());
            retstr += ", ";
            retstr += GenerateNode((SYMBOL) rc.kids.Pop());
            retstr += ")";

            return retstr;
        }

        /// <summary>
        /// Generates the code for a ListConstant node.
        /// </summary>
        /// <param name="lc">The ListConstant node.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string GenerateListConstant(ListConstant lc)
        {
            string retstr = String.Empty;

            retstr += String.Format("new {0}(", lc.Type);

            foreach (SYMBOL kid in lc.kids)
                retstr += GenerateNode(kid);

            retstr += ")";

            return retstr;
        }

        /// <summary>
        /// Prints text correctly indented, followed by a newline.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string WriteIndentedLine(string s)
        {
            return WriteIndented(s) + "\n";
        }

        /// <summary>
        /// Prints text correctly indented.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string WriteIndented(string s)
        {
            return Indent() + s;
        }

        /// <summary>
        /// Prints correct indentation.
        /// </summary>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        private string Indent()
        {
            string retstr = String.Empty;

            for (int i = 0; i < m_braceCount; i++)
                retstr += "    ";

            return retstr;
        }
    }
}
