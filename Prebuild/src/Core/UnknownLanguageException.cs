/*
 * $RCSfile$
 * Copyright (C) 2004, 2005 David Hudson (jendave@yahoo.com)
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Runtime.Serialization;

namespace Prebuild.Core
{
    /// <summary>
    /// </summary>
    [Serializable()]
    public class UnknownLanguageException : Exception
    {
        /// <summary>
        /// Basic exception.
        /// </summary>
        public UnknownLanguageException()
        {
        }

        /// <summary>
        /// Exception with specified string
        /// </summary>
        /// <param name="message">Exception message</param>
        public UnknownLanguageException(string message): base(message)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public UnknownLanguageException(string message, Exception exception) : base(message, exception)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected UnknownLanguageException(SerializationInfo info, StreamingContext context) : base( info, context )
        {
        }
    }
}
