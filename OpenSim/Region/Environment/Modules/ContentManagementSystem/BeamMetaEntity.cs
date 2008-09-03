// BeamMetaEntity.cs created with MonoDevelop
// User: bongiojp at 3:03 PM 8/6/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using System.Collections.Generic;
using System.Drawing;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using log4net;
using OpenSim.Region.Physics.Manager;
using Axiom.Math;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{
	
	
	public class BeamMetaEntity : PointMetaEntity
	{
		
		public BeamMetaEntity(Scene scene, uint LocalId, LLVector3 groupPos, float transparency, SceneObjectPart To, LLVector3 color) : base(scene, LocalId, groupPos, transparency)
		{
			SetBeamToUUID(To, color);
		}
		
		public BeamMetaEntity(Scene scene, LLUUID uuid, uint LocalId, LLVector3 groupPos, float transparency, SceneObjectPart To, LLVector3 color) : base(scene, uuid, LocalId, groupPos, transparency)
		{
			SetBeamToUUID(To, color);
		}
		
		public void SetBeamToUUID(SceneObjectPart To, LLVector3 color)
		{
			SceneObjectPart From = m_Entity.RootPart;
			//Scale size of particles to distance objects are apart (for better visibility)
			LLVector3 FromPos = From.GetWorldPosition();
			LLVector3 ToPos = From.GetWorldPosition();
			LLUUID toUUID = To.UUID;
			float distance = (float) (Math.Sqrt(Math.Pow(FromPos.X-ToPos.X, 2) + 
			                                    Math.Pow(FromPos.X-ToPos.Y, 2) + 
			                                    Math.Pow(FromPos.X-ToPos.Z, 2)
			                                    )
			                          );
			//float rate = (float)  (distance/4f);
			float rate = 0.5f;
			float scale = (float) (distance/128f);
			float speed = (float) (2.0f - distance/128f);	
			
			SetBeamToUUID(From, To, color, rate, scale, speed);
		}
		
		public void SetBeamToUUID(SceneObjectPart From, SceneObjectPart To,  LLVector3 color, float rate, float scale, float speed)
		{	
			Primitive.ParticleSystem prules = new Primitive.ParticleSystem();
			//prules.PartDataFlags = Primitive.ParticleSystem.ParticleDataFlags.Emissive |                                                                                                                    
			//      Primitive.ParticleSystem.ParticleDataFlags.FollowSrc;   //PSYS_PART_FLAGS   
			prules.PartDataFlags = Primitive.ParticleSystem.ParticleDataFlags.Beam | 
				Primitive.ParticleSystem.ParticleDataFlags.TargetPos;
			prules.PartStartColor.R = color.X;                                               //PSYS_PART_START_COLOR                                                                                          
			prules.PartStartColor.G = color.Y;
			prules.PartStartColor.B = color.Z;
			prules.PartStartColor.A = 1.0f;                                               //PSYS_PART_START_ALPHA, transparency                                                                               
			prules.PartEndColor.R = color.X;                                                 //PSYS_PART_END_COLOR                                                                                            
			prules.PartEndColor.G = color.Y;
			prules.PartEndColor.B = color.Z;
			prules.PartEndColor.A = 1.0f;                                                 //PSYS_PART_END_ALPHA, transparency                                                                                 
			prules.PartStartScaleX = scale;                                                //PSYS_PART_START_SCALE                                                                                             
			prules.PartStartScaleY = scale;
			prules.PartEndScaleX = scale;                                                  //PSYS_PART_END_SCALE                                                                                               
			prules.PartEndScaleY = scale;
			prules.PartMaxAge = 1.0f;                                                     //PSYS_PART_MAX_AGE                                                                                                 
			prules.PartAcceleration.X = 0.0f;                                             //PSYS_SRC_ACCEL                                                                                                                
			prules.PartAcceleration.Y = 0.0f;
			prules.PartAcceleration.Z = 0.0f;
			//prules.Pattern = Primitive.ParticleSystem.SourcePattern.Explode;                 //PSYS_SRC_PATTERN                                                                                                           
			//prules.Texture = LLUUID.Zero;//= LLUUID                                                     //PSYS_SRC_TEXTURE, default used if blank                                                           
			prules.BurstRate = rate;                                                      //PSYS_SRC_BURST_RATE                                                                                                           
			prules.BurstPartCount = 1;                                                   //PSYS_SRC_BURST_PART_COUNT                                                                                                      
			prules.BurstRadius = 0.5f;                                                    //PSYS_SRC_BURST_RADIUS                                                                                             
			prules.BurstSpeedMin = speed;                                                  //PSYS_SRC_BURST_SPEED_MIN                                                                                                      
            prules.BurstSpeedMax = speed;                                                  //PSYS_SRC_BURST_SPEED_MAX                                                                                                       
			prules.MaxAge = 0.0f;                                                         //PSYS_SRC_MAX_AGE                                                                                                              
			prules.Target = To.UUID;                                                 //PSYS_SRC_TARGET_KEY                                                                                                                     
			prules.AngularVelocity.X = 0.0f;                                              //PSYS_SRC_OMEGA                                                                                                                
			prules.AngularVelocity.Y = 0.0f;
			prules.AngularVelocity.Z = 0.0f;
			prules.InnerAngle = 0.0f;                                                     //PSYS_SRC_ANGLE_BEGIN                                                                                              
			prules.OuterAngle = 0.0f;                                                     //PSYS_SRC_ANGLE_END                                                                                                            
			
			prules.CRC = 1;  //activates the particle system??                                                                                                                                                
			From.AddNewParticleSystem(prules);
		}
	}
}
