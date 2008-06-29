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
 */

using System;
using libsecondlife;
using log4net;

namespace OpenSim.Framework
{
    public class RegionSettings
    {
		private LLUUID m_RegionUUID;

		public LLUUID RegionUUID
		{
			get { return m_RegionUUID; }
			set { m_RegionUUID = value; }
		}

		private bool m_BlockTerraform;
		
		public bool BlockTerraform
		{
			get { return m_BlockTerraform; }
			set { m_BlockTerraform = value; }
		}

		private bool m_BlockFly;
		
		public bool BlockFly
		{
			get { return m_BlockFly; }
			set { m_BlockFly = value; }
		}

		private bool m_AllowDamage;
		
		public bool AllowDamage
		{
			get { return m_AllowDamage; }
			set { m_AllowDamage = value; }
		}

		private bool m_RestrictPushing;
		
		public bool RestrictPushing
		{
			get { return m_RestrictPushing; }
			set { m_RestrictPushing = value; }
		}

		private bool m_AllowLandResell;
		
		public bool AllowLandResell
		{
			get { return m_AllowLandResell; }
			set { m_AllowLandResell = value; }
		}

		private bool m_AllowLandJoinDivide;
		
		public bool AllowLandJoinDivide
		{
			get { return m_AllowLandJoinDivide; }
			set { m_AllowLandJoinDivide = value; }
		}

		private bool m_BlockShowInSearch;
		
		public bool BlockShowInSearch
		{
			get { return m_BlockShowInSearch; }
			set { m_BlockShowInSearch = value; }
		}

		private int m_AgentLimit;
		
		public int AgentLimit
		{
			get { return m_AgentLimit; }
			set { m_AgentLimit = value; }
		}

		private double m_ObjectBonus;
		
		public double ObjectBonus
		{
			get { return m_ObjectBonus; }
			set { m_ObjectBonus = value; }
		}

		private int m_Maturity;
		
		public int Maturity
		{
			get { return m_Maturity; }
			set { m_Maturity = value; }
		}

		private bool m_DisableScripts;
		
		public bool DisableScripts
		{
			get { return m_DisableScripts; }
			set { m_DisableScripts = value; }
		}

		private bool m_DisableCollisions;
		
		public bool DisableCollisions
		{
			get { return m_DisableCollisions; }
			set { m_DisableCollisions = value; }
		}

		private bool m_DisablePhysics;
		
		public bool DisablePhysics
		{
			get { return m_DisablePhysics; }
			set { m_DisablePhysics = value; }
		}

		private LLUUID m_TerrainTexture1;
		
		public LLUUID TerrainTexture1
		{
			get { return m_TerrainTexture1; }
			set { m_TerrainTexture1 = value; }
		}

		private LLUUID m_TerrainTexture2;
		
		public LLUUID TerrainTexture2
		{
			get { return m_TerrainTexture2; }
			set { m_TerrainTexture2 = value; }
		}

		private LLUUID m_TerrainTexture3;
		
		public LLUUID TerrainTexture3
		{
			get { return m_TerrainTexture3; }
			set { m_TerrainTexture3 = value; }
		}

		private LLUUID m_TerrainTexture4;
		
		public LLUUID TerrainTexture4
		{
			get { return m_TerrainTexture4; }
			set { m_TerrainTexture4 = value; }
		}

		private double m_Elevation1NW;
		
		public double Elevation1NW
		{
			get { return m_Elevation1NW; }
			set { m_Elevation1NW = value; }
		}

		private double m_Elevation2NW;
		
		public double Elevation2NW
		{
			get { return m_Elevation2NW; }
			set { m_Elevation2NW = value; }
		}

		private double m_Elevation1NE;
		
		public double Elevation1NE
		{
			get { return m_Elevation1NE; }
			set { m_Elevation1NE = value; }
		}

		private double m_Elevation2NE;
		
		public double Elevation2NE
		{
			get { return m_Elevation2NE; }
			set { m_Elevation2NE = value; }
		}

		private double m_Elevation1SE;
		
		public double Elevation1SE
		{
			get { return m_Elevation1SE; }
			set { m_Elevation1SE = value; }
		}

		private double m_Elevation2SE;
		
		public double Elevation2SE
		{
			get { return m_Elevation2SE; }
			set { m_Elevation2SE = value; }
		}

		private double m_Elevation1SW;
		
		public double Elevation1SW
		{
			get { return m_Elevation1SW; }
			set { m_Elevation1SW = value; }
		}

		private double m_Elevation2SW;
		
		public double Elevation2SW
		{
			get { return m_Elevation2SW; }
			set { m_Elevation2SW = value; }
		}

		private double m_WaterHeight;
		
		public double WaterHeight
		{
			get { return m_WaterHeight; }
			set { m_WaterHeight = value; }
		}

		private double m_TerrainRaiseLimit;
		
		public double TerrainRaiseLimit
		{
			get { return m_TerrainRaiseLimit; }
			set { m_TerrainRaiseLimit = value; }
		}

		private double m_TerrainLowerLimit;
		
		public double TerrainLowerLimit
		{
			get { return m_TerrainLowerLimit; }
			set { m_TerrainLowerLimit = value; }
		}

		private bool m_UseEstateSun;
		
		public bool UseEstateSun
		{
			get { return m_UseEstateSun; }
			set { m_UseEstateSun = value; }
		}

		private bool m_FixedSun;
		
		public bool FixedSun
		{
			get { return m_FixedSun; }
			set { m_FixedSun = value; }
		}

		private double m_SunPosition;
		
		public double SunPosition
		{
			get { return m_SunPosition; }
			set { m_SunPosition = value; }
		}

		private LLUUID m_Covenant;
		
		public LLUUID Covenant
		{
			get { return m_Covenant; }
			set { m_Covenant = value; }
		}
	}
}
