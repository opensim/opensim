/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
namespace Nwc.XmlRpc
{
  using System;
  using System.Collections;
  using System.IO;
  using System.Xml;
  using System.Net;
  using System.Text;
  using System.Reflection;

  /// <summary>Class that collects individual <c>XmlRpcRequest</c> objects and submits them as a <i>boxcarred</i> request.</summary>
  /// <remarks>A boxcared request is when a number of request are collected before being sent via XML-RPC, and then are sent via
  /// a single HTTP connection. This results in a speed up from reduced connection time.  The results are then retuned collectively
  /// as well.
  ///</remarks>
  /// <seealso cref="XmlRpcRequest"/>
  public class XmlRpcBoxcarRequest : XmlRpcRequest
  {
    /// <summary>ArrayList to collect the requests to boxcar.</summary>
    public IList Requests = new ArrayList();

    /// <summary>Basic constructor.</summary>
    public XmlRpcBoxcarRequest()
      {
      }

    /// <summary>Returns the <c>String</c> "system.multiCall" which is the server method that handles boxcars.</summary>
    public override String MethodName
      {
	get { return  "system.multiCall";  }
      }

    /// <summary>The <c>ArrayList</c> of boxcarred <paramref>Requests</paramref> as properly formed parameters.</summary>
    public override IList Params
      {
	get {
	  _params.Clear();
	  ArrayList reqArray = new ArrayList();
	  foreach (XmlRpcRequest request in Requests)
	    {
	      Hashtable requestEntry = new Hashtable();
	      requestEntry.Add(XmlRpcXmlTokens.METHOD_NAME, request.MethodName);
	      requestEntry.Add(XmlRpcXmlTokens.PARAMS, request.Params);
	      reqArray.Add(requestEntry);
	    }
	  _params.Add(reqArray);
	  return _params;
	}
      }
  }
}
