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
    /// A FindallAnswers holds answers for findall.
    /// </summary>
    public class FindallAnswers
    {
        private object _template;
        private List<object> _bagArray;

        public FindallAnswers(object Template)
        {
            _template = Template;
            _bagArray = new List<object>();
        }

        public void add()
        {
            _bagArray.Add(YP.makeCopy(_template, new Variable.CopyStore()));
        }

        public List<object> resultArray()
        {
            return _bagArray;
        }

        /// <summary>
        /// Unify Bag with the result. This frees the internal answers, so you can only call this once.
        /// </summary>
        /// <param name="Bag"></param>
        /// <returns></returns>
        public IEnumerable<bool> result(object Bag)
        {
            object result = ListPair.make(_bagArray);
            // Try to free the memory.
            _bagArray = null;
            return YP.unify(Bag, result);
        }

        // disable warning on l1, don't see how we can
        // code this differently
        #pragma warning disable 0168, 0219

        /// <summary>
        /// This is a simplified findall when the goal is a single call.
        /// </summary>
        /// <param name="Template"></param>
        /// <param name="goal"></param>
        /// <param name="Bag"></param>
        /// <returns></returns>
        public static IEnumerable<bool> findall(object Template, IEnumerable<bool> goal, object Bag)
        {
            FindallAnswers findallAnswers = new FindallAnswers(Template);
            foreach (bool l1 in goal)
                findallAnswers.add();
            return findallAnswers.result(Bag);
        }

        /// <summary>
        /// Like findall, except return an array of the results.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="goal"></param>
        /// <returns></returns>
        public static List<object> findallArray(object Template, IEnumerable<bool> goal)
        {
            FindallAnswers findallAnswers = new FindallAnswers(Template);
            foreach (bool l1 in goal)
                findallAnswers.add();
            return findallAnswers.resultArray();
        }
        #pragma warning restore 0168, 0219
    }
}
