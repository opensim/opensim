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

namespace OpenSim.Region.ScriptEngine.Yengine
{
    /**
     * @brief Implements a queue of XMRInstance's.
     *        Do our own queue to avoid shitty little mallocs.
     *
     * Note: looping inst.m_NextInst and m_PrevInst back to itself
     *       when inst is removed from a queue is purely for debug.
     */
    public class XMRInstQueue
    {
        private XMRInstance m_Head = null;
        private XMRInstance m_Tail = null;

        /**
         * @brief Insert instance at head of queue (in front of all others)
         * @param inst = instance to insert
         */
        public void InsertHead(XMRInstance inst)
        {
            if((inst.m_PrevInst != inst) || (inst.m_NextInst != inst))
                throw new Exception("already in list");

            inst.m_PrevInst = null;
            if((inst.m_NextInst = m_Head) == null)
                m_Tail = inst;
            else
                m_Head.m_PrevInst = inst;

            m_Head = inst;
        }

        /**
         * @brief Insert instance at tail of queue (behind all others)
         * @param inst = instance to insert
         */
        public void InsertTail(XMRInstance inst)
        {
            if((inst.m_PrevInst != inst) || (inst.m_NextInst != inst))
                throw new Exception("already in list");

            inst.m_NextInst = null;
            if((inst.m_PrevInst = m_Tail) == null)
                m_Head = inst;
            else
                m_Tail.m_NextInst = inst;

            m_Tail = inst;
        }

        /**
         * @brief Insert instance before another element in queue
         * @param inst  = instance to insert
         * @param after = element that is to come after one being inserted
         */
        public void InsertBefore(XMRInstance inst, XMRInstance after)
        {
            if((inst.m_PrevInst != inst) || (inst.m_NextInst != inst))
                throw new Exception("already in list");

            if(after == null)
                InsertTail(inst);
            else
            {
                inst.m_NextInst = after;
                inst.m_PrevInst = after.m_PrevInst;
                if(inst.m_PrevInst == null)
                    m_Head = inst;
                else
                    inst.m_PrevInst.m_NextInst = inst;
                after.m_PrevInst = inst;
            }
        }

        /**
         * @brief Peek to see if anything in queue
         * @returns first XMRInstance in queue but doesn't remove it
         *          null if queue is empty
         */
        public XMRInstance PeekHead()
        {
            return m_Head;
        }

        /**
         * @brief Remove first element from queue, if any
         * @returns null if queue is empty
         *          else returns first element in queue and removes it
         */
        public XMRInstance RemoveHead()
        {
            XMRInstance inst = m_Head;
            if(inst != null)
            {
                if((m_Head = inst.m_NextInst) == null)
                    m_Tail = null;
                else
                    m_Head.m_PrevInst = null;

                inst.m_NextInst = inst;
                inst.m_PrevInst = inst;
            }
            return inst;
        }

        /**
         * @brief Remove last element from queue, if any
         * @returns null if queue is empty
         *          else returns last element in queue and removes it
         */
        public XMRInstance RemoveTail()
        {
            XMRInstance inst = m_Tail;
            if(inst != null)
            {
                if((m_Tail = inst.m_PrevInst) == null)
                    m_Head = null;
                else
                    m_Tail.m_NextInst = null;

                inst.m_NextInst = inst;
                inst.m_PrevInst = inst;
            }
            return inst;
        }

        /**
         * @brief Remove arbitrary element from queue, if any
         * @param inst = element to remove (assumed to be in the queue)
         * @returns with element removed
         */
        public void Remove(XMRInstance inst)
        {
            XMRInstance next = inst.m_NextInst;
            XMRInstance prev = inst.m_PrevInst;
            if((prev == inst) || (next == inst))
                throw new Exception("not in a list");

            if(next == null)
            {
                if(m_Tail != inst)
                    throw new Exception("not in this list");

                m_Tail = prev;
            }
            else
                next.m_PrevInst = prev;

            if(prev == null)
            {
                if(m_Head != inst)
                    throw new Exception("not in this list");

                m_Head = next;
            }
            else
                prev.m_NextInst = next;

            inst.m_NextInst = inst;
            inst.m_PrevInst = inst;
        }
    }
}
