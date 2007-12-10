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

namespace libTerrain
{
    partial class Channel
    {
        /* Operator combination of channel datatypes */

        public static Channel operator +(Channel A, Channel B)
        {
            if (A.h != B.h)
                throw new Exception("Cannot add heightmaps, of different height.");
            if (A.w != B.w)
                throw new Exception("Cannot add heightmaps, of different width.");

            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    if (B.map[x, y] != 0)
                        A.SetDiff(x, y);

                    A.map[x, y] += B.map[x, y];
                }
            }

            return A;
        }

        public static Channel operator *(Channel A, Channel B)
        {
            if (A.h != B.h)
                throw new Exception("Cannot multiply heightmaps, of different height.");
            if (A.w != B.w)
                throw new Exception("Cannot multiply heightmaps, of different width.");

            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] *= B.map[x, y];
                }
            }

            A.SetDiff();

            return A;
        }

        public static Channel operator -(Channel A, Channel B)
        {
            if (A.h != B.h)
                throw new Exception("Cannot subtract heightmaps, of different height.");
            if (A.w != B.w)
                throw new Exception("Cannot subtract heightmaps, of different width.");

            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    if (B.map[x, y] != 0)
                        A.SetDiff(x, y);
                    A.map[x, y] -= B.map[x, y];
                }
            }

            return A;
        }

        public static Channel operator /(Channel A, Channel B)
        {
            if (A.h != B.h)
                throw new Exception("Cannot divide heightmaps, of different height.");
            if (A.w != B.w)
                throw new Exception("Cannot divide heightmaps, of different width.");

            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] /= B.map[x, y];
                }
            }

            A.SetDiff();

            return A;
        }

        public static Channel operator ^(Channel A, Channel B)
        {
            if (A.h != B.h)
                throw new Exception("Cannot divide heightmaps, of different height.");
            if (A.w != B.w)
                throw new Exception("Cannot divide heightmaps, of different width.");

            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] = Math.Pow(A.map[x, y], B.map[x, y]);
                }
            }

            A.SetDiff();

            return A;
        }


        /* Operator combination of channel and double datatypes */

        public static Channel operator +(Channel A, double B)
        {
            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] += B;
                }
            }

            if (B != 0)
                A.SetDiff();

            return A;
        }

        public static Channel operator -(Channel A, double B)
        {
            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] -= B;
                }
            }

            if (B != 0)
                A.SetDiff();

            return A;
        }

        public static Channel operator *(Channel A, double B)
        {
            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] *= B;
                }
            }

            if (B != 1)
                A.SetDiff();

            return A;
        }

        public static Channel operator /(Channel A, double B)
        {
            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] /= B;
                }
            }

            if (B != 1)
                A.SetDiff();

            return A;
        }

        public static Channel operator ^(Channel A, double B)
        {
            int x, y;

            for (x = 0; x < A.w; x++)
            {
                for (y = 0; y < A.h; y++)
                {
                    A.map[x, y] = Math.Pow(A.map[x, y], B);
                }
            }

            A.SetDiff();

            return A;
        }
    }
}
