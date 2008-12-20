using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.OpenJpeg
{

    public static class j2k
    {
    }

    public enum J2K_STATUS
    {
        J2K_STATE_MHSOC = 0x0001, /**< a SOC marker is expected */
        J2K_STATE_MHSIZ = 0x0002, /**< a SIZ marker is expected */
        J2K_STATE_MH = 0x0004, /**< the decoding process is in the main header */
        J2K_STATE_TPHSOT = 0x0008, /**< the decoding process is in a tile part header and expects a SOT marker */
        J2K_STATE_TPH = 0x0010, /**< the decoding process is in a tile part header */
        J2K_STATE_MT = 0x0020, /**< the EOC marker has just been read */
        J2K_STATE_NEOC = 0x0040, /**< the decoding process must not expect a EOC marker because the codestream is truncated */
        J2K_STATE_ERR = 0x0080  /**< the decoding process has encountered an error */
    }

    public enum J2K_T2_MODE
    {
        THRESH_CALC = 0,	/** Function called in Rate allocation process*/
        FINAL_PASS = 1		/** Function called in Tier 2 process*/
    }

    public struct opj_stepsize
    {
        public int expn;
        public int mant;
    }

    public struct opj_tccp
    {
        public int csty;
        public int numresolutions;
        public int cblkw;
        public int cblkh;
        public int cblksty;
        public int qmfbid;
        public int qntsty;
        /// <summary>
        /// don't forget to initialize 97 elements
        /// </summary>
        public opj_stepsize[] stepsizes;
        public int numgbits;
        public int roishift;
        /// <summary>
        /// Don't forget to initialize 33 elements
        /// </summary>
        public int[] prcw;

    }

    public struct opj_tcp
    {
        public int first;
        public int csty;
        public PROG_ORDER prg;
        public int numlayers;
        public int mct;
        /// <summary>
        /// don't forget to initialize to 100
        /// </summary>
        public float[] rates;
        public int numpocs;
        public int POC;
        /// <summary>
        /// Don't forget to initialize to 32
        /// </summary>
        public opj_poc[] pocs;
        public byte ppt_data;
        public byte ppt_data_first;
        public int ppt;
        public int ppt_store;
        public int ppt_len;
        /// <summary>
        /// Don't forget to initialize 100 elements
        /// </summary>
        public float[] distoratio;
        public opj_tccp tccps;

    }

    public struct opj_cp
    {
        public CINEMA_MODE cinema;
        public int max_comp_size;
        public int img_size;
        public RSIZ_CAPABILITIES rsiz;
        public sbyte tp_on;
        public sbyte tp_flag;
        public int tp_pos;
        public int distro_alloc;
        public int fixed_alloc;
        public int fixed_quality;
        public int reduce;
        public int layer;
        public LIMIT_DECODING limit_decoding;
        public int tx0;
        public int ty0;
        public int tdx;
        public int tdy;
        public sbyte? comment;
        public int tw;
        public int th;
        public int? tileno;
        public byte ppm_data;
        public byte ppm_data_first;
        public int ppm;
        public int ppm_store;
        public int ppm_previous;
        public int ppm_len;
        public opj_tcp tcps;
        public int matrice;
    }

    public static class j2kdefines
    {
        public const uint J2K_CP_CSTY_PRT = 0x01;
        public const uint J2K_CP_CSTY_SOP = 0x02;
        public const uint J2K_CP_CSTY_EPH = 0x04;
        public const uint J2K_CCP_CSTY_PRT = 0x01;
        public const uint J2K_CCP_CBLKSTY_LAZY = 0x01;
        public const uint J2K_CCP_CBLKSTY_RESET = 0x02;
        public const uint J2K_CCP_CBLKSTY_TERMALL = 0x04;
        public const uint J2K_CCP_CBLKSTY_VSC = 0x08;
        public const uint J2K_CCP_CBLKSTY_PTERM  =0x10;
        public const uint J2K_CCP_CBLKSTY_SEGSYM = 0x20;
        public const uint J2K_CCP_QNTSTY_NOQNT = 0;
        public const uint J2K_CCP_QNTSTY_SIQNT = 1;
        public const uint J2K_CCP_QNTSTY_SEQNT = 2;

        public const uint J2K_MS_SOC = 0xff4f;	/**< SOC marker value */
        public const uint J2K_MS_SOT = 0xff90;	/**< SOT marker value */
        public const uint J2K_MS_SOD = 0xff93;	/**< SOD marker value */
        public const uint J2K_MS_EOC = 0xffd9;	/**< EOC marker value */
        public const uint J2K_MS_SIZ = 0xff51;	/**< SIZ marker value */
        public const uint J2K_MS_COD = 0xff52;	/**< COD marker value */
        public const uint J2K_MS_COC = 0xff53;	/**< COC marker value */
        public const uint J2K_MS_RGN = 0xff5e;	/**< RGN marker value */
        public const uint J2K_MS_QCD = 0xff5c;	/**< QCD marker value */
        public const uint J2K_MS_QCC = 0xff5d;	/**< QCC marker value */
        public const uint J2K_MS_POC = 0xff5f;	/**< POC marker value */
        public const uint J2K_MS_TLM = 0xff55;	/**< TLM marker value */
        public const uint J2K_MS_PLM = 0xff57;	/**< PLM marker value */
        public const uint J2K_MS_PLT = 0xff58;	/**< PLT marker value */
        public const uint J2K_MS_PPM = 0xff60;	/**< PPM marker value */
        public const uint J2K_MS_PPT = 0xff61;	/**< PPT marker value */
        public const uint J2K_MS_SOP = 0xff91;	/**< SOP marker value */
        public const uint J2K_MS_EPH = 0xff92;	/**< EPH marker value */
        public const uint J2K_MS_CRG = 0xff63;	/**< CRG marker value */
        public const uint J2K_MS_COM = 0xff64;	/**< COM marker value */
    }
}
