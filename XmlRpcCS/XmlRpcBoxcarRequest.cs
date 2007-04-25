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
