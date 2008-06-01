/*
 * Copyright (C) 2007-2008, Jeff Thompson
 * 
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without 
 * modification, are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright 
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright 
 *       notice, this list of conditions and the following disclaimer in the 
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the copyright holder nor the names of its contributors 
 *       may be used to endorse or promote products derived from this software 
 *       without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.YieldProlog
{
    /// <summary>
    /// A PrologException is used as the exception thrown by YP.throw(Term).
    /// </summary>
    public class PrologException : Exception
    {
        public readonly object _term;

        /// <summary>
        /// Create a PrologException with the given Term.  The printable exception message is the full Term.
        /// </summary>
        /// <param name="Term">the term of the exception</param>
        /// </param>
        public PrologException(object Term)
            : base(YP.getValue(Term).ToString())
        {
            _term = YP.makeCopy(Term, new Variable.CopyStore());
        }

        /// <summary>
        /// Create a PrologException where the Term is error(ErrorTerm, Message).
        /// This uses YP.makeCopy to copy the ErrorTerm and Message so that they are valid after unbinding.
        /// </summary>
        /// <param name="ErrorTerm">the term of the exception</param>
        /// <param name="Messsage">the message, converted to a string, to use as the printable exception message
        /// </param>
        public PrologException(object ErrorTerm, object Message)
            : base(YP.getValue(Message).ToString())
        {
            _term = YP.makeCopy(new Functor2(Atom.a("error"), ErrorTerm, Message), new Variable.CopyStore());
        }

        public object Term
        {
            get { return _term; }
        }
    }
}
