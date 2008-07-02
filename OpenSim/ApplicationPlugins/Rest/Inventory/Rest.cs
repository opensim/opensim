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
 *     * Neither the name of the OpenSim Project nor the
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
 * 
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using Nini.Config;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{

    public class Rest
    {

        internal static readonly log4net.ILog  Log = 
            log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        internal static bool DEBUG = Log.IsDebugEnabled;

        /// <summary>
        /// These values have a single value for the whole
        /// domain and lifetime of the plugin handler. We
        /// make them static for ease of reference within
        /// the assembly. These are initialized by the
        /// RestHandler class during start-up.
        /// </summary>

        internal static RestHandler               Plugin            = null;
        internal static OpenSimBase               main              = null;
        internal static CommunicationsManager     Comms             = null;
        internal static IInventoryServices        InventoryServices = null;
        internal static IUserService              UserServices      = null;
        internal static AssetCache                AssetServices     = null;
        internal static string                    Prefix            = null;
        internal static IConfig                   Config            = null;
        internal static string                    GodKey            = null;
        internal static bool                      Authenticate      = true;
        internal static bool                      Secure            = true;
        internal static bool                      ExtendedEscape    = true;
        internal static bool                      DumpAsset         = false;
        internal static string                    Realm             = "REST";
        internal static Dictionary<string,string> Domains           = new Dictionary<string,string>();
        internal static int                       CreationDate      = (int) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        internal static int                       DumpLineSize      = 32; // Should be a multiple of 16 or (possibly) 4

        internal static string MsgId
        {
            get { return Plugin.MsgId; }
        }

        internal static string RequestId
        {
            get { return Plugin.RequestId; }
        }

        internal static Encoding Encoding = Encoding.UTF8;

        /// <summary>
        /// Version control for REST implementation. This
        /// refers to the overall infrastructure represented
        /// by the following classes
        ///     RequestData
        ///     RequestInventoryPlugin
        ///     Rest
        /// It does no describe implementation classes such as
        /// RestInventoryServices, which may morph much more
        /// often. Such classes ARE dependent upon this however
        /// and should check it in their Initialize method.
        /// </summary>

        public static readonly float Version  = 1.0F;
        public const  string  Name            = "REST 1.0";

        /// <summary>
        /// Currently defined HTTP methods.
        /// Only GET and HEAD are required to be
        /// supported by all servers. See Respond
        /// to see how these are handled.
        /// </summary>
        
        // REST AGENT 1.0 interpretations
        public const string GET     = "get";       // information retrieval - server state unchanged
        public const string HEAD    = "head";      // same as get except only the headers are returned.
        public const string POST    = "post";      // Replace the URI designated resource with the entity.
        public const string PUT     = "put";       // Add the entity to the context represented by the URI
        public const string DELETE  = "delete";    // Remove the URI designated resource from the server.

        public const string OPTIONS = "options";  //
        public const string TRACE   = "trace";    //
        public const string CONNECT = "connect";  //

        // Define this in one place...

        public const string UrlPathSeparator   = "/";
        public const string UrlMethodSeparator = ":";

        // Redirection qualifications

        public const bool PERMANENT = false;
        public const bool TEMPORARY = true;

        // Constant arrays used by String.Split

        public   static   readonly char   C_SPACE    = ' ';
        public   static   readonly char   C_SLASH    = '/';
        public   static   readonly char   C_PATHSEP  = '/';
        public   static   readonly char   C_COLON    = ':';
        public   static   readonly char   C_PLUS     = '+';
        public   static   readonly char   C_PERIOD   = '.';
        public   static   readonly char   C_COMMA    = ',';
        public   static   readonly char   C_DQUOTE   = '"';
        
        public   static   readonly string   CS_SPACE    = " ";
        public   static   readonly string   CS_SLASH    = "/";
        public   static   readonly string   CS_PATHSEP  = "/";
        public   static   readonly string   CS_COLON    = ":";
        public   static   readonly string   CS_PLUS     = "+";
        public   static   readonly string   CS_PERIOD   = ".";
        public   static   readonly string   CS_COMMA    = ",";
        public   static   readonly string   CS_DQUOTE   = "\"";
        
        public   static   readonly char[] CA_SPACE   = { C_SPACE   };
        public   static   readonly char[] CA_SLASH   = { C_SLASH   };
        public   static   readonly char[] CA_PATHSEP = { C_PATHSEP };
        public   static   readonly char[] CA_COLON   = { C_COLON   };
        public   static   readonly char[] CA_PERIOD  = { C_PERIOD  };
        public   static   readonly char[] CA_PLUS    = { C_PLUS    };
        public   static   readonly char[] CA_COMMA   = { C_COMMA   };
        public   static   readonly char[] CA_DQUOTE  = { C_DQUOTE  };

        // HTTP Code Values (in value order)

        public const int HttpStatusCodeContinue           = 100;
        public const int HttpStatusCodeSwitchingProtocols = 101;

        public const int HttpStatusCodeOK                 = 200;
        public const int HttpStatusCodeCreated            = 201;
        public const int HttpStatusCodeAccepted           = 202;
        public const int HttpStatusCodeNonAuthoritative   = 203;
        public const int HttpStatusCodeNoContent          = 204;
        public const int HttpStatusCodeResetContent       = 205;
        public const int HttpStatusCodePartialContent     = 206;

        public const int HttpStatusCodeMultipleChoices    = 300;
        public const int HttpStatusCodePermanentRedirect  = 301;
        public const int HttpStatusCodeFound              = 302;
        public const int HttpStatusCodeSeeOther           = 303;
        public const int HttpStatusCodeNotModified        = 304;
        public const int HttpStatusCodeUseProxy           = 305;
        public const int HttpStatusCodeReserved306        = 306;
        public const int HttpStatusCodeTemporaryRedirect  = 307;

        public const int HttpStatusCodeBadRequest         = 400;
        public const int HttpStatusCodeNotAuthorized      = 401;
        public const int HttpStatusCodePaymentRequired    = 402;
        public const int HttpStatusCodeForbidden          = 403;
        public const int HttpStatusCodeNotFound           = 404;
        public const int HttpStatusCodeMethodNotAllowed   = 405;
        public const int HttpStatusCodeNotAcceptable      = 406;
        public const int HttpStatusCodeProxyAuthenticate  = 407;
        public const int HttpStatusCodeTimeOut            = 408;
        public const int HttpStatusCodeConflict           = 409;
        public const int HttpStatusCodeGone               = 410;
        public const int HttpStatusCodeLengthRequired     = 411;
        public const int HttpStatusCodePreconditionFailed = 412;
        public const int HttpStatusCodeEntityTooLarge     = 413;
        public const int HttpStatusCodeUriTooLarge        = 414;
        public const int HttpStatusCodeUnsupportedMedia   = 415;
        public const int HttpStatusCodeRangeNotSatsified  = 416;
        public const int HttpStatusCodeExpectationFailed  = 417;

        public const int HttpStatusCodeServerError        = 500;
        public const int HttpStatusCodeNotImplemented     = 501;
        public const int HttpStatusCodeBadGateway         = 502;
        public const int HttpStatusCodeServiceUnavailable = 503;
        public const int HttpStatusCodeGatewayTimeout     = 504;
        public const int HttpStatusCodeHttpVersionError   = 505;

        // HTTP Status Descriptions (in status code order)

        public const string HttpStatusDescContinue            = "Continue Request";        // 100
        public const string HttpStatusDescSwitchingProtocols  = "Switching Protocols";    // 101

        public const string HttpStatusDescOK                  = "OK";
        public const string HttpStatusDescCreated             = "CREATED";
        public const string HttpStatusDescAccepted            = "ACCEPTED";
        public const string HttpStatusDescNonAuthoritative    = "NON-AUTHORITATIVE INFORMATION";
        public const string HttpStatusDescNoContent           = "NO CONTENT";
        public const string HttpStatusDescResetContent        = "RESET CONTENT";
        public const string HttpStatusDescPartialContent      = "PARTIAL CONTENT";

        public const string HttpStatusDescMultipleChoices     = "MULTIPLE CHOICES";
        public const string HttpStatusDescPermanentRedirect   = "PERMANENT REDIRECT";
        public const string HttpStatusDescFound               = "FOUND";
        public const string HttpStatusDescSeeOther            = "SEE OTHER";
        public const string HttpStatusDescNotModified         = "NOT MODIFIED";
        public const string HttpStatusDescUseProxy            = "USE PROXY";
        public const string HttpStatusDescReserved306         = "RESERVED CODE 306";
        public const string HttpStatusDescTemporaryRedirect   = "TEMPORARY REDIRECT";

        public const string HttpStatusDescBadRequest          = "BAD REQUEST";
        public const string HttpStatusDescNotAuthorized       = "NOT AUTHORIZED";
        public const string HttpStatusDescPaymentRequired     = "PAYMENT REQUIRED";
        public const string HttpStatusDescForbidden           = "FORBIDDEN";
        public const string HttpStatusDescNotFound            = "NOT FOUND";
        public const string HttpStatusDescMethodNotAllowed    = "METHOD NOT ALLOWED";
        public const string HttpStatusDescNotAcceptable       = "NOT ACCEPTABLE";
        public const string HttpStatusDescProxyAuthenticate   = "PROXY AUTHENTICATION REQUIRED";
        public const string HttpStatusDescTimeOut             = "TIMEOUT";
        public const string HttpStatusDescConflict            = "CONFLICT";
        public const string HttpStatusDescGone                = "GONE";
        public const string HttpStatusDescLengthRequired      = "LENGTH REQUIRED";
        public const string HttpStatusDescPreconditionFailed  = "PRECONDITION FAILED";
        public const string HttpStatusDescEntityTooLarge      = "ENTITY TOO LARGE";
        public const string HttpStatusDescUriTooLarge         = "URI TOO LARGE";
        public const string HttpStatusDescUnsupportedMedia    = "UNSUPPORTED MEDIA";
        public const string HttpStatusDescRangeNotSatisfied   = "RANGE NOT SATISFIED";
        public const string HttpStatusDescExpectationFailed   = "EXPECTATION FAILED";

        public const string HttpStatusDescServerError         = "SERVER ERROR";
        public const string HttpStatusDescNotImplemented      = "NOT IMPLEMENTED";
        public const string HttpStatusDescBadGateway          = "BAD GATEWAY";
        public const string HttpStatusDescServiceUnavailable  = "SERVICE UNAVAILABLE";
        public const string HttpStatusDescGatewayTimeout      = "GATEWAY TIMEOUT";
        public const string HttpStatusDescHttpVersionError    = "HTTP VERSION NOT SUPPORTED";

        // HTTP Headers

        public const string HttpHeaderAccept              = "Accept";
        public const string HttpHeaderAcceptCharset       = "Accept-Charset";
        public const string HttpHeaderAcceptEncoding      = "Accept-Encoding";
        public const string HttpHeaderAcceptLanguage      = "Accept-Language";
        public const string HttpHeaderAcceptRanges        = "Accept-Ranges";
        public const string HttpHeaderAge                 = "Age";
        public const string HttpHeaderAllow               = "Allow";
        public const string HttpHeaderAuthorization       = "Authorization";
        public const string HttpHeaderCacheControl        = "Cache-Control";
        public const string HttpHeaderConnection          = "Connection";
        public const string HttpHeaderContentEncoding     = "Content-Encoding";
        public const string HttpHeaderContentLanguage     = "Content-Language";
        public const string HttpHeaderContentLength       = "Content-Length";
        public const string HttpHeaderContentLocation     = "Content-Location";
        public const string HttpHeaderContentMD5          = "Content-MD5";
        public const string HttpHeaderContentRange        = "Content-Range";
        public const string HttpHeaderContentType         = "Content-Type";
        public const string HttpHeaderDate                = "Date";
        public const string HttpHeaderETag                = "ETag";
        public const string HttpHeaderExpect              = "Expect";
        public const string HttpHeaderExpires             = "Expires";
        public const string HttpHeaderFrom                = "From";
        public const string HttpHeaderHost                = "Host";
        public const string HttpHeaderIfMatch             = "If-Match";
        public const string HttpHeaderIfModifiedSince     = "If-Modified-Since";
        public const string HttpHeaderIfNoneMatch         = "If-None-Match";
        public const string HttpHeaderIfRange             = "If-Range";
        public const string HttpHeaderIfUnmodifiedSince   = "If-Unmodified-Since";
        public const string HttpHeaderLastModified        = "Last-Modified";
        public const string HttpHeaderLocation            = "Location";
        public const string HttpHeaderMaxForwards         = "Max-Forwards";
        public const string HttpHeaderPragma              = "Pragma";
        public const string HttpHeaderProxyAuthenticate   = "Proxy-Authenticate";
        public const string HttpHeaderProxyAuthorization  = "Proxy-Authorization";
        public const string HttpHeaderRange               = "Range";
        public const string HttpHeaderReferer             = "Referer";
        public const string HttpHeaderRetryAfter          = "Retry-After";
        public const string HttpHeaderServer              = "Server";
        public const string HttpHeaderTE                  = "TE";
        public const string HttpHeaderTrailer             = "Trailer";
        public const string HttpHeaderTransferEncoding    = "Transfer-Encoding";
        public const string HttpHeaderUpgrade             = "Upgrade";
        public const string HttpHeaderUserAgent           = "User-Agent";
        public const string HttpHeaderVary                = "Vary";
        public const string HttpHeaderVia                 = "Via";
        public const string HttpHeaderWarning             = "Warning";
        public const string HttpHeaderWWWAuthenticate     = "WWW-Authenticate";

        /// <summary>
        /// Supported authentication schemes
        /// </summary>

        public const string AS_BASIC                      = "Basic";
        public const string AS_DIGEST                     = "Digest";

        /// Supported Digest algorithms
 
        public const string Digest_MD5                    = "MD5"; // assumedd efault if omitted
        public const string Digest_MD5Sess                = "MD5-sess";

        public const string Qop_Auth                      = "auth";
        public const string Qop_Int                       = "auth-int";

        /// Utility routines

        public static string StringToBase64(string str)
        {
            try
            {
                byte[] encData_byte = new byte[str.Length];
                encData_byte = Encoding.UTF8.GetBytes(str);
                return Convert.ToBase64String(encData_byte);
            }
            catch
            {
                return String.Empty;
            }
        }

        public static string Base64ToString(string str)
        {
            UTF8Encoding encoder = new UTF8Encoding();
            Decoder utf8Decode = encoder.GetDecoder();
            try
            {
                byte[] todecode_byte = Convert.FromBase64String(str);
                int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                return new String(decoded_char);
            }
            catch
            {
                return String.Empty;
            }
        }

        private const string hvals = "0123456789abcdef";

        public static int Hex2Int(string hex)
        {
            int    val = 0;
            int    sum = 0;
            string tmp = null;
            
            if (hex != null)
            {
                tmp = hex.ToLower();
                for (int i = 0; i < tmp.Length; i++)
                {
                    val = hvals.IndexOf(tmp[i]);
                    if (val == -1)
                        break;
                    sum *= 16;
                    sum += val;
                }
            }

            return sum;

        }

        public static string Int2Hex8(int val)
        {
            string res = String.Empty;
            for (int i = 0; i < 8; i++)
            {
                res = (val % 16) + res;
                val = val / 16;
            }
            return res;
        }

        public static string ToHex32(int val)
        {
            return String.Empty;
        }

        public static string ToHex32(string val)
        {
            return String.Empty;
        }

        // Nonce management

        public static string NonceGenerator()
        {
            return StringToBase64(Guid.NewGuid().ToString());
        }

        // Dump he specified data stream;

        public static void Dump(byte[] data)
        {

            char[] buffer = new char[Rest.DumpLineSize];
            int cc = 0;

            for (int i = 0; i < data.Length; i++)
            {

                if (i % Rest.DumpLineSize == 0) Console.Write("\n{0}: ",i.ToString("d8"));

                if (i % 4  == 0) Console.Write(" ");
//                if (i%16 == 0) Console.Write(" ");

                Console.Write("{0}",data[i].ToString("x2"));

                if (data[i] < 127 && data[i] > 31)
                    buffer[i % Rest.DumpLineSize] = (char) data[i];
                else
                    buffer[i % Rest.DumpLineSize] = '.';

                cc++;

                if (i != 0 && (i + 1) % Rest.DumpLineSize == 0)
                {
                    Console.Write(" |"+(new String(buffer))+"|");
                    cc = 0;
                }

            }

            // Finish off any incomplete line

            if (cc != 0)
            {
                for (int i = cc ; i < Rest.DumpLineSize; i++)
                {
                    if (i % 4  == 0) Console.Write(" ");
                    // if (i%16 == 0) Console.Write(" ");
                    Console.Write("  "); 
                    buffer[i % Rest.DumpLineSize] = ' ';
                }
                Console.WriteLine(" |"+(new String(buffer))+"|");
            }
            else
            {
                Console.Write("\n"); 
            }

        }

    }
  
    // Local exception type

    public class RestException : Exception
    {

        internal int    statusCode;
        internal string statusDesc;
        internal string httpmethod;
        internal string httppath;

        public RestException(string msg) : base(msg) 
        { 
        }
    }

}
