using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.OpenJpeg
{
    public static class bio
    {
        
        public static opj_bio bio_create()
        {
            opj_bio bio = new opj_bio();
            return bio;
        }

        public static void bio_destroy(opj_bio bio)
        {
            // not needed on C#
        }

        public static int bio_numbytes(opj_bio bio)
        {
            return (bio.bp - bio.start);
        }

        public static void bio_init_enc(opj_bio bio, sbyte bp, int len)
        {
            bio.start = (byte)bp;
            bio.end = (byte)(bp + (byte)len);
            bio.bp = (byte)bp;
            bio.buf = 0;
            bio.ct = 8;
        }

        public static void bio_init_dec(opj_bio bio, sbyte bp, int len)
        {
            bio.start = (byte)bp;
            bio.end = (byte)(bp + len);
            bio.bp = (byte)bp;
            bio.buf = 0;
            bio.ct = 0;
        }

        public static void bio_write(opj_bio bio, int v, int n)
        {
            for (int i = n - 1; i >= 0; i--)
                bio_putbit(bio, (v >> i) & 1);
        }

        public static int bio_read(opj_bio bio, int n)
        {
            int v = 0;
            for (int i = n - 1; i >= 0; i--)
                v += bio_getbit(bio) << i;

            return v;
        }

        public static int bio_flush(opj_bio bio)
        {
            bio.ct = 0;
            if (bio_byteout(bio) != 0)
                return 1;

            if (bio.ct == 7)
            {
                bio.ct = 0;
                if (bio_byteout(bio) != 0)
                    return 1;
            }
            return 0;
        }

        public static int bio_inalign(opj_bio bio)
        {
            bio.ct = 0;
            if ((bio.buf & 0xff) == 0xff)
            {
                if (bio_bytein(bio) != 0)
                    return 1;
                bio.ct = 0;
            }
            return 0;
        }

        private static int bio_bytein(opj_bio bio)
        {
            bio.buf = (bio.buf << 8) & 0xffff;
            bio.ct = bio.buf == 0xff00 ? 7 : 8;
            if (bio.bp >= bio.end)
                return 1;
            bio.buf |= bio.bp++;

            return 0;
        }

        private static int bio_byteout(opj_bio bio)
        {
            bio.buf = (bio.buf << 8) & 0xffff;
            bio.ct = bio.buf == 0xff00 ? 7 : 8;
            if (bio.bp >= bio.end)
                return 1;

            bio.bp = (byte)(bio.buf >> 8);
            bio.bp++;
            return 0;
        }

        private static void bio_putbit(opj_bio bio, int b)
        {
            if (bio.ct == 0)
                bio_byteout(bio);

            bio.ct--;
            bio.buf |= (byte)(b << bio.ct);

        }

        private static int bio_getbit(opj_bio bio)
        {
            if (bio.ct == 0)
                bio_bytein(bio);
            bio.ct--;

            return (int)((bio.buf >> bio.ct) & 1);
        }

    }

    public struct opj_bio
    {
        public byte start;
        public byte end;
        public byte bp;
        public uint buf;
        public int ct;
    }
}
