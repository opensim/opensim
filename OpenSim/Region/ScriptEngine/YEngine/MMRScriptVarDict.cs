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
using System.Collections;
using System.Collections.Generic;

/**
 * @brief Collection of variable/function/method definitions
 */

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public class VarDict: IEnumerable
    {
        public VarDict outerVarDict;            // next outer VarDict to search
        public TokenDeclSDTypeClass thisClass;  // this VarDict is for members of thisClass

        private struct ArgTypes
        {
            public TokenType[] argTypes;

            public bool CanBeCalledBy(TokenType[] calledBy)
            {
                if((argTypes == null) && (calledBy == null))
                    return true;
                if((argTypes == null) || (calledBy == null))
                    return false;
                if(argTypes.Length != calledBy.Length)
                    return false;
                for(int i = argTypes.Length; --i >= 0;)
                {
                    if(!TypeCast.IsAssignableFrom(argTypes[i], calledBy[i]))
                        return false;
                }
                return true;
            }

            public override bool Equals(Object that)
            {
                if(that == null)
                    return false;
                if(that.GetType() != typeof(ArgTypes))
                    return false;
                TokenType[] at = this.argTypes;
                TokenType[] bt = ((ArgTypes)that).argTypes;
                if((at == null) && (bt == null))
                    return true;
                if((at == null) || (bt == null))
                    return false;
                if(at.Length != bt.Length)
                    return false;
                for(int i = at.Length; --i >= 0;)
                {
                    if(at[i].ToString() != bt[i].ToString())
                        return false;
                }
                return true;
            }

            public override int GetHashCode()
            {
                TokenType[] at = this.argTypes;
                if(at == null)
                    return -1;
                int hc = 0;
                for(int i = at.Length; --i >= 0;)
                {
                    int c = (hc < 0) ? 1 : 0;
                    hc = hc * 2 + c;
                    hc ^= at[i].ToString().GetHashCode();
                }
                return hc;
            }
        }

        private struct TDVEntry
        {
            public int count;
            public TokenDeclVar var;
        }

        private bool isFrozen = false;
        private bool locals;
        private Dictionary<string, Dictionary<ArgTypes, TDVEntry>> master = new Dictionary<string, Dictionary<ArgTypes, TDVEntry>>();
        private int count = 0;
        private VarDict frozenLocals = null;

        /**
         * @brief Constructor.
         * @param locals = false: cannot be frozen, allows forward references
         *                  true: can be frozen, thus forbidding forward references
         */
        public VarDict(bool locals)
        {
            this.locals = locals;
        }

        /**
         * @brief Add new variable to the dictionary.
         */
        public bool AddEntry(TokenDeclVar var)
        {
            if(isFrozen)
            {
                throw new Exception("var dict is frozen");
            }

             // Make sure we have a sub-dictionary based on the bare name (ie, no signature)
            Dictionary<ArgTypes, TDVEntry> typedic;
            if(!master.TryGetValue(var.name.val, out typedic))
            {
                typedic = new Dictionary<ArgTypes, TDVEntry>();
                master.Add(var.name.val, typedic);
            }

             // See if there is an entry in the sub-dictionary that matches the argument signature.
             // Note that fields have null argument lists.
             // Methods always have a non-null argument list, even if only 0 entries long.
            ArgTypes types;
            types.argTypes = (var.argDecl == null) ? null : KeyTypesToStringTypes(var.argDecl.types);
            if(typedic.ContainsKey(types))
                return false;

             // It is unique, add to its name-specific sub-dictionary.
            TDVEntry entry;
            entry.count = ++count;
            entry.var = var;
            typedic.Add(types, entry);
            return true;
        }

        public int Count
        {
            get
            {
                return count;
            }
        }

        /**
         * @brief If this is not a local variable frame, just return the frame as is.
         *        If this is a local variable frame, return a version that is frozen,
         *        ie, one that does not contain any future additions.
         */
        public VarDict FreezeLocals()
        {
             // If not local var frame, return original frame as is.
             // This will allow forward references as the future additions
             // will be seen by lookups done in this dictionary.
            if(!locals)
                return this;

             // If local var frame, return a copy frozen at this point.
             // This disallows forward referenes as those future additions
             // will not be seen by lookups done in the frozen dictionary.
            if((frozenLocals == null) || (frozenLocals.count != this.count))
            {
                 // Make a copy of the current var dictionary frame.
                 // We copy a reference to the dictionary, and though it may
                 // contain additions made after this point, those additions
                 // will have a count .gt. frozen count and will be ignored.
                frozenLocals = new VarDict(true);

                frozenLocals.outerVarDict = this.outerVarDict;
                frozenLocals.thisClass = this.thisClass;
                frozenLocals.master = this.master;
                frozenLocals.count = this.count;
                frozenLocals.frozenLocals = frozenLocals;

                 // Mark it as being frozen.
                 // - assert fail if any attempt is made to add to it
                 // - ignore any additions to the dictionary with greater count
                frozenLocals.isFrozen = true;
            }
            return frozenLocals;
        }

        /**
         * @brief Find all functions/variables that are callable
         * @param name = name of function/variable to look for
         * @param argTypes = the argument types the function is being called with
         *                   null to look for a variable
         * @returns null: no matching function/variable found
         *          else: list of matching functions/variables
         *                for variables, always of length 1
         */
        private List<TokenDeclVar> found = new List<TokenDeclVar>();
        public TokenDeclVar[] FindCallables(string name, TokenType[] argTypes)
        {
            argTypes = KeyTypesToStringTypes(argTypes);
            TokenDeclVar var = FindExact(name, argTypes);
            if(var != null)
                return new TokenDeclVar[] { var };

            Dictionary<ArgTypes, TDVEntry> typedic;
            if(!master.TryGetValue(name, out typedic))
                return null;

            found.Clear();
            foreach(KeyValuePair<ArgTypes, TDVEntry> kvp in typedic)
            {
                if((kvp.Value.count <= this.count) && kvp.Key.CanBeCalledBy(argTypes))
                {
                    found.Add(kvp.Value.var);
                }
            }
            return (found.Count > 0) ? found.ToArray() : null;
        }

        /**
         * @brief Find exact matching function/variable
         * @param name = name of function to look for
         * @param argTypes = argument types the function was declared with
         *                   null to look for a variable
         * @returns null: no matching function/variable found
         *          else: the matching function/variable
         */
        public TokenDeclVar FindExact(string name, TokenType[] argTypes)
        {
             // Look for list of stuff that matches the given name.
            Dictionary<ArgTypes, TDVEntry> typedic;
            if(!master.TryGetValue(name, out typedic))
                return null;

             // Loop through all fields/methods declared by that name, regardless of arg signature.
            foreach(TDVEntry entry in typedic.Values)
            {
                if(entry.count > this.count)
                    continue;
                TokenDeclVar var = entry.var;

                 // Get argument types of declaration.
                 //   fields are always null
                 //   methods are always non-null, though may be zero-length
                TokenType[] declArgs = (var.argDecl == null) ? null : var.argDecl.types;

                 // Convert any key args to string args.
                declArgs = KeyTypesToStringTypes(declArgs);

                 // If both are null, they are signature-less (ie, both are fields), and so match.
                if((declArgs == null) && (argTypes == null))
                    return var;

                 // If calling a delegate, it is a match, regardless of delegate arg types.
                 // If it turns out the arg types do not match, the compiler will give an error
                 // trying to cast the arguments to the delegate arg types.
                 // We don't allow overloading same field name with different delegate types.
                if((declArgs == null) && (argTypes != null))
                {
                    TokenType fieldType = var.type;
                    if(fieldType is TokenTypeSDTypeDelegate)
                        return var;
                }

                 // If not both null, no match, keep looking.
                if((declArgs == null) || (argTypes == null))
                    continue;

                 // Both not null, match argument types to make sure we have correct overload.
                int i = declArgs.Length;
                if(i != argTypes.Length)
                    continue;
                while(--i >= 0)
                {
                    string da = declArgs[i].ToString();
                    string ga = argTypes[i].ToString();
                    if(da == "key")
                        da = "string";
                    if(ga == "key")
                        ga = "string";
                    if(da != ga)
                        break;
                }
                if(i < 0)
                    return var;
            }

             // No match.
            return null;
        }

        /**
         * @brief Replace any TokenTypeKey elements with TokenTypeStr so that
         *        it doesn't matter if functions are declared with key or string,
         *        they will accept either.
         * @param argTypes = argument types as declared in source code
         * @returns argTypes with any key replaced by string
         */
        private static TokenType[] KeyTypesToStringTypes(TokenType[] argTypes)
        {
            if(argTypes != null)
            {
                int i;
                int nats = argTypes.Length;
                for(i = nats; --i >= 0;)
                {
                    if(argTypes[i] is TokenTypeKey)
                        break;
                }
                if(i >= 0)
                {
                    TokenType[] at = new TokenType[nats];
                    for(i = nats; --i >= 0;)
                    {
                        at[i] = argTypes[i];
                        if(argTypes[i] is TokenTypeKey)
                        {
                            at[i] = new TokenTypeStr(argTypes[i]);
                        }
                    }
                    return at;
                }
            }
            return argTypes;
        }

        // foreach goes through all the TokenDeclVars that were added

        // IEnumerable
        public IEnumerator GetEnumerator()
        {
            return new VarDictEnumerator(this.master, this.count);
        }

        private class VarDictEnumerator: IEnumerator
        {
            private IEnumerator masterEnum;
            private IEnumerator typedicEnum;
            private int count;

            public VarDictEnumerator(Dictionary<string, Dictionary<ArgTypes, TDVEntry>> master, int count)
            {
                masterEnum = master.Values.GetEnumerator();
                this.count = count;
            }

            // IEnumerator
            public void Reset()
            {
                masterEnum.Reset();
                typedicEnum = null;
            }

            // IEnumerator
            public bool MoveNext()
            {
                while(true)
                {
                    if(typedicEnum != null)
                    {
                        while(typedicEnum.MoveNext())
                        {
                            if(((TDVEntry)typedicEnum.Current).count <= this.count)
                                return true;
                        }
                        typedicEnum = null;
                    }
                    if(!masterEnum.MoveNext())
                        return false;
                    Dictionary<ArgTypes, TDVEntry> ctd;
                    ctd = (Dictionary<ArgTypes, TDVEntry>)masterEnum.Current;
                    typedicEnum = ctd.Values.GetEnumerator();
                }
            }

            // IEnumerator
            public object Current
            {
                get
                {
                    return ((TDVEntry)typedicEnum.Current).var;
                }
            }
        }
    }
}
