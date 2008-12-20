using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.OpenJpeg
{
    public class openjpeg
    {
        public openjpeg()
        {
           

        }
    }

    public enum PROG_ORDER
    {
        PROG_UNKNOWN = -1,
        LRCP = 0,
        RLCP = 1,
        RPCL = 2, 
        PCRL = 3,
        CPRL = 4
    }

    public enum RSIZ_CAPABILITIES
    {
        STD_RSIZ = 0,
        CINEMA2K = 3,
        CINEMA4K = 4
    }

    public enum CINEMA_MODE
    {
        OFF = 0,
        CINEMA2K_24 = 1,
        CINEMA2K_48 = 2,
        CINEMA4K_24 = 3
    }

    public enum COLOR_SPACE
    {
        CLRSPC_UNKNOWN = -1,
        CLRSPC_SRGB = 1,
        CLRSPC_GRAY = 2,
        CLRSPC_SYCC = 3
    }

    public enum CODEC_FORMAT
    {
        CODEC_UNKNOWN = -1,
        CODEC_J2K = 0,
        CODEC_JPT = 1,
        CODEC_JP2 = 2
    }

    public enum LIMIT_DECODING
    {
        NO_LIMITATION = 0,
        LIMIT_TO_MAIN_HEADER=1,
        DECODE_ALL_BUT_PACKETS = 2
    }

    public struct opj_poc
    {
        public int resno0, compno0;
        public int layno1, resno1, compno1;
        public int layno0, precno0, precno1;
        public PROG_ORDER prg1, prg;
        /// <summary>
        /// Don't forget to initialize with 5 elements
        /// </summary>
        public sbyte[] progorder;
        public int tile;
        public int tx0, tx1, ty0, ty1;
        public int layS, resS, copmS, prcS;
        public int layE, resE, compE, prcE;
        public int txS, txE, tyS, tyE, dx, dy;
        public int lay_t, res_t, comp_t, prc_t, tx0_t, ty0_t;
    }

    public struct opj_cparameters
    {
        public bool tile_size_on;
        public int cp_tx0;
        public int cp_ty0;
        public int cp_tdx;
        public int cp_tdy;
        public int cp_disto_alloc;
        public int cp_fixed_alloc;
        public int cp_fixed_wuality;
        public int cp_matrice;
        public sbyte cp_comment;
        public int csty;
        public PROG_ORDER prog_order;

        /// <summary>
        /// Don't forget to initialize 32 elements
        /// </summary>
        public opj_poc[] POC;
        public int numpocs;
        public int tcp_numlayers;
        /// <summary>
        /// Don't forget to intitialize 100 elements
        /// </summary>
        public float[] tcp_rates;
        /// <summary>
        /// Don't forget to initialize 100 elements
        /// </summary>
        public float[] tcp_distoratio;
        public int numresolution;
        public int cblockw_init;
        public int cblockh_init;
        public int mode;
        public int irreversible;
        public int roi_compno;
        public int roi_shift;
        public int res_spec;

        /// <summary>
        /// Don't forget to initialize 33 elements
        /// </summary>
        public int[] prc_init;
        /// <summary>
        /// Don't forget to initialize 33 elements
        /// </summary>
        public int[] prch_init;

        public string infile;
        public string outfile;
        public int index_on;
        public string index;
        public int image_offset_x0;
        public int image_offset_y0;
        public int subsampling_dx;
        public int subsampling_dy;
        public int decod_format;
        public int cod_format;
        public bool jpwl_epc_on;
        public int jpwl_hprot_MH;
        /// <summary>
        /// Don't forget to initialize 16 elements
        /// </summary>
        public int[] jpwl_hprot_TPH_tileno;
        /// <summary>
        /// Don't forget to initialize 16 elements
        /// </summary>
        public int[] jpwl_hprot_TPH;

        /// <summary>
        /// Don't forget to initialize 16 elements
        /// </summary>
        public int[] jpwl_pprot_tileno;
        public int[] jpwl_pprot_packno;
        public int[] jpwl_pprot;
        public int jpwl_sens_size;
        public int jpwl_sense_addr;
        public int jpwl_sens_range;
        public int jpwl_sens_MH;

        /// <summary>
        /// Don't forget to initialize 16 elements
        /// </summary>
        public int[] jpwl_sens_TPH_tileno;

        /// <summary>
        /// Don't forget to initialize 16 elements
        /// </summary>
        public int[] jpwl_sens_TPH;
        public CINEMA_MODE cp_cinema;
        public int max_comp_size;
        public sbyte tp_on;
        public sbyte tp_flag;
        public sbyte tcp_mct;
    }

    public struct opj_dparameters
    {
        public int cp_reduce;
        public int cp_layer;
        public string infile;
        public string outfile;
        public int decod_format;
        public int cod_format;
        public bool jpwl_correct;
        public int jpwl_exp_comps;
        public int jpwl_max_tiles;
        public LIMIT_DECODING cp_limit_decoding;

    }

    public struct opj_common_fields
    {
        public bool is_decompressor;
        public CODEC_FORMAT codec_format;
    }

    public struct opj_common_struct
    {
        public opj_common_fields flds;
    }

    public struct opj_cinfo
    {
        public opj_common_fields flds;
    }
    public struct opj_dinfo
    {
        public opj_common_fields flds;
    }

    public struct opj_cio
    {
        public opj_common_struct cinfo;
        public int openmode;
        public byte buffer;
        public int length;
        public byte start;
        public byte end;
        public byte bp;
    }

    public struct opj_image_comp
    {
        public int dx;
        public int dy;
        public int w;
        public int h;
        public int x0;
        public int y0;
        public int prec;
        public int bpp;
        public int sgnd;
        public int resno_decoded;
        public int factor;
        public int data;
    }

    public struct opj_image
    {
        public int x0;
        public int y0;
        public int x1;
        public int y1;
        public int numcomps;
        public COLOR_SPACE color_space;
        public opj_image_comp comps;
    }

    public struct opj_image_comptparm
    {
        public int dx;
        public int dy;
        public int w;
        public int h;
        public int x0;
        public int y0;
        public int prec;
        public int bpp;
        public int sgnd;
    }

    public struct opj_packet_info
    {
        public int start_pos;
        public int end_ph_pos;
        public int end_pos;
        public double disto;
    }

    public struct opj_tp_info
    {
        public int tp_start_pos;
        public int tp_end_header;
        public int tp_end_pos;
        public int tp_start_pack;
        public int tp_numpacks;
    }

    public struct opj_tile_info
    {
        public double thresh;
        public int tileno;
        public int start_pos;
        public int end_header;
        public int end_pos;
        /// <summary>
        /// Don't forget to initialize 33 elements
        /// </summary>
        public int[] pw;
        /// <summary>
        /// Don't forget to initialize 33 elements
        /// </summary>
        public int[] ph;
        /// <summary>
        /// Don't forget to initialize 33 elements
        /// </summary>
        public int[] pdx;
        /// <summary>
        /// Don't forget to initialize 33 elements
        /// </summary>
        public int[] pdy;

        public opj_packet_info packet;
        public int numpix;
        public double distotile;
        public int num_tps;
        public opj_tp_info tp;
    }

    public struct opj_marker_info_t
    {
        public ushort type;
        public int pos;
        public int len;
    }

    public struct opj_codestream_info
    {
        public double D_max;
        public int packno;
        public int index_write;
        public int image_w;
        public int image_h;

        public PROG_ORDER prog;

        public int tile_x;
        public int tile_y;
        public int tile_Ox;
        public int tile_Oy;
        public int tw;
        public int numcomps;
        public int numlayers;
        public int numdecompos;
        public int marknum;
        public opj_marker_info_t marker;
        public int maxmarknum;
        public int main_head_start;
        public int main_head_end;
        public int codestream_size;
        public opj_tile_info tile;

    }






    public static class opj_defines
    {
        public const int OPJ_STREAM_READ = 0x0001;
        public const int OPJ_STREAM_WRITE = 0x0002;
    
    }

}
