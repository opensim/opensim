/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Text;

namespace libTerrain
{
    partial class Channel
    {
        // Navier Stokes Algorithms ported from
        // "Real-Time Fluid Dynamics for Games" by Jos Stam.
        // presented at GDC 2003.

        // Poorly ported from C++. (I gave up making it properly native somewhere after nsSetBnd)

        private static int nsIX(int i, int j, int N)
        {
            return ((i) + (N + 2) * (j));
        }

        private static void nsSwap(ref double x0, ref double x)
        {
            double tmp = x0;
            x0 = x;
            x = tmp;
        }

        private static void nsSwap(ref double[] x0, ref double[] x)
        {
            double[] tmp = x0;
            x0 = x;
            x = tmp;
        }

        private void nsAddSource(int N, ref double[] x, ref double[] s, double dt)
        {
            int i;
            int size = (N + 2) * (N + 2);
            for (i = 0; i < size; i++)
            {
                x[i] += dt * s[i];
            }
        }

        private void nsSetBnd(int N, int b, ref double[] x)
        {
            int i;
            for (i = 0; i <= N; i++)
            {
                x[nsIX(0, i, N)]        = b == 1 ? -x[nsIX(1, i, N)] : x[nsIX(1, i, N)];
                x[nsIX(0, N + 1, N)]    = b == 1 ? -x[nsIX(N, i, N)] : x[nsIX(N, i, N)];
                x[nsIX(i, 0, N)]        = b == 2 ? -x[nsIX(i, 1, N)] : x[nsIX(i, 1, N)];
                x[nsIX(i, N + 1, N)]    = b == 2 ? -x[nsIX(i, N, N)] : x[nsIX(i, N, N)];
            }
            x[nsIX(0, 0, N)]            = 0.5f * (x[nsIX(1, 0, N)]      + x[nsIX(0, 1, N)]);
            x[nsIX(0, N + 1, N)]        = 0.5f * (x[nsIX(1, N + 1, N)]  + x[nsIX(0, N, N)]);
            x[nsIX(N + 1, 0, N)]        = 0.5f * (x[nsIX(N, 0, N)]      + x[nsIX(N + 1, 1, N)]);
            x[nsIX(N + 1, N + 1, N)]    = 0.5f * (x[nsIX(N, N + 1, N)]  + x[nsIX(N + 1, N, N)]);
        }

        private void nsLinSolve(int N, int b, ref double[] x, ref double[] x0, double a, double c)
        {
            int i, j;
            for (i = 1; i <= N; i++)
            {
                for (j = 1; j <= N; j++)
                {
                    x[nsIX(i, j, N)] = (x0[nsIX(i, j, N)] + a * 
                        (x[nsIX(i - 1, j, N)] + 
                         x[nsIX(i + 1, j, N)] + 
                         x[nsIX(i, j - 1, N)] + x[nsIX(i, j + 1, N)])
                        ) / c;
                }
            }

            nsSetBnd(N, b, ref x);
        }

        private void nsDiffuse(int N, int b, ref double[] x, ref double[] x0, double diff, double dt)
        {
            double a = dt * diff * N * N;
            nsLinSolve(N, b, ref x, ref x0, a, 1 + 4 * a);
        }

        private void nsAdvect(int N, int b, ref double[] d, ref double[] d0, ref double[] u, ref double[] v, double dt)
        {
            int i, j, i0, j0, i1, j1;
            double x, y, s0, t0, s1, t1, dt0;

            dt0 = dt * N;

            for (i = 1; i <= N; i++)
            {
                for (j = 1; j <= N; j++)
                {
                    x = i - dt0 * u[nsIX(i, j, N)];
                    y = j - dt0 * v[nsIX(i, j, N)];

                    if (x < 0.5)
                        x = 0.5;
                    if (x > N + 0.5)
                        x = N + 0.5;
                    i0 = (int)x; 
                    i1 = i0 + 1;

                    if (y < 0.5)
                        y = 0.5;
                    if (y > N + 0.5)
                        y = N + 0.5;
                    j0 = (int)y;
                    j1 = j0 + 1;

                    s1 = x - i0;
                    s0 = 1 - s1;
                    t1 = y - j0;
                    t0 = 1 - t1;

                    d[nsIX(i, j, N)] =  s0 * (t0 * d0[nsIX(i0, j0, N)] + t1 * d0[nsIX(i0, j1, N)]) +
                                        s1 * (t0 * d0[nsIX(i1, j0, N)] + t1 * d0[nsIX(i1, j1, N)]);
                }
            }

            nsSetBnd(N, b, ref d);
        }

        public void nsProject(int N, ref double[] u, ref double[] v, ref double[] p, ref double[] div)
        {
            int i, j;

            for (i = 1; i <= N; i++)
            {
                for (j = 1; j <= N; j++)
                {
                    div[nsIX(i, j, N)] = -0.5 * (u[nsIX(i + 1, j, N)] - u[nsIX(i - 1, j, N)] + v[nsIX(i, j + 1, N)] - v[nsIX(i, j - 1, N)]) / N;
                    p[nsIX(i, j, N)] = 0;
                }
            }

            nsSetBnd(N, 0, ref div);
            nsSetBnd(N, 0, ref p);

            nsLinSolve(N, 0, ref p, ref div, 1, 4);

            for (i = 1; i <= N; i++)
            {
                for (j = 1; j <= N; j++)
                {
                    u[nsIX(i, j, N)] -= 0.5 * N * (p[nsIX(i + 1, j, N)] - p[nsIX(i - 1, j, N)]);
                    v[nsIX(i, j, N)] -= 0.5 * N * (p[nsIX(i, j + 1, N)] - p[nsIX(i, j - 1, N)]);
                }
            }

            nsSetBnd(N, 1, ref u);
            nsSetBnd(N, 2, ref v);
        }

        private void nsDensStep(int N, ref double[] x, ref double[] x0, ref double[] u, ref double[] v, double diff, double dt)
        {
            nsAddSource(N, ref x, ref x0, dt);
            nsSwap(ref x0, ref x);
            nsDiffuse(N, 0, ref x, ref x0, diff, dt);
            nsSwap(ref x0, ref x);
            nsAdvect(N, 0, ref x, ref x0, ref u, ref v, dt);
        }

        private void nsVelStep(int N, ref double[] u, ref double[] v, ref double[] u0, ref double[] v0, double visc, double dt)
        {
            nsAddSource(N, ref u, ref u0, dt);
            nsAddSource(N, ref v, ref v0, dt);
            nsSwap(ref u0, ref u);
            nsDiffuse(N, 1, ref u, ref u0, visc, dt);
            nsSwap(ref v0, ref v);
            nsDiffuse(N, 2, ref v, ref v0, visc, dt);
            nsProject(N, ref u, ref v, ref u0, ref v0);
            nsSwap(ref u0, ref u);
            nsSwap(ref v0, ref v);
            nsAdvect(N, 1, ref u, ref u0, ref u0, ref v0, dt);
            nsAdvect(N, 2, ref v, ref v0, ref u0, ref v0, dt);
            nsProject(N, ref u, ref v, ref u0, ref v0);
        }

        private void nsBufferToDoubles(ref double[] dens, int N, ref double[,] doubles)
        {
            int i;
            int j;

            for (i = 0; i <= N; i++)
            {
                for (j = 0; j <= N; j++)
                {
                    doubles[i, j] = dens[nsIX(i, j, N)];
                }
            }
        }

        private void nsDoublesToBuffer(double[,] doubles, int N, ref double[] dens)
        {
            int i;
            int j;

            for (i = 0; i <= N; i++)
            {
                for (j = 0; j <= N; j++)
                {
                    dens[nsIX(i, j, N)] = doubles[i, j];
                }
            }
        }

        private void nsSimulate(int N, int rounds, double dt, double diff, double visc)
        {
            int size = (N * 2) * (N * 2);

            double[] u          = new double[size]; // Force, X axis
            double[] v          = new double[size]; // Force, Y axis
            double[] u_prev     = new double[size];
            double[] v_prev     = new double[size];
            double[] dens       = new double[size];
            double[] dens_prev  = new double[size];

            nsDoublesToBuffer(this.map, N, ref dens);
            nsDoublesToBuffer(this.map, N, ref dens_prev);

            for (int i = 0; i < rounds; i++)
            {
                u_prev = u;
                v_prev = v;
                dens_prev = dens;

                nsVelStep(N, ref u, ref v, ref u_prev, ref v_prev, visc, dt);
                nsDensStep(N, ref dens, ref dens_prev, ref u, ref v, diff, dt);
            }

            nsBufferToDoubles(ref dens, N, ref this.map);
        }

        /// <summary>
        /// Performs computational fluid dynamics on a channel
        /// </summary>
        /// <param name="rounds">The number of steps to perform (Recommended: 20)</param>
        /// <param name="dt">Delta Time - The time between steps (Recommended: 0.1)</param>
        /// <param name="diff">Fluid diffusion rate (Recommended: 0.0)</param>
        /// <param name="visc">Fluid viscosity (Recommended: 0.0)</param>
        public void navierStokes(int rounds, double dt, double diff, double visc)
        {
            nsSimulate(this.h, rounds, dt, diff, visc);
        }
    }
}