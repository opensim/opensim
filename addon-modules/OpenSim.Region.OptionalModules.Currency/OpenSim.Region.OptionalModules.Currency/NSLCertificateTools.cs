/* 
 * Copyright (c) Contributors, http://www.nsl.tuis.ac.jp
 *
 */

#pragma warning disable S1128 // Unused "using" should be removed
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using log4net;
#pragma warning restore S1128 // Unused "using" should be removed


namespace NSL.Certificate.Tools
{
    /// <summary>
    /// class NSL Certificate Verify
    /// </summary>
    public class NSLCertificateVerify
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private X509Chain m_chain = null;
        private X509Certificate2 m_cacert = null;

        private Mono.Security.X509.X509Crl m_clientcrl = null;


        /// <summary>
        /// NSL Certificate Verify
        /// </summary>
        public NSLCertificateVerify()
        {
            m_chain = null;
            m_cacert = null;
            m_clientcrl = null;
        }


        /// <summary>
        /// NSL Certificate Verify
        /// </summary>
        /// <param name="certfile"></param>
        public NSLCertificateVerify(string certfile)
        {
            SetPrivateCA(certfile);
        }


        /// <summary>
        /// NSL Certificate Verify
        /// </summary>
        /// <param name="certfile"></param>
        /// <param name="crlfile"></param>
        public NSLCertificateVerify(string certfile, string crlfile)
        {
            SetPrivateCA(certfile);
            SetPrivateCRL(crlfile);
        }


        /// <summary>
        /// Set Private CA
        /// </summary>
        /// <param name="certfile"></param>
        public void SetPrivateCA(string certfile)
        {
            try
            {
                m_cacert = new X509Certificate2(certfile);
            }
            catch (Exception ex)
            {
                m_cacert = null;
                m_log.ErrorFormat("[SET PRIVATE CA]: CA File reading error [{0}]. {1}", certfile, ex);
            }

            if (m_cacert != null)
            {
                m_chain = new X509Chain();
                m_chain.ChainPolicy.ExtraStore.Add(m_cacert);
                m_chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                m_chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            }
        }


        //
        public void SetPrivateCRL(string crlfile)
        {
            try
            {
                m_clientcrl = Mono.Security.X509.X509Crl.CreateFromFile(crlfile);
            }
            catch (Exception ex)
            {
                m_clientcrl = null;
                m_log.ErrorFormat("[SET PRIVATE CRL]: CRL File reading error [{0}]. {1}", crlfile, ex);
            }
        }


        /// <summary>
        /// Check Private Chain
        /// </summary>
        /// <param name="cert"></param>
        /// <returns></returns>
        public bool CheckPrivateChain(X509Certificate2 cert)
        {
            if (m_chain == null || m_cacert == null)
            {
                return false;
            }

            bool ret = m_chain.Build((X509Certificate2)cert);
            if (ret)
            {
                return true;
            }

            for (int i = 0; i < m_chain.ChainStatus.Length; i++)
            {
                if (m_chain.ChainStatus[i].Status == X509ChainStatusFlags.UntrustedRoot) return true;
            }
            //
            return false;
        }


        /*
        SslPolicyErrors:
            RemoteCertificateNotAvailable = 1, // 証明書が利用できません．
            RemoteCertificateNameMismatch = 2, // 証明書名が不一致です．
            RemoteCertificateChainErrors  = 4, // ChainStatus が空でない配列を返しました．
        */


        /// <summary>
        /// Validate Server Certificate
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        public bool ValidateServerCertificate(object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            m_log.InfoFormat("[NSL SERVER CERT VERIFY]: ValidateServerCertificate: Start.");

            if (obj is HttpWebRequest)
            {
                HttpWebRequest Request = (HttpWebRequest)obj;
                string noVerify = Request.Headers.Get("NoVerifyCert");
                if (noVerify != null && noVerify.ToLower() == "true")
                {
                    m_log.InfoFormat("[NSL SERVER CERT VERIFY]: ValidateServerCertificate: No Verify Certificate.");
                    return true;
                }
            }

            X509Certificate2 certificate2 = new X509Certificate2(certificate);
            string simplename = certificate2.GetNameInfo(X509NameType.SimpleName, false);

            // None, ChainErrors Error except for．
            if (sslPolicyErrors != SslPolicyErrors.None && sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
            {
                m_log.ErrorFormat("[NSL SERVER CERT VERIFY]: ValidateServerCertificate: Policy Error! {0}", sslPolicyErrors);
                return false;
            }

            bool valid = CheckPrivateChain(certificate2);
            if (valid)
            {
                m_log.InfoFormat("[NSL SERVER CERT VERIFY]: ValidateServerCertificate: Valid Server Certification for \"{0}\"", simplename);
            }
            else
            {
                m_log.InfoFormat("[NSL SERVER CERT VERIFY]: ValidateServerCertificate: Failed to Verify Server Certification for \"{0}\"", simplename);
            }
            return valid;
        }


        /// <summary>
        /// Validate Client Certificate
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        public bool ValidateClientCertificate(object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            m_log.InfoFormat("[NSL CLIENT CERT VERIFY]: ValidateClientCertificate: Start");

            X509Certificate2 certificate2 = new X509Certificate2(certificate);
            string simplename = certificate2.GetNameInfo(X509NameType.SimpleName, false);
            m_log.InfoFormat("[NSL CLIENT CERT VERIFY]: ValidateClientCertificate: Simple Name is \"{0}\"", simplename);

            // None, ChainErrors 以外は全てエラーとする．
            if (sslPolicyErrors != SslPolicyErrors.None && sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
            {
                m_log.InfoFormat("[NSL CLIENT CERT VERIFY]: ValidateClientCertificate: Policy Error! {0}", sslPolicyErrors);
                return false;
            }

            // check CRL
            if (m_clientcrl != null)
            {
                Mono.Security.X509.X509Certificate monocert = new Mono.Security.X509.X509Certificate(certificate.GetRawCertData());
                Mono.Security.X509.X509Crl.X509CrlEntry entry = m_clientcrl.GetCrlEntry(monocert);
                if (entry != null)
                {
                    m_log.InfoFormat("[NSL CLIENT CERT VERIFY]: Common Name \"{0}\" was revoked at {1}", simplename, entry.RevocationDate.ToString());
                    return false;
                }
            }

            bool valid = CheckPrivateChain(certificate2);
            if (valid)
            {
                m_log.InfoFormat("[NSL CLIENT CERT VERIFY]: Valid Client Certification for \"{0}\"", simplename);
            }
            else
            {
                m_log.InfoFormat("[NSL CLIENT CERT VERIFY]: Failed to Verify Client Certification for \"{0}\"", simplename);
            }
            return valid;
        }
    }


    /// <summary>
    /// class NSL Certificate Policy
    /// </summary>
    public class NSLCertificatePolicy : ICertificatePolicy
    {
        /// <summary>
        /// Check Validation Result
        /// </summary>
        /// <param name="srvPoint"></param>
        /// <param name="certificate"></param>
        /// <param name="request"></param>
        /// <param name="certificateProblem"></param>
        /// <returns></returns>
        public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem)
        {
            if (certificateProblem == 0 ||              //normal
                certificateProblem == -2146762487 ||    //Not trusted?
                certificateProblem == -2146762495 ||    //Expired
                certificateProblem == -2146762481)
            {   //Incorrect name?
                return true;
            }
            else
            {
                return false;
            }
        }
    }

}
