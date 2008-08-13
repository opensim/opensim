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
    public class Functor : IUnifiable
    {
        public readonly Atom _name;
        public readonly object[] _args;

        public Functor(Atom name, object[] args)
        {
            if (args.Length <= 3)
            {
                if (args.Length == 0)
                    throw new Exception("For arity 0 functor, just use name as an Atom");
                else if (args.Length == 1)
                    throw new Exception("For arity 1 functor, use Functor1");
                else if (args.Length == 2)
                    throw new Exception("For arity 2 functor, use Functor2");
                else if (args.Length == 3)
                    throw new Exception("For arity 3 functor, use Functor3");
                else
                    // (This shouldn't happen, but include it for completeness.
                    throw new Exception("Cannot create a Functor of arity " + args.Length);
            }

            _name = name;
            _args = args;
        }

        public Functor(string name, object[] args)
            : this(Atom.a(name), args)
        {
        }

        /// <summary>
        /// Return an Atom, Functor1, Functor2, Functor3 or Functor depending on the
        /// length of args.
        /// Note that this is different than the Functor constructor which requires
        /// the length of args to be greater than 3.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static object make(Atom name, object[] args)
        {
            if (args.Length <= 0)
                return name;
            else if (args.Length == 1)
                return new Functor1(name, args[0]);
            else if (args.Length == 2)
                return new Functor2(name, args[0], args[1]);
            else if (args.Length == 3)
                return new Functor3(name, args[0], args[1], args[2]);
            else
                return new Functor(name, args);
        }

        /// <summary>
        /// Call the main make, first converting name to an Atom.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static object make(string name, object[] args)
        {
            return make(Atom.a(name), args);
        }

        /// <summary>
        /// If arg is another Functor, then succeed (yield once) if this and arg have the
        /// same name and all functor args unify, otherwise fail (don't yield).
        /// If arg is a Variable, then call its unify to unify with this.
        /// Otherwise fail (don't yield).
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public IEnumerable<bool> unify(object arg)
        {
            arg = YP.getValue(arg);
            if (arg is Functor)
            {
                Functor argFunctor = (Functor)arg;
                if (_name.Equals(argFunctor._name))
                    return YP.unifyArrays(_args, argFunctor._args);
                else
                    return YP.fail();
            }
            else if (arg is Variable)
                return ((Variable)arg).unify(this);
            else
                return YP.fail();
        }

        public override string ToString()
        {
            string result = _name + "(" + YP.getValue(_args[0]);
            for (int i = 1; i < _args.Length; ++i)
                result += ", " + YP.getValue(_args[i]);
            result += ")";
            return result;
        }

        public bool termEqual(object term)
        {
            term = YP.getValue(term);
            if (term is Functor)
            {
                Functor termFunctor = (Functor)term;
                if (_name.Equals(termFunctor._name) && _args.Length == termFunctor._args.Length)
                {
                    for (int i = 0; i < _args.Length; ++i)
                    {
                        if (!YP.termEqual(_args[i], termFunctor._args[i]))
                            return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public bool lessThan(Functor functor)
        {
            // Do the equal check first since it is faster.
            if (!_name.Equals(functor._name))
                return _name.lessThan(functor._name);

            if (_args.Length != functor._args.Length)
                return _args.Length < functor._args.Length;

            for (int i = 0; i < _args.Length; ++i)
            {
                if (!YP.termEqual(_args[i], functor._args[i]))
                    return YP.termLessThan(_args[i], functor._args[i]);
            }

            return false;
        }

        public bool ground()
        {
            for (int i = 0; i < _args.Length; ++i)
            {
                if (!YP.ground(_args[i]))
                    return false;
            }
            return true;
        }

        public void addUniqueVariables(List<Variable> variableSet)
        {
            for (int i = 0; i < _args.Length; ++i)
                YP.addUniqueVariables(_args[i], variableSet);
        }

        public object makeCopy(Variable.CopyStore copyStore)
        {
            object[] argsCopy = new object[_args.Length];
            for (int i = 0; i < _args.Length; ++i)
                argsCopy[i] = YP.makeCopy(_args[i], copyStore);
            return new Functor(_name, argsCopy);
        }
    }
}
