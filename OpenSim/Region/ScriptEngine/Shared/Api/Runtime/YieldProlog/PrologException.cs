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

namespace OpenSim.Region.ScriptEngine.Shared.YieldProlog
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
        public PrologException(object Term)
            : base(YP.getValue(Term).ToString())
        {
            _term = YP.makeCopy(Term, new Variable.CopyStore());
        }

        /// <summary>
        /// Create a PrologException where the Term is error(ErrorTerm, Message).
        /// This uses YP.makeCopy to copy the ErrorTerm and Message so that they are valid after unbinding.
        /// </summary>
        /// <param name="ErrorTerm">the error term of the error</param>
        /// <param name="Messsage">the message term of the error.  If this is a string, it is converted to an
        /// Atom so it can be used by Prolog code.
        /// Message, converted to a string, is use as the printable exception message.
        /// </param>
        public PrologException(object ErrorTerm, object Message)
            : base(YP.getValue(Message).ToString())
        {
            if (Message is string)
                Message = Atom.a((string)Message);
            _term = YP.makeCopy(new Functor2(Atom.a("error"), ErrorTerm, Message), new Variable.CopyStore());
        }

        public class TypeErrorInfo
        {
            public readonly Atom _Type;
            public readonly object _Culprit;
            public readonly object _Message;

            public TypeErrorInfo(Atom Type, object Culprit, object Message)
            {
                _Type = Type;
                _Culprit = Culprit;
                _Message = Message;
            }
        }
        /// <summary>
        /// Return the TypeErrorInfo for this exception, or null if _term does not match
        ///   error(type_error(Type, Culprit), Message).
        /// </summary>
        /// <returns></returns>
        public TypeErrorInfo getTypeErrorInfo()
        {
            if (!(_term is Functor2 && ((Functor2)_term)._name._name == "error"))
                return null;
            object errorTerm = ((Functor2)_term)._arg1;
            if (!(errorTerm is Functor2 && ((Functor2)errorTerm)._name._name == "type_error"))
                return null;
            if (!(((Functor2)errorTerm)._arg1 is Atom))
                return null;
            return new TypeErrorInfo
                ((Atom)((Functor2)errorTerm)._arg1, ((Functor2)errorTerm)._arg2, ((Functor2)_term)._arg2);
        }

        public class ExistenceErrorInfo
        {
            public readonly Atom _Type;
            public readonly object _Culprit;
            public readonly object _Message;

            public ExistenceErrorInfo(Atom Type, object Culprit, object Message)
            {
                _Type = Type;
                _Culprit = Culprit;
                _Message = Message;
            }

            /// <summary>
            /// If _Type is procedure and _Culprit is name/artity, return the name.  Otherwise return null.
            /// </summary>
            /// <returns></returns>
            public object getProcedureName()
            {
                if (!(_Type._name == "procedure" &&
                      _Culprit is Functor2 && ((Functor2)_Culprit)._name == Atom.SLASH))
                    return null;
                return ((Functor2)_Culprit)._arg1;
            }

            /// <summary>
            /// If _Type is procedure and _Culprit is name/arity and arity is an integer, return the arity.
            /// Otherwise return -1.
            /// </summary>
            /// <returns></returns>
            public int getProcedureArity()
            {
                if (!(_Type._name == "procedure" &&
                      _Culprit is Functor2 && ((Functor2)_Culprit)._name == Atom.SLASH))
                    return -1;
                if (!(((Functor2)_Culprit)._arg2 is int))
                    return -1;
                return (int)((Functor2)_Culprit)._arg2;
            }
        }
        /// <summary>
        /// Return the ExistenceErrorInfo for this exception, or null if _term does not match
        ///   error(existence_error(Type, Culprit), Message).  If the returned ExistenceErrorInfo _Culprit is
        ///   procedure, you can use its getProcedureName and getProcedureArity.
        /// </summary>
        /// <returns></returns>
        public ExistenceErrorInfo getExistenceErrorInfo()
        {
            if (!(_term is Functor2 && ((Functor2)_term)._name._name == "error"))
                return null;
            object errorTerm = ((Functor2)_term)._arg1;
            if (!(errorTerm is Functor2 && ((Functor2)errorTerm)._name._name == "existence_error"))
                return null;
            if (!(((Functor2)errorTerm)._arg1 is Atom))
                return null;
            return new ExistenceErrorInfo
                ((Atom)((Functor2)errorTerm)._arg1, ((Functor2)errorTerm)._arg2, ((Functor2)_term)._arg2);
        }
    }
}
