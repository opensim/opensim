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
    public class ListPair : Functor2
    {
        public ListPair(object head, object tail) : base(Atom.DOT, head, tail)
        {
        }

        public static object make(List<object> list)
        {
            if (list.Count <= 0)
                return Atom.NIL;

            object result = Atom.NIL;
            // Start from the end.
            for (int i = list.Count - 1; i >= 0; --i)
                result = new ListPair(list[i], result);
            return result;
        }

        public static object make(object[] array)
        {
            if (array.Length <= 0)
                return Atom.NIL;

            object result = Atom.NIL;
            // Start from the end.
            for (int i = array.Length - 1; i >= 0; --i)
                result = new ListPair(array[i], result);
            return result;
        }

        /// <summary>
        /// Return a ListPair version of array, where repeated elements
        /// (according to YP.termEqual) are removed.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static object makeWithoutRepeatedTerms(object[] array)
        {
            if (array.Length <= 0)
                return Atom.NIL;

            // Start from the end.
            object previousTerm = array[array.Length - 1];
            object result = new ListPair(previousTerm, Atom.NIL);
            for (int i = array.Length - 2; i >= 0; --i)
            {
                object term = array[i];
                if (YP.termEqual(term, previousTerm))
                    continue;
                result = new ListPair(term, result);
                previousTerm = term;
            }
            return result;
        }

        /// <summary>
        /// Return a ListPair version of array, where repeated elements
        /// (according to YP.termEqual) are removed.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static object makeWithoutRepeatedTerms(List<object> array)
        {
            if (array.Count <= 0)
                return Atom.NIL;

            // Start from the end.
            object previousTerm = array[array.Count - 1];
            object result = new ListPair(previousTerm, Atom.NIL);
            for (int i = array.Count - 2; i >= 0; --i)
            {
                object term = array[i];
                if (YP.termEqual(term, previousTerm))
                    continue;
                result = new ListPair(term, result);
                previousTerm = term;
            }
            return result;
        }

        public static object make(object element1)
        {
            return new ListPair(element1, Atom.NIL);
        }

        public static object make(object element1, object element2)
        {
            return new ListPair(element1, new ListPair(element2, Atom.NIL));
        }

        public static object make(object element1, object element2, object element3)
        {
            return new ListPair(element1,
                new ListPair(element2, new ListPair(element3, Atom.NIL)));
        }

        /// <summary>
        /// Return an array of the elements in list or null if it is not
        /// a proper list.  If list is Atom.NIL, return an array of zero elements.
        /// If the list or one of the tails of the list is Variable, raise an instantiation_error.
        /// This does not call YP.getValue on each element.
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static object[] toArray(object list)
        {
            list = YP.getValue(list);
            if (list.Equals(Atom.NIL))
                return new object[0];

            List<object> result = new List<object>();
            object element = list;
            while (true) {
                if (element == Atom.NIL)
                    break;
                if (element is Variable)
                    throw new PrologException(Atom.a("instantiation_error"),
                        "List tail is an unbound variable");
                if (!(element is Functor2 && ((Functor2)element)._name == Atom.DOT))
                    // Not a proper list.
                    return null;
                result.Add(((Functor2)element)._arg1);
                element = YP.getValue(((Functor2)element)._arg2);
            }

            if (result.Count <= 0)
                return null;
            return result.ToArray();
        }
    }
}
