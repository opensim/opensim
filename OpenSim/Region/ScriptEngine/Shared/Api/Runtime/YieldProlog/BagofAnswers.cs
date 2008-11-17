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
using System.Collections;
using System.Collections.Generic;

namespace OpenSim.Region.ScriptEngine.Shared.YieldProlog
{
    /// <summary>
    /// A BagofAnswers holds answers for bagof and setof.
    /// </summary>
    public class BagofAnswers
    {
        private object _template;
        private Variable[] _freeVariables;
        private Dictionary<object[], List<object>> _bagForFreeVariables;
        private List<object> _findallBagArray;
        private static TermArrayEqualityComparer _termArrayEqualityComparer =
            new TermArrayEqualityComparer();

        /// <summary>
        /// To get the free variables, split off any existential qualifiers from Goal such as the X in
        /// "X ^ f(Y)", get the set of unbound variables in Goal that are not qualifiers, then remove
        /// the unbound variables that are qualifiers as well as the unbound variables in Template.
        /// </summary>
        /// <param name="Template"></param>
        /// <param name="Goal"></param>
        public BagofAnswers(object Template, object Goal)
        {
            _template = Template;

            // First get the set of variables that are not free variables.
            List<Variable> variableSet = new List<Variable>();
            YP.addUniqueVariables(Template, variableSet);
            object UnqualifiedGoal = YP.getValue(Goal);
            while (UnqualifiedGoal is Functor2 && ((Functor2)UnqualifiedGoal)._name == Atom.HAT)
            {
                YP.addUniqueVariables(((Functor2)UnqualifiedGoal)._arg1, variableSet);
                UnqualifiedGoal = YP.getValue(((Functor2)UnqualifiedGoal)._arg2);
            }

            // Remember how many non-free variables there are so we can find the unique free variables
            //   that are added.
            int nNonFreeVariables = variableSet.Count;
            YP.addUniqueVariables(UnqualifiedGoal, variableSet);
            int nFreeVariables = variableSet.Count - nNonFreeVariables;
            if (nFreeVariables == 0)
            {
                // There were no free variables added, so we won't waste time with _bagForFreeVariables.
                _freeVariables = null;
                _findallBagArray = new List<object>();
            }
            else
            {
                // Copy the free variables.
                _freeVariables = new Variable[nFreeVariables];
                for (int i = 0; i < nFreeVariables; ++i)
                    _freeVariables[i] = variableSet[i + nNonFreeVariables];

                _bagForFreeVariables = new Dictionary<object[], List<object>>(_termArrayEqualityComparer);
            }
        }

        public void add()
        {
            if (_freeVariables == null)
                // The goal has bound the values in _template but we don't bother with _freeVariables.
                _findallBagArray.Add(YP.makeCopy(_template, new Variable.CopyStore()));
            else
            {
                // The goal has bound the values in _template and _freeVariables.
                // Find the entry for this set of _freeVariables values.
                object[] freeVariableValues = new object[_freeVariables.Length];
                for (int i = 0; i < _freeVariables.Length; ++i)
                    freeVariableValues[i] = YP.getValue(_freeVariables[i]);
                List<object> bagArray;
                if (!_bagForFreeVariables.TryGetValue(freeVariableValues, out bagArray))
                {
                    bagArray = new List<object>();
                    _bagForFreeVariables[freeVariableValues] = bagArray;
                }

                // Now copy the template and add to the bag for the freeVariables values.
                bagArray.Add(YP.makeCopy(_template, new Variable.CopyStore()));
            }
        }

        // disable warning on l1, don't see how we can
        // code this differently
        #pragma warning disable 0168, 0219

        /// <summary>
        /// For each result, unify the _freeVariables and unify bagArrayVariable with the associated bag.
        /// </summary>
        /// <param name="bagArrayVariable">this is unified with the List<object> of matches for template that
        /// corresponds to the bindings for freeVariables.  Be very careful: this does not unify with a Prolog
        /// list.</param>
        /// <returns></returns>
        public IEnumerable<bool> resultArray(Variable bagArrayVariable)
        {
            if (_freeVariables == null)
            {
                // No unbound free variables, so we only filled one bag.  If empty, bagof fails.
                if (_findallBagArray.Count > 0)
                {
                    foreach (bool l1 in bagArrayVariable.unify(_findallBagArray))
                        yield return false;
                }
            }
            else
            {
                foreach (KeyValuePair<object[], List<object>> valuesAndBag in _bagForFreeVariables)
                {
                    foreach (bool l1 in YP.unifyArrays(_freeVariables, valuesAndBag.Key))
                    {
                        foreach (bool l2 in bagArrayVariable.unify(valuesAndBag.Value))
                            yield return false;
                    }
                    // Debug: Should we free memory of the answers already returned?
                }
            }
        }

        /// <summary>
        /// For each result, unify the _freeVariables and unify Bag with the associated bag.
        /// </summary>
        /// <param name="Bag"></param>
        /// <returns></returns>
        public IEnumerable<bool> result(object Bag)
        {
            Variable bagArrayVariable = new Variable();
            foreach (bool l1 in resultArray(bagArrayVariable))
            {
                foreach (bool l2 in YP.unify(Bag, ListPair.make((List<object>)bagArrayVariable.getValue())))
                    yield return false;
            }
        }

        /// <summary>
        /// For each result, unify the _freeVariables and unify Bag with the associated bag which is sorted
        /// with duplicates removed, as in setof.
        /// </summary>
        /// <param name="Bag"></param>
        /// <returns></returns>
        public IEnumerable<bool> resultSet(object Bag)
        {
            Variable bagArrayVariable = new Variable();
            foreach (bool l1 in resultArray(bagArrayVariable))
            {
                List<object> bagArray = (List<object>)bagArrayVariable.getValue();
                YP.sortArray(bagArray);
                foreach (bool l2 in YP.unify(Bag, ListPair.makeWithoutRepeatedTerms(bagArray)))
                    yield return false;
            }
        }

        public static IEnumerable<bool> bagofArray
            (object Template, object Goal, IEnumerable<bool> goalIterator, Variable bagArrayVariable)
        {
            BagofAnswers bagOfAnswers = new BagofAnswers(Template, Goal);
            foreach (bool l1 in goalIterator)
                bagOfAnswers.add();
            return bagOfAnswers.resultArray(bagArrayVariable);
        }

        public static IEnumerable<bool> bagof
            (object Template, object Goal, IEnumerable<bool> goalIterator, object Bag)
        {
            BagofAnswers bagOfAnswers = new BagofAnswers(Template, Goal);
            foreach (bool l1 in goalIterator)
                bagOfAnswers.add();
            return bagOfAnswers.result(Bag);
        }

        public static IEnumerable<bool> setof
            (object Template, object Goal, IEnumerable<bool> goalIterator, object Bag)
        {
            BagofAnswers bagOfAnswers = new BagofAnswers(Template, Goal);
            foreach (bool l1 in goalIterator)
                bagOfAnswers.add();
            return bagOfAnswers.resultSet(Bag);
        }
        #pragma warning restore 0168, 0219

        /// <summary>
        /// A TermArrayEqualityComparer implements IEqualityComparer to compare two object arrays using YP.termEqual.
        /// </summary>
        private class TermArrayEqualityComparer : IEqualityComparer<object[]>
        {
            public bool Equals(object[] array1, object[] array2)
            {
                if (array1.Length != array2.Length)
                    return false;
                for (int i = 0; i < array1.Length; ++i)
                {
                    if (!YP.termEqual(array1[i], array2[i]))
                        return false;
                }
                return true;
            }

            public int GetHashCode(object[] array)
            {
                int hashCode = 0;
                for (int i = 0; i < array.Length; ++i)
                    hashCode ^= array[i].GetHashCode();
                return hashCode;
            }
        }
    }
}
