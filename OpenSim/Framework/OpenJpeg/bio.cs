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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

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
