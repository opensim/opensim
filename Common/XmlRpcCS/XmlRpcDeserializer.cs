namespace Nwc.XmlRpc
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Xml;
    using System.Globalization;

    /// <summary>Parser context, we maintain contexts in a stack to avoiding recursion. </summary>
    struct Context
    {
        public String Name;
        public Object Container;
    }

    /// <summary>Basic XML-RPC data deserializer.</summary>
    /// <remarks>Uses <c>XmlTextReader</c> to parse the XML data. This level of the class 
    /// only handles the tokens common to both Requests and Responses. This class is not useful in and of itself
    /// but is designed to be subclassed.</remarks>
    public class XmlRpcDeserializer : XmlRpcXmlTokens
    {
        private static DateTimeFormatInfo _dateFormat = new DateTimeFormatInfo();

        private Object _container;
        private Stack _containerStack;

        /// <summary>Protected reference to last text.</summary>
        protected String _text;
        /// <summary>Protected reference to last deserialized value.</summary>
        protected Object _value;
        /// <summary>Protected reference to last name field.</summary>
        protected String _name;


        /// <summary>Basic constructor.</summary>
        public XmlRpcDeserializer()
        {
            Reset();
            _dateFormat.FullDateTimePattern = ISO_DATETIME;
        }

        /// <summary>Static method that parses XML data into a response using the Singleton.</summary>
        /// <param name="xmlData"><c>StreamReader</c> containing an XML-RPC response.</param>
        /// <returns><c>Object</c> object resulting from the deserialization.</returns>
        virtual public Object Deserialize(TextReader xmlData)
        {
            return null;
        }

        /// <summary>Protected method to parse a node in an XML-RPC XML stream.</summary>
        /// <remarks>Method deals with elements common to all XML-RPC data, subclasses of 
        /// this object deal with request/response spefic elements.</remarks>
        /// <param name="reader"><c>XmlTextReader</c> of the in progress parsing data stream.</param>
        protected void DeserializeNode(XmlTextReader reader)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (Logger.Delegate != null)
                        Logger.WriteEntry("START " + reader.Name, LogLevel.Information);
                    switch (reader.Name)
                    {
                        case VALUE:
                            _value = null;
                            _text = null;
                            break;
                        case STRUCT:
                            PushContext();
                            _container = new Hashtable();
                            break;
                        case ARRAY:
                            PushContext();
                            _container = new ArrayList();
                            break;
                    }
                    break;
                case XmlNodeType.EndElement:
                    if (Logger.Delegate != null)
                        Logger.WriteEntry("END " + reader.Name, LogLevel.Information);
                    switch (reader.Name)
                    {
                        case BASE64:
                            _value = Convert.FromBase64String(_text);
                            break;
                        case BOOLEAN:
                            int val = Int16.Parse(_text);
                            if (val == 0)
                                _value = false;
                            else if (val == 1)
                                _value = true;
                            break;
                        case STRING:
                            _value = _text;
                            break;
                        case DOUBLE:
                            _value = Double.Parse(_text);
                            break;
                        case INT:
                        case ALT_INT:
                            _value = Int32.Parse(_text);
                            break;
                        case DATETIME:
#if __MONO__
		    _value = DateParse(_text);
#else
                            _value = DateTime.ParseExact(_text, "F", _dateFormat);
#endif
                            break;
                        case NAME:
                            _name = _text;
                            break;
                        case VALUE:
                            if (_value == null)
                                _value = _text; // some kits don't use <string> tag, they just do <value>

                            if ((_container != null) && (_container is IList)) // in an array?  If so add value to it.
                                ((IList)_container).Add(_value);
                            break;
                        case MEMBER:
                            if ((_container != null) && (_container is IDictionary)) // in an struct?  If so add value to it.
                                ((IDictionary)_container).Add(_name, _value);
                            break;
                        case ARRAY:
                        case STRUCT:
                            _value = _container;
                            PopContext();
                            break;
                    }
                    break;
                case XmlNodeType.Text:
                    if (Logger.Delegate != null)
                        Logger.WriteEntry("Text " + reader.Value, LogLevel.Information);
                    _text = reader.Value;
                    break;
                default:
                    break;
            }
        }

        /// <summary>Static method that parses XML in a <c>String</c> into a 
        /// request using the Singleton.</summary>
        /// <param name="xmlData"><c>String</c> containing an XML-RPC request.</param>
        /// <returns><c>XmlRpcRequest</c> object resulting from the parse.</returns>
        public Object Deserialize(String xmlData)
        {
            StringReader sr = new StringReader(xmlData);
            return Deserialize(sr);
        }

        /// <summary>Pop a Context of the stack, an Array or Struct has closed.</summary>
        private void PopContext()
        {
            Context c = (Context)_containerStack.Pop();
            _container = c.Container;
            _name = c.Name;
        }

        /// <summary>Push a Context on the stack, an Array or Struct has opened.</summary>
        private void PushContext()
        {
            Context context;

            context.Container = _container;
            context.Name = _name;

            _containerStack.Push(context);
        }

        /// <summary>Reset the internal state of the deserializer.</summary>
        protected void Reset()
        {
            _text = null;
            _value = null;
            _name = null;
            _container = null;
            _containerStack = new Stack();
        }

#if __MONO__
    private DateTime DateParse(String str)
      {
	int year = Int32.Parse(str.Substring(0,4));
	int month = Int32.Parse(str.Substring(4,2));
	int day = Int32.Parse(str.Substring(6,2));
	int hour = Int32.Parse(str.Substring(9,2));
	int min = Int32.Parse(str.Substring(12,2));
	int sec = Int32.Parse(str.Substring(15,2));
	return new DateTime(year,month,day,hour,min,sec);
      }
#endif

    }
}


