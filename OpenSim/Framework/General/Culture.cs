using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace OpenSim.Framework
{
    public class Culture
    {
        private static readonly CultureInfo m_cultureInfo = new System.Globalization.CultureInfo("en-US", true);

        public static NumberFormatInfo NumberFormatInfo
        {
            get
            {
                return m_cultureInfo.NumberFormat;
            }
        }

        public static void SetCurrentCulture()
        {
            Thread.CurrentThread.CurrentCulture = m_cultureInfo;
        }
    }
}
