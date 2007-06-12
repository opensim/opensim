/*
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of libTerrain nor the names of
      its contributors may be used to endorse or promote products
      derived from this software without specific prior written
      permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/


using System;
using System.Collections.Generic;
using System.Text;
using libTerrain;

namespace libTerrainTests
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine("Starting tests...");

            Channel test;

            try
            {
                System.Console.WriteLine("Blank Heightmap");
                test = new Channel();
                test.fill(0);
                test.saveImage("test_blank.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }


            try
            {
                System.Console.WriteLine("Grayscale Cube");
                test = new Channel();
                test.fill(0);
                test.gradientCube();
                test.saveImage("test_cube.png");

                test.Polar();
                test.saveImage("test_polarcube.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Spiral Planters");
                test = new Channel();
                test.fill(0);

                test.SpiralPlanter(200, Math.PI / 15, 0.75, 0, 0);
                test.normalise();
                //test.Spiral(192, 192, 50);
                test.saveImage("test_spiral.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Spiral Cells");
                test = new Channel();
                test.fill(0);

                double[] c = new double[2];
                c[0] = -1;
                c[1] = 1;

                test.SpiralCells(200, Math.PI / 15, 0.75, 0, 0, c);
                test.normalise();
                //test.Spiral(192, 192, 50);
                test.saveImage("test_spiralcells.png");

                test.fill(0);
                test.SpiralCells(30, Math.PI / 30, 0, 75, 0, c);
                test.normalise();
                //test.Spiral(192, 192, 50);
                test.saveImage("test_circlecells.png");

            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Fracturing");
                test = new Channel();
                test.fill(0);
                test.fracture(300, 0, 1);
                test.saveImage("test_fracture.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Voronoi (Flat)");
                test = new Channel();
                test.fill(0);
                test.voroflatDiagram(64, 384);
                test.saveImage("test_voroflat.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }


            try
            {
                System.Console.WriteLine("Voronoi (Flat / Fixnormal)");
                test = new Channel();
                test.fill(0);
                test.voroflatDiagram(64, 384);
                test ^= 4;
                test.normalise();
                test.saveImage("test_voroflatfixnormal.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("File Import (Mask Flatten)");
                test = new Channel();
                Channel test2;
                test2 = test.loadImage("test_chained_aerobic.png");

                test.fill(0);
                test.voroflatDiagram(64, 384);
                test ^= 4;
                test.normalise();

                test.smooth(4);
                test.normalise();

                test.saveImage("test_flatmask.png");
                test2.flatten(test, 1.0);

                test2.saveImage("test_fileflatmask.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Worms");
                test = new Channel();
                test.worms(100, 10, 20.0, 16, false);
                test.normalise();
                test.saveImage("test_worms.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Noise");
                test = new Channel();
                test.noise();
                test.saveImage("test_noise.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Hills (Spherical)");
                test = new Channel();
                test.hillsSpheres(200, 20, 30, true, true, false);
                test.saveImage("test_hillspheres.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Hills (Blocks)");
                test = new Channel();
                test.hillsBlocks(200, 20, 30, true, true, false);
//                test.hillsSpheres(200, 20, 30, true, true, false);
                test.saveImage("test_hillblocks.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Hills (Cones)");
                test = new Channel();
                test.fill(0);
                test.hillsCones(200, 20, 30, true, true, false);
                test.normalise();
                test.saveImage("test_hillcones.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Voronoi Diagram");
                test = new Channel();
                double[] c = new double[2];
                c[0] = -1;
                c[1] = 1;
                test.voronoiDiagram(4, 128, c);
                test.saveImage("test_voronoi.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Raising Terrain");
                test = new Channel();
                test.fill(0);
                test.raise(128, 128, 64, 1.0);
                test.normalise();
                test.saveImage("test_raise.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            try
            {
                System.Console.WriteLine("Flattening Terrain (unmasked)");
                test = new Channel();
                test.noise();
                test.flatten(128, 128, 64, 1.0);
                test.normalise();
                test.saveImage("test_flatten.png");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Unhandled exception: " + e.ToString());
            }

            

            System.Console.WriteLine("Done");
        }
    }
}
