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

using pbd = global::Google.ProtocolBuffers.Descriptors;
using pb = global::Google.ProtocolBuffers;
namespace Sirikata.PB {
    public class ExternalMessage : PBJ.IMessage {
        protected _PBJ_Internal.ExternalMessage super;
        public _PBJ_Internal.ExternalMessage _PBJSuper{ get { return super;} }
        public ExternalMessage() {
            super=new _PBJ_Internal.ExternalMessage();
        }
        public ExternalMessage(_PBJ_Internal.ExternalMessage reference) {
            super=reference;
        }
        public static ExternalMessage defaultInstance= new ExternalMessage (_PBJ_Internal.ExternalMessage.DefaultInstance);
        public static ExternalMessage DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ExternalMessage.Descriptor; }        }
        public static class Types {
        public class SubMessage : PBJ.IMessage {
            protected _PBJ_Internal.ExternalMessage.Types.SubMessage super;
            public _PBJ_Internal.ExternalMessage.Types.SubMessage _PBJSuper{ get { return super;} }
            public SubMessage() {
                super=new _PBJ_Internal.ExternalMessage.Types.SubMessage();
            }
            public SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage reference) {
                super=reference;
            }
            public static SubMessage defaultInstance= new SubMessage (_PBJ_Internal.ExternalMessage.Types.SubMessage.DefaultInstance);
            public static SubMessage DefaultInstance{
                get {return defaultInstance;}
            }
            public static pbd.MessageDescriptor Descriptor {
                get { return _PBJ_Internal.ExternalMessage.Types.SubMessage.Descriptor; }            }
            public static class Types {
            }
            public static bool WithinReservedFieldTagRange(int field_tag) {
                return false;
            }
            public static bool WithinExtensionFieldTagRange(int field_tag) {
                return false;
            }
            public const int SubuuidFieldTag=1;
            public bool HasSubuuid{ get {return super.HasSubuuid&&PBJ._PBJ.ValidateUuid(super.Subuuid);} }
            public PBJ.UUID Subuuid{ get {
                if (HasSubuuid) {
                    return PBJ._PBJ.CastUuid(super.Subuuid);
                } else {
                    return PBJ._PBJ.CastUuid();
                }
            }
            }
            public const int SubvectorFieldTag=2;
            public bool HasSubvector{ get {return super.SubvectorCount>=3;} }
            public PBJ.Vector3d Subvector{ get  {
                int index=0;
                if (HasSubvector) {
                    return PBJ._PBJ.CastVector3d(super.GetSubvector(index*3+0),super.GetSubvector(index*3+1),super.GetSubvector(index*3+2));
                } else {
                    return PBJ._PBJ.CastVector3d();
                }
            }
            }
            public const int SubdurationFieldTag=3;
            public bool HasSubduration{ get {return super.HasSubduration&&PBJ._PBJ.ValidateDuration(super.Subduration);} }
            public PBJ.Duration Subduration{ get {
                if (HasSubduration) {
                    return PBJ._PBJ.CastDuration(super.Subduration);
                } else {
                    return PBJ._PBJ.CastDuration();
                }
            }
            }
            public const int SubnormalFieldTag=4;
            public bool HasSubnormal{ get {return super.SubnormalCount>=2;} }
            public PBJ.Vector3f Subnormal{ get  {
                int index=0;
                if (HasSubnormal) {
                    return PBJ._PBJ.CastNormal(super.GetSubnormal(index*2+0),super.GetSubnormal(index*2+1));
                } else {
                    return PBJ._PBJ.CastNormal();
                }
            }
            }
                public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
            public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
            public static Builder CreateBuilder() { return new Builder(); }
            public static Builder CreateBuilder(SubMessage prototype) {
                return (Builder)new Builder().MergeFrom(prototype);
            }
            public static SubMessage ParseFrom(pb::ByteString data) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data,er));
            }
            public static SubMessage ParseFrom(byte[] data) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(byte[] data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data,er));
            }
            public static SubMessage ParseFrom(global::System.IO.Stream data) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data,er));
            }
            public static SubMessage ParseFrom(pb::CodedInputStream data) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.ExternalMessage.Types.SubMessage.ParseFrom(data,er));
            }
            protected override bool _HasAllPBJFields{ get {
                return true
                    ;
            } }
            public bool IsInitialized { get {
                return super.IsInitialized&&_HasAllPBJFields;
            } }
            public class Builder : global::PBJ.IMessage.IBuilder{
            protected override bool _HasAllPBJFields{ get {
                return true
                    ;
            } }
            public bool IsInitialized { get {
                return super.IsInitialized&&_HasAllPBJFields;
            } }
                protected _PBJ_Internal.ExternalMessage.Types.SubMessage.Builder super;
                public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
                public _PBJ_Internal.ExternalMessage.Types.SubMessage.Builder _PBJSuper{ get { return super;} }
                public Builder() {super = new _PBJ_Internal.ExternalMessage.Types.SubMessage.Builder();}
                public Builder(_PBJ_Internal.ExternalMessage.Types.SubMessage.Builder other) {
                    super=other;
                }
                public Builder Clone() {return new Builder(super.Clone());}
                public Builder MergeFrom(SubMessage prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
                public Builder Clear() {super.Clear();return this;}
                public SubMessage BuildPartial() {return new SubMessage(super.BuildPartial());}
                public SubMessage Build() {if (_HasAllPBJFields) return new SubMessage(super.Build());return null;}
                public pbd::MessageDescriptor DescriptorForType {
                    get { return SubMessage.Descriptor; }                }
            public Builder ClearSubuuid() { super.ClearSubuuid();return this;}
            public const int SubuuidFieldTag=1;
            public bool HasSubuuid{ get {return super.HasSubuuid&&PBJ._PBJ.ValidateUuid(super.Subuuid);} }
            public PBJ.UUID Subuuid{ get {
                if (HasSubuuid) {
                    return PBJ._PBJ.CastUuid(super.Subuuid);
                } else {
                    return PBJ._PBJ.CastUuid();
                }
            }
            set {
                super.Subuuid=(PBJ._PBJ.Construct(value));
            }
            }
            public Builder ClearSubvector() { super.ClearSubvector();return this;}
            public const int SubvectorFieldTag=2;
            public bool HasSubvector{ get {return super.SubvectorCount>=3;} }
            public PBJ.Vector3d Subvector{ get  {
                int index=0;
                if (HasSubvector) {
                    return PBJ._PBJ.CastVector3d(super.GetSubvector(index*3+0),super.GetSubvector(index*3+1),super.GetSubvector(index*3+2));
                } else {
                    return PBJ._PBJ.CastVector3d();
                }
            }
            set {
                super.ClearSubvector();
                double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
                super.AddSubvector(_PBJtempArray[0]);
                super.AddSubvector(_PBJtempArray[1]);
                super.AddSubvector(_PBJtempArray[2]);
            }
            }
            public Builder ClearSubduration() { super.ClearSubduration();return this;}
            public const int SubdurationFieldTag=3;
            public bool HasSubduration{ get {return super.HasSubduration&&PBJ._PBJ.ValidateDuration(super.Subduration);} }
            public PBJ.Duration Subduration{ get {
                if (HasSubduration) {
                    return PBJ._PBJ.CastDuration(super.Subduration);
                } else {
                    return PBJ._PBJ.CastDuration();
                }
            }
            set {
                super.Subduration=(PBJ._PBJ.Construct(value));
            }
            }
            public Builder ClearSubnormal() { super.ClearSubnormal();return this;}
            public const int SubnormalFieldTag=4;
            public bool HasSubnormal{ get {return super.SubnormalCount>=2;} }
            public PBJ.Vector3f Subnormal{ get  {
                int index=0;
                if (HasSubnormal) {
                    return PBJ._PBJ.CastNormal(super.GetSubnormal(index*2+0),super.GetSubnormal(index*2+1));
                } else {
                    return PBJ._PBJ.CastNormal();
                }
            }
            set {
                super.ClearSubnormal();
                float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
                super.AddSubnormal(_PBJtempArray[0]);
                super.AddSubnormal(_PBJtempArray[1]);
            }
            }
            }
        }
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int IsTrueFieldTag=40;
        public bool HasIsTrue{ get {return super.HasIsTrue&&PBJ._PBJ.ValidateBool(super.IsTrue);} }
        public bool IsTrue{ get {
            if (HasIsTrue) {
                return PBJ._PBJ.CastBool(super.IsTrue);
            } else {
                return true;
            }
        }
        }
        public const int V2FFieldTag=2;
        public bool HasV2F{ get {return super.V2FCount>=2;} }
        public PBJ.Vector2f V2F{ get  {
            int index=0;
            if (HasV2F) {
                return PBJ._PBJ.CastVector2f(super.GetV2F(index*2+0),super.GetV2F(index*2+1));
            } else {
                return PBJ._PBJ.CastVector2f();
            }
        }
        }
        public const int SubMesFieldTag=30;
        public bool HasSubMes{ get {return super.HasSubMes;} }
        public Types.SubMessage SubMes{ get {
            if (HasSubMes) {
                return new Types.SubMessage(super.SubMes);
            } else {
                return new Types.SubMessage();
            }
        }
        }
        public const int SubmessersFieldTag=31;
        public int SubmessersCount { get { return super.SubmessersCount;} }
        public bool HasSubmessers(int index) {return true;}
        public Types.SubMessage Submessers(int index) {
            return new Types.SubMessage(super.GetSubmessers(index));
        }
        public const int ShaFieldTag=32;
        public bool HasSha{ get {return super.HasSha&&PBJ._PBJ.ValidateSha256(super.Sha);} }
        public PBJ.SHA256 Sha{ get {
            if (HasSha) {
                return PBJ._PBJ.CastSha256(super.Sha);
            } else {
                return PBJ._PBJ.CastSha256();
            }
        }
        }
        public const int ShasFieldTag=33;
        public int ShasCount { get { return super.ShasCount;} }
        public bool HasShas(int index) {return PBJ._PBJ.ValidateSha256(super.GetShas(index));}
        public PBJ.SHA256 Shas(int index) {
            return (PBJ.SHA256)PBJ._PBJ.CastSha256(super.GetShas(index));
        }
        public const int V3FFieldTag=4;
        public bool HasV3F{ get {return super.V3FCount>=3;} }
        public PBJ.Vector3f V3F{ get  {
            int index=0;
            if (HasV3F) {
                return PBJ._PBJ.CastVector3f(super.GetV3F(index*3+0),super.GetV3F(index*3+1),super.GetV3F(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int V3FfFieldTag=5;
        public int V3FfCount { get { return super.V3FfCount/3;} }
        public bool HasV3Ff(int index) { return true; }
        public PBJ.Vector3f GetV3Ff(int index) {
            if (HasV3Ff(index)) {
                return PBJ._PBJ.CastVector3f(super.GetV3Ff(index*3+0),super.GetV3Ff(index*3+1),super.GetV3Ff(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ExternalMessage prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ExternalMessage ParseFrom(pb::ByteString data) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data));
        }
        public static ExternalMessage ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data,er));
        }
        public static ExternalMessage ParseFrom(byte[] data) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data));
        }
        public static ExternalMessage ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data,er));
        }
        public static ExternalMessage ParseFrom(global::System.IO.Stream data) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data));
        }
        public static ExternalMessage ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data,er));
        }
        public static ExternalMessage ParseFrom(pb::CodedInputStream data) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data));
        }
        public static ExternalMessage ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ExternalMessage(_PBJ_Internal.ExternalMessage.ParseFrom(data,er));
        }
        protected override bool _HasAllPBJFields{ get {
            return true
                &&HasV3F
                ;
        } }
        public bool IsInitialized { get {
            return super.IsInitialized&&_HasAllPBJFields;
        } }
        public class Builder : global::PBJ.IMessage.IBuilder{
        protected override bool _HasAllPBJFields{ get {
            return true
                &&HasV3F
                ;
        } }
        public bool IsInitialized { get {
            return super.IsInitialized&&_HasAllPBJFields;
        } }
            protected _PBJ_Internal.ExternalMessage.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ExternalMessage.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ExternalMessage.Builder();}
            public Builder(_PBJ_Internal.ExternalMessage.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ExternalMessage prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ExternalMessage BuildPartial() {return new ExternalMessage(super.BuildPartial());}
            public ExternalMessage Build() {if (_HasAllPBJFields) return new ExternalMessage(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ExternalMessage.Descriptor; }            }
        public Builder ClearIsTrue() { super.ClearIsTrue();return this;}
        public const int IsTrueFieldTag=40;
        public bool HasIsTrue{ get {return super.HasIsTrue&&PBJ._PBJ.ValidateBool(super.IsTrue);} }
        public bool IsTrue{ get {
            if (HasIsTrue) {
                return PBJ._PBJ.CastBool(super.IsTrue);
            } else {
                return true;
            }
        }
        set {
            super.IsTrue=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearV2F() { super.ClearV2F();return this;}
        public const int V2FFieldTag=2;
        public bool HasV2F{ get {return super.V2FCount>=2;} }
        public PBJ.Vector2f V2F{ get  {
            int index=0;
            if (HasV2F) {
                return PBJ._PBJ.CastVector2f(super.GetV2F(index*2+0),super.GetV2F(index*2+1));
            } else {
                return PBJ._PBJ.CastVector2f();
            }
        }
        set {
            super.ClearV2F();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector2f(value);
            super.AddV2F(_PBJtempArray[0]);
            super.AddV2F(_PBJtempArray[1]);
        }
        }
        public Builder ClearSubMes() { super.ClearSubMes();return this;}
        public const int SubMesFieldTag=30;
        public bool HasSubMes{ get {return super.HasSubMes;} }
        public Types.SubMessage SubMes{ get {
            if (HasSubMes) {
                return new Types.SubMessage(super.SubMes);
            } else {
                return new Types.SubMessage();
            }
        }
        set {
            super.SubMes=value._PBJSuper;
        }
        }
        public Builder ClearSubmessers() { super.ClearSubmessers();return this;}
        public Builder SetSubmessers(int index,Types.SubMessage value) {
            super.SetSubmessers(index,value._PBJSuper);
            return this;
        }
        public const int SubmessersFieldTag=31;
        public int SubmessersCount { get { return super.SubmessersCount;} }
        public bool HasSubmessers(int index) {return true;}
        public Types.SubMessage Submessers(int index) {
            return new Types.SubMessage(super.GetSubmessers(index));
        }
        public Builder AddSubmessers(Types.SubMessage value) {
            super.AddSubmessers(value._PBJSuper);
            return this;
        }
        public Builder ClearSha() { super.ClearSha();return this;}
        public const int ShaFieldTag=32;
        public bool HasSha{ get {return super.HasSha&&PBJ._PBJ.ValidateSha256(super.Sha);} }
        public PBJ.SHA256 Sha{ get {
            if (HasSha) {
                return PBJ._PBJ.CastSha256(super.Sha);
            } else {
                return PBJ._PBJ.CastSha256();
            }
        }
        set {
            super.Sha=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearShas() { super.ClearShas();return this;}
        public Builder SetShas(int index, PBJ.SHA256 value) {
            super.SetShas(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int ShasFieldTag=33;
        public int ShasCount { get { return super.ShasCount;} }
        public bool HasShas(int index) {return PBJ._PBJ.ValidateSha256(super.GetShas(index));}
        public PBJ.SHA256 Shas(int index) {
            return (PBJ.SHA256)PBJ._PBJ.CastSha256(super.GetShas(index));
        }
        public Builder AddShas(PBJ.SHA256 value) {
            super.AddShas(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearV3F() { super.ClearV3F();return this;}
        public const int V3FFieldTag=4;
        public bool HasV3F{ get {return super.V3FCount>=3;} }
        public PBJ.Vector3f V3F{ get  {
            int index=0;
            if (HasV3F) {
                return PBJ._PBJ.CastVector3f(super.GetV3F(index*3+0),super.GetV3F(index*3+1),super.GetV3F(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearV3F();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddV3F(_PBJtempArray[0]);
            super.AddV3F(_PBJtempArray[1]);
            super.AddV3F(_PBJtempArray[2]);
        }
        }
        public Builder ClearV3Ff() { super.ClearV3Ff();return this;}
        public const int V3FfFieldTag=5;
        public int V3FfCount { get { return super.V3FfCount/3;} }
        public bool HasV3Ff(int index) { return true; }
        public PBJ.Vector3f GetV3Ff(int index) {
            if (HasV3Ff(index)) {
                return PBJ._PBJ.CastVector3f(super.GetV3Ff(index*3+0),super.GetV3Ff(index*3+1),super.GetV3Ff(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        public Builder AddV3Ff(PBJ.Vector3f value) {
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddV3Ff(_PBJtempArray[0]);
            super.AddV3Ff(_PBJtempArray[1]);
            super.AddV3Ff(_PBJtempArray[2]);
            return this;
        }
        public Builder SetV3Ff(int index,PBJ.Vector3f value) {
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.SetV3Ff(index*3+0,_PBJtempArray[0]);
            super.SetV3Ff(index*3+1,_PBJtempArray[1]);
            super.SetV3Ff(index*3+2,_PBJtempArray[2]);
            return this;
        }
        }
    }
}
namespace Sirikata.PB {
    public class TestMessage : PBJ.IMessage {
        protected _PBJ_Internal.TestMessage super;
        public _PBJ_Internal.TestMessage _PBJSuper{ get { return super;} }
        public TestMessage() {
            super=new _PBJ_Internal.TestMessage();
        }
        public TestMessage(_PBJ_Internal.TestMessage reference) {
            super=reference;
        }
        public static TestMessage defaultInstance= new TestMessage (_PBJ_Internal.TestMessage.DefaultInstance);
        public static TestMessage DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.TestMessage.Descriptor; }        }
        public static class Types {
        public enum Flagsf32 {
            UNIVERSA=_PBJ_Internal.TestMessage.Types.Flagsf32.UNIVERSA,
            WE=_PBJ_Internal.TestMessage.Types.Flagsf32.WE,
            IMAGE=_PBJ_Internal.TestMessage.Types.Flagsf32.IMAGE,
            LOCA=_PBJ_Internal.TestMessage.Types.Flagsf32.LOCA
        };
        public enum Flagsf64 {
            UNIVERSAL=_PBJ_Internal.TestMessage.Types.Flagsf64.UNIVERSAL,
            WEB=_PBJ_Internal.TestMessage.Types.Flagsf64.WEB,
            IMAGES=_PBJ_Internal.TestMessage.Types.Flagsf64.IMAGES,
            LOCAL=_PBJ_Internal.TestMessage.Types.Flagsf64.LOCAL
        };
        public enum Enum32 {
            UNIVERSAL1=_PBJ_Internal.TestMessage.Types.Enum32.UNIVERSAL1,
            WEB1=_PBJ_Internal.TestMessage.Types.Enum32.WEB1,
            IMAGES1=_PBJ_Internal.TestMessage.Types.Enum32.IMAGES1,
            LOCAL1=_PBJ_Internal.TestMessage.Types.Enum32.LOCAL1
        };
        public class SubMessage : PBJ.IMessage {
            protected _PBJ_Internal.TestMessage.Types.SubMessage super;
            public _PBJ_Internal.TestMessage.Types.SubMessage _PBJSuper{ get { return super;} }
            public SubMessage() {
                super=new _PBJ_Internal.TestMessage.Types.SubMessage();
            }
            public SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage reference) {
                super=reference;
            }
            public static SubMessage defaultInstance= new SubMessage (_PBJ_Internal.TestMessage.Types.SubMessage.DefaultInstance);
            public static SubMessage DefaultInstance{
                get {return defaultInstance;}
            }
            public static pbd.MessageDescriptor Descriptor {
                get { return _PBJ_Internal.TestMessage.Types.SubMessage.Descriptor; }            }
            public static class Types {
            }
            public static bool WithinReservedFieldTagRange(int field_tag) {
                return false;
            }
            public static bool WithinExtensionFieldTagRange(int field_tag) {
                return false;
            }
            public const int SubuuidFieldTag=1;
            public bool HasSubuuid{ get {return super.HasSubuuid&&PBJ._PBJ.ValidateUuid(super.Subuuid);} }
            public PBJ.UUID Subuuid{ get {
                if (HasSubuuid) {
                    return PBJ._PBJ.CastUuid(super.Subuuid);
                } else {
                    return PBJ._PBJ.CastUuid();
                }
            }
            }
            public const int SubvectorFieldTag=2;
            public bool HasSubvector{ get {return super.SubvectorCount>=3;} }
            public PBJ.Vector3d Subvector{ get  {
                int index=0;
                if (HasSubvector) {
                    return PBJ._PBJ.CastVector3d(super.GetSubvector(index*3+0),super.GetSubvector(index*3+1),super.GetSubvector(index*3+2));
                } else {
                    return PBJ._PBJ.CastVector3d();
                }
            }
            }
            public const int SubdurationFieldTag=3;
            public bool HasSubduration{ get {return super.HasSubduration&&PBJ._PBJ.ValidateDuration(super.Subduration);} }
            public PBJ.Duration Subduration{ get {
                if (HasSubduration) {
                    return PBJ._PBJ.CastDuration(super.Subduration);
                } else {
                    return PBJ._PBJ.CastDuration();
                }
            }
            }
            public const int SubnormalFieldTag=4;
            public bool HasSubnormal{ get {return super.SubnormalCount>=2;} }
            public PBJ.Vector3f Subnormal{ get  {
                int index=0;
                if (HasSubnormal) {
                    return PBJ._PBJ.CastNormal(super.GetSubnormal(index*2+0),super.GetSubnormal(index*2+1));
                } else {
                    return PBJ._PBJ.CastNormal();
                }
            }
            }
                public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
            public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
            public static Builder CreateBuilder() { return new Builder(); }
            public static Builder CreateBuilder(SubMessage prototype) {
                return (Builder)new Builder().MergeFrom(prototype);
            }
            public static SubMessage ParseFrom(pb::ByteString data) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data,er));
            }
            public static SubMessage ParseFrom(byte[] data) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(byte[] data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data,er));
            }
            public static SubMessage ParseFrom(global::System.IO.Stream data) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data,er));
            }
            public static SubMessage ParseFrom(pb::CodedInputStream data) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data));
            }
            public static SubMessage ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
                return new SubMessage(_PBJ_Internal.TestMessage.Types.SubMessage.ParseFrom(data,er));
            }
            protected override bool _HasAllPBJFields{ get {
                return true
                    ;
            } }
            public bool IsInitialized { get {
                return super.IsInitialized&&_HasAllPBJFields;
            } }
            public class Builder : global::PBJ.IMessage.IBuilder{
            protected override bool _HasAllPBJFields{ get {
                return true
                    ;
            } }
            public bool IsInitialized { get {
                return super.IsInitialized&&_HasAllPBJFields;
            } }
                protected _PBJ_Internal.TestMessage.Types.SubMessage.Builder super;
                public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
                public _PBJ_Internal.TestMessage.Types.SubMessage.Builder _PBJSuper{ get { return super;} }
                public Builder() {super = new _PBJ_Internal.TestMessage.Types.SubMessage.Builder();}
                public Builder(_PBJ_Internal.TestMessage.Types.SubMessage.Builder other) {
                    super=other;
                }
                public Builder Clone() {return new Builder(super.Clone());}
                public Builder MergeFrom(SubMessage prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
                public Builder Clear() {super.Clear();return this;}
                public SubMessage BuildPartial() {return new SubMessage(super.BuildPartial());}
                public SubMessage Build() {if (_HasAllPBJFields) return new SubMessage(super.Build());return null;}
                public pbd::MessageDescriptor DescriptorForType {
                    get { return SubMessage.Descriptor; }                }
            public Builder ClearSubuuid() { super.ClearSubuuid();return this;}
            public const int SubuuidFieldTag=1;
            public bool HasSubuuid{ get {return super.HasSubuuid&&PBJ._PBJ.ValidateUuid(super.Subuuid);} }
            public PBJ.UUID Subuuid{ get {
                if (HasSubuuid) {
                    return PBJ._PBJ.CastUuid(super.Subuuid);
                } else {
                    return PBJ._PBJ.CastUuid();
                }
            }
            set {
                super.Subuuid=(PBJ._PBJ.Construct(value));
            }
            }
            public Builder ClearSubvector() { super.ClearSubvector();return this;}
            public const int SubvectorFieldTag=2;
            public bool HasSubvector{ get {return super.SubvectorCount>=3;} }
            public PBJ.Vector3d Subvector{ get  {
                int index=0;
                if (HasSubvector) {
                    return PBJ._PBJ.CastVector3d(super.GetSubvector(index*3+0),super.GetSubvector(index*3+1),super.GetSubvector(index*3+2));
                } else {
                    return PBJ._PBJ.CastVector3d();
                }
            }
            set {
                super.ClearSubvector();
                double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
                super.AddSubvector(_PBJtempArray[0]);
                super.AddSubvector(_PBJtempArray[1]);
                super.AddSubvector(_PBJtempArray[2]);
            }
            }
            public Builder ClearSubduration() { super.ClearSubduration();return this;}
            public const int SubdurationFieldTag=3;
            public bool HasSubduration{ get {return super.HasSubduration&&PBJ._PBJ.ValidateDuration(super.Subduration);} }
            public PBJ.Duration Subduration{ get {
                if (HasSubduration) {
                    return PBJ._PBJ.CastDuration(super.Subduration);
                } else {
                    return PBJ._PBJ.CastDuration();
                }
            }
            set {
                super.Subduration=(PBJ._PBJ.Construct(value));
            }
            }
            public Builder ClearSubnormal() { super.ClearSubnormal();return this;}
            public const int SubnormalFieldTag=4;
            public bool HasSubnormal{ get {return super.SubnormalCount>=2;} }
            public PBJ.Vector3f Subnormal{ get  {
                int index=0;
                if (HasSubnormal) {
                    return PBJ._PBJ.CastNormal(super.GetSubnormal(index*2+0),super.GetSubnormal(index*2+1));
                } else {
                    return PBJ._PBJ.CastNormal();
                }
            }
            set {
                super.ClearSubnormal();
                float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
                super.AddSubnormal(_PBJtempArray[0]);
                super.AddSubnormal(_PBJtempArray[1]);
            }
            }
            }
        }
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false||(field_tag>=100&&field_tag<=199);
        }
        public const int XxdFieldTag=20;
        public bool HasXxd{ get {return super.HasXxd&&PBJ._PBJ.ValidateDouble(super.Xxd);} }
        public double Xxd{ get {
            if (HasXxd) {
                return PBJ._PBJ.CastDouble(super.Xxd);
            } else {
                return 10.3;
            }
        }
        }
        public const int XxfFieldTag=21;
        public bool HasXxf{ get {return super.HasXxf&&PBJ._PBJ.ValidateFloat(super.Xxf);} }
        public float Xxf{ get {
            if (HasXxf) {
                return PBJ._PBJ.CastFloat(super.Xxf);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int Xxu32FieldTag=22;
        public bool HasXxu32{ get {return super.HasXxu32&&PBJ._PBJ.ValidateUint32(super.Xxu32);} }
        public uint Xxu32{ get {
            if (HasXxu32) {
                return PBJ._PBJ.CastUint32(super.Xxu32);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int XxsFieldTag=23;
        public bool HasXxs{ get {return super.HasXxs&&PBJ._PBJ.ValidateString(super.Xxs);} }
        public string Xxs{ get {
            if (HasXxs) {
                return PBJ._PBJ.CastString(super.Xxs);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
        public const int XxbFieldTag=24;
        public bool HasXxb{ get {return super.HasXxb&&PBJ._PBJ.ValidateBytes(super.Xxb);} }
        public pb::ByteString Xxb{ get {
            if (HasXxb) {
                return PBJ._PBJ.CastBytes(super.Xxb);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        }
        public const int XxssFieldTag=25;
        public int XxssCount { get { return super.XxssCount;} }
        public bool HasXxss(int index) {return PBJ._PBJ.ValidateString(super.GetXxss(index));}
        public string Xxss(int index) {
            return (string)PBJ._PBJ.CastString(super.GetXxss(index));
        }
        public const int XxbbFieldTag=26;
        public int XxbbCount { get { return super.XxbbCount;} }
        public bool HasXxbb(int index) {return PBJ._PBJ.ValidateBytes(super.GetXxbb(index));}
        public pb::ByteString Xxbb(int index) {
            return (pb::ByteString)PBJ._PBJ.CastBytes(super.GetXxbb(index));
        }
        public const int XxffFieldTag=27;
        public int XxffCount { get { return super.XxffCount;} }
        public bool HasXxff(int index) {return PBJ._PBJ.ValidateFloat(super.GetXxff(index));}
        public float Xxff(int index) {
            return (float)PBJ._PBJ.CastFloat(super.GetXxff(index));
        }
        public const int XxnnFieldTag=29;
        public int XxnnCount { get { return super.XxnnCount/2;} }
        public bool HasXxnn(int index) { return true; }
        public PBJ.Vector3f GetXxnn(int index) {
            if (HasXxnn(index)) {
                return PBJ._PBJ.CastNormal(super.GetXxnn(index*2+0),super.GetXxnn(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        public const int XxfrFieldTag=28;
        public bool HasXxfr{ get {return super.HasXxfr&&PBJ._PBJ.ValidateFloat(super.Xxfr);} }
        public float Xxfr{ get {
            if (HasXxfr) {
                return PBJ._PBJ.CastFloat(super.Xxfr);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int NFieldTag=1;
        public bool HasN{ get {return super.NCount>=2;} }
        public PBJ.Vector3f N{ get  {
            int index=0;
            if (HasN) {
                return PBJ._PBJ.CastNormal(super.GetN(index*2+0),super.GetN(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        }
        public const int V2FFieldTag=2;
        public bool HasV2F{ get {return super.V2FCount>=2;} }
        public PBJ.Vector2f V2F{ get  {
            int index=0;
            if (HasV2F) {
                return PBJ._PBJ.CastVector2f(super.GetV2F(index*2+0),super.GetV2F(index*2+1));
            } else {
                return PBJ._PBJ.CastVector2f();
            }
        }
        }
        public const int V2DFieldTag=3;
        public bool HasV2D{ get {return super.V2DCount>=2;} }
        public PBJ.Vector2d V2D{ get  {
            int index=0;
            if (HasV2D) {
                return PBJ._PBJ.CastVector2d(super.GetV2D(index*2+0),super.GetV2D(index*2+1));
            } else {
                return PBJ._PBJ.CastVector2d();
            }
        }
        }
        public const int V3FFieldTag=4;
        public bool HasV3F{ get {return super.V3FCount>=3;} }
        public PBJ.Vector3f V3F{ get  {
            int index=0;
            if (HasV3F) {
                return PBJ._PBJ.CastVector3f(super.GetV3F(index*3+0),super.GetV3F(index*3+1),super.GetV3F(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int V3DFieldTag=5;
        public bool HasV3D{ get {return super.V3DCount>=3;} }
        public PBJ.Vector3d V3D{ get  {
            int index=0;
            if (HasV3D) {
                return PBJ._PBJ.CastVector3d(super.GetV3D(index*3+0),super.GetV3D(index*3+1),super.GetV3D(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        }
        public const int V4FFieldTag=6;
        public bool HasV4F{ get {return super.V4FCount>=4;} }
        public PBJ.Vector4f V4F{ get  {
            int index=0;
            if (HasV4F) {
                return PBJ._PBJ.CastVector4f(super.GetV4F(index*4+0),super.GetV4F(index*4+1),super.GetV4F(index*4+2),super.GetV4F(index*4+3));
            } else {
                return PBJ._PBJ.CastVector4f();
            }
        }
        }
        public const int V4DFieldTag=7;
        public bool HasV4D{ get {return super.V4DCount>=4;} }
        public PBJ.Vector4d V4D{ get  {
            int index=0;
            if (HasV4D) {
                return PBJ._PBJ.CastVector4d(super.GetV4D(index*4+0),super.GetV4D(index*4+1),super.GetV4D(index*4+2),super.GetV4D(index*4+3));
            } else {
                return PBJ._PBJ.CastVector4d();
            }
        }
        }
        public const int QFieldTag=8;
        public bool HasQ{ get {return super.QCount>=3;} }
        public PBJ.Quaternion Q{ get  {
            int index=0;
            if (HasQ) {
                return PBJ._PBJ.CastQuaternion(super.GetQ(index*3+0),super.GetQ(index*3+1),super.GetQ(index*3+2));
            } else {
                return PBJ._PBJ.CastQuaternion();
            }
        }
        }
        public const int UFieldTag=9;
        public bool HasU{ get {return super.HasU&&PBJ._PBJ.ValidateUuid(super.U);} }
        public PBJ.UUID U{ get {
            if (HasU) {
                return PBJ._PBJ.CastUuid(super.U);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int AFieldTag=10;
        public bool HasA{ get {return super.HasA&&PBJ._PBJ.ValidateAngle(super.A);} }
        public float A{ get {
            if (HasA) {
                return PBJ._PBJ.CastAngle(super.A);
            } else {
                return PBJ._PBJ.CastAngle();
            }
        }
        }
        public const int TFieldTag=11;
        public bool HasT{ get {return super.HasT&&PBJ._PBJ.ValidateTime(super.T);} }
        public PBJ.Time T{ get {
            if (HasT) {
                return PBJ._PBJ.CastTime(super.T);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        }
        public const int DFieldTag=12;
        public bool HasD{ get {return super.HasD&&PBJ._PBJ.ValidateDuration(super.D);} }
        public PBJ.Duration D{ get {
            if (HasD) {
                return PBJ._PBJ.CastDuration(super.D);
            } else {
                return PBJ._PBJ.CastDuration();
            }
        }
        }
        public const int F32FieldTag=13;
        public bool HasF32 { get {
            if (!super.HasF32) return false;
            return PBJ._PBJ.ValidateFlags(super.F32,(ulong)Types.Flagsf32.UNIVERSA|(ulong)Types.Flagsf32.WE|(ulong)Types.Flagsf32.IMAGE|(ulong)Types.Flagsf32.LOCA);
        } }
        public uint F32{ get {
            if (HasF32) {
                return (uint)PBJ._PBJ.CastFlags(super.F32,(ulong)Types.Flagsf32.UNIVERSA|(ulong)Types.Flagsf32.WE|(ulong)Types.Flagsf32.IMAGE|(ulong)Types.Flagsf32.LOCA);
            } else {
                return (uint)PBJ._PBJ.CastFlags((ulong)Types.Flagsf32.UNIVERSA|(ulong)Types.Flagsf32.WE|(ulong)Types.Flagsf32.IMAGE|(ulong)Types.Flagsf32.LOCA);
            }
        }
        }
        public const int F64FieldTag=14;
        public bool HasF64 { get {
            if (!super.HasF64) return false;
            return PBJ._PBJ.ValidateFlags(super.F64,(ulong)Types.Flagsf64.UNIVERSAL|(ulong)Types.Flagsf64.WEB|(ulong)Types.Flagsf64.IMAGES|(ulong)Types.Flagsf64.LOCAL);
        } }
        public ulong F64{ get {
            if (HasF64) {
                return (ulong)PBJ._PBJ.CastFlags(super.F64,(ulong)Types.Flagsf64.UNIVERSAL|(ulong)Types.Flagsf64.WEB|(ulong)Types.Flagsf64.IMAGES|(ulong)Types.Flagsf64.LOCAL);
            } else {
                return (ulong)PBJ._PBJ.CastFlags((ulong)Types.Flagsf64.UNIVERSAL|(ulong)Types.Flagsf64.WEB|(ulong)Types.Flagsf64.IMAGES|(ulong)Types.Flagsf64.LOCAL);
            }
        }
        }
        public const int BsfFieldTag=15;
        public bool HasBsf{ get {return super.BsfCount>=4;} }
        public PBJ.BoundingSphere3f Bsf{ get  {
            int index=0;
            if (HasBsf) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBsf(index*4+0),super.GetBsf(index*4+1),super.GetBsf(index*4+2),super.GetBsf(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        }
        public const int BsdFieldTag=16;
        public bool HasBsd{ get {return super.BsdCount>=4;} }
        public PBJ.BoundingSphere3d Bsd{ get  {
            int index=0;
            if (HasBsd) {
                return PBJ._PBJ.CastBoundingsphere3d(super.GetBsd(index*4+0),super.GetBsd(index*4+1),super.GetBsd(index*4+2),super.GetBsd(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3d();
            }
        }
        }
        public const int BbfFieldTag=17;
        public bool HasBbf{ get {return super.BbfCount>=6;} }
        public PBJ.BoundingBox3f3f Bbf{ get  {
            int index=0;
            if (HasBbf) {
                return PBJ._PBJ.CastBoundingbox3f3f(super.GetBbf(index*6+0),super.GetBbf(index*6+1),super.GetBbf(index*6+2),super.GetBbf(index*6+3),super.GetBbf(index*6+4),super.GetBbf(index*6+5));
            } else {
                return PBJ._PBJ.CastBoundingbox3f3f();
            }
        }
        }
        public const int BbdFieldTag=18;
        public bool HasBbd{ get {return super.BbdCount>=6;} }
        public PBJ.BoundingBox3d3f Bbd{ get  {
            int index=0;
            if (HasBbd) {
                return PBJ._PBJ.CastBoundingbox3d3f(super.GetBbd(index*6+0),super.GetBbd(index*6+1),super.GetBbd(index*6+2),super.GetBbd(index*6+3),super.GetBbd(index*6+4),super.GetBbd(index*6+5));
            } else {
                return PBJ._PBJ.CastBoundingbox3d3f();
            }
        }
        }
        public const int E32FieldTag=19;
        public bool HasE32{ get {return super.HasE32;} }
        public Types.Enum32 E32{ get {
            if (HasE32) {
                return (Types.Enum32)super.E32;
            } else {
                return new Types.Enum32();
            }
        }
        }
        public const int SubmesFieldTag=30;
        public bool HasSubmes{ get {return super.HasSubmes;} }
        public Types.SubMessage Submes{ get {
            if (HasSubmes) {
                return new Types.SubMessage(super.Submes);
            } else {
                return new Types.SubMessage();
            }
        }
        }
        public const int SubmessersFieldTag=31;
        public int SubmessersCount { get { return super.SubmessersCount;} }
        public bool HasSubmessers(int index) {return true;}
        public Types.SubMessage Submessers(int index) {
            return new Types.SubMessage(super.GetSubmessers(index));
        }
        public const int ShaFieldTag=32;
        public bool HasSha{ get {return super.HasSha&&PBJ._PBJ.ValidateSha256(super.Sha);} }
        public PBJ.SHA256 Sha{ get {
            if (HasSha) {
                return PBJ._PBJ.CastSha256(super.Sha);
            } else {
                return PBJ._PBJ.CastSha256();
            }
        }
        }
        public const int ShasFieldTag=33;
        public int ShasCount { get { return super.ShasCount;} }
        public bool HasShas(int index) {return PBJ._PBJ.ValidateSha256(super.GetShas(index));}
        public PBJ.SHA256 Shas(int index) {
            return (PBJ.SHA256)PBJ._PBJ.CastSha256(super.GetShas(index));
        }
        public const int ExtmesFieldTag=34;
        public bool HasExtmes{ get {return super.HasExtmes;} }
        public ExternalMessage Extmes{ get {
            if (HasExtmes) {
                return new ExternalMessage(super.Extmes);
            } else {
                return new ExternalMessage();
            }
        }
        }
        public const int ExtmessersFieldTag=35;
        public int ExtmessersCount { get { return super.ExtmessersCount;} }
        public bool HasExtmessers(int index) {return true;}
        public ExternalMessage Extmessers(int index) {
            return new ExternalMessage(super.GetExtmessers(index));
        }
        public const int ExtmesserFieldTag=36;
        public bool HasExtmesser{ get {return super.HasExtmesser;} }
        public ExternalMessage Extmesser{ get {
            if (HasExtmesser) {
                return new ExternalMessage(super.Extmesser);
            } else {
                return new ExternalMessage();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(TestMessage prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static TestMessage ParseFrom(pb::ByteString data) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data));
        }
        public static TestMessage ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data,er));
        }
        public static TestMessage ParseFrom(byte[] data) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data));
        }
        public static TestMessage ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data,er));
        }
        public static TestMessage ParseFrom(global::System.IO.Stream data) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data));
        }
        public static TestMessage ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data,er));
        }
        public static TestMessage ParseFrom(pb::CodedInputStream data) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data));
        }
        public static TestMessage ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new TestMessage(_PBJ_Internal.TestMessage.ParseFrom(data,er));
        }
        protected override bool _HasAllPBJFields{ get {
            return true
                &&HasV3F
                ;
        } }
        public bool IsInitialized { get {
            return super.IsInitialized&&_HasAllPBJFields;
        } }
        public class Builder : global::PBJ.IMessage.IBuilder{
        protected override bool _HasAllPBJFields{ get {
            return true
                &&HasV3F
                ;
        } }
        public bool IsInitialized { get {
            return super.IsInitialized&&_HasAllPBJFields;
        } }
            protected _PBJ_Internal.TestMessage.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.TestMessage.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.TestMessage.Builder();}
            public Builder(_PBJ_Internal.TestMessage.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(TestMessage prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public TestMessage BuildPartial() {return new TestMessage(super.BuildPartial());}
            public TestMessage Build() {if (_HasAllPBJFields) return new TestMessage(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return TestMessage.Descriptor; }            }
        public Builder ClearXxd() { super.ClearXxd();return this;}
        public const int XxdFieldTag=20;
        public bool HasXxd{ get {return super.HasXxd&&PBJ._PBJ.ValidateDouble(super.Xxd);} }
        public double Xxd{ get {
            if (HasXxd) {
                return PBJ._PBJ.CastDouble(super.Xxd);
            } else {
                return 10.3;
            }
        }
        set {
            super.Xxd=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearXxf() { super.ClearXxf();return this;}
        public const int XxfFieldTag=21;
        public bool HasXxf{ get {return super.HasXxf&&PBJ._PBJ.ValidateFloat(super.Xxf);} }
        public float Xxf{ get {
            if (HasXxf) {
                return PBJ._PBJ.CastFloat(super.Xxf);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.Xxf=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearXxu32() { super.ClearXxu32();return this;}
        public const int Xxu32FieldTag=22;
        public bool HasXxu32{ get {return super.HasXxu32&&PBJ._PBJ.ValidateUint32(super.Xxu32);} }
        public uint Xxu32{ get {
            if (HasXxu32) {
                return PBJ._PBJ.CastUint32(super.Xxu32);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.Xxu32=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearXxs() { super.ClearXxs();return this;}
        public const int XxsFieldTag=23;
        public bool HasXxs{ get {return super.HasXxs&&PBJ._PBJ.ValidateString(super.Xxs);} }
        public string Xxs{ get {
            if (HasXxs) {
                return PBJ._PBJ.CastString(super.Xxs);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.Xxs=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearXxb() { super.ClearXxb();return this;}
        public const int XxbFieldTag=24;
        public bool HasXxb{ get {return super.HasXxb&&PBJ._PBJ.ValidateBytes(super.Xxb);} }
        public pb::ByteString Xxb{ get {
            if (HasXxb) {
                return PBJ._PBJ.CastBytes(super.Xxb);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        set {
            super.Xxb=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearXxss() { super.ClearXxss();return this;}
        public Builder SetXxss(int index, string value) {
            super.SetXxss(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int XxssFieldTag=25;
        public int XxssCount { get { return super.XxssCount;} }
        public bool HasXxss(int index) {return PBJ._PBJ.ValidateString(super.GetXxss(index));}
        public string Xxss(int index) {
            return (string)PBJ._PBJ.CastString(super.GetXxss(index));
        }
        public Builder AddXxss(string value) {
            super.AddXxss(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearXxbb() { super.ClearXxbb();return this;}
        public Builder SetXxbb(int index, pb::ByteString value) {
            super.SetXxbb(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int XxbbFieldTag=26;
        public int XxbbCount { get { return super.XxbbCount;} }
        public bool HasXxbb(int index) {return PBJ._PBJ.ValidateBytes(super.GetXxbb(index));}
        public pb::ByteString Xxbb(int index) {
            return (pb::ByteString)PBJ._PBJ.CastBytes(super.GetXxbb(index));
        }
        public Builder AddXxbb(pb::ByteString value) {
            super.AddXxbb(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearXxff() { super.ClearXxff();return this;}
        public Builder SetXxff(int index, float value) {
            super.SetXxff(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int XxffFieldTag=27;
        public int XxffCount { get { return super.XxffCount;} }
        public bool HasXxff(int index) {return PBJ._PBJ.ValidateFloat(super.GetXxff(index));}
        public float Xxff(int index) {
            return (float)PBJ._PBJ.CastFloat(super.GetXxff(index));
        }
        public Builder AddXxff(float value) {
            super.AddXxff(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearXxnn() { super.ClearXxnn();return this;}
        public const int XxnnFieldTag=29;
        public int XxnnCount { get { return super.XxnnCount/2;} }
        public bool HasXxnn(int index) { return true; }
        public PBJ.Vector3f GetXxnn(int index) {
            if (HasXxnn(index)) {
                return PBJ._PBJ.CastNormal(super.GetXxnn(index*2+0),super.GetXxnn(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        public Builder AddXxnn(PBJ.Vector3f value) {
            float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
            super.AddXxnn(_PBJtempArray[0]);
            super.AddXxnn(_PBJtempArray[1]);
            return this;
        }
        public Builder SetXxnn(int index,PBJ.Vector3f value) {
            float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
            super.SetXxnn(index*2+0,_PBJtempArray[0]);
            super.SetXxnn(index*2+1,_PBJtempArray[1]);
            return this;
        }
        public Builder ClearXxfr() { super.ClearXxfr();return this;}
        public const int XxfrFieldTag=28;
        public bool HasXxfr{ get {return super.HasXxfr&&PBJ._PBJ.ValidateFloat(super.Xxfr);} }
        public float Xxfr{ get {
            if (HasXxfr) {
                return PBJ._PBJ.CastFloat(super.Xxfr);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.Xxfr=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearN() { super.ClearN();return this;}
        public const int NFieldTag=1;
        public bool HasN{ get {return super.NCount>=2;} }
        public PBJ.Vector3f N{ get  {
            int index=0;
            if (HasN) {
                return PBJ._PBJ.CastNormal(super.GetN(index*2+0),super.GetN(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        set {
            super.ClearN();
            float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
            super.AddN(_PBJtempArray[0]);
            super.AddN(_PBJtempArray[1]);
        }
        }
        public Builder ClearV2F() { super.ClearV2F();return this;}
        public const int V2FFieldTag=2;
        public bool HasV2F{ get {return super.V2FCount>=2;} }
        public PBJ.Vector2f V2F{ get  {
            int index=0;
            if (HasV2F) {
                return PBJ._PBJ.CastVector2f(super.GetV2F(index*2+0),super.GetV2F(index*2+1));
            } else {
                return PBJ._PBJ.CastVector2f();
            }
        }
        set {
            super.ClearV2F();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector2f(value);
            super.AddV2F(_PBJtempArray[0]);
            super.AddV2F(_PBJtempArray[1]);
        }
        }
        public Builder ClearV2D() { super.ClearV2D();return this;}
        public const int V2DFieldTag=3;
        public bool HasV2D{ get {return super.V2DCount>=2;} }
        public PBJ.Vector2d V2D{ get  {
            int index=0;
            if (HasV2D) {
                return PBJ._PBJ.CastVector2d(super.GetV2D(index*2+0),super.GetV2D(index*2+1));
            } else {
                return PBJ._PBJ.CastVector2d();
            }
        }
        set {
            super.ClearV2D();
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector2d(value);
            super.AddV2D(_PBJtempArray[0]);
            super.AddV2D(_PBJtempArray[1]);
        }
        }
        public Builder ClearV3F() { super.ClearV3F();return this;}
        public const int V3FFieldTag=4;
        public bool HasV3F{ get {return super.V3FCount>=3;} }
        public PBJ.Vector3f V3F{ get  {
            int index=0;
            if (HasV3F) {
                return PBJ._PBJ.CastVector3f(super.GetV3F(index*3+0),super.GetV3F(index*3+1),super.GetV3F(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearV3F();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddV3F(_PBJtempArray[0]);
            super.AddV3F(_PBJtempArray[1]);
            super.AddV3F(_PBJtempArray[2]);
        }
        }
        public Builder ClearV3D() { super.ClearV3D();return this;}
        public const int V3DFieldTag=5;
        public bool HasV3D{ get {return super.V3DCount>=3;} }
        public PBJ.Vector3d V3D{ get  {
            int index=0;
            if (HasV3D) {
                return PBJ._PBJ.CastVector3d(super.GetV3D(index*3+0),super.GetV3D(index*3+1),super.GetV3D(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        set {
            super.ClearV3D();
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
            super.AddV3D(_PBJtempArray[0]);
            super.AddV3D(_PBJtempArray[1]);
            super.AddV3D(_PBJtempArray[2]);
        }
        }
        public Builder ClearV4F() { super.ClearV4F();return this;}
        public const int V4FFieldTag=6;
        public bool HasV4F{ get {return super.V4FCount>=4;} }
        public PBJ.Vector4f V4F{ get  {
            int index=0;
            if (HasV4F) {
                return PBJ._PBJ.CastVector4f(super.GetV4F(index*4+0),super.GetV4F(index*4+1),super.GetV4F(index*4+2),super.GetV4F(index*4+3));
            } else {
                return PBJ._PBJ.CastVector4f();
            }
        }
        set {
            super.ClearV4F();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector4f(value);
            super.AddV4F(_PBJtempArray[0]);
            super.AddV4F(_PBJtempArray[1]);
            super.AddV4F(_PBJtempArray[2]);
            super.AddV4F(_PBJtempArray[3]);
        }
        }
        public Builder ClearV4D() { super.ClearV4D();return this;}
        public const int V4DFieldTag=7;
        public bool HasV4D{ get {return super.V4DCount>=4;} }
        public PBJ.Vector4d V4D{ get  {
            int index=0;
            if (HasV4D) {
                return PBJ._PBJ.CastVector4d(super.GetV4D(index*4+0),super.GetV4D(index*4+1),super.GetV4D(index*4+2),super.GetV4D(index*4+3));
            } else {
                return PBJ._PBJ.CastVector4d();
            }
        }
        set {
            super.ClearV4D();
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector4d(value);
            super.AddV4D(_PBJtempArray[0]);
            super.AddV4D(_PBJtempArray[1]);
            super.AddV4D(_PBJtempArray[2]);
            super.AddV4D(_PBJtempArray[3]);
        }
        }
        public Builder ClearQ() { super.ClearQ();return this;}
        public const int QFieldTag=8;
        public bool HasQ{ get {return super.QCount>=3;} }
        public PBJ.Quaternion Q{ get  {
            int index=0;
            if (HasQ) {
                return PBJ._PBJ.CastQuaternion(super.GetQ(index*3+0),super.GetQ(index*3+1),super.GetQ(index*3+2));
            } else {
                return PBJ._PBJ.CastQuaternion();
            }
        }
        set {
            super.ClearQ();
            float[] _PBJtempArray=PBJ._PBJ.ConstructQuaternion(value);
            super.AddQ(_PBJtempArray[0]);
            super.AddQ(_PBJtempArray[1]);
            super.AddQ(_PBJtempArray[2]);
        }
        }
        public Builder ClearU() { super.ClearU();return this;}
        public const int UFieldTag=9;
        public bool HasU{ get {return super.HasU&&PBJ._PBJ.ValidateUuid(super.U);} }
        public PBJ.UUID U{ get {
            if (HasU) {
                return PBJ._PBJ.CastUuid(super.U);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.U=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearA() { super.ClearA();return this;}
        public const int AFieldTag=10;
        public bool HasA{ get {return super.HasA&&PBJ._PBJ.ValidateAngle(super.A);} }
        public float A{ get {
            if (HasA) {
                return PBJ._PBJ.CastAngle(super.A);
            } else {
                return PBJ._PBJ.CastAngle();
            }
        }
        set {
            super.A=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearT() { super.ClearT();return this;}
        public const int TFieldTag=11;
        public bool HasT{ get {return super.HasT&&PBJ._PBJ.ValidateTime(super.T);} }
        public PBJ.Time T{ get {
            if (HasT) {
                return PBJ._PBJ.CastTime(super.T);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        set {
            super.T=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearD() { super.ClearD();return this;}
        public const int DFieldTag=12;
        public bool HasD{ get {return super.HasD&&PBJ._PBJ.ValidateDuration(super.D);} }
        public PBJ.Duration D{ get {
            if (HasD) {
                return PBJ._PBJ.CastDuration(super.D);
            } else {
                return PBJ._PBJ.CastDuration();
            }
        }
        set {
            super.D=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearF32() { super.ClearF32();return this;}
        public const int F32FieldTag=13;
        public bool HasF32 { get {
            if (!super.HasF32) return false;
            return PBJ._PBJ.ValidateFlags(super.F32,(ulong)Types.Flagsf32.UNIVERSA|(ulong)Types.Flagsf32.WE|(ulong)Types.Flagsf32.IMAGE|(ulong)Types.Flagsf32.LOCA);
        } }
        public uint F32{ get {
            if (HasF32) {
                return (uint)PBJ._PBJ.CastFlags(super.F32,(ulong)Types.Flagsf32.UNIVERSA|(ulong)Types.Flagsf32.WE|(ulong)Types.Flagsf32.IMAGE|(ulong)Types.Flagsf32.LOCA);
            } else {
                return (uint)PBJ._PBJ.CastFlags((ulong)Types.Flagsf32.UNIVERSA|(ulong)Types.Flagsf32.WE|(ulong)Types.Flagsf32.IMAGE|(ulong)Types.Flagsf32.LOCA);
            }
        }
        set {
            super.F32=((value));
        }
        }
        public Builder ClearF64() { super.ClearF64();return this;}
        public const int F64FieldTag=14;
        public bool HasF64 { get {
            if (!super.HasF64) return false;
            return PBJ._PBJ.ValidateFlags(super.F64,(ulong)Types.Flagsf64.UNIVERSAL|(ulong)Types.Flagsf64.WEB|(ulong)Types.Flagsf64.IMAGES|(ulong)Types.Flagsf64.LOCAL);
        } }
        public ulong F64{ get {
            if (HasF64) {
                return (ulong)PBJ._PBJ.CastFlags(super.F64,(ulong)Types.Flagsf64.UNIVERSAL|(ulong)Types.Flagsf64.WEB|(ulong)Types.Flagsf64.IMAGES|(ulong)Types.Flagsf64.LOCAL);
            } else {
                return (ulong)PBJ._PBJ.CastFlags((ulong)Types.Flagsf64.UNIVERSAL|(ulong)Types.Flagsf64.WEB|(ulong)Types.Flagsf64.IMAGES|(ulong)Types.Flagsf64.LOCAL);
            }
        }
        set {
            super.F64=((value));
        }
        }
        public Builder ClearBsf() { super.ClearBsf();return this;}
        public const int BsfFieldTag=15;
        public bool HasBsf{ get {return super.BsfCount>=4;} }
        public PBJ.BoundingSphere3f Bsf{ get  {
            int index=0;
            if (HasBsf) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBsf(index*4+0),super.GetBsf(index*4+1),super.GetBsf(index*4+2),super.GetBsf(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        set {
            super.ClearBsf();
            float[] _PBJtempArray=PBJ._PBJ.ConstructBoundingsphere3f(value);
            super.AddBsf(_PBJtempArray[0]);
            super.AddBsf(_PBJtempArray[1]);
            super.AddBsf(_PBJtempArray[2]);
            super.AddBsf(_PBJtempArray[3]);
        }
        }
        public Builder ClearBsd() { super.ClearBsd();return this;}
        public const int BsdFieldTag=16;
        public bool HasBsd{ get {return super.BsdCount>=4;} }
        public PBJ.BoundingSphere3d Bsd{ get  {
            int index=0;
            if (HasBsd) {
                return PBJ._PBJ.CastBoundingsphere3d(super.GetBsd(index*4+0),super.GetBsd(index*4+1),super.GetBsd(index*4+2),super.GetBsd(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3d();
            }
        }
        set {
            super.ClearBsd();
            double[] _PBJtempArray=PBJ._PBJ.ConstructBoundingsphere3d(value);
            super.AddBsd(_PBJtempArray[0]);
            super.AddBsd(_PBJtempArray[1]);
            super.AddBsd(_PBJtempArray[2]);
            super.AddBsd(_PBJtempArray[3]);
        }
        }
        public Builder ClearBbf() { super.ClearBbf();return this;}
        public const int BbfFieldTag=17;
        public bool HasBbf{ get {return super.BbfCount>=6;} }
        public PBJ.BoundingBox3f3f Bbf{ get  {
            int index=0;
            if (HasBbf) {
                return PBJ._PBJ.CastBoundingbox3f3f(super.GetBbf(index*6+0),super.GetBbf(index*6+1),super.GetBbf(index*6+2),super.GetBbf(index*6+3),super.GetBbf(index*6+4),super.GetBbf(index*6+5));
            } else {
                return PBJ._PBJ.CastBoundingbox3f3f();
            }
        }
        set {
            super.ClearBbf();
            float[] _PBJtempArray=PBJ._PBJ.ConstructBoundingbox3f3f(value);
            super.AddBbf(_PBJtempArray[0]);
            super.AddBbf(_PBJtempArray[1]);
            super.AddBbf(_PBJtempArray[2]);
            super.AddBbf(_PBJtempArray[3]);
            super.AddBbf(_PBJtempArray[4]);
            super.AddBbf(_PBJtempArray[5]);
        }
        }
        public Builder ClearBbd() { super.ClearBbd();return this;}
        public const int BbdFieldTag=18;
        public bool HasBbd{ get {return super.BbdCount>=6;} }
        public PBJ.BoundingBox3d3f Bbd{ get  {
            int index=0;
            if (HasBbd) {
                return PBJ._PBJ.CastBoundingbox3d3f(super.GetBbd(index*6+0),super.GetBbd(index*6+1),super.GetBbd(index*6+2),super.GetBbd(index*6+3),super.GetBbd(index*6+4),super.GetBbd(index*6+5));
            } else {
                return PBJ._PBJ.CastBoundingbox3d3f();
            }
        }
        set {
            super.ClearBbd();
            double[] _PBJtempArray=PBJ._PBJ.ConstructBoundingbox3d3f(value);
            super.AddBbd(_PBJtempArray[0]);
            super.AddBbd(_PBJtempArray[1]);
            super.AddBbd(_PBJtempArray[2]);
            super.AddBbd(_PBJtempArray[3]);
            super.AddBbd(_PBJtempArray[4]);
            super.AddBbd(_PBJtempArray[5]);
        }
        }
        public Builder ClearE32() { super.ClearE32();return this;}
        public const int E32FieldTag=19;
        public bool HasE32{ get {return super.HasE32;} }
        public Types.Enum32 E32{ get {
            if (HasE32) {
                return (Types.Enum32)super.E32;
            } else {
                return new Types.Enum32();
            }
        }
        set {
            super.E32=((_PBJ_Internal.TestMessage.Types.Enum32)value);
        }
        }
        public Builder ClearSubmes() { super.ClearSubmes();return this;}
        public const int SubmesFieldTag=30;
        public bool HasSubmes{ get {return super.HasSubmes;} }
        public Types.SubMessage Submes{ get {
            if (HasSubmes) {
                return new Types.SubMessage(super.Submes);
            } else {
                return new Types.SubMessage();
            }
        }
        set {
            super.Submes=value._PBJSuper;
        }
        }
        public Builder ClearSubmessers() { super.ClearSubmessers();return this;}
        public Builder SetSubmessers(int index,Types.SubMessage value) {
            super.SetSubmessers(index,value._PBJSuper);
            return this;
        }
        public const int SubmessersFieldTag=31;
        public int SubmessersCount { get { return super.SubmessersCount;} }
        public bool HasSubmessers(int index) {return true;}
        public Types.SubMessage Submessers(int index) {
            return new Types.SubMessage(super.GetSubmessers(index));
        }
        public Builder AddSubmessers(Types.SubMessage value) {
            super.AddSubmessers(value._PBJSuper);
            return this;
        }
        public Builder ClearSha() { super.ClearSha();return this;}
        public const int ShaFieldTag=32;
        public bool HasSha{ get {return super.HasSha&&PBJ._PBJ.ValidateSha256(super.Sha);} }
        public PBJ.SHA256 Sha{ get {
            if (HasSha) {
                return PBJ._PBJ.CastSha256(super.Sha);
            } else {
                return PBJ._PBJ.CastSha256();
            }
        }
        set {
            super.Sha=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearShas() { super.ClearShas();return this;}
        public Builder SetShas(int index, PBJ.SHA256 value) {
            super.SetShas(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int ShasFieldTag=33;
        public int ShasCount { get { return super.ShasCount;} }
        public bool HasShas(int index) {return PBJ._PBJ.ValidateSha256(super.GetShas(index));}
        public PBJ.SHA256 Shas(int index) {
            return (PBJ.SHA256)PBJ._PBJ.CastSha256(super.GetShas(index));
        }
        public Builder AddShas(PBJ.SHA256 value) {
            super.AddShas(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearExtmes() { super.ClearExtmes();return this;}
        public const int ExtmesFieldTag=34;
        public bool HasExtmes{ get {return super.HasExtmes;} }
        public ExternalMessage Extmes{ get {
            if (HasExtmes) {
                return new ExternalMessage(super.Extmes);
            } else {
                return new ExternalMessage();
            }
        }
        set {
            super.Extmes=value._PBJSuper;
        }
        }
        public Builder ClearExtmessers() { super.ClearExtmessers();return this;}
        public Builder SetExtmessers(int index,ExternalMessage value) {
            super.SetExtmessers(index,value._PBJSuper);
            return this;
        }
        public const int ExtmessersFieldTag=35;
        public int ExtmessersCount { get { return super.ExtmessersCount;} }
        public bool HasExtmessers(int index) {return true;}
        public ExternalMessage Extmessers(int index) {
            return new ExternalMessage(super.GetExtmessers(index));
        }
        public Builder AddExtmessers(ExternalMessage value) {
            super.AddExtmessers(value._PBJSuper);
            return this;
        }
        public Builder ClearExtmesser() { super.ClearExtmesser();return this;}
        public const int ExtmesserFieldTag=36;
        public bool HasExtmesser{ get {return super.HasExtmesser;} }
        public ExternalMessage Extmesser{ get {
            if (HasExtmesser) {
                return new ExternalMessage(super.Extmesser);
            } else {
                return new ExternalMessage();
            }
        }
        set {
            super.Extmesser=value._PBJSuper;
        }
        }
        }
    }
}
namespace Sirikata.PB {
}
