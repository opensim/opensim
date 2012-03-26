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
    /// An IndexedAnswers holds answers to a query based on the values of index arguments.
    /// </summary>
    public class IndexedAnswers : YP.IClause
    {
        private int _arity;
        // addAnswer adds the answer here and indexes it later.
        private List<object[]> _allAnswers = new List<object[]>();
        // The key has the arity of answers with non-null values for each indexed arg.  The value
        //   is a list of the matching answers.  The signature is implicit in the pattern on non-null index args.
        private Dictionary<HashedList, List<object[]>> _indexedAnswers =
            new Dictionary<HashedList, List<object[]>>();
        // Keeps track of whether we have started adding entries to _indexedAnswers for the signature.
        private Dictionary<int, object> _gotAnswersForSignature = new Dictionary<int, object>();
        private const int MAX_INDEX_ARGS = 31;

        public IndexedAnswers(int arity)
        {
            _arity = arity;
        }

        /// <summary>
        /// Append the answer to the list and update the indexes, if any.
        /// Elements of answer must be ground, since arguments with unbound variables make this
        /// into a dynamic rule which we don't index.
        /// </summary>
        /// <param name="answer"></param>
        public void addAnswer(object[] answer)
        {
            addOrPrependAnswer(answer, false);
        }

        /// <summary>
        /// Prepend the answer to the list and clear the indexes so that they must be re-computed
        /// on the next call to match.  (Only addAnswer will maintain the indexes while adding answers.)
        /// Elements of answer must be ground, since arguments with unbound variables make this
        /// into a dynamic rule which we don't index.
        /// </summary>
        /// <param name="answer"></param>
        public void prependAnswer(object[] answer)
        {
            addOrPrependAnswer(answer, true);
        }

        /// <summary>
        /// Do the work of addAnswer or prependAnswer.
        /// </summary>
        /// <param name="answer"></param>
        private void addOrPrependAnswer(object[] answer, bool prepend)
        {
            if (answer.Length != _arity)
                return;

            // Store a copy of the answer array.
            object[] answerCopy = new object[answer.Length];
            Variable.CopyStore copyStore = new Variable.CopyStore();
            for (int i = 0; i < answer.Length; ++i)
                answerCopy[i] = YP.makeCopy(answer[i], copyStore);
            if (copyStore.getNUniqueVariables() > 0)
                throw new InvalidOperationException
                    ("Elements of answer must be ground, but found " + copyStore.getNUniqueVariables() +
                     " unbound variables");

            if (prepend)
            {
                _allAnswers.Insert(0, answerCopy);
                clearIndexes();
            }
            else
            {
                _allAnswers.Add(answerCopy);
                // If match has already indexed answers for a signature, we need to add
                //   this to the existing indexed answers.
                foreach (int signature in _gotAnswersForSignature.Keys)
                    indexAnswerForSignature(answerCopy, signature);
            }
        }

        private void indexAnswerForSignature(object[] answer, int signature)
        {
            // First find out which of the answer values can be used as an index.
            object[] indexValues = new object[answer.Length];
            for (int i = 0; i < answer.Length; ++i)
            {
                // We limit the number of indexed args in a 32-bit signature.
                if (i >= MAX_INDEX_ARGS)
                    indexValues[i] = null;
                else
                    indexValues[i] = getIndexValue(YP.getValue(answer[i]));
            }

            // We need an entry in indexArgs from indexValues for each 1 bit in signature.
            HashedList indexArgs = new HashedList(indexValues.Length);
            for (int i = 0; i < indexValues.Length; ++i)
            {
                if ((signature & (1 << i)) == 0)
                    indexArgs.Add(null);
                else
                {
                    if (indexValues[i] == null)
                        // The signature wants an index value here, but we don't have one so
                        //   we can't add it as an answer for this signature.
                        return;
                    else
                        indexArgs.Add(indexValues[i]);
                }
            }

            // Add the answer to the answers list for indexArgs, creating the entry if needed.
            List<object[]> answers;
            if (!_indexedAnswers.TryGetValue(indexArgs, out answers))
            {
                answers = new List<object[]>();
                _indexedAnswers[indexArgs] = answers;
            }
            answers.Add(answer);
        }

        public IEnumerable<bool> match(object[] arguments)
        {
            if (arguments.Length != _arity)
                yield break;

            // Set up indexArgs, up to arg position MAX_INDEX_ARGS.  The signature has a 1 bit for
            //   each non-null index arg.
            HashedList indexArgs = new HashedList(arguments.Length);
            bool gotAllIndexArgs = true;
            int signature = 0;
            for (int i = 0; i < arguments.Length; ++i)
            {
                object indexValue = null;
                if (i < MAX_INDEX_ARGS)
                {
                    // We limit the number of args in a 32-bit signature.
                    indexValue = getIndexValue(YP.getValue(arguments[i]));
                    if (indexValue != null)
                        signature += (1 << i);
                }
                if (indexValue == null)
                    gotAllIndexArgs = false;
                indexArgs.Add(indexValue);
            }

            List<object[]> answers;
            if (signature == 0)
                // No index args, so we have to match from _allAnswers.
                answers = _allAnswers;
            else
            {
                if (!_gotAnswersForSignature.ContainsKey(signature))
                {
                    // We need to create the entry in _indexedAnswers.
                    foreach (object[] answer in _allAnswers)
                        indexAnswerForSignature(answer, signature);
                    // Mark that we did this signature.
                    _gotAnswersForSignature[signature] = null;
                }
                if (!_indexedAnswers.TryGetValue(indexArgs, out answers))
                    yield break;
            }

            if (gotAllIndexArgs)
            {
                // All the arguments were already bound, so we don't need to do bindings.
                yield return false;
                yield break;
            }

            // Find matches in answers.
            IEnumerator<bool>[] iterators = new IEnumerator<bool>[arguments.Length];
            // Debug: If the caller asserts another answer into this same predicate during yield, the iterator
            //   over clauses will be corrupted.  Should we take the time to copy answers?
            foreach (object[] answer in answers)
            {
                bool gotMatch = true;
                int nIterators = 0;
                // Try to bind all the arguments.
                for (int i = 0; i < arguments.Length; ++i)
                {
                    if (indexArgs[i] != null)
                        // We already matched this argument by looking up _indexedAnswers.
                        continue;

                    IEnumerator<bool> iterator = YP.unify(arguments[i], answer[i]).GetEnumerator();
                    iterators[nIterators++] = iterator;
                    // MoveNext() is true if YP.unify succeeds.
                    if (!iterator.MoveNext())
                    {
                        gotMatch = false;
                        break;
                    }
                }
                int z = 0;
                try
                {
                    if (gotMatch)
                        yield return false;
                }
                finally
                {
                    // Manually finalize all the iterators.
                    for (z = 0; z < nIterators; ++z)
                        iterators[z].Dispose();
                }
            }
        }

        public IEnumerable<bool> clause(object Head, object Body)
        {
            Head = YP.getValue(Head);
            if (Head is Variable)
                throw new PrologException("instantiation_error", "Head is an unbound variable");
            object[] arguments = YP.getFunctorArgs(Head);

            // We always match Head from _allAnswers, and the Body is Atom.a("true").
            #pragma warning disable 0168, 0219
            foreach (bool l1 in YP.unify(Body, Atom.a("true")))
            {
                // The caller can assert another answer into this same predicate during yield, so we have to
                //   make a copy of the answers.
                foreach (object[] answer in _allAnswers.ToArray())
                {
                    foreach (bool l2 in YP.unifyArrays(arguments, answer))
                        yield return false;
                }
            }
            #pragma warning restore 0168, 0219
        }

        public IEnumerable<bool> retract(object Head, object Body)
        {
            Head = YP.getValue(Head);
            if (Head is Variable)
                throw new PrologException("instantiation_error", "Head is an unbound variable");
            object[] arguments = YP.getFunctorArgs(Head);

            // We always match Head from _allAnswers, and the Body is Atom.a("true").
            #pragma warning disable 0168, 0219
            foreach (bool l1 in YP.unify(Body, Atom.a("true")))
            {
                // The caller can assert another answer into this same predicate during yield, so we have to
                //   make a copy of the answers.
                foreach (object[] answer in _allAnswers.ToArray())
                {
                    foreach (bool l2 in YP.unifyArrays(arguments, answer))
                    {
                        _allAnswers.Remove(answer);
                        clearIndexes();
                        yield return false;
                    }
                }
            }
            #pragma warning restore 0168, 0219
        }

        /// <summary>
        /// After retracting or prepending an answer in _allAnswers, the indexes are invalid, so clear them.
        /// </summary>
        private void clearIndexes()
        {
            _indexedAnswers.Clear();
            _gotAnswersForSignature.Clear();
        }

        /// <summary>
        /// A HashedList extends an ArrayList with methods to get a hash and to check equality
        /// based on the elements of the list.
        /// </summary>
        public class HashedList : ArrayList
        {
            private bool _gotHashCode = false;
            private int _hashCode;

            public HashedList()
                : base()
            {
            }

            public HashedList(int capacity)
                : base(capacity)
            {
            }

            public HashedList(ICollection c)
                : base(c)
            {
            }

            // Debug: Should override all the other methods that change this.
            public override int Add(object value)
            {
                _gotHashCode = false;
                return base.Add(value);
            }

            public override int GetHashCode()
            {
                if (!_gotHashCode)
                {
                    int hashCode = 1;
                    foreach (object obj in this)
                        hashCode = 31 * hashCode + (obj == null ? 0 : obj.GetHashCode());
                    _hashCode = hashCode;
                    _gotHashCode = true;
                }
                return _hashCode;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is ArrayList))
                    return false;

                ArrayList objList = (ArrayList)obj;
                if (objList.Count != Count)
                    return false;

                for (int i = 0; i < Count; ++i)
                {
                    object value = objList[i];
                    if (value == null)
                    {
                        if (this[i] != null)
                            return false;
                    }
                    else
                    {
                        if (!value.Equals(this[i]))
                            return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// If we keep an index on value, return the value, or null if we don't index it.
        /// </summary>
        /// <param name="value">the term to examine.  Assume you already called YP.getValue(value)</param>
        /// <returns></returns>
        public static object getIndexValue(object value)
        {
            if (value is Atom || value is string || value is Int32 || value is DateTime)
                return value;
            else
                return null;
        }
    }
}
