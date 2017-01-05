#region BSD License
/*
Copyright (c) 2004-2005 Matthew Holmes (matthew@wildfiregames.com), Dan Moorehead (dan05a@gmail.com)

Redistribution and use in source and binary forms, with or without modification, are permitted
provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this list of conditions
  and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice, this list of conditions
  and the following disclaimer in the documentation and/or other materials provided with the
  distribution.
* The name of the author may not be used to endorse or promote products derived from this software
  without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#endregion

using System;
using System.Runtime.Serialization;

namespace Prebuild.Core
{
    /// <summary>
    ///
    /// </summary>
    [Serializable()]
    public class FatalException : Exception
    {
        #region Constructors


        /// <summary>
        /// Initializes a new instance of the <see cref="FatalException"/> class.
        /// </summary>
        public FatalException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FatalException"/> class.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The args.</param>
        public FatalException(string format, params object[] args)
            : base(String.Format(format, args))
        {
        }

        /// <summary>
        /// Exception with specified string
        /// </summary>
        /// <param name="message">Exception message</param>
        public FatalException(string message): base(message)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public FatalException(string message, Exception exception) : base(message, exception)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected FatalException(SerializationInfo info, StreamingContext context) : base( info, context )
        {
        }

        #endregion
    }
}
