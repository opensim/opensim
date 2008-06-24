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
using System.Text;

namespace OpenSim.Region.ScriptEngine.Shared.YieldProlog
{
    public class Atom : IUnifiable
    {
        private static Dictionary<string, Atom> _atomStore = new Dictionary<string, Atom>();
        public readonly string _name;
        public readonly Atom _module;

        /// <summary>
        /// You should not call this constructor, but use Atom.a instead.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="module"></param>
        private Atom(string name, Atom module)
        {
            _name = name;
            _module = module;
        }

        /// <summary>
        /// Return the unique Atom object for name where module is null. You should use this to create
        /// an Atom instead of calling the Atom constructor.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Atom a(string name)
        {
            Atom atom;
            if (!_atomStore.TryGetValue(name, out atom))
            {
                atom = new Atom(name, null);
                _atomStore[name] = atom;
            }
            return atom;
        }

        /// <summary>
        /// Return an Atom object with the name and module.  If module is null or Atom.NIL,
        /// this behaves like Atom.a(name) and returns the unique object where the module is null.
        /// If module is not null or Atom.NIL, this may or may not be the same object as another Atom
        /// with the same name and module.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public static Atom a(string name, Atom module)
        {
            if (module == null || module == Atom.NIL)
                return a(name);
            return new Atom(name, module);
        }

        /// <summary>
        /// If Obj is an Atom unify its _module with Module.  If the Atom's _module is null, use Atom.NIL.
        /// </summary>
        /// <param name="Atom"></param>
        /// <param name="Module"></param>
        /// <returns></returns>
        public static IEnumerable<bool> module(object Obj, object Module)
        {
            Obj = YP.getValue(Obj);
            if (Obj is Atom)
            {
                if (((Atom)Obj)._module == null)
                    return YP.unify(Module, Atom.NIL);
                else
                    return YP.unify(Module, ((Atom)Obj)._module);
            }
            return YP.fail();
        }

        public static readonly Atom NIL = Atom.a("[]");
        public static readonly Atom DOT = Atom.a(".");
        public static readonly Atom F = Atom.a("f");
        public static readonly Atom SLASH = Atom.a("/");
        public static readonly Atom HAT = Atom.a("^");
        public static readonly Atom RULE = Atom.a(":-");

        public IEnumerable<bool> unify(object arg)
        {
            arg = YP.getValue(arg);
            if (arg is Atom)
                return Equals(arg) ? YP.succeed() : YP.fail();
            else if (arg is Variable)
                return ((Variable)arg).unify(this);
            else
                return YP.fail();
        }

        public void addUniqueVariables(List<Variable> variableSet)
        {
            // Atom does not contain variables.
        }

        public object makeCopy(Variable.CopyStore copyStore)
        {
            // Atom does not contain variables that need to be copied.
            return this;
        }

        public bool termEqual(object term)
        {
            return Equals(YP.getValue(term));
        }

        public bool ground()
        {
            // Atom is always ground.
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is Atom)
            {
                if (_module == null && ((Atom)obj)._module == null)
                    // When _declaringClass is null, we always use an identical object from _atomStore.
                    return this == obj;
                // Otherwise, ignore _declaringClass and do a normal string compare on the _name.
                return _name == ((Atom)obj)._name;
            }
            return false;
        }

        public override string ToString()
        {
            return _name;
        }

        public override int GetHashCode()
        {
            // Debug: need to check _declaringClass.
            return _name.GetHashCode();
        }

        public string toQuotedString()
        {
            if (_name.Length == 0)
                return "''";
            else if (this == Atom.NIL)
                return "[]";

            StringBuilder result = new StringBuilder(_name.Length);
            bool useQuotes = false;
            foreach (char c in _name)
            {
                int cInt = (int)c;
                if (c == '\'')
                {
                    result.Append("''");
                    useQuotes = true;
                }
                else if (c == '_' || cInt >= (int)'a' && cInt <= (int)'z' ||
                         cInt >= (int)'A' && cInt <= (int)'Z' || cInt >= (int)'0' && cInt <= (int)'9')
                    result.Append(c);
                else
                {
                    // Debug: Need to handle non-printable chars.
                    result.Append(c);
                    useQuotes = true;
                }
            }

            if (!useQuotes && (int)_name[0] >= (int)'a' && (int)_name[0] <= (int)'z')
                return result.ToString();
            else
            {
                // Surround in single quotes.
                result.Append('\'');
                return "'" + result;
            }
        }

        /// <summary>
        /// Return true if _name is lexicographically less than atom._name.
        /// </summary>
        /// <param name="atom"></param>
        /// <returns></returns>
        public bool lessThan(Atom atom)
        {
            return _name.CompareTo(atom._name) < 0;
        }
    }
}
