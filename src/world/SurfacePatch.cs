using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.world
{
    public class SurfacePatch
    {
    	public float[] HeightMap;

	public SurfacePatch() {
		HeightMap = new float[16*16];
		
		int xinc;
            	int yinc;
		for(xinc=0; xinc<16; xinc++) for(yinc=0; yinc<16; yinc++) {
			HeightMap[xinc+(yinc*16)]=100.0f;
		}
					
	}
    }
}
