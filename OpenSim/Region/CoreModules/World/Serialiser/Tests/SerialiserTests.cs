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

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Tests.Common;
using OpenMetaverse.StructuredData;

namespace OpenSim.Region.CoreModules.World.Serialiser.Tests
{
    [TestFixture]
    public class SerialiserTests : OpenSimTestCase
    {
        private const string ObjectRootPartStubXml =
@"<SceneObjectGroup>
    <RootPart>
        <SceneObjectPart xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
            <AllowedDrop>false</AllowedDrop>
            <CreatorID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></CreatorID>
            <FolderID><Guid>e6a5a05e-e8cc-4816-8701-04165e335790</Guid></FolderID>
            <InventorySerial>1</InventorySerial>
            <TaskInventory />
            <ObjectFlags>0</ObjectFlags>
            <UUID><Guid>e6a5a05e-e8cc-4816-8701-04165e335790</Guid></UUID>
            <LocalId>2698615125</LocalId>
            <Name>PrimMyRide</Name>
            <Material>0</Material>
            <PassTouches>false</PassTouches>
            <RegionHandle>1099511628032000</RegionHandle>
            <ScriptAccessPin>0</ScriptAccessPin>
            <GroupPosition><X>147.23</X><Y>92.698</Y><Z>22.78084</Z></GroupPosition>
            <OffsetPosition><X>0</X><Y>0</Y><Z>0</Z></OffsetPosition>
            <RotationOffset><X>-4.371139E-08</X><Y>-1</Y><Z>-4.371139E-08</Z><W>0</W></RotationOffset>
            <Velocity><X>0</X><Y>0</Y><Z>0</Z></Velocity>
            <RotationalVelocity><X>0</X><Y>0</Y><Z>0</Z></RotationalVelocity>
            <AngularVelocity><X>0</X><Y>0</Y><Z>0</Z></AngularVelocity>
            <Acceleration><X>0</X><Y>0</Y><Z>0</Z></Acceleration>
            <Description />
            <Color />
            <Text />
            <SitName />
            <TouchName />
            <LinkNum>0</LinkNum>
            <ClickAction>0</ClickAction>
            <Shape>
                <ProfileCurve>1</ProfileCurve>
                <TextureEntry>AAAAAAAAERGZmQAAAAAABQCVlZUAAAAAQEAAAABAQAAAAAAAAAAAAAAAAAAAAA==</TextureEntry>
                <ExtraParams>AA==</ExtraParams>
                <PathBegin>0</PathBegin>
                <PathCurve>16</PathCurve>
                <PathEnd>0</PathEnd>
                <PathRadiusOffset>0</PathRadiusOffset>
                <PathRevolutions>0</PathRevolutions>
                <PathScaleX>100</PathScaleX>
                <PathScaleY>100</PathScaleY>
                <PathShearX>0</PathShearX>
                <PathShearY>0</PathShearY>
                <PathSkew>0</PathSkew>
                <PathTaperX>0</PathTaperX>
                <PathTaperY>0</PathTaperY>
                <PathTwist>0</PathTwist>
                <PathTwistBegin>0</PathTwistBegin>
                <PCode>9</PCode>
                <ProfileBegin>0</ProfileBegin>
                <ProfileEnd>0</ProfileEnd>
                <ProfileHollow>0</ProfileHollow>
                <Scale><X>10</X><Y>10</Y><Z>0.5</Z></Scale>
                <State>0</State>
                <ProfileShape>Square</ProfileShape>
                <HollowShape>Same</HollowShape>
                <SculptTexture><Guid>00000000-0000-0000-0000-000000000000</Guid></SculptTexture>
                <SculptType>0</SculptType><SculptData />
                <FlexiSoftness>0</FlexiSoftness>
                <FlexiTension>0</FlexiTension>
                <FlexiDrag>0</FlexiDrag>
                <FlexiGravity>0</FlexiGravity>
                <FlexiWind>0</FlexiWind>
                <FlexiForceX>0</FlexiForceX>
                <FlexiForceY>0</FlexiForceY>
                <FlexiForceZ>0</FlexiForceZ>
                <LightColorR>0</LightColorR>
                <LightColorG>0</LightColorG>
                <LightColorB>0</LightColorB>
                <LightColorA>1</LightColorA>
                <LightRadius>0</LightRadius>
                <LightCutoff>0</LightCutoff>
                <LightFalloff>0</LightFalloff>
                <LightIntensity>1</LightIntensity>
                <FlexiEntry>false</FlexiEntry>
                <LightEntry>false</LightEntry>
                <SculptEntry>false</SculptEntry>
            </Shape>
            <Scale><X>10</X><Y>10</Y><Z>0.5</Z></Scale>
            <UpdateFlag>0</UpdateFlag>
            <SitTargetOrientation><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientation>
            <SitTargetPosition><X>0</X><Y>0</Y><Z>0</Z></SitTargetPosition>
            <SitTargetPositionLL><X>0</X><Y>0</Y><Z>0</Z></SitTargetPositionLL>
            <SitTargetOrientationLL><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientationLL>
            <ParentID>0</ParentID>
            <CreationDate>1211330445</CreationDate>
            <Category>0</Category>
            <SalePrice>0</SalePrice>
            <ObjectSaleType>0</ObjectSaleType>
            <OwnershipCost>0</OwnershipCost>
            <GroupID><Guid>00000000-0000-0000-0000-000000000000</Guid></GroupID>
            <OwnerID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></OwnerID>
            <LastOwnerID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></LastOwnerID>
            <BaseMask>2147483647</BaseMask>
            <OwnerMask>2147483647</OwnerMask>
            <GroupMask>0</GroupMask>
            <EveryoneMask>0</EveryoneMask>
            <NextOwnerMask>2147483647</NextOwnerMask>
            <Flags>None</Flags>
            <CollisionSound><Guid>00000000-0000-0000-0000-000000000000</Guid></CollisionSound>
            <CollisionSoundVolume>0</CollisionSoundVolume>
            <DynAttrs>
                <llsd>
                    <map>
                        <key>MyNamespace</key>
                        <map>
                            <key>MyStore</key>
                            <map>
                                <key>the answer</key>
                                <integer>42</integer>
                            </map>
                        </map>
                    </map>
                </llsd>
            </DynAttrs>
        </SceneObjectPart>
    </RootPart>";

        private const string ObjectWithNoOtherPartsXml = ObjectRootPartStubXml +
@"
    <OtherParts />
</SceneObjectGroup>";

        private const string ObjectWithOtherPartsXml = ObjectRootPartStubXml +
@"
  <OtherParts>
    <Part>
      <SceneObjectPart xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
        <AllowedDrop>false</AllowedDrop>
        <CreatorID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></CreatorID>
        <FolderID><Guid>9958feb1-02a6-49e4-a4ce-eba6f578ee13</Guid></FolderID>
        <InventorySerial>3</InventorySerial>
        <UUID><Guid>9958feb1-02a6-49e4-a4ce-eba6f578ee13</Guid></UUID>
        <LocalId>1154704500</LocalId>
        <Name>Alien Head 1</Name>
        <Material>3</Material>
        <PassTouches>false</PassTouches>
        <PassCollisions>false</PassCollisions>
        <RegionHandle>21990232560640000</RegionHandle>
        <ScriptAccessPin>0</ScriptAccessPin>
        <GroupPosition><X>125.5655</X><Y>127.346</Y><Z>22.48036</Z></GroupPosition>
        <OffsetPosition><X>-0.2171936</X><Y>0.1083984</Y><Z>0.0009994507</Z></OffsetPosition>
        <RotationOffset><X>-0.5122106</X><Y>0.4851225</Y><Z>-0.4957454</Z><W>0.5064908</W></RotationOffset>
        <Velocity><X>0</X><Y>0</Y><Z>0</Z></Velocity>
        <AngularVelocity><X>0</X><Y>0</Y><Z>0</Z></AngularVelocity>
        <Acceleration><X>0</X><Y>0</Y><Z>0</Z></Acceleration>
        <Description>(No Description)</Description>
        <Color><R>0</R><G>0</G><B>0</B><A>255</A></Color>
        <Text/>
        <SitName/>
        <TouchName/>
        <LinkNum>253</LinkNum>
        <ClickAction>0</ClickAction>
        <Shape>
          <ProfileCurve>5</ProfileCurve>
          <TextureEntry>Vw3dpvgTRUOiIUOGsnpWlAB/f38AAAAAgL8AAACAPwAAAAAAAAAF4ABAAAAAAAAAAAAAAAAAAAAAAAAAAAAA</TextureEntry>
          <ExtraParams>AA==</ExtraParams>
          <PathBegin>0</PathBegin>
          <PathCurve>32</PathCurve>
          <PathEnd>0</PathEnd>
          <PathRadiusOffset>0</PathRadiusOffset>
          <PathRevolutions>0</PathRevolutions>
          <PathScaleX>100</PathScaleX>
          <PathScaleY>100</PathScaleY>
          <PathShearX>0</PathShearX>
          <PathShearY>0</PathShearY>
          <PathSkew>0</PathSkew>
          <PathTaperX>0</PathTaperX>
          <PathTaperY>0</PathTaperY>
          <PathTwist>0</PathTwist>
          <PathTwistBegin>0</PathTwistBegin>
          <PCode>9</PCode>
          <ProfileBegin>0</ProfileBegin>
          <ProfileEnd>0</ProfileEnd>
          <ProfileHollow>0</ProfileHollow>
          <State>9</State>
          <LastAttachPoint>0</LastAttachPoint>
          <ProfileShape>HalfCircle</ProfileShape>
          <HollowShape>Same</HollowShape>
          <SculptTexture><Guid>00000000-0000-0000-0000-000000000000</Guid></SculptTexture>
          <SculptType>0</SculptType>
          <FlexiSoftness>0</FlexiSoftness>
          <FlexiTension>0</FlexiTension>
          <FlexiDrag>0</FlexiDrag>
          <FlexiGravity>0</FlexiGravity>
          <FlexiWind>0</FlexiWind>
          <FlexiForceX>0</FlexiForceX>
          <FlexiForceY>0</FlexiForceY>
          <FlexiForceZ>0</FlexiForceZ>
          <LightColorR>0</LightColorR>
          <LightColorG>0</LightColorG>
          <LightColorB>0</LightColorB>
          <LightColorA>1</LightColorA>
          <LightRadius>0</LightRadius>
          <LightCutoff>0</LightCutoff>
          <LightFalloff>0</LightFalloff>
          <LightIntensity>1</LightIntensity>
          <FlexiEntry>false</FlexiEntry>
          <LightEntry>false</LightEntry>
          <SculptEntry>false</SculptEntry>
        </Shape>
        <Scale><X>0.1148195</X><Y>0.0143891</Y><Z>0.02768878</Z></Scale>
        <SitTargetOrientation><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientation>
        <SitTargetPosition><X>0</X><Y>0</Y><Z>0</Z></SitTargetPosition>
        <SitTargetPositionLL><X>0</X><Y>0</Y><Z>0</Z></SitTargetPositionLL>
        <SitTargetOrientationLL><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientationLL>
        <ParentID>1154704499</ParentID>
        <CreationDate>1256611042</CreationDate>
        <Category>0</Category>
        <SalePrice>10</SalePrice>
        <ObjectSaleType>0</ObjectSaleType>
        <OwnershipCost>0</OwnershipCost>
        <GroupID><Guid>00000000-0000-0000-0000-000000000000</Guid></GroupID>
        <OwnerID><Guid>7b2022f0-5f19-488c-b7e5-829d8f96b448</Guid></OwnerID>
        <LastOwnerID><Guid>7b2022f0-5f19-488c-b7e5-829d8f96b448</Guid></LastOwnerID>
        <BaseMask>647168</BaseMask>
        <OwnerMask>647168</OwnerMask>
        <GroupMask>0</GroupMask>
        <EveryoneMask>0</EveryoneMask>
        <NextOwnerMask>581632</NextOwnerMask>
        <Flags>None</Flags>
        <CollisionSound><Guid>00000000-0000-0000-0000-000000000000</Guid></CollisionSound>
        <CollisionSoundVolume>0</CollisionSoundVolume>
        <AttachedPos><X>0</X><Y>0</Y><Z>0</Z></AttachedPos>
        <TextureAnimation/>
        <ParticleSystem/>
        <PayPrice0>-2</PayPrice0>
        <PayPrice1>-2</PayPrice1>
        <PayPrice2>-2</PayPrice2>
        <PayPrice3>-2</PayPrice3>
        <PayPrice4>-2</PayPrice4>
      </SceneObjectPart>
    </Part>
    <Part>
      <SceneObjectPart xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
        <AllowedDrop>false</AllowedDrop>
        <CreatorID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></CreatorID>
        <FolderID><Guid>674b6b86-f5aa-439a-8e00-0d75bc08c80a</Guid></FolderID>
        <InventorySerial>3</InventorySerial>
        <UUID><Guid>674b6b86-f5aa-439a-8e00-0d75bc08c80a</Guid></UUID>
        <LocalId>1154704501</LocalId>
        <Name>Alien Head 2</Name>
        <Material>3</Material>
        <PassTouches>false</PassTouches>
        <PassCollisions>false</PassCollisions>
        <RegionHandle>21990232560640000</RegionHandle>
        <ScriptAccessPin>0</ScriptAccessPin>
        <GroupPosition><X>125.5655</X><Y>127.346</Y><Z>22.48036</Z></GroupPosition>
        <OffsetPosition><X>-0.2490997</X><Y>0.08520126</Y><Z>0.0009002686</Z></OffsetPosition>
        <RotationOffset><X>-0.4765368</X><Y>0.5194498</Y><Z>-0.5301372</Z><W>0.4712104</W></RotationOffset>
        <Velocity><X>0</X><Y>0</Y><Z>0</Z></Velocity>
        <AngularVelocity><X>0</X><Y>0</Y><Z>0</Z></AngularVelocity>
        <Acceleration><X>0</X><Y>0</Y><Z>0</Z></Acceleration>
        <Description>(No Description)</Description>
        <Color><R>0</R><G>0</G><B>0</B><A>255</A></Color>
        <Text/>
        <SitName/>
        <TouchName/>
        <LinkNum>252</LinkNum>
        <ClickAction>0</ClickAction>
        <Shape>
          <ProfileCurve>0</ProfileCurve>
          <TextureEntry>Vw3dpvgTRUOiIUOGsnpWlAB/f38AAAAAgL8AAACAPwAAAAAAAAAF4ABAAAAAAAAAAAAAAAAAAAAAAAAAAAAA</TextureEntry>
          <ExtraParams>AA==</ExtraParams>
          <PathBegin>0</PathBegin>
          <PathCurve>32</PathCurve>
          <PathEnd>0</PathEnd>
          <PathRadiusOffset>0</PathRadiusOffset>
          <PathRevolutions>0</PathRevolutions>
          <PathScaleX>100</PathScaleX>
          <PathScaleY>150</PathScaleY>
          <PathShearX>0</PathShearX>
          <PathShearY>0</PathShearY>
          <PathSkew>0</PathSkew>
          <PathTaperX>0</PathTaperX>
          <PathTaperY>0</PathTaperY>
          <PathTwist>0</PathTwist>
          <PathTwistBegin>0</PathTwistBegin>
          <PCode>9</PCode>
          <ProfileBegin>0</ProfileBegin>
          <ProfileEnd>0</ProfileEnd>
          <ProfileHollow>0</ProfileHollow>
          <State>9</State>
          <LastAttachPoint>0</LastAttachPoint>
          <ProfileShape>Circle</ProfileShape>
          <HollowShape>Same</HollowShape>
          <SculptTexture><Guid>00000000-0000-0000-0000-000000000000</Guid></SculptTexture>
          <SculptType>0</SculptType>
          <FlexiSoftness>0</FlexiSoftness>
          <FlexiTension>0</FlexiTension>
          <FlexiDrag>0</FlexiDrag>
          <FlexiGravity>0</FlexiGravity>
          <FlexiWind>0</FlexiWind>
          <FlexiForceX>0</FlexiForceX>
          <FlexiForceY>0</FlexiForceY>
          <FlexiForceZ>0</FlexiForceZ>
          <LightColorR>0</LightColorR>
          <LightColorG>0</LightColorG>
          <LightColorB>0</LightColorB>
          <LightColorA>1</LightColorA>
          <LightRadius>0</LightRadius>
          <LightCutoff>0</LightCutoff>
          <LightFalloff>0</LightFalloff>
          <LightIntensity>1</LightIntensity>
          <FlexiEntry>false</FlexiEntry>
          <LightEntry>false</LightEntry>
          <SculptEntry>false</SculptEntry>
        </Shape>
        <Scale><X>0.03574385</X><Y>0.05958032</Y><Z>0.04764182</Z></Scale>
        <SitTargetOrientation><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientation>
        <SitTargetPosition><X>0</X><Y>0</Y><Z>0</Z></SitTargetPosition>
        <SitTargetPositionLL><X>0</X><Y>0</Y><Z>0</Z></SitTargetPositionLL>
        <SitTargetOrientationLL><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientationLL>
        <ParentID>1154704499</ParentID>
        <CreationDate>1256611042</CreationDate>
        <Category>0</Category>
        <SalePrice>10</SalePrice>
        <ObjectSaleType>0</ObjectSaleType>
        <OwnershipCost>0</OwnershipCost>
        <GroupID><Guid>00000000-0000-0000-0000-000000000000</Guid></GroupID>
        <OwnerID><Guid>7b2022f0-5f19-488c-b7e5-829d8f96b448</Guid></OwnerID>
        <LastOwnerID><Guid>7b2022f0-5f19-488c-b7e5-829d8f96b448</Guid></LastOwnerID>
        <BaseMask>647168</BaseMask>
        <OwnerMask>647168</OwnerMask>
        <GroupMask>0</GroupMask>
        <EveryoneMask>0</EveryoneMask>
        <NextOwnerMask>581632</NextOwnerMask>
        <Flags>None</Flags>
        <CollisionSound><Guid>00000000-0000-0000-0000-000000000000</Guid></CollisionSound>
        <CollisionSoundVolume>0</CollisionSoundVolume>
        <AttachedPos><X>0</X><Y>0</Y><Z>0</Z></AttachedPos>
        <TextureAnimation/>
        <ParticleSystem/>
        <PayPrice0>-2</PayPrice0>
        <PayPrice1>-2</PayPrice1>
        <PayPrice2>-2</PayPrice2>
        <PayPrice3>-2</PayPrice3>
        <PayPrice4>-2</PayPrice4>
      </SceneObjectPart>
    </Part>
  </OtherParts>
</SceneObjectGroup>";

        private const string ObjectWithBadFloatsXml = @"
        <SceneObjectGroup>
            <RootPart>
                <SceneObjectPart xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
                    <AllowedDrop>false</AllowedDrop>
                    <CreatorID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></CreatorID>
                    <FolderID><Guid>e6a5a05e-e8cc-4816-8701-04165e335790</Guid></FolderID>
                    <InventorySerial>1</InventorySerial>
                    <TaskInventory />
                    <ObjectFlags>0</ObjectFlags>
                    <UUID><Guid>e6a5a05e-e8cc-4816-8701-04165e335790</Guid></UUID>
                    <LocalId>2698615125</LocalId>
                    <Name>NaughtyPrim</Name>
                    <Material>0</Material>
                    <PassTouches>false</PassTouches>
                    <RegionHandle>1099511628032000</RegionHandle>
                    <ScriptAccessPin>0</ScriptAccessPin>
                    <GroupPosition><X>147.23</X><Y>92.698</Y><Z>22.78084</Z></GroupPosition>
                    <OffsetPosition><X>0</X><Y>0</Y><Z>0</Z></OffsetPosition>
                    <RotationOffset><X>-4.371139E-08</X><Y>-1</Y><Z>-4.371139E-08</Z><W>0</W></RotationOffset>
                    <Velocity><X>0</X><Y>0</Y><Z>0</Z></Velocity>
                    <RotationalVelocity><X>0</X><Y>0</Y><Z>0</Z></RotationalVelocity>
                    <AngularVelocity><X>0</X><Y>0</Y><Z>0</Z></AngularVelocity>
                    <Acceleration><X>0</X><Y>0</Y><Z>0</Z></Acceleration>
                    <Description />
                    <Color />
                    <Text />
                    <SitName />
                    <TouchName />
                    <LinkNum>0</LinkNum>
                    <ClickAction>0</ClickAction>
                    <Shape>
                        <ProfileCurve>1</ProfileCurve>
                        <TextureEntry>AAAAAAAAERGZmQAAAAAABQCVlZUAAAAAQEAAAABAQAAAAAAAAAAAAAAAAAAAAA==</TextureEntry>
                        <ExtraParams>AA==</ExtraParams>
                        <PathBegin>0</PathBegin>
                        <PathCurve>16</PathCurve>
                        <PathEnd>0</PathEnd>
                        <PathRadiusOffset>0</PathRadiusOffset>
                        <PathRevolutions>0</PathRevolutions>
                        <PathScaleX>100</PathScaleX>
                        <PathScaleY>100</PathScaleY>
                        <PathShearX>0</PathShearX>
                        <PathShearY>0</PathShearY>
                        <PathSkew>0</PathSkew>
                        <PathTaperX>0</PathTaperX>
                        <PathTaperY>0</PathTaperY>
                        <PathTwist>0</PathTwist>
                        <PathTwistBegin>0</PathTwistBegin>
                        <PCode>9</PCode>
                        <ProfileBegin>0</ProfileBegin>
                        <ProfileEnd>0</ProfileEnd>
                        <ProfileHollow>0</ProfileHollow>
                        <Scale><X>10</X><Y>10</Y><Z>0.5</Z></Scale>
                        <State>0</State>
                        <ProfileShape>Square</ProfileShape>
                        <HollowShape>Same</HollowShape>
                        <SculptTexture><Guid>00000000-0000-0000-0000-000000000000</Guid></SculptTexture>
                        <SculptType>0</SculptType><SculptData />
                        <FlexiSoftness>0</FlexiSoftness>
                        <FlexiTension>0,5</FlexiTension>
                        <FlexiDrag>yo mamma</FlexiDrag>
                        <FlexiGravity>0</FlexiGravity>
                        <FlexiWind>0</FlexiWind>
                        <FlexiForceX>0</FlexiForceX>
                        <FlexiForceY>0</FlexiForceY>
                        <FlexiForceZ>0</FlexiForceZ>
                        <LightColorR>0</LightColorR>
                        <LightColorG>0</LightColorG>
                        <LightColorB>0</LightColorB>
                        <LightColorA>1</LightColorA>
                        <LightRadius>0</LightRadius>
                        <LightCutoff>0</LightCutoff>
                        <LightFalloff>0</LightFalloff>
                        <LightIntensity>1</LightIntensity>
                        <FlexiEntry>false</FlexiEntry>
                        <LightEntry>false</LightEntry>
                        <SculptEntry>false</SculptEntry>
                    </Shape>
                    <Scale><X>10</X><Y>10</Y><Z>0.5</Z></Scale>
                    <UpdateFlag>0</UpdateFlag>
                    <SitTargetOrientation><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientation>
                    <SitTargetPosition><X>0</X><Y>0</Y><Z>0</Z></SitTargetPosition>
                    <SitTargetPositionLL><X>0</X><Y>0</Y><Z>0</Z></SitTargetPositionLL>
                    <SitTargetOrientationLL><X>0</X><Y>0</Y><Z>0</Z><W>1</W></SitTargetOrientationLL>
                    <ParentID>0</ParentID>
                    <CreationDate>1211330445</CreationDate>
                    <Category>0</Category>
                    <SalePrice>0</SalePrice>
                    <ObjectSaleType>0</ObjectSaleType>
                    <OwnershipCost>0</OwnershipCost>
                    <GroupID><Guid>00000000-0000-0000-0000-000000000000</Guid></GroupID>
                    <OwnerID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></OwnerID>
                    <LastOwnerID><Guid>a6dacf01-4636-4bb9-8a97-30609438af9d</Guid></LastOwnerID>
                    <BaseMask>2147483647</BaseMask>
                    <OwnerMask>2147483647</OwnerMask>
                    <GroupMask>0</GroupMask>
                    <EveryoneMask>0</EveryoneMask>
                    <NextOwnerMask>2147483647</NextOwnerMask>
                    <Flags>None</Flags>
                    <CollisionSound><Guid>00000000-0000-0000-0000-000000000000</Guid></CollisionSound>
                    <CollisionSoundVolume>0</CollisionSoundVolume>
                </SceneObjectPart>
            </RootPart>
            <OtherParts />
        </SceneObjectGroup>";

        private const string ObjectWithNoPartsXml2 = @"
        <SceneObjectGroup>
            <SceneObjectPart xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
                <CreatorID><UUID>b46ef588-411e-4a8b-a284-d7dcfe8e74ef</UUID></CreatorID>
                <FolderID><UUID>9be68fdd-f740-4a0f-9675-dfbbb536b946</UUID></FolderID>
                <InventorySerial>0</InventorySerial>
                <TaskInventory />
                <ObjectFlags>0</ObjectFlags>
                <UUID><UUID>9be68fdd-f740-4a0f-9675-dfbbb536b946</UUID></UUID>
                <LocalId>720005</LocalId>
                <Name>PrimFun</Name>
                <Material>0</Material>
                <RegionHandle>1099511628032000</RegionHandle>
                <ScriptAccessPin>0</ScriptAccessPin>
                <GroupPosition><X>153.9854</X><Y>121.4908</Y><Z>62.21781</Z></GroupPosition>
                <OffsetPosition><X>0</X><Y>0</Y><Z>0</Z></OffsetPosition>
                <RotationOffset><X>0</X><Y>0</Y><Z>0</Z><W>1</W></RotationOffset>
                <Velocity><X>0</X><Y>0</Y><Z>0</Z></Velocity>
                <RotationalVelocity><X>0</X><Y>0</Y><Z>0</Z></RotationalVelocity>
                <AngularVelocity><X>0</X><Y>0</Y><Z>0</Z></AngularVelocity>
                <Acceleration><X>0</X><Y>0</Y><Z>0</Z></Acceleration>
                <Description />
                <Color />
                <Text />
                <SitName />
                <TouchName />
                <LinkNum>0</LinkNum>
                <ClickAction>0</ClickAction>
                <Shape>
                    <PathBegin>0</PathBegin>
                    <PathCurve>16</PathCurve>
                    <PathEnd>0</PathEnd>
                    <PathRadiusOffset>0</PathRadiusOffset>
                    <PathRevolutions>0</PathRevolutions>
                    <PathScaleX>200</PathScaleX>
                    <PathScaleY>200</PathScaleY>
                    <PathShearX>0</PathShearX>
                    <PathShearY>0</PathShearY>
                    <PathSkew>0</PathSkew>
                    <PathTaperX>0</PathTaperX>
                    <PathTaperY>0</PathTaperY>
                    <PathTwist>0</PathTwist>
                    <PathTwistBegin>0</PathTwistBegin>
                    <PCode>9</PCode>
                    <ProfileBegin>0</ProfileBegin>
                    <ProfileEnd>0</ProfileEnd>
                    <ProfileHollow>0</ProfileHollow>
                    <Scale><X>1.283131</X><Y>5.903858</Y><Z>4.266288</Z></Scale>
                    <State>0</State>
                    <ProfileShape>Circle</ProfileShape>
                    <HollowShape>Same</HollowShape>
                    <ProfileCurve>0</ProfileCurve>
                    <TextureEntry>iVVnRyTLQ+2SC0fK7RVGXwJ6yc/SU4RDA5nhJbLUw3R1AAAAAAAAaOw8QQOhPSRAAKE9JEAAAAAAAAAAAAAAAAAAAAA=</TextureEntry>
                    <ExtraParams>AA==</ExtraParams>
                </Shape>
                <Scale><X>1.283131</X><Y>5.903858</Y><Z>4.266288</Z></Scale>
                <UpdateFlag>0</UpdateFlag>
                <SitTargetOrientation><w>0</w><x>0</x><y>0</y><z>1</z></SitTargetOrientation>
                <SitTargetPosition><x>0</x><y>0</y><z>0</z></SitTargetPosition>
                <SitTargetPositionLL><X>0</X><Y>0</Y><Z>0</Z></SitTargetPositionLL>
                <SitTargetOrientationLL><X>0</X><Y>0</Y><Z>1</Z><W>0</W></SitTargetOrientationLL>
                <ParentID>0</ParentID>
                <CreationDate>1216066902</CreationDate>
                <Category>0</Category>
                <SalePrice>0</SalePrice>
                <ObjectSaleType>0</ObjectSaleType>
                <OwnershipCost>0</OwnershipCost>
                <GroupID><UUID>00000000-0000-0000-0000-000000000000</UUID></GroupID>
                <OwnerID><UUID>b46ef588-411e-4a8b-a284-d7dcfe8e74ef</UUID></OwnerID>
                <LastOwnerID><UUID>b46ef588-411e-4a8b-a284-d7dcfe8e74ef</UUID></LastOwnerID>
                <BaseMask>2147483647</BaseMask>
                <OwnerMask>2147483647</OwnerMask>
                <GroupMask>0</GroupMask>
                <EveryoneMask>0</EveryoneMask>
                <NextOwnerMask>2147483647</NextOwnerMask>
                <Flags>None</Flags>
                <DynAttrs>
                    <llsd>
                        <map>
                            <key>MyNamespace</key>
                            <map>
                                <key>MyStore</key>
                                <map>
                                    <key>last words</key>
                                    <string>Rosebud</string>
                                </map>
                            </map>
                        </map>
                    </llsd>
                </DynAttrs>
                <SitTargetAvatar><UUID>00000000-0000-0000-0000-000000000000</UUID></SitTargetAvatar>
            </SceneObjectPart>
            <OtherParts />
        </SceneObjectGroup>";

        protected Scene m_scene;
        protected SerialiserModule m_serialiserModule;

        [TestFixtureSetUp]
        public void Init()
        {
            m_serialiserModule = new SerialiserModule();
            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, m_serialiserModule);
        }

        [Test]
        public void TestDeserializeXmlObjectWithNoOtherParts()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            SceneObjectGroup so = SceneObjectSerializer.FromOriginalXmlFormat(ObjectWithNoOtherPartsXml);
            SceneObjectPart rootPart = so.RootPart;

            Assert.That(rootPart.UUID, Is.EqualTo(new UUID("e6a5a05e-e8cc-4816-8701-04165e335790")));
            Assert.That(rootPart.CreatorID, Is.EqualTo(new UUID("a6dacf01-4636-4bb9-8a97-30609438af9d")));
            Assert.That(rootPart.Name, Is.EqualTo("PrimMyRide"));
            OSDMap store = rootPart.DynAttrs.GetStore("MyNamespace", "MyStore");
            Assert.AreEqual(42, store["the answer"].AsInteger());

            // TODO: Check other properties
        }

        [Test]
        public void TestDeserializeXmlObjectWithOtherParts()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            SceneObjectGroup so = SceneObjectSerializer.FromOriginalXmlFormat(ObjectWithOtherPartsXml);
            SceneObjectPart[] parts = so.Parts;
            Assert.AreEqual(3, so.Parts.Length);

            {
                SceneObjectPart part = parts[0];

                Assert.That(part.UUID, Is.EqualTo(new UUID("e6a5a05e-e8cc-4816-8701-04165e335790")));
                Assert.That(part.CreatorID, Is.EqualTo(new UUID("a6dacf01-4636-4bb9-8a97-30609438af9d")));
                Assert.That(part.Name, Is.EqualTo("PrimMyRide"));
                OSDMap store = part.DynAttrs.GetStore("MyNamespace", "MyStore");
                Assert.AreEqual(42, store["the answer"].AsInteger());
            }

            {
                SceneObjectPart part = parts[1];

                Assert.That(part.UUID, Is.EqualTo(new UUID("9958feb1-02a6-49e4-a4ce-eba6f578ee13")));
                Assert.That(part.CreatorID, Is.EqualTo(new UUID("a6dacf01-4636-4bb9-8a97-30609438af9d")));
                Assert.That(part.Name, Is.EqualTo("Alien Head 1"));
            }

            {
                SceneObjectPart part = parts[2];

                Assert.That(part.UUID, Is.EqualTo(new UUID("674b6b86-f5aa-439a-8e00-0d75bc08c80a")));
                Assert.That(part.CreatorID, Is.EqualTo(new UUID("a6dacf01-4636-4bb9-8a97-30609438af9d")));
                Assert.That(part.Name, Is.EqualTo("Alien Head 2"));
            }

            // TODO: Check other properties
        }

        [Test]
        public void TestDeserializeBadFloatsXml()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SceneObjectGroup so = SceneObjectSerializer.FromOriginalXmlFormat(ObjectWithBadFloatsXml);
            SceneObjectPart rootPart = so.RootPart;

            Assert.That(rootPart.UUID, Is.EqualTo(new UUID("e6a5a05e-e8cc-4816-8701-04165e335790")));
            Assert.That(rootPart.CreatorID, Is.EqualTo(new UUID("a6dacf01-4636-4bb9-8a97-30609438af9d")));
            Assert.That(rootPart.Name, Is.EqualTo("NaughtyPrim"));

            // This terminates the deserialization earlier if couldn't be parsed.
            // TODO: Need to address this
            Assert.That(rootPart.GroupPosition.X, Is.EqualTo(147.23f));

            Assert.That(rootPart.Shape.PathCurve, Is.EqualTo(16));

            // Defaults for bad parses
            Assert.That(rootPart.Shape.FlexiTension, Is.EqualTo(0));
            Assert.That(rootPart.Shape.FlexiDrag, Is.EqualTo(0));

            // TODO: Check other properties
        }

        [Test]
        public void TestSerializeXml()
        {
            TestHelpers.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            string rpName = "My Little Donkey";
            UUID rpUuid = UUID.Parse("00000000-0000-0000-0000-000000000964");
            UUID rpCreatorId = UUID.Parse("00000000-0000-0000-0000-000000000915");
            PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
//            Vector3 groupPosition = new Vector3(10, 20, 30);
//            Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
//            Vector3 offsetPosition = new Vector3(5, 10, 15);

            SceneObjectPart rp = new SceneObjectPart();
            rp.UUID = rpUuid;
            rp.Name = rpName;
            rp.CreatorID = rpCreatorId;
            rp.Shape = shape;

            string daNamespace = "MyNamespace";
            string daStoreName = "MyStore";
            string daKey = "foo";
            string daValue = "bar";
            OSDMap myStore = new OSDMap();
            myStore.Add(daKey, daValue);
            rp.DynAttrs = new DAMap();
            rp.DynAttrs.SetStore(daNamespace, daStoreName, myStore);

            SceneObjectGroup so = new SceneObjectGroup(rp);

            // Need to add the object to the scene so that the request to get script state succeeds
            m_scene.AddSceneObject(so);

            string xml = SceneObjectSerializer.ToOriginalXmlFormat(so);

            XmlTextReader xtr = new XmlTextReader(new StringReader(xml));
            xtr.ReadStartElement("SceneObjectGroup");
            xtr.ReadStartElement("RootPart");
            xtr.ReadStartElement("SceneObjectPart");

            UUID uuid = UUID.Zero;
            string name = null;
            UUID creatorId = UUID.Zero;
            DAMap daMap = null;

            while (xtr.Read() && xtr.Name != "SceneObjectPart")
            {
                if (xtr.NodeType != XmlNodeType.Element)
                    continue;

                switch (xtr.Name)
                {
                    case "UUID":
                        xtr.ReadStartElement("UUID");
                        try
                        {
                            uuid = UUID.Parse(xtr.ReadElementString("UUID"));
                            xtr.ReadEndElement();
                        }
                        catch { } // ignore everything but <UUID><UUID>...</UUID></UUID>
                        break;
                    case "Name":
                        name = xtr.ReadElementContentAsString();
                        break;
                    case "CreatorID":
                        xtr.ReadStartElement("CreatorID");
                        creatorId = UUID.Parse(xtr.ReadElementString("UUID"));
                        xtr.ReadEndElement();
                        break;
                    case "DynAttrs":
                        daMap = new DAMap();
                        daMap.ReadXml(xtr);
                        break;
                }
            }

            xtr.ReadEndElement();
            xtr.ReadEndElement();
            xtr.ReadStartElement("OtherParts");
            xtr.ReadEndElement();
            xtr.Close();

            // TODO: More checks
            Assert.That(uuid, Is.EqualTo(rpUuid));
            Assert.That(name, Is.EqualTo(rpName));
            Assert.That(creatorId, Is.EqualTo(rpCreatorId));
            Assert.NotNull(daMap);
            Assert.AreEqual(daValue, daMap.GetStore(daNamespace, daStoreName)[daKey].AsString());
        }

        [Test]
        public void TestDeserializeXml2()
        {
            TestHelpers.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            SceneObjectGroup so = m_serialiserModule.DeserializeGroupFromXml2(ObjectWithNoPartsXml2);
            SceneObjectPart rootPart = so.RootPart;

            Assert.That(rootPart.UUID, Is.EqualTo(new UUID("9be68fdd-f740-4a0f-9675-dfbbb536b946")));
            Assert.That(rootPart.CreatorID, Is.EqualTo(new UUID("b46ef588-411e-4a8b-a284-d7dcfe8e74ef")));
            Assert.That(rootPart.Name, Is.EqualTo("PrimFun"));
            OSDMap store = rootPart.DynAttrs.GetStore("MyNamespace", "MyStore");
            Assert.AreEqual("Rosebud", store["last words"].AsString());

            // TODO: Check other properties
        }

        [Test]
        public void TestSerializeXml2()
        {
            TestHelpers.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            string rpName = "My Little Pony";
            UUID rpUuid = UUID.Parse("00000000-0000-0000-0000-000000000064");
            UUID rpCreatorId = UUID.Parse("00000000-0000-0000-0000-000000000015");
            PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
//            Vector3 groupPosition = new Vector3(10, 20, 30);
//            Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
//            Vector3 offsetPosition = new Vector3(5, 10, 15);

            SceneObjectPart rp = new SceneObjectPart();
            rp.UUID = rpUuid;
            rp.Name = rpName;
            rp.CreatorID = rpCreatorId;
            rp.Shape = shape;

            string daNamespace = "MyNamespace";
            string daStoreName = "MyStore";
            string daKey = "foo";
            string daValue = "bar";
            OSDMap myStore = new OSDMap();
            myStore.Add(daKey, daValue);
            rp.DynAttrs = new DAMap();
            rp.DynAttrs.SetStore(daNamespace, daStoreName, myStore);

            SceneObjectGroup so = new SceneObjectGroup(rp);

            // Need to add the object to the scene so that the request to get script state succeeds
            m_scene.AddSceneObject(so);

            Dictionary<string, object> options = new Dictionary<string, object>();
            options["old-guids"] = true;
            string xml2 = m_serialiserModule.SerializeGroupToXml2(so, options);

            XmlTextReader xtr = new XmlTextReader(new StringReader(xml2));
            xtr.ReadStartElement("SceneObjectGroup");
            xtr.ReadStartElement("SceneObjectPart");

            UUID uuid = UUID.Zero;
            string name = null;
            UUID creatorId = UUID.Zero;
            DAMap daMap = null;

            while (xtr.Read() && xtr.Name != "SceneObjectPart")
            {
                if (xtr.NodeType != XmlNodeType.Element)
                    continue;

                switch (xtr.Name)
                {
                    case "UUID":
                        xtr.ReadStartElement("UUID");
                        uuid = UUID.Parse(xtr.ReadElementString("Guid"));
                        xtr.ReadEndElement();
                        break;
                    case "Name":
                        name = xtr.ReadElementContentAsString();
                        break;
                    case "CreatorID":
                        xtr.ReadStartElement("CreatorID");
                        creatorId = UUID.Parse(xtr.ReadElementString("Guid"));
                        xtr.ReadEndElement();
                        break;
                    case "DynAttrs":
                        daMap = new DAMap();
                        daMap.ReadXml(xtr);
                        break;
                }
            }

            xtr.ReadEndElement();
            xtr.ReadStartElement("OtherParts");
            xtr.ReadEndElement();
            xtr.Close();

            // TODO: More checks
            Assert.That(uuid, Is.EqualTo(rpUuid));
            Assert.That(name, Is.EqualTo(rpName));
            Assert.That(creatorId, Is.EqualTo(rpCreatorId));
            Assert.NotNull(daMap);
            Assert.AreEqual(daValue, daMap.GetStore(daNamespace, daStoreName)[daKey].AsString());
        }
    }
}