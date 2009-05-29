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

using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.World.Serialiser.Tests
{
    [TestFixture]
    public class SerialiserTests
    {
        private string xml2 = @"
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
                <SitTargetAvatar><UUID>00000000-0000-0000-0000-000000000000</UUID></SitTargetAvatar>
            </SceneObjectPart>
            <OtherParts />
        </SceneObjectGroup>";

        protected SerialiserModule m_serialiserModule;

        [TestFixtureSetUp]
        public void Init()
        {
            m_serialiserModule = new SerialiserModule();
            SceneSetupHelpers.SetupSceneModules(SceneSetupHelpers.SetupScene(false), m_serialiserModule);            
        }

        [Test]
        public void TestLoadXml2()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            SceneObjectGroup so = m_serialiserModule.DeserializeGroupFromXml2(xml2);
            SceneObjectPart rootPart = so.RootPart;

            Assert.That(rootPart.UUID, Is.EqualTo(new UUID("9be68fdd-f740-4a0f-9675-dfbbb536b946")));
            Assert.That(rootPart.CreatorID, Is.EqualTo(new UUID("b46ef588-411e-4a8b-a284-d7dcfe8e74ef")));
            Assert.That(rootPart.Name, Is.EqualTo("PrimFun"));

            // TODO: Check other properties
        }

        //[Test]
        public void TestSaveXml2()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();            
        }
    }
}