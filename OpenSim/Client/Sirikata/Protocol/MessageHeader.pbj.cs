using pbd = global::Google.ProtocolBuffers.Descriptors;
using pb = global::Google.ProtocolBuffers;
namespace Sirikata.Protocol {
    public class Header : PBJ.IMessage {
        protected _PBJ_Internal.Header super;
        public _PBJ_Internal.Header _PBJSuper{ get { return super;} }
        public Header() {
            super=new _PBJ_Internal.Header();
        }
        public Header(_PBJ_Internal.Header reference) {
            super=reference;
        }
        public static Header defaultInstance= new Header (_PBJ_Internal.Header.DefaultInstance);
        public static Header DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.Header.Descriptor; }        }
        public static class Types {
        public enum ReturnStatus {
            SUCCESS=_PBJ_Internal.Header.Types.ReturnStatus.SUCCESS,
            NETWORK_FAILURE=_PBJ_Internal.Header.Types.ReturnStatus.NETWORK_FAILURE,
            TIMEOUT_FAILURE=_PBJ_Internal.Header.Types.ReturnStatus.TIMEOUT_FAILURE,
            PROTOCOL_ERROR=_PBJ_Internal.Header.Types.ReturnStatus.PROTOCOL_ERROR,
            PORT_FAILURE=_PBJ_Internal.Header.Types.ReturnStatus.PORT_FAILURE,
            UNKNOWN_OBJECT=_PBJ_Internal.Header.Types.ReturnStatus.UNKNOWN_OBJECT
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int SourceObjectFieldTag=1;
        public bool HasSourceObject{ get {return super.HasSourceObject&&PBJ._PBJ.ValidateUuid(super.SourceObject);} }
        public PBJ.UUID SourceObject{ get {
            if (HasSourceObject) {
                return PBJ._PBJ.CastUuid(super.SourceObject);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int SourcePortFieldTag=3;
        public bool HasSourcePort{ get {return super.HasSourcePort&&PBJ._PBJ.ValidateUint32(super.SourcePort);} }
        public uint SourcePort{ get {
            if (HasSourcePort) {
                return PBJ._PBJ.CastUint32(super.SourcePort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int SourceSpaceFieldTag=1536;
        public bool HasSourceSpace{ get {return super.HasSourceSpace&&PBJ._PBJ.ValidateUuid(super.SourceSpace);} }
        public PBJ.UUID SourceSpace{ get {
            if (HasSourceSpace) {
                return PBJ._PBJ.CastUuid(super.SourceSpace);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int DestinationObjectFieldTag=2;
        public bool HasDestinationObject{ get {return super.HasDestinationObject&&PBJ._PBJ.ValidateUuid(super.DestinationObject);} }
        public PBJ.UUID DestinationObject{ get {
            if (HasDestinationObject) {
                return PBJ._PBJ.CastUuid(super.DestinationObject);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int DestinationPortFieldTag=4;
        public bool HasDestinationPort{ get {return super.HasDestinationPort&&PBJ._PBJ.ValidateUint32(super.DestinationPort);} }
        public uint DestinationPort{ get {
            if (HasDestinationPort) {
                return PBJ._PBJ.CastUint32(super.DestinationPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int DestinationSpaceFieldTag=1537;
        public bool HasDestinationSpace{ get {return super.HasDestinationSpace&&PBJ._PBJ.ValidateUuid(super.DestinationSpace);} }
        public PBJ.UUID DestinationSpace{ get {
            if (HasDestinationSpace) {
                return PBJ._PBJ.CastUuid(super.DestinationSpace);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int IdFieldTag=7;
        public bool HasId{ get {return super.HasId&&PBJ._PBJ.ValidateInt64(super.Id);} }
        public long Id{ get {
            if (HasId) {
                return PBJ._PBJ.CastInt64(super.Id);
            } else {
                return PBJ._PBJ.CastInt64();
            }
        }
        }
        public const int ReplyIdFieldTag=8;
        public bool HasReplyId{ get {return super.HasReplyId&&PBJ._PBJ.ValidateInt64(super.ReplyId);} }
        public long ReplyId{ get {
            if (HasReplyId) {
                return PBJ._PBJ.CastInt64(super.ReplyId);
            } else {
                return PBJ._PBJ.CastInt64();
            }
        }
        }
        public const int ReturnStatusFieldTag=1792;
        public bool HasReturnStatus{ get {return super.HasReturnStatus;} }
        public Types.ReturnStatus ReturnStatus{ get {
            if (HasReturnStatus) {
                return (Types.ReturnStatus)super.ReturnStatus;
            } else {
                return new Types.ReturnStatus();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(Header prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static Header ParseFrom(pb::ByteString data) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data));
        }
        public static Header ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data,er));
        }
        public static Header ParseFrom(byte[] data) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data));
        }
        public static Header ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data,er));
        }
        public static Header ParseFrom(global::System.IO.Stream data) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data));
        }
        public static Header ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data,er));
        }
        public static Header ParseFrom(pb::CodedInputStream data) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data));
        }
        public static Header ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new Header(_PBJ_Internal.Header.ParseFrom(data,er));
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
            protected _PBJ_Internal.Header.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.Header.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.Header.Builder();}
            public Builder(_PBJ_Internal.Header.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(Header prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public Header BuildPartial() {return new Header(super.BuildPartial());}
            public Header Build() {if (_HasAllPBJFields) return new Header(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return Header.Descriptor; }            }
        public Builder ClearSourceObject() { super.ClearSourceObject();return this;}
        public const int SourceObjectFieldTag=1;
        public bool HasSourceObject{ get {return super.HasSourceObject&&PBJ._PBJ.ValidateUuid(super.SourceObject);} }
        public PBJ.UUID SourceObject{ get {
            if (HasSourceObject) {
                return PBJ._PBJ.CastUuid(super.SourceObject);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.SourceObject=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearSourcePort() { super.ClearSourcePort();return this;}
        public const int SourcePortFieldTag=3;
        public bool HasSourcePort{ get {return super.HasSourcePort&&PBJ._PBJ.ValidateUint32(super.SourcePort);} }
        public uint SourcePort{ get {
            if (HasSourcePort) {
                return PBJ._PBJ.CastUint32(super.SourcePort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.SourcePort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearSourceSpace() { super.ClearSourceSpace();return this;}
        public const int SourceSpaceFieldTag=1536;
        public bool HasSourceSpace{ get {return super.HasSourceSpace&&PBJ._PBJ.ValidateUuid(super.SourceSpace);} }
        public PBJ.UUID SourceSpace{ get {
            if (HasSourceSpace) {
                return PBJ._PBJ.CastUuid(super.SourceSpace);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.SourceSpace=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearDestinationObject() { super.ClearDestinationObject();return this;}
        public const int DestinationObjectFieldTag=2;
        public bool HasDestinationObject{ get {return super.HasDestinationObject&&PBJ._PBJ.ValidateUuid(super.DestinationObject);} }
        public PBJ.UUID DestinationObject{ get {
            if (HasDestinationObject) {
                return PBJ._PBJ.CastUuid(super.DestinationObject);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.DestinationObject=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearDestinationPort() { super.ClearDestinationPort();return this;}
        public const int DestinationPortFieldTag=4;
        public bool HasDestinationPort{ get {return super.HasDestinationPort&&PBJ._PBJ.ValidateUint32(super.DestinationPort);} }
        public uint DestinationPort{ get {
            if (HasDestinationPort) {
                return PBJ._PBJ.CastUint32(super.DestinationPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.DestinationPort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearDestinationSpace() { super.ClearDestinationSpace();return this;}
        public const int DestinationSpaceFieldTag=1537;
        public bool HasDestinationSpace{ get {return super.HasDestinationSpace&&PBJ._PBJ.ValidateUuid(super.DestinationSpace);} }
        public PBJ.UUID DestinationSpace{ get {
            if (HasDestinationSpace) {
                return PBJ._PBJ.CastUuid(super.DestinationSpace);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.DestinationSpace=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearId() { super.ClearId();return this;}
        public const int IdFieldTag=7;
        public bool HasId{ get {return super.HasId&&PBJ._PBJ.ValidateInt64(super.Id);} }
        public long Id{ get {
            if (HasId) {
                return PBJ._PBJ.CastInt64(super.Id);
            } else {
                return PBJ._PBJ.CastInt64();
            }
        }
        set {
            super.Id=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearReplyId() { super.ClearReplyId();return this;}
        public const int ReplyIdFieldTag=8;
        public bool HasReplyId{ get {return super.HasReplyId&&PBJ._PBJ.ValidateInt64(super.ReplyId);} }
        public long ReplyId{ get {
            if (HasReplyId) {
                return PBJ._PBJ.CastInt64(super.ReplyId);
            } else {
                return PBJ._PBJ.CastInt64();
            }
        }
        set {
            super.ReplyId=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearReturnStatus() { super.ClearReturnStatus();return this;}
        public const int ReturnStatusFieldTag=1792;
        public bool HasReturnStatus{ get {return super.HasReturnStatus;} }
        public Types.ReturnStatus ReturnStatus{ get {
            if (HasReturnStatus) {
                return (Types.ReturnStatus)super.ReturnStatus;
            } else {
                return new Types.ReturnStatus();
            }
        }
        set {
            super.ReturnStatus=((_PBJ_Internal.Header.Types.ReturnStatus)value);
        }
        }
        }
    }
}
