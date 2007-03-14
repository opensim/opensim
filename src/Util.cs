/*
Copyright (c) OpenSim project, http://osgrid.org/

* Copyright (c) <year>, <copyright holder>
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
	/// <summary>
	/// </summary>
	/// 
		public class Util
		{
			public static ulong UIntsToLong(uint X, uint Y)
			{
				return Helpers.UIntsToLong(X,Y);
			}
			public Util()
			{
				
			}
		}
		
        public class QueItem {
                public QueItem()
                {
                }

                public Packet Packet;
                public bool Incoming;
        }

        public class agentcircuitdata {
                public agentcircuitdata() { }
                public LLUUID AgentID;
                public LLUUID SessionID;
                public LLUUID SecureSessionID;
                public string firstname;
                public string lastname;
                public uint circuitcode;
        }


        public class BlockingQueue< T > {
                private Queue< T > _queue = new Queue< T >();
                private object _queueSync = new object();

                public void Enqueue(T value)
                {
                        lock(_queueSync)
                        {
                                _queue.Enqueue(value);
                                Monitor.Pulse(_queueSync);
                        }
                }

                public T Dequeue()
                {
                        lock(_queueSync)
                        {
                                if( _queue.Count < 1)
                                        Monitor.Wait(_queueSync);

                                return _queue.Dequeue();
                        }
                }
        }
}
