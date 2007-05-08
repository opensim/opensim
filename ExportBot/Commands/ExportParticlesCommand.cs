using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using libsecondlife;

namespace libsecondlife.TestClient
{
    public class ExportParticlesCommand : Command
    {
        public ExportParticlesCommand(TestClient testClient)
        {
            Name = "exportparticles";
            Description = "Reverse engineers a prim with a particle system to an LSL script. Usage: exportscript [prim-uuid]";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: exportparticles [prim-uuid]";

            LLUUID id;
            if (!LLUUID.TryParse(args[0], out id))
                return "Usage: exportparticles [prim-uuid]";

            lock (Client.SimPrims)
            {
                if (Client.SimPrims.ContainsKey(Client.Network.CurrentSim))
                {
                    foreach (Primitive prim in Client.SimPrims[Client.Network.CurrentSim].Values)
                    {
                        if (prim.ID == id)
                        {
                            if (prim.ParticleSys.CRC != 0)
                            {
                                StringBuilder lsl = new StringBuilder();

                                lsl.Append("default" + Environment.NewLine);
                                lsl.Append("{" + Environment.NewLine);
                                lsl.Append("    state_entry()" + Environment.NewLine);
                                lsl.Append("    {" + Environment.NewLine);
                                lsl.Append("         llParticleSystem([" + Environment.NewLine);

                                lsl.Append("         PSYS_PART_FLAGS, 0");

                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.InterpColor) != 0)
                                    lsl.Append(" | PSYS_PART_INTERP_COLOR_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.InterpScale) != 0)
                                    lsl.Append(" | PSYS_PART_INTERP_SCALE_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Bounce) != 0)
                                    lsl.Append(" | PSYS_PART_BOUNCE_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Wind) != 0)
                                    lsl.Append(" | PSYS_PART_WIND_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.FollowSrc) != 0)
                                    lsl.Append(" | PSYS_PART_FOLLOW_SRC_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.FollowVelocity) != 0)
                                    lsl.Append(" | PSYS_PART_FOLLOW_VELOCITY_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.TargetPos) != 0)
                                    lsl.Append(" | PSYS_PART_TARGET_POS_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.TargetLinear) != 0)
                                    lsl.Append(" | PSYS_PART_TARGET_LINEAR_MASK");
                                if ((prim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Emissive) != 0)
                                    lsl.Append(" | PSYS_PART_EMISSIVE_MASK");

                                lsl.Append(","); lsl.Append(Environment.NewLine);
                                lsl.Append("         PSYS_SRC_PATTERN, 0");

                                if ((prim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.Drop) != 0)
                                    lsl.Append(" | PSYS_SRC_PATTERN_DROP");
                                if ((prim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.Explode) != 0)
                                    lsl.Append(" | PSYS_SRC_PATTERN_EXPLODE");
                                if ((prim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.Angle) != 0)
                                    lsl.Append(" | PSYS_SRC_PATTERN_ANGLE");
                                if ((prim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.AngleCone) != 0)
                                    lsl.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE");
                                if ((prim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.AngleConeEmpty) != 0)
                                    lsl.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE_EMPTY");

                                lsl.Append("," + Environment.NewLine);

                                lsl.Append("         PSYS_PART_START_ALPHA, " + String.Format("{0:0.00000}", prim.ParticleSys.PartStartColor.A) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_PART_END_ALPHA, " + String.Format("{0:0.00000}", prim.ParticleSys.PartEndColor.A) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_PART_START_COLOR, " + prim.ParticleSys.PartStartColor.ToStringRGB() + "," + Environment.NewLine);
                                lsl.Append("         PSYS_PART_END_COLOR, " + prim.ParticleSys.PartEndColor.ToStringRGB() + "," + Environment.NewLine);
                                lsl.Append("         PSYS_PART_START_SCALE, <" + String.Format("{0:0.00000}", prim.ParticleSys.PartStartScaleX) + ", " + String.Format("{0:0.00000}", prim.ParticleSys.PartStartScaleY) + ", 0>, " + Environment.NewLine);
                                lsl.Append("         PSYS_PART_END_SCALE, <" + String.Format("{0:0.00000}", prim.ParticleSys.PartEndScaleX) + ", " + String.Format("{0:0.00000}", prim.ParticleSys.PartEndScaleY) + ", 0>, " + Environment.NewLine);
                                lsl.Append("         PSYS_PART_MAX_AGE, " + String.Format("{0:0.00000}", prim.ParticleSys.PartMaxAge) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_MAX_AGE, " + String.Format("{0:0.00000}", prim.ParticleSys.MaxAge) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_ACCEL, " + prim.ParticleSys.PartAcceleration.ToString() + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_BURST_PART_COUNT, " + String.Format("{0:0}", prim.ParticleSys.BurstPartCount) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_BURST_RADIUS, " + String.Format("{0:0.00000}", prim.ParticleSys.BurstRadius) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_BURST_RATE, " + String.Format("{0:0.00000}", prim.ParticleSys.BurstRate) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_BURST_SPEED_MIN, " + String.Format("{0:0.00000}", prim.ParticleSys.BurstSpeedMin) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_BURST_SPEED_MAX, " + String.Format("{0:0.00000}", prim.ParticleSys.BurstSpeedMax) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_INNERANGLE, " + String.Format("{0:0.00000}", prim.ParticleSys.InnerAngle) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_OUTERANGLE, " + String.Format("{0:0.00000}", prim.ParticleSys.OuterAngle) + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_OMEGA, " + prim.ParticleSys.AngularVelocity.ToString() + "," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_TEXTURE, (key)\"" + prim.ParticleSys.Texture.ToStringHyphenated() + "\"," + Environment.NewLine);
                                lsl.Append("         PSYS_SRC_TARGET_KEY, (key)\"" + prim.ParticleSys.Target.ToStringHyphenated() + "\"" + Environment.NewLine);
                                
                                lsl.Append("         ]);" + Environment.NewLine);
                                lsl.Append("    }" + Environment.NewLine);
                                lsl.Append("}" + Environment.NewLine);

                                return lsl.ToString();
                            }
                            else
                            {
                                return "Prim " + prim.LocalID + " does not have a particle system";
                            }
                        }
                    }
                }
            }

            return "Couldn't find prim " + id.ToStringHyphenated();
        }
    }
}
