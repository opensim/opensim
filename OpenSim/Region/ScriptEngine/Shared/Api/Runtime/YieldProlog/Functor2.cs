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
using System.Collections.Generic;

namespace OpenSim.Region.ScriptEngine.Shared.YieldProlog
{
    public class Functor2 : IUnifiable
    {
        public readonly Atom _name;
        public readonly object _arg1;
        public readonly object _arg2;

        public Functor2(Atom name, object arg1, object arg2)
        {
            _name = name;
            _arg1 = arg1;
            _arg2 = arg2;
        }

        public Functor2(string name, object arg1, object arg2)
            : this(Atom.a(name), arg1, arg2)
        {
        }

        // disable warning on l1, don't see how we can
        // code this differently
        #pragma warning disable 0168, 0219
        /// If arg is another Functor2, then succeed (yield once) if this and arg have the
        /// same name and all functor args unify, otherwise fail (don't yield).
        /// If arg is a Variable, then call its unify to unify with this.
        /// Otherwise fail (don't yield).
        public IEnumerable<bool> unify(object arg)
        {
            arg = YP.getValue(arg);
            if (arg is Functor2)
            {
                Functor2 argFunctor = (Functor2)arg;
                if (_name.Equals(argFunctor._name))
                {
                    foreach (bool l1 in YP.unify(_arg1, argFunctor._arg1))
                    {
                        foreach (bool l2 in YP.unify(_arg2, argFunctor._arg2))
                            yield return false;
                    }
                }
            }
            else if (arg is Variable)
            {
                foreach (bool l1 in ((Variable)arg).unify(this))
                    yield return false;
            }
        }
        #pragma warning restore 0168, 0219


        public override string ToString()
        {
            if (_name == Atom.DOT)
                return listPairToString(this);
            else
                return _name + "(" + YP.getValue(_arg1) + ", " +  YP.getValue(_arg2) + ")";
        }

        public bool termEqual(object term)
        {
            term = YP.getValue(term);
            if (term is Functor2)
            {
                Functor2 termFunctor = (Functor2)term;
                return _name.Equals(termFunctor._name) && YP.termEqual(_arg1, termFunctor._arg1)
                     && YP.termEqual(_arg2, termFunctor._arg2);
            }
            return false;
        }

        public bool lessThan(Functor2 functor)
        {
            // Do the equal check first since it is faster.
            if (!_name.Equals(functor._name))
                return _name.lessThan(functor._name);

            if (!YP.termEqual(_arg1, functor._arg1))
                return YP.termLessThan(_arg1, functor._arg1);

            return YP.termLessThan(_arg2, functor._arg2);
        }

        public bool ground()
        {
            return YP.ground(_arg1) && YP.ground(_arg2);
        }

        public void addUniqueVariables(List<Variable> variableSet)
        {
            YP.addUniqueVariables(_arg1, variableSet);
            YP.addUniqueVariables(_arg2, variableSet);
        }

        public object makeCopy(Variable.CopyStore copyStore)
        {
            return new Functor2(_name, YP.makeCopy(_arg1, copyStore),
                YP.makeCopy(_arg2, copyStore));
        }

        private static string listPairToString(Functor2 listPair)
        {
            string result = "[";
            while (true)
            {
                object head = YP.getValue(listPair._arg1);
                object tail = YP.getValue(listPair._arg2);
                if (tail == (object)Atom.NIL)
                {
                    result += head;
                    break;
                }
                else if (tail is Functor2 && ((Functor2)tail)._name == Atom.DOT)
                {
                    result += head + ", ";
                    listPair = (Functor2)tail;
                    // Loop again.
                }
                else
                {
                    // The list is not terminated with NIL.
                    result += head + "|" + tail;
                    break;
                }
            }
            result += "]";
            return result;
        }
    }
}
