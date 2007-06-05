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
  using System.Runtime.Remoting.Proxies;
  using System.Runtime.Remoting.Messaging;

  /// <summary>This class provides support for creating local proxies of XML-RPC remote objects</summary>
  /// <remarks>
  /// To create a local proxy you need to create a local C# interface and then, via <i>createProxy</i>
  /// associate that interface with a remote object at a given URL.
  /// </remarks>
public class XmlRpcClientProxy : RealProxy
{
  private String _remoteObjectName;
  private String _url;
  private XmlRpcRequest _client = new XmlRpcRequest();

  /// <summary>Factory method to create proxies.</summary>
  /// <remarks>
  /// To create a local proxy you need to create a local C# interface with methods that mirror those of the server object.
  /// Next, pass that interface into <c>createProxy</c> along with the object name and URL of the remote object and 
  /// cast the resulting object to the specifice interface.
  /// </remarks>
  /// <param name="remoteObjectName"><c>String</c> The name of the remote object.</param>
  /// <param name="url"><c>String</c> The URL of the remote object.</param>
  /// <param name="anInterface"><c>Type</c> The typeof() of a C# interface.</param>
  /// <returns><c>Object</c> A proxy for your specified interface. Cast to appropriate type.</returns>
  public static Object createProxy(String remoteObjectName, String url, Type anInterface)
    {
      return new XmlRpcClientProxy(remoteObjectName, url, anInterface).GetTransparentProxy();
    }

  private XmlRpcClientProxy(String remoteObjectName, String url, Type t) : base(t)
    {
      _remoteObjectName = remoteObjectName;
      _url = url;
    }

  /// <summary>The local method dispatcher - do not invoke.</summary>
  override public IMessage Invoke(IMessage msg)
    {
      IMethodCallMessage methodMessage = (IMethodCallMessage)msg;

      _client.MethodName = _remoteObjectName + "." + methodMessage.MethodName;
      _client.Params.Clear();
      foreach (Object o in methodMessage.Args)
	_client.Params.Add(o);

      try
	{
	  Object ret = _client.Invoke(_url);
	  return new ReturnMessage(ret,null,0,
				   methodMessage.LogicalCallContext, methodMessage);
	}
      catch (Exception e)
	{
	  return new ReturnMessage(e, methodMessage);
	}
    }
}
}
