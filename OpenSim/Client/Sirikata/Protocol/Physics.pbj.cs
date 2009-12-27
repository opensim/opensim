using pbd = global::Google.ProtocolBuffers.Descriptors;
using pb = global::Google.ProtocolBuffers;
namespace Sirikata.Physics.Protocol {
    public class CollisionBegin : PBJ.IMessage {
        protected _PBJ_Internal.CollisionBegin super;
        public _PBJ_Internal.CollisionBegin _PBJSuper{ get { return super;} }
        public CollisionBegin() {
            super=new _PBJ_Internal.CollisionBegin();
        }
        public CollisionBegin(_PBJ_Internal.CollisionBegin reference) {
            super=reference;
        }
        public static CollisionBegin defaultInstance= new CollisionBegin (_PBJ_Internal.CollisionBegin.DefaultInstance);
        public static CollisionBegin DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.CollisionBegin.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int TimestampFieldTag=2;
        public bool HasTimestamp{ get {return super.HasTimestamp&&PBJ._PBJ.ValidateTime(super.Timestamp);} }
        public PBJ.Time Timestamp{ get {
            if (HasTimestamp) {
                return PBJ._PBJ.CastTime(super.Timestamp);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        }
        public const int ThisPositionFieldTag=3;
        public int ThisPositionCount { get { return super.ThisPositionCount/3;} }
        public bool HasThisPosition(int index) { return true; }
        public PBJ.Vector3d GetThisPosition(int index) {
            if (HasThisPosition(index)) {
                return PBJ._PBJ.CastVector3d(super.GetThisPosition(index*3+0),super.GetThisPosition(index*3+1),super.GetThisPosition(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        public const int OtherPositionFieldTag=4;
        public int OtherPositionCount { get { return super.OtherPositionCount/3;} }
        public bool HasOtherPosition(int index) { return true; }
        public PBJ.Vector3d GetOtherPosition(int index) {
            if (HasOtherPosition(index)) {
                return PBJ._PBJ.CastVector3d(super.GetOtherPosition(index*3+0),super.GetOtherPosition(index*3+1),super.GetOtherPosition(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        public const int ThisNormalFieldTag=5;
        public int ThisNormalCount { get { return super.ThisNormalCount/2;} }
        public bool HasThisNormal(int index) { return true; }
        public PBJ.Vector3f GetThisNormal(int index) {
            if (HasThisNormal(index)) {
                return PBJ._PBJ.CastNormal(super.GetThisNormal(index*2+0),super.GetThisNormal(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        public const int ImpulseFieldTag=6;
        public int ImpulseCount { get { return super.ImpulseCount;} }
        public bool HasImpulse(int index) {return PBJ._PBJ.ValidateFloat(super.GetImpulse(index));}
        public float Impulse(int index) {
            return (float)PBJ._PBJ.CastFloat(super.GetImpulse(index));
        }
        public const int OtherObjectReferenceFieldTag=7;
        public bool HasOtherObjectReference{ get {return super.HasOtherObjectReference&&PBJ._PBJ.ValidateUuid(super.OtherObjectReference);} }
        public PBJ.UUID OtherObjectReference{ get {
            if (HasOtherObjectReference) {
                return PBJ._PBJ.CastUuid(super.OtherObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(CollisionBegin prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static CollisionBegin ParseFrom(pb::ByteString data) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data));
        }
        public static CollisionBegin ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data,er));
        }
        public static CollisionBegin ParseFrom(byte[] data) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data));
        }
        public static CollisionBegin ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data,er));
        }
        public static CollisionBegin ParseFrom(global::System.IO.Stream data) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data));
        }
        public static CollisionBegin ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data,er));
        }
        public static CollisionBegin ParseFrom(pb::CodedInputStream data) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data));
        }
        public static CollisionBegin ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new CollisionBegin(_PBJ_Internal.CollisionBegin.ParseFrom(data,er));
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
            protected _PBJ_Internal.CollisionBegin.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.CollisionBegin.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.CollisionBegin.Builder();}
            public Builder(_PBJ_Internal.CollisionBegin.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(CollisionBegin prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public CollisionBegin BuildPartial() {return new CollisionBegin(super.BuildPartial());}
            public CollisionBegin Build() {if (_HasAllPBJFields) return new CollisionBegin(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return CollisionBegin.Descriptor; }            }
        public Builder ClearTimestamp() { super.ClearTimestamp();return this;}
        public const int TimestampFieldTag=2;
        public bool HasTimestamp{ get {return super.HasTimestamp&&PBJ._PBJ.ValidateTime(super.Timestamp);} }
        public PBJ.Time Timestamp{ get {
            if (HasTimestamp) {
                return PBJ._PBJ.CastTime(super.Timestamp);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        set {
            super.Timestamp=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearThisPosition() { super.ClearThisPosition();return this;}
        public const int ThisPositionFieldTag=3;
        public int ThisPositionCount { get { return super.ThisPositionCount/3;} }
        public bool HasThisPosition(int index) { return true; }
        public PBJ.Vector3d GetThisPosition(int index) {
            if (HasThisPosition(index)) {
                return PBJ._PBJ.CastVector3d(super.GetThisPosition(index*3+0),super.GetThisPosition(index*3+1),super.GetThisPosition(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        public Builder AddThisPosition(PBJ.Vector3d value) {
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
            super.AddThisPosition(_PBJtempArray[0]);
            super.AddThisPosition(_PBJtempArray[1]);
            super.AddThisPosition(_PBJtempArray[2]);
            return this;
        }
        public Builder SetThisPosition(int index,PBJ.Vector3d value) {
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
            super.SetThisPosition(index*3+0,_PBJtempArray[0]);
            super.SetThisPosition(index*3+1,_PBJtempArray[1]);
            super.SetThisPosition(index*3+2,_PBJtempArray[2]);
            return this;
        }
        public Builder ClearOtherPosition() { super.ClearOtherPosition();return this;}
        public const int OtherPositionFieldTag=4;
        public int OtherPositionCount { get { return super.OtherPositionCount/3;} }
        public bool HasOtherPosition(int index) { return true; }
        public PBJ.Vector3d GetOtherPosition(int index) {
            if (HasOtherPosition(index)) {
                return PBJ._PBJ.CastVector3d(super.GetOtherPosition(index*3+0),super.GetOtherPosition(index*3+1),super.GetOtherPosition(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        public Builder AddOtherPosition(PBJ.Vector3d value) {
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
            super.AddOtherPosition(_PBJtempArray[0]);
            super.AddOtherPosition(_PBJtempArray[1]);
            super.AddOtherPosition(_PBJtempArray[2]);
            return this;
        }
        public Builder SetOtherPosition(int index,PBJ.Vector3d value) {
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
            super.SetOtherPosition(index*3+0,_PBJtempArray[0]);
            super.SetOtherPosition(index*3+1,_PBJtempArray[1]);
            super.SetOtherPosition(index*3+2,_PBJtempArray[2]);
            return this;
        }
        public Builder ClearThisNormal() { super.ClearThisNormal();return this;}
        public const int ThisNormalFieldTag=5;
        public int ThisNormalCount { get { return super.ThisNormalCount/2;} }
        public bool HasThisNormal(int index) { return true; }
        public PBJ.Vector3f GetThisNormal(int index) {
            if (HasThisNormal(index)) {
                return PBJ._PBJ.CastNormal(super.GetThisNormal(index*2+0),super.GetThisNormal(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        public Builder AddThisNormal(PBJ.Vector3f value) {
            float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
            super.AddThisNormal(_PBJtempArray[0]);
            super.AddThisNormal(_PBJtempArray[1]);
            return this;
        }
        public Builder SetThisNormal(int index,PBJ.Vector3f value) {
            float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
            super.SetThisNormal(index*2+0,_PBJtempArray[0]);
            super.SetThisNormal(index*2+1,_PBJtempArray[1]);
            return this;
        }
        public Builder ClearImpulse() { super.ClearImpulse();return this;}
        public Builder SetImpulse(int index, float value) {
            super.SetImpulse(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int ImpulseFieldTag=6;
        public int ImpulseCount { get { return super.ImpulseCount;} }
        public bool HasImpulse(int index) {return PBJ._PBJ.ValidateFloat(super.GetImpulse(index));}
        public float Impulse(int index) {
            return (float)PBJ._PBJ.CastFloat(super.GetImpulse(index));
        }
        public Builder AddImpulse(float value) {
            super.AddImpulse(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearOtherObjectReference() { super.ClearOtherObjectReference();return this;}
        public const int OtherObjectReferenceFieldTag=7;
        public bool HasOtherObjectReference{ get {return super.HasOtherObjectReference&&PBJ._PBJ.ValidateUuid(super.OtherObjectReference);} }
        public PBJ.UUID OtherObjectReference{ get {
            if (HasOtherObjectReference) {
                return PBJ._PBJ.CastUuid(super.OtherObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.OtherObjectReference=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Physics.Protocol {
    public class CollisionEnd : PBJ.IMessage {
        protected _PBJ_Internal.CollisionEnd super;
        public _PBJ_Internal.CollisionEnd _PBJSuper{ get { return super;} }
        public CollisionEnd() {
            super=new _PBJ_Internal.CollisionEnd();
        }
        public CollisionEnd(_PBJ_Internal.CollisionEnd reference) {
            super=reference;
        }
        public static CollisionEnd defaultInstance= new CollisionEnd (_PBJ_Internal.CollisionEnd.DefaultInstance);
        public static CollisionEnd DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.CollisionEnd.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int TimestampFieldTag=2;
        public bool HasTimestamp{ get {return super.HasTimestamp&&PBJ._PBJ.ValidateTime(super.Timestamp);} }
        public PBJ.Time Timestamp{ get {
            if (HasTimestamp) {
                return PBJ._PBJ.CastTime(super.Timestamp);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        }
        public const int OtherObjectReferenceFieldTag=6;
        public bool HasOtherObjectReference{ get {return super.HasOtherObjectReference&&PBJ._PBJ.ValidateUuid(super.OtherObjectReference);} }
        public PBJ.UUID OtherObjectReference{ get {
            if (HasOtherObjectReference) {
                return PBJ._PBJ.CastUuid(super.OtherObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(CollisionEnd prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static CollisionEnd ParseFrom(pb::ByteString data) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data));
        }
        public static CollisionEnd ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data,er));
        }
        public static CollisionEnd ParseFrom(byte[] data) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data));
        }
        public static CollisionEnd ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data,er));
        }
        public static CollisionEnd ParseFrom(global::System.IO.Stream data) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data));
        }
        public static CollisionEnd ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data,er));
        }
        public static CollisionEnd ParseFrom(pb::CodedInputStream data) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data));
        }
        public static CollisionEnd ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new CollisionEnd(_PBJ_Internal.CollisionEnd.ParseFrom(data,er));
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
            protected _PBJ_Internal.CollisionEnd.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.CollisionEnd.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.CollisionEnd.Builder();}
            public Builder(_PBJ_Internal.CollisionEnd.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(CollisionEnd prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public CollisionEnd BuildPartial() {return new CollisionEnd(super.BuildPartial());}
            public CollisionEnd Build() {if (_HasAllPBJFields) return new CollisionEnd(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return CollisionEnd.Descriptor; }            }
        public Builder ClearTimestamp() { super.ClearTimestamp();return this;}
        public const int TimestampFieldTag=2;
        public bool HasTimestamp{ get {return super.HasTimestamp&&PBJ._PBJ.ValidateTime(super.Timestamp);} }
        public PBJ.Time Timestamp{ get {
            if (HasTimestamp) {
                return PBJ._PBJ.CastTime(super.Timestamp);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        set {
            super.Timestamp=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearOtherObjectReference() { super.ClearOtherObjectReference();return this;}
        public const int OtherObjectReferenceFieldTag=6;
        public bool HasOtherObjectReference{ get {return super.HasOtherObjectReference&&PBJ._PBJ.ValidateUuid(super.OtherObjectReference);} }
        public PBJ.UUID OtherObjectReference{ get {
            if (HasOtherObjectReference) {
                return PBJ._PBJ.CastUuid(super.OtherObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.OtherObjectReference=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
