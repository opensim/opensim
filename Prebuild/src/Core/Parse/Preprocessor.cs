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

#region CVS Information
/*
 * $Source$
 * $Author: jendave $
 * $Date: 2006-09-01 19:55:06 +0200 (fr, 01 sep 2006) $
 * $Revision: 147 $
 */
#endregion

using System;
using System.Collections;
using System.IO;
using System.Xml;

namespace Prebuild.Core.Parse
{
	/// <summary>
	/// 
	/// </summary>
	public enum OperatorSymbol
	{
		/// <summary>
		/// 
		/// </summary>
		None,
		/// <summary>
		/// 
		/// </summary>
		Equal,
		/// <summary>
		/// 
		/// </summary>
		NotEqual,
		/// <summary>
		/// 
		/// </summary>
		LessThan,
		/// <summary>
		/// 
		/// </summary>
		GreaterThan,
		/// <summary>
		/// 
		/// </summary>
		LessThanEqual,
		/// <summary>
		/// 
		/// </summary>
		GreaterThanEqual
	}

	/// <summary>
	/// 
	/// </summary>
	public class Preprocessor
	{
		#region Fields

		XmlDocument m_OutDoc;
		Stack m_IfStack;
		Hashtable m_Variables;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Preprocessor"/> class.
		/// </summary>
		public Preprocessor()
		{
			m_OutDoc = new XmlDocument();
			m_IfStack = new Stack();
			m_Variables = new Hashtable();

			RegisterVariable("OS", GetOS());
			RegisterVariable("RuntimeVersion", Environment.Version.Major);
			RegisterVariable("RuntimeMajor", Environment.Version.Major);
			RegisterVariable("RuntimeMinor", Environment.Version.Minor);
			RegisterVariable("RuntimeRevision", Environment.Version.Revision);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the processed doc.
		/// </summary>
		/// <value>The processed doc.</value>
		public XmlDocument ProcessedDoc
		{
			get
			{
				return m_OutDoc;
			}
		}

		#endregion

		#region Private Methods
		 
		/// <summary>
		/// Parts of this code were taken from NAnt and is subject to the GPL
		/// as per NAnt's license. Thanks to the NAnt guys for this little gem.
		/// </summary>
		/// <returns></returns>
		public static string GetOS()
		{
			PlatformID platId = Environment.OSVersion.Platform;
			if(platId == PlatformID.Win32NT || platId == PlatformID.Win32Windows)
			{
				return "Win32";
			}

			/*
			 * .NET 1.x, under Mono, the UNIX code is 128. Under
			 * .NET 2.x, Mono or MS, the UNIX code is 4
			 */
			if(Environment.Version.Major == 1)
			{
				if((int)platId == 128)
				{
					return "UNIX";
				}
			}
			else if((int)platId == 4)
			{
				return "UNIX";
			}

			return "Unknown";
		}

		private static bool CompareNum(OperatorSymbol oper, int val1, int val2)
		{
			switch(oper)
			{
				case OperatorSymbol.Equal:
					return (val1 == val2);
				case OperatorSymbol.NotEqual:
					return (val1 != val2);
				case OperatorSymbol.LessThan:
					return (val1 < val2);
				case OperatorSymbol.LessThanEqual:
					return (val1 <= val2);
				case OperatorSymbol.GreaterThan:
					return (val1 > val2);
				case OperatorSymbol.GreaterThanEqual:
					return (val1 >= val2);
			}

			throw new WarningException("Unknown operator type");
		}

		private static bool CompareStr(OperatorSymbol oper, string val1, string val2)
		{
			switch(oper)
			{
				case OperatorSymbol.Equal:
					return (val1 == val2);
				case OperatorSymbol.NotEqual:
					return (val1 != val2);
				case OperatorSymbol.LessThan:
					return (val1.CompareTo(val2) < 0);
				case OperatorSymbol.LessThanEqual:
					return (val1.CompareTo(val2) <= 0);
				case OperatorSymbol.GreaterThan:
					return (val1.CompareTo(val2) > 0);
				case OperatorSymbol.GreaterThanEqual:
					return (val1.CompareTo(val2) >= 0);
			}

			throw new WarningException("Unknown operator type");
		}

		private static char NextChar(int idx, string str)
		{
			if((idx + 1) >= str.Length)
			{
				return Char.MaxValue;
			}

			return str[idx + 1];
		}
		// Very very simple expression parser. Can only match expressions of the form
		// <var> <op> <value>:
		// OS = Windows
		// OS != Linux
		// RuntimeMinor > 0
		private bool ParseExpression(string exp)
		{
			if(exp == null)
			{
				throw new ArgumentException("Invalid expression, cannot be null");
			}

			exp = exp.Trim();
			if(exp.Length < 1)
			{
				throw new ArgumentException("Invalid expression, cannot be 0 length");
			}

			string id = "";
			string str = "";
			OperatorSymbol oper = OperatorSymbol.None;
			bool inStr = false;
			char c;
            
			for(int i = 0; i < exp.Length; i++)
			{
				c = exp[i];
				if(Char.IsWhiteSpace(c))
				{
					continue;
				}

				if(Char.IsLetterOrDigit(c) || c == '_')
				{
					if(inStr)
					{
						str += c;
					}
					else
					{
						id += c;
					}
				}
				else if(c == '\"')
				{
					inStr = !inStr;
					if(inStr)
					{
						str = "";
					}
				}
				else
				{
					if(inStr)
					{
						str += c;
					}
					else
					{
						switch(c)
						{
							case '=':
								oper = OperatorSymbol.Equal;
								break;

							case '!':
								if(NextChar(i, exp) == '=')
								{
									oper = OperatorSymbol.NotEqual;
								}
                                
								break;

							case '<':
								if(NextChar(i, exp) == '=')
								{
									oper = OperatorSymbol.LessThanEqual;
								}
								else
								{
									oper = OperatorSymbol.LessThan;
								}
                                
								break;

							case '>':
								if(NextChar(i, exp) == '=')
								{
									oper = OperatorSymbol.GreaterThanEqual;
								}
								else
								{
									oper = OperatorSymbol.GreaterThan;
								}

								break;
						}
					}
				}
			}

            
			if(inStr)
			{
				throw new WarningException("Expected end of string in expression");
			}

			if(oper == OperatorSymbol.None)
			{
				throw new WarningException("Expected operator in expression");
			}
			else if(id.Length < 1)
			{
				throw new WarningException("Expected identifier in expression");
			}
			else if(str.Length < 1)
			{
				throw new WarningException("Expected value in expression");
			}

			bool ret = false;
			try
			{
				object val = m_Variables[id.ToLower()];
				if(val == null)
				{
					throw new WarningException("Unknown identifier '{0}'", id);
				}

				int numVal, numVal2;
				string strVal, strVal2;
				Type t = val.GetType();
				if(t.IsAssignableFrom(typeof(int)))
				{
					numVal = (int)val;
					numVal2 = Int32.Parse(str);
					ret = CompareNum(oper, numVal, numVal2);
				}
				else
				{
					strVal = val.ToString();
					strVal2 = str;
					ret = CompareStr(oper, strVal, strVal2);
				}
			}
			catch(ArgumentException ex)
			{
				ex.ToString();
				throw new WarningException("Invalid value type for system variable '{0}', expected int", id);
			}

			return ret;
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="variableValue"></param>
		public void RegisterVariable(string name, object variableValue)
		{
			if(name == null || variableValue == null)
			{
				return;
			}

			m_Variables[name.ToLower()] = variableValue;
		}

		/// <summary>
		/// Performs validation on the xml source as well as evaluates conditional and flow expresions
		/// </summary>
		/// <exception cref="ArgumentException">For invalid use of conditional expressions or for invalid XML syntax.  If a XmlValidatingReader is passed, then will also throw exceptions for non-schema-conforming xml</exception>
		/// <param name="reader"></param>
		/// <returns>the output xml </returns>
		public string Process(XmlReader reader)
		{
			if(reader == null)
			{
				throw new ArgumentException("Invalid XML reader to pre-process");
			}

			IfContext context = new IfContext(true, true, IfState.None);
			StringWriter xmlText = new StringWriter();
			XmlTextWriter writer = new XmlTextWriter(xmlText);
			writer.Formatting = Formatting.Indented;
			while(reader.Read())
			{
				if(reader.NodeType == XmlNodeType.ProcessingInstruction)
				{
					bool ignore = false;
					switch(reader.LocalName)
					{
						case "if":
							m_IfStack.Push(context);
							context = new IfContext(context.Keep & context.Active, ParseExpression(reader.Value), IfState.If);
							ignore = true;
							break;

						case "elseif":
							if(m_IfStack.Count == 0)
							{
								throw new WarningException("Unexpected 'elseif' outside of 'if'");
							}
							else if(context.State != IfState.If && context.State != IfState.ElseIf)
							{
								throw new WarningException("Unexpected 'elseif' outside of 'if'");
							}

							context.State = IfState.ElseIf;
							if(!context.EverKept)
							{
								context.Keep = ParseExpression(reader.Value);
							}
							else
							{
								context.Keep = false;
							}

							ignore = true;
							break;

						case "else":
							if(m_IfStack.Count == 0)
							{
								throw new WarningException("Unexpected 'else' outside of 'if'");
							}
							else if(context.State != IfState.If && context.State != IfState.ElseIf)
							{
								throw new WarningException("Unexpected 'else' outside of 'if'");
							}

							context.State = IfState.Else;
							context.Keep = !context.EverKept;
							ignore = true;
							break;

						case "endif":
							if(m_IfStack.Count == 0)
							{
								throw new WarningException("Unexpected 'endif' outside of 'if'");
							}

							context = (IfContext)m_IfStack.Pop();
							ignore = true;
							break;
					}

					if(ignore)
					{
						continue;
					}
				}//end pre-proc instruction

				if(!context.Active || !context.Keep)
				{
					continue;
				}

				switch(reader.NodeType)
				{
					case XmlNodeType.Element:
						bool empty = reader.IsEmptyElement;
						writer.WriteStartElement(reader.Name);

						while (reader.MoveToNextAttribute())
						{
							writer.WriteAttributeString(reader.Name, reader.Value);
						}

						if(empty)
						{
							writer.WriteEndElement();
						}
                        
						break;

					case XmlNodeType.EndElement:
						writer.WriteEndElement();
						break;

					case XmlNodeType.Text:
						writer.WriteString(reader.Value);
						break;

					case XmlNodeType.CDATA:
						writer.WriteCData(reader.Value);
						break;

					default:
						break;
				}
			}

			if(m_IfStack.Count != 0)
			{
				throw new WarningException("Mismatched 'if', 'endif' pair");
			}
            
			return xmlText.ToString();
		}

		#endregion
	}
}
