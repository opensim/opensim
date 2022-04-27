using System;
using System.Collections;
using System.Collections.Generic;

namespace OSHttpServer
{
    /// <summary>
    /// Returns item either from a form or a query string (checks them in that order)
    /// </summary>
    public class HttpParam : IHttpInput
    {
        /// <summary> Representation of a non-initialized HttpParam </summary>
        public static readonly HttpParam Empty = new HttpParam(HttpInput.Empty, HttpInput.Empty);

        private IHttpInput m_form;
        private IHttpInput m_query;

        private List<HttpInputItem> _items = new List<HttpInputItem>();

        /// <summary>Initialises the class to hold a value either from a post request or a querystring request</summary>
        public HttpParam(IHttpInput form, IHttpInput query)
        {
            m_form = form;
            m_query = query;
        }

        #region IHttpInput Members

        /// <summary>
        /// The add method is not availible for HttpParam
        /// since HttpParam checks both Request.Form and Request.QueryString
        /// </summary>
        /// <param name="name">name identifying the value</param>
        /// <param name="value">value to add</param>
        /// <exception cref="NotImplementedException"></exception>
        [Obsolete("Not implemented for HttpParam")]
        public void Add(string name, string value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks whether the form or querystring has the specified value
        /// </summary>
        /// <param name="name">Name, case sensitive</param>
        /// <returns>true if found; otherwise false.</returns>
        public bool Contains(string name)
        {
            return m_form.Contains(name) || m_query.Contains(name);
        }

        /// <summary>
        /// Fetch an item from the form or querystring (in that order).
        /// </summary>
        /// <param name="name"></param>
        /// <returns>Item if found; otherwise HttpInputItem.EmptyLanguageNode</returns>
        public HttpInputItem this[string name]
        {
            get
            {
                if (m_form[name] != HttpInputItem.Empty)
                    return m_form[name];
                else
                    return m_query[name];
            }
        }

        #endregion

        internal void SetQueryString(HttpInput query)
        {
            m_query = query;
        }

        internal void SetForm(HttpInput form)
        {
            m_form = form;
        }

        ///<summary>
        ///Returns an enumerator that iterates through the collection.
        ///</summary>
        ///
        ///<returns>
        ///A <see cref="T:System.Collections.Generic.IEnumerator`1"></see> that can be used to iterate through the collection.
        ///</returns>
        ///<filterpriority>1</filterpriority>
        IEnumerator<HttpInputItem> IEnumerable<HttpInputItem>.GetEnumerator()
        {
            List<HttpInputItem> items = new List<HttpInputItem>(m_query);
            items.AddRange(m_form);
            return items.GetEnumerator();
        }

        #region IEnumerable Members

        ///<summary>
        ///Returns an enumerator that iterates through a collection.
        ///</summary>
        ///
        ///<returns>
        ///An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the collection.
        ///</returns>
        ///<filterpriority>2</filterpriority>
        public IEnumerator GetEnumerator()
        {
            List<HttpInputItem> items = new List<HttpInputItem>(m_query);
            items.AddRange(m_form);
            return items.GetEnumerator();
        }

        #endregion
    }
}
