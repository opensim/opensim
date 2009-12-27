using pbd = global::Google.ProtocolBuffers.Descriptors;
using pb = global::Google.ProtocolBuffers;
namespace Sirikata.Protocol {
    public class MessageBody : PBJ.IMessage {
        protected _PBJ_Internal.MessageBody super;
        public _PBJ_Internal.MessageBody _PBJSuper{ get { return super;} }
        public MessageBody() {
            super=new _PBJ_Internal.MessageBody();
        }
        public MessageBody(_PBJ_Internal.MessageBody reference) {
            super=reference;
        }
        public static MessageBody defaultInstance= new MessageBody (_PBJ_Internal.MessageBody.DefaultInstance);
        public static MessageBody DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.MessageBody.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int MessageNamesFieldTag=9;
        public int MessageNamesCount { get { return super.MessageNamesCount;} }
        public bool HasMessageNames(int index) {return PBJ._PBJ.ValidateString(super.GetMessageNames(index));}
        public string MessageNames(int index) {
            return (string)PBJ._PBJ.CastString(super.GetMessageNames(index));
        }
        public const int MessageArgumentsFieldTag=10;
        public int MessageArgumentsCount { get { return super.MessageArgumentsCount;} }
        public bool HasMessageArguments(int index) {return PBJ._PBJ.ValidateBytes(super.GetMessageArguments(index));}
        public pb::ByteString MessageArguments(int index) {
            return (pb::ByteString)PBJ._PBJ.CastBytes(super.GetMessageArguments(index));
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(MessageBody prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static MessageBody ParseFrom(pb::ByteString data) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data));
        }
        public static MessageBody ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data,er));
        }
        public static MessageBody ParseFrom(byte[] data) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data));
        }
        public static MessageBody ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data,er));
        }
        public static MessageBody ParseFrom(global::System.IO.Stream data) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data));
        }
        public static MessageBody ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data,er));
        }
        public static MessageBody ParseFrom(pb::CodedInputStream data) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data));
        }
        public static MessageBody ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new MessageBody(_PBJ_Internal.MessageBody.ParseFrom(data,er));
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
            protected _PBJ_Internal.MessageBody.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.MessageBody.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.MessageBody.Builder();}
            public Builder(_PBJ_Internal.MessageBody.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(MessageBody prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public MessageBody BuildPartial() {return new MessageBody(super.BuildPartial());}
            public MessageBody Build() {if (_HasAllPBJFields) return new MessageBody(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return MessageBody.Descriptor; }            }
        public Builder ClearMessageNames() { super.ClearMessageNames();return this;}
        public Builder SetMessageNames(int index, string value) {
            super.SetMessageNames(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int MessageNamesFieldTag=9;
        public int MessageNamesCount { get { return super.MessageNamesCount;} }
        public bool HasMessageNames(int index) {return PBJ._PBJ.ValidateString(super.GetMessageNames(index));}
        public string MessageNames(int index) {
            return (string)PBJ._PBJ.CastString(super.GetMessageNames(index));
        }
        public Builder AddMessageNames(string value) {
            super.AddMessageNames(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearMessageArguments() { super.ClearMessageArguments();return this;}
        public Builder SetMessageArguments(int index, pb::ByteString value) {
            super.SetMessageArguments(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int MessageArgumentsFieldTag=10;
        public int MessageArgumentsCount { get { return super.MessageArgumentsCount;} }
        public bool HasMessageArguments(int index) {return PBJ._PBJ.ValidateBytes(super.GetMessageArguments(index));}
        public pb::ByteString MessageArguments(int index) {
            return (pb::ByteString)PBJ._PBJ.CastBytes(super.GetMessageArguments(index));
        }
        public Builder AddMessageArguments(pb::ByteString value) {
            super.AddMessageArguments(PBJ._PBJ.Construct(value));
            return this;
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class ReadOnlyMessage : PBJ.IMessage {
        protected _PBJ_Internal.ReadOnlyMessage super;
        public _PBJ_Internal.ReadOnlyMessage _PBJSuper{ get { return super;} }
        public ReadOnlyMessage() {
            super=new _PBJ_Internal.ReadOnlyMessage();
        }
        public ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage reference) {
            super=reference;
        }
        public static ReadOnlyMessage defaultInstance= new ReadOnlyMessage (_PBJ_Internal.ReadOnlyMessage.DefaultInstance);
        public static ReadOnlyMessage DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ReadOnlyMessage.Descriptor; }        }
        public static class Types {
        public enum ReturnStatus {
            SUCCESS=_PBJ_Internal.ReadOnlyMessage.Types.ReturnStatus.SUCCESS,
            NETWORK_FAILURE=_PBJ_Internal.ReadOnlyMessage.Types.ReturnStatus.NETWORK_FAILURE,
            TIMEOUT_FAILURE=_PBJ_Internal.ReadOnlyMessage.Types.ReturnStatus.TIMEOUT_FAILURE,
            PROTOCOL_ERROR=_PBJ_Internal.ReadOnlyMessage.Types.ReturnStatus.PROTOCOL_ERROR,
            PORT_FAILURE=_PBJ_Internal.ReadOnlyMessage.Types.ReturnStatus.PORT_FAILURE
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
        public const int MessageNamesFieldTag=9;
        public int MessageNamesCount { get { return super.MessageNamesCount;} }
        public bool HasMessageNames(int index) {return PBJ._PBJ.ValidateString(super.GetMessageNames(index));}
        public string MessageNames(int index) {
            return (string)PBJ._PBJ.CastString(super.GetMessageNames(index));
        }
        public const int MessageArgumentsFieldTag=10;
        public int MessageArgumentsCount { get { return super.MessageArgumentsCount;} }
        public bool HasMessageArguments(int index) {return PBJ._PBJ.ValidateBytes(super.GetMessageArguments(index));}
        public pb::ByteString MessageArguments(int index) {
            return (pb::ByteString)PBJ._PBJ.CastBytes(super.GetMessageArguments(index));
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ReadOnlyMessage prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ReadOnlyMessage ParseFrom(pb::ByteString data) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data));
        }
        public static ReadOnlyMessage ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data,er));
        }
        public static ReadOnlyMessage ParseFrom(byte[] data) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data));
        }
        public static ReadOnlyMessage ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data,er));
        }
        public static ReadOnlyMessage ParseFrom(global::System.IO.Stream data) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data));
        }
        public static ReadOnlyMessage ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data,er));
        }
        public static ReadOnlyMessage ParseFrom(pb::CodedInputStream data) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data));
        }
        public static ReadOnlyMessage ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ReadOnlyMessage(_PBJ_Internal.ReadOnlyMessage.ParseFrom(data,er));
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
            protected _PBJ_Internal.ReadOnlyMessage.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ReadOnlyMessage.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ReadOnlyMessage.Builder();}
            public Builder(_PBJ_Internal.ReadOnlyMessage.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ReadOnlyMessage prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ReadOnlyMessage BuildPartial() {return new ReadOnlyMessage(super.BuildPartial());}
            public ReadOnlyMessage Build() {if (_HasAllPBJFields) return new ReadOnlyMessage(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ReadOnlyMessage.Descriptor; }            }
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
            super.ReturnStatus=((_PBJ_Internal.ReadOnlyMessage.Types.ReturnStatus)value);
        }
        }
        public Builder ClearMessageNames() { super.ClearMessageNames();return this;}
        public Builder SetMessageNames(int index, string value) {
            super.SetMessageNames(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int MessageNamesFieldTag=9;
        public int MessageNamesCount { get { return super.MessageNamesCount;} }
        public bool HasMessageNames(int index) {return PBJ._PBJ.ValidateString(super.GetMessageNames(index));}
        public string MessageNames(int index) {
            return (string)PBJ._PBJ.CastString(super.GetMessageNames(index));
        }
        public Builder AddMessageNames(string value) {
            super.AddMessageNames(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearMessageArguments() { super.ClearMessageArguments();return this;}
        public Builder SetMessageArguments(int index, pb::ByteString value) {
            super.SetMessageArguments(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int MessageArgumentsFieldTag=10;
        public int MessageArgumentsCount { get { return super.MessageArgumentsCount;} }
        public bool HasMessageArguments(int index) {return PBJ._PBJ.ValidateBytes(super.GetMessageArguments(index));}
        public pb::ByteString MessageArguments(int index) {
            return (pb::ByteString)PBJ._PBJ.CastBytes(super.GetMessageArguments(index));
        }
        public Builder AddMessageArguments(pb::ByteString value) {
            super.AddMessageArguments(PBJ._PBJ.Construct(value));
            return this;
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class SpaceServices : PBJ.IMessage {
        protected _PBJ_Internal.SpaceServices super;
        public _PBJ_Internal.SpaceServices _PBJSuper{ get { return super;} }
        public SpaceServices() {
            super=new _PBJ_Internal.SpaceServices();
        }
        public SpaceServices(_PBJ_Internal.SpaceServices reference) {
            super=reference;
        }
        public static SpaceServices defaultInstance= new SpaceServices (_PBJ_Internal.SpaceServices.DefaultInstance);
        public static SpaceServices DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.SpaceServices.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int RegistrationPortFieldTag=33;
        public bool HasRegistrationPort{ get {return super.HasRegistrationPort&&PBJ._PBJ.ValidateUint32(super.RegistrationPort);} }
        public uint RegistrationPort{ get {
            if (HasRegistrationPort) {
                return PBJ._PBJ.CastUint32(super.RegistrationPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int LocPortFieldTag=34;
        public bool HasLocPort{ get {return super.HasLocPort&&PBJ._PBJ.ValidateUint32(super.LocPort);} }
        public uint LocPort{ get {
            if (HasLocPort) {
                return PBJ._PBJ.CastUint32(super.LocPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int GeomPortFieldTag=35;
        public bool HasGeomPort{ get {return super.HasGeomPort&&PBJ._PBJ.ValidateUint32(super.GeomPort);} }
        public uint GeomPort{ get {
            if (HasGeomPort) {
                return PBJ._PBJ.CastUint32(super.GeomPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int OsegPortFieldTag=36;
        public bool HasOsegPort{ get {return super.HasOsegPort&&PBJ._PBJ.ValidateUint32(super.OsegPort);} }
        public uint OsegPort{ get {
            if (HasOsegPort) {
                return PBJ._PBJ.CastUint32(super.OsegPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int CsegPortFieldTag=37;
        public bool HasCsegPort{ get {return super.HasCsegPort&&PBJ._PBJ.ValidateUint32(super.CsegPort);} }
        public uint CsegPort{ get {
            if (HasCsegPort) {
                return PBJ._PBJ.CastUint32(super.CsegPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int RouterPortFieldTag=38;
        public bool HasRouterPort{ get {return super.HasRouterPort&&PBJ._PBJ.ValidateUint32(super.RouterPort);} }
        public uint RouterPort{ get {
            if (HasRouterPort) {
                return PBJ._PBJ.CastUint32(super.RouterPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int PreConnectionBufferFieldTag=64;
        public bool HasPreConnectionBuffer{ get {return super.HasPreConnectionBuffer&&PBJ._PBJ.ValidateUint64(super.PreConnectionBuffer);} }
        public ulong PreConnectionBuffer{ get {
            if (HasPreConnectionBuffer) {
                return PBJ._PBJ.CastUint64(super.PreConnectionBuffer);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        }
        public const int MaxPreConnectionMessagesFieldTag=65;
        public bool HasMaxPreConnectionMessages{ get {return super.HasMaxPreConnectionMessages&&PBJ._PBJ.ValidateUint64(super.MaxPreConnectionMessages);} }
        public ulong MaxPreConnectionMessages{ get {
            if (HasMaxPreConnectionMessages) {
                return PBJ._PBJ.CastUint64(super.MaxPreConnectionMessages);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(SpaceServices prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static SpaceServices ParseFrom(pb::ByteString data) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data));
        }
        public static SpaceServices ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data,er));
        }
        public static SpaceServices ParseFrom(byte[] data) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data));
        }
        public static SpaceServices ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data,er));
        }
        public static SpaceServices ParseFrom(global::System.IO.Stream data) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data));
        }
        public static SpaceServices ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data,er));
        }
        public static SpaceServices ParseFrom(pb::CodedInputStream data) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data));
        }
        public static SpaceServices ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new SpaceServices(_PBJ_Internal.SpaceServices.ParseFrom(data,er));
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
            protected _PBJ_Internal.SpaceServices.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.SpaceServices.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.SpaceServices.Builder();}
            public Builder(_PBJ_Internal.SpaceServices.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(SpaceServices prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public SpaceServices BuildPartial() {return new SpaceServices(super.BuildPartial());}
            public SpaceServices Build() {if (_HasAllPBJFields) return new SpaceServices(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return SpaceServices.Descriptor; }            }
        public Builder ClearRegistrationPort() { super.ClearRegistrationPort();return this;}
        public const int RegistrationPortFieldTag=33;
        public bool HasRegistrationPort{ get {return super.HasRegistrationPort&&PBJ._PBJ.ValidateUint32(super.RegistrationPort);} }
        public uint RegistrationPort{ get {
            if (HasRegistrationPort) {
                return PBJ._PBJ.CastUint32(super.RegistrationPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.RegistrationPort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearLocPort() { super.ClearLocPort();return this;}
        public const int LocPortFieldTag=34;
        public bool HasLocPort{ get {return super.HasLocPort&&PBJ._PBJ.ValidateUint32(super.LocPort);} }
        public uint LocPort{ get {
            if (HasLocPort) {
                return PBJ._PBJ.CastUint32(super.LocPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.LocPort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearGeomPort() { super.ClearGeomPort();return this;}
        public const int GeomPortFieldTag=35;
        public bool HasGeomPort{ get {return super.HasGeomPort&&PBJ._PBJ.ValidateUint32(super.GeomPort);} }
        public uint GeomPort{ get {
            if (HasGeomPort) {
                return PBJ._PBJ.CastUint32(super.GeomPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.GeomPort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearOsegPort() { super.ClearOsegPort();return this;}
        public const int OsegPortFieldTag=36;
        public bool HasOsegPort{ get {return super.HasOsegPort&&PBJ._PBJ.ValidateUint32(super.OsegPort);} }
        public uint OsegPort{ get {
            if (HasOsegPort) {
                return PBJ._PBJ.CastUint32(super.OsegPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.OsegPort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearCsegPort() { super.ClearCsegPort();return this;}
        public const int CsegPortFieldTag=37;
        public bool HasCsegPort{ get {return super.HasCsegPort&&PBJ._PBJ.ValidateUint32(super.CsegPort);} }
        public uint CsegPort{ get {
            if (HasCsegPort) {
                return PBJ._PBJ.CastUint32(super.CsegPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.CsegPort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearRouterPort() { super.ClearRouterPort();return this;}
        public const int RouterPortFieldTag=38;
        public bool HasRouterPort{ get {return super.HasRouterPort&&PBJ._PBJ.ValidateUint32(super.RouterPort);} }
        public uint RouterPort{ get {
            if (HasRouterPort) {
                return PBJ._PBJ.CastUint32(super.RouterPort);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.RouterPort=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearPreConnectionBuffer() { super.ClearPreConnectionBuffer();return this;}
        public const int PreConnectionBufferFieldTag=64;
        public bool HasPreConnectionBuffer{ get {return super.HasPreConnectionBuffer&&PBJ._PBJ.ValidateUint64(super.PreConnectionBuffer);} }
        public ulong PreConnectionBuffer{ get {
            if (HasPreConnectionBuffer) {
                return PBJ._PBJ.CastUint64(super.PreConnectionBuffer);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        set {
            super.PreConnectionBuffer=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearMaxPreConnectionMessages() { super.ClearMaxPreConnectionMessages();return this;}
        public const int MaxPreConnectionMessagesFieldTag=65;
        public bool HasMaxPreConnectionMessages{ get {return super.HasMaxPreConnectionMessages&&PBJ._PBJ.ValidateUint64(super.MaxPreConnectionMessages);} }
        public ulong MaxPreConnectionMessages{ get {
            if (HasMaxPreConnectionMessages) {
                return PBJ._PBJ.CastUint64(super.MaxPreConnectionMessages);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        set {
            super.MaxPreConnectionMessages=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class ObjLoc : PBJ.IMessage {
        protected _PBJ_Internal.ObjLoc super;
        public _PBJ_Internal.ObjLoc _PBJSuper{ get { return super;} }
        public ObjLoc() {
            super=new _PBJ_Internal.ObjLoc();
        }
        public ObjLoc(_PBJ_Internal.ObjLoc reference) {
            super=reference;
        }
        public static ObjLoc defaultInstance= new ObjLoc (_PBJ_Internal.ObjLoc.DefaultInstance);
        public static ObjLoc DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ObjLoc.Descriptor; }        }
        public static class Types {
        public enum UpdateFlags {
            FORCE=_PBJ_Internal.ObjLoc.Types.UpdateFlags.FORCE
        };
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
        public const int PositionFieldTag=3;
        public bool HasPosition{ get {return super.PositionCount>=3;} }
        public PBJ.Vector3d Position{ get  {
            int index=0;
            if (HasPosition) {
                return PBJ._PBJ.CastVector3d(super.GetPosition(index*3+0),super.GetPosition(index*3+1),super.GetPosition(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        }
        public const int OrientationFieldTag=4;
        public bool HasOrientation{ get {return super.OrientationCount>=3;} }
        public PBJ.Quaternion Orientation{ get  {
            int index=0;
            if (HasOrientation) {
                return PBJ._PBJ.CastQuaternion(super.GetOrientation(index*3+0),super.GetOrientation(index*3+1),super.GetOrientation(index*3+2));
            } else {
                return PBJ._PBJ.CastQuaternion();
            }
        }
        }
        public const int VelocityFieldTag=5;
        public bool HasVelocity{ get {return super.VelocityCount>=3;} }
        public PBJ.Vector3f Velocity{ get  {
            int index=0;
            if (HasVelocity) {
                return PBJ._PBJ.CastVector3f(super.GetVelocity(index*3+0),super.GetVelocity(index*3+1),super.GetVelocity(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int RotationalAxisFieldTag=7;
        public bool HasRotationalAxis{ get {return super.RotationalAxisCount>=2;} }
        public PBJ.Vector3f RotationalAxis{ get  {
            int index=0;
            if (HasRotationalAxis) {
                return PBJ._PBJ.CastNormal(super.GetRotationalAxis(index*2+0),super.GetRotationalAxis(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        }
        public const int AngularSpeedFieldTag=8;
        public bool HasAngularSpeed{ get {return super.HasAngularSpeed&&PBJ._PBJ.ValidateFloat(super.AngularSpeed);} }
        public float AngularSpeed{ get {
            if (HasAngularSpeed) {
                return PBJ._PBJ.CastFloat(super.AngularSpeed);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int UpdateFlagsFieldTag=6;
        public bool HasUpdateFlags { get {
            if (!super.HasUpdateFlags) return false;
            return PBJ._PBJ.ValidateFlags(super.UpdateFlags,(ulong)Types.UpdateFlags.FORCE);
        } }
        public byte UpdateFlags{ get {
            if (HasUpdateFlags) {
                return (byte)PBJ._PBJ.CastFlags(super.UpdateFlags,(ulong)Types.UpdateFlags.FORCE);
            } else {
                return (byte)PBJ._PBJ.CastFlags((ulong)Types.UpdateFlags.FORCE);
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ObjLoc prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ObjLoc ParseFrom(pb::ByteString data) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data));
        }
        public static ObjLoc ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data,er));
        }
        public static ObjLoc ParseFrom(byte[] data) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data));
        }
        public static ObjLoc ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data,er));
        }
        public static ObjLoc ParseFrom(global::System.IO.Stream data) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data));
        }
        public static ObjLoc ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data,er));
        }
        public static ObjLoc ParseFrom(pb::CodedInputStream data) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data));
        }
        public static ObjLoc ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ObjLoc(_PBJ_Internal.ObjLoc.ParseFrom(data,er));
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
            protected _PBJ_Internal.ObjLoc.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ObjLoc.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ObjLoc.Builder();}
            public Builder(_PBJ_Internal.ObjLoc.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ObjLoc prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ObjLoc BuildPartial() {return new ObjLoc(super.BuildPartial());}
            public ObjLoc Build() {if (_HasAllPBJFields) return new ObjLoc(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ObjLoc.Descriptor; }            }
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
        public Builder ClearPosition() { super.ClearPosition();return this;}
        public const int PositionFieldTag=3;
        public bool HasPosition{ get {return super.PositionCount>=3;} }
        public PBJ.Vector3d Position{ get  {
            int index=0;
            if (HasPosition) {
                return PBJ._PBJ.CastVector3d(super.GetPosition(index*3+0),super.GetPosition(index*3+1),super.GetPosition(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        set {
            super.ClearPosition();
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
            super.AddPosition(_PBJtempArray[0]);
            super.AddPosition(_PBJtempArray[1]);
            super.AddPosition(_PBJtempArray[2]);
        }
        }
        public Builder ClearOrientation() { super.ClearOrientation();return this;}
        public const int OrientationFieldTag=4;
        public bool HasOrientation{ get {return super.OrientationCount>=3;} }
        public PBJ.Quaternion Orientation{ get  {
            int index=0;
            if (HasOrientation) {
                return PBJ._PBJ.CastQuaternion(super.GetOrientation(index*3+0),super.GetOrientation(index*3+1),super.GetOrientation(index*3+2));
            } else {
                return PBJ._PBJ.CastQuaternion();
            }
        }
        set {
            super.ClearOrientation();
            float[] _PBJtempArray=PBJ._PBJ.ConstructQuaternion(value);
            super.AddOrientation(_PBJtempArray[0]);
            super.AddOrientation(_PBJtempArray[1]);
            super.AddOrientation(_PBJtempArray[2]);
        }
        }
        public Builder ClearVelocity() { super.ClearVelocity();return this;}
        public const int VelocityFieldTag=5;
        public bool HasVelocity{ get {return super.VelocityCount>=3;} }
        public PBJ.Vector3f Velocity{ get  {
            int index=0;
            if (HasVelocity) {
                return PBJ._PBJ.CastVector3f(super.GetVelocity(index*3+0),super.GetVelocity(index*3+1),super.GetVelocity(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearVelocity();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddVelocity(_PBJtempArray[0]);
            super.AddVelocity(_PBJtempArray[1]);
            super.AddVelocity(_PBJtempArray[2]);
        }
        }
        public Builder ClearRotationalAxis() { super.ClearRotationalAxis();return this;}
        public const int RotationalAxisFieldTag=7;
        public bool HasRotationalAxis{ get {return super.RotationalAxisCount>=2;} }
        public PBJ.Vector3f RotationalAxis{ get  {
            int index=0;
            if (HasRotationalAxis) {
                return PBJ._PBJ.CastNormal(super.GetRotationalAxis(index*2+0),super.GetRotationalAxis(index*2+1));
            } else {
                return PBJ._PBJ.CastNormal();
            }
        }
        set {
            super.ClearRotationalAxis();
            float[] _PBJtempArray=PBJ._PBJ.ConstructNormal(value);
            super.AddRotationalAxis(_PBJtempArray[0]);
            super.AddRotationalAxis(_PBJtempArray[1]);
        }
        }
        public Builder ClearAngularSpeed() { super.ClearAngularSpeed();return this;}
        public const int AngularSpeedFieldTag=8;
        public bool HasAngularSpeed{ get {return super.HasAngularSpeed&&PBJ._PBJ.ValidateFloat(super.AngularSpeed);} }
        public float AngularSpeed{ get {
            if (HasAngularSpeed) {
                return PBJ._PBJ.CastFloat(super.AngularSpeed);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.AngularSpeed=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearUpdateFlags() { super.ClearUpdateFlags();return this;}
        public const int UpdateFlagsFieldTag=6;
        public bool HasUpdateFlags { get {
            if (!super.HasUpdateFlags) return false;
            return PBJ._PBJ.ValidateFlags(super.UpdateFlags,(ulong)Types.UpdateFlags.FORCE);
        } }
        public byte UpdateFlags{ get {
            if (HasUpdateFlags) {
                return (byte)PBJ._PBJ.CastFlags(super.UpdateFlags,(ulong)Types.UpdateFlags.FORCE);
            } else {
                return (byte)PBJ._PBJ.CastFlags((ulong)Types.UpdateFlags.FORCE);
            }
        }
        set {
            super.UpdateFlags=((value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class LocRequest : PBJ.IMessage {
        protected _PBJ_Internal.LocRequest super;
        public _PBJ_Internal.LocRequest _PBJSuper{ get { return super;} }
        public LocRequest() {
            super=new _PBJ_Internal.LocRequest();
        }
        public LocRequest(_PBJ_Internal.LocRequest reference) {
            super=reference;
        }
        public static LocRequest defaultInstance= new LocRequest (_PBJ_Internal.LocRequest.DefaultInstance);
        public static LocRequest DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.LocRequest.Descriptor; }        }
        public static class Types {
        public enum Fields {
            POSITION=_PBJ_Internal.LocRequest.Types.Fields.POSITION,
            ORIENTATION=_PBJ_Internal.LocRequest.Types.Fields.ORIENTATION,
            VELOCITY=_PBJ_Internal.LocRequest.Types.Fields.VELOCITY,
            ROTATIONAL_AXIS=_PBJ_Internal.LocRequest.Types.Fields.ROTATIONAL_AXIS,
            ANGULAR_SPEED=_PBJ_Internal.LocRequest.Types.Fields.ANGULAR_SPEED
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int RequestedFieldsFieldTag=2;
        public bool HasRequestedFields { get {
            if (!super.HasRequestedFields) return false;
            return PBJ._PBJ.ValidateFlags(super.RequestedFields,(ulong)Types.Fields.POSITION|(ulong)Types.Fields.ORIENTATION|(ulong)Types.Fields.VELOCITY|(ulong)Types.Fields.ROTATIONAL_AXIS|(ulong)Types.Fields.ANGULAR_SPEED);
        } }
        public uint RequestedFields{ get {
            if (HasRequestedFields) {
                return (uint)PBJ._PBJ.CastFlags(super.RequestedFields,(ulong)Types.Fields.POSITION|(ulong)Types.Fields.ORIENTATION|(ulong)Types.Fields.VELOCITY|(ulong)Types.Fields.ROTATIONAL_AXIS|(ulong)Types.Fields.ANGULAR_SPEED);
            } else {
                return (uint)PBJ._PBJ.CastFlags((ulong)Types.Fields.POSITION|(ulong)Types.Fields.ORIENTATION|(ulong)Types.Fields.VELOCITY|(ulong)Types.Fields.ROTATIONAL_AXIS|(ulong)Types.Fields.ANGULAR_SPEED);
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(LocRequest prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static LocRequest ParseFrom(pb::ByteString data) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data));
        }
        public static LocRequest ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data,er));
        }
        public static LocRequest ParseFrom(byte[] data) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data));
        }
        public static LocRequest ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data,er));
        }
        public static LocRequest ParseFrom(global::System.IO.Stream data) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data));
        }
        public static LocRequest ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data,er));
        }
        public static LocRequest ParseFrom(pb::CodedInputStream data) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data));
        }
        public static LocRequest ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new LocRequest(_PBJ_Internal.LocRequest.ParseFrom(data,er));
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
            protected _PBJ_Internal.LocRequest.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.LocRequest.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.LocRequest.Builder();}
            public Builder(_PBJ_Internal.LocRequest.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(LocRequest prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public LocRequest BuildPartial() {return new LocRequest(super.BuildPartial());}
            public LocRequest Build() {if (_HasAllPBJFields) return new LocRequest(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return LocRequest.Descriptor; }            }
        public Builder ClearRequestedFields() { super.ClearRequestedFields();return this;}
        public const int RequestedFieldsFieldTag=2;
        public bool HasRequestedFields { get {
            if (!super.HasRequestedFields) return false;
            return PBJ._PBJ.ValidateFlags(super.RequestedFields,(ulong)Types.Fields.POSITION|(ulong)Types.Fields.ORIENTATION|(ulong)Types.Fields.VELOCITY|(ulong)Types.Fields.ROTATIONAL_AXIS|(ulong)Types.Fields.ANGULAR_SPEED);
        } }
        public uint RequestedFields{ get {
            if (HasRequestedFields) {
                return (uint)PBJ._PBJ.CastFlags(super.RequestedFields,(ulong)Types.Fields.POSITION|(ulong)Types.Fields.ORIENTATION|(ulong)Types.Fields.VELOCITY|(ulong)Types.Fields.ROTATIONAL_AXIS|(ulong)Types.Fields.ANGULAR_SPEED);
            } else {
                return (uint)PBJ._PBJ.CastFlags((ulong)Types.Fields.POSITION|(ulong)Types.Fields.ORIENTATION|(ulong)Types.Fields.VELOCITY|(ulong)Types.Fields.ROTATIONAL_AXIS|(ulong)Types.Fields.ANGULAR_SPEED);
            }
        }
        set {
            super.RequestedFields=((value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class NewObj : PBJ.IMessage {
        protected _PBJ_Internal.NewObj super;
        public _PBJ_Internal.NewObj _PBJSuper{ get { return super;} }
        public NewObj() {
            super=new _PBJ_Internal.NewObj();
        }
        public NewObj(_PBJ_Internal.NewObj reference) {
            super=reference;
        }
        public static NewObj defaultInstance= new NewObj (_PBJ_Internal.NewObj.DefaultInstance);
        public static NewObj DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.NewObj.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ObjectUuidEvidenceFieldTag=2;
        public bool HasObjectUuidEvidence{ get {return super.HasObjectUuidEvidence&&PBJ._PBJ.ValidateUuid(super.ObjectUuidEvidence);} }
        public PBJ.UUID ObjectUuidEvidence{ get {
            if (HasObjectUuidEvidence) {
                return PBJ._PBJ.CastUuid(super.ObjectUuidEvidence);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int RequestedObjectLocFieldTag=3;
        public bool HasRequestedObjectLoc{ get {return super.HasRequestedObjectLoc;} }
        public ObjLoc RequestedObjectLoc{ get {
            if (HasRequestedObjectLoc) {
                return new ObjLoc(super.RequestedObjectLoc);
            } else {
                return new ObjLoc();
            }
        }
        }
        public const int BoundingSphereFieldTag=4;
        public bool HasBoundingSphere{ get {return super.BoundingSphereCount>=4;} }
        public PBJ.BoundingSphere3f BoundingSphere{ get  {
            int index=0;
            if (HasBoundingSphere) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBoundingSphere(index*4+0),super.GetBoundingSphere(index*4+1),super.GetBoundingSphere(index*4+2),super.GetBoundingSphere(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(NewObj prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static NewObj ParseFrom(pb::ByteString data) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data));
        }
        public static NewObj ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data,er));
        }
        public static NewObj ParseFrom(byte[] data) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data));
        }
        public static NewObj ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data,er));
        }
        public static NewObj ParseFrom(global::System.IO.Stream data) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data));
        }
        public static NewObj ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data,er));
        }
        public static NewObj ParseFrom(pb::CodedInputStream data) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data));
        }
        public static NewObj ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new NewObj(_PBJ_Internal.NewObj.ParseFrom(data,er));
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
            protected _PBJ_Internal.NewObj.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.NewObj.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.NewObj.Builder();}
            public Builder(_PBJ_Internal.NewObj.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(NewObj prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public NewObj BuildPartial() {return new NewObj(super.BuildPartial());}
            public NewObj Build() {if (_HasAllPBJFields) return new NewObj(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return NewObj.Descriptor; }            }
        public Builder ClearObjectUuidEvidence() { super.ClearObjectUuidEvidence();return this;}
        public const int ObjectUuidEvidenceFieldTag=2;
        public bool HasObjectUuidEvidence{ get {return super.HasObjectUuidEvidence&&PBJ._PBJ.ValidateUuid(super.ObjectUuidEvidence);} }
        public PBJ.UUID ObjectUuidEvidence{ get {
            if (HasObjectUuidEvidence) {
                return PBJ._PBJ.CastUuid(super.ObjectUuidEvidence);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.ObjectUuidEvidence=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearRequestedObjectLoc() { super.ClearRequestedObjectLoc();return this;}
        public const int RequestedObjectLocFieldTag=3;
        public bool HasRequestedObjectLoc{ get {return super.HasRequestedObjectLoc;} }
        public ObjLoc RequestedObjectLoc{ get {
            if (HasRequestedObjectLoc) {
                return new ObjLoc(super.RequestedObjectLoc);
            } else {
                return new ObjLoc();
            }
        }
        set {
            super.RequestedObjectLoc=value._PBJSuper;
        }
        }
        public Builder ClearBoundingSphere() { super.ClearBoundingSphere();return this;}
        public const int BoundingSphereFieldTag=4;
        public bool HasBoundingSphere{ get {return super.BoundingSphereCount>=4;} }
        public PBJ.BoundingSphere3f BoundingSphere{ get  {
            int index=0;
            if (HasBoundingSphere) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBoundingSphere(index*4+0),super.GetBoundingSphere(index*4+1),super.GetBoundingSphere(index*4+2),super.GetBoundingSphere(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        set {
            super.ClearBoundingSphere();
            float[] _PBJtempArray=PBJ._PBJ.ConstructBoundingsphere3f(value);
            super.AddBoundingSphere(_PBJtempArray[0]);
            super.AddBoundingSphere(_PBJtempArray[1]);
            super.AddBoundingSphere(_PBJtempArray[2]);
            super.AddBoundingSphere(_PBJtempArray[3]);
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class RetObj : PBJ.IMessage {
        protected _PBJ_Internal.RetObj super;
        public _PBJ_Internal.RetObj _PBJSuper{ get { return super;} }
        public RetObj() {
            super=new _PBJ_Internal.RetObj();
        }
        public RetObj(_PBJ_Internal.RetObj reference) {
            super=reference;
        }
        public static RetObj defaultInstance= new RetObj (_PBJ_Internal.RetObj.DefaultInstance);
        public static RetObj DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.RetObj.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ObjectReferenceFieldTag=2;
        public bool HasObjectReference{ get {return super.HasObjectReference&&PBJ._PBJ.ValidateUuid(super.ObjectReference);} }
        public PBJ.UUID ObjectReference{ get {
            if (HasObjectReference) {
                return PBJ._PBJ.CastUuid(super.ObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int LocationFieldTag=3;
        public bool HasLocation{ get {return super.HasLocation;} }
        public ObjLoc Location{ get {
            if (HasLocation) {
                return new ObjLoc(super.Location);
            } else {
                return new ObjLoc();
            }
        }
        }
        public const int BoundingSphereFieldTag=4;
        public bool HasBoundingSphere{ get {return super.BoundingSphereCount>=4;} }
        public PBJ.BoundingSphere3f BoundingSphere{ get  {
            int index=0;
            if (HasBoundingSphere) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBoundingSphere(index*4+0),super.GetBoundingSphere(index*4+1),super.GetBoundingSphere(index*4+2),super.GetBoundingSphere(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(RetObj prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static RetObj ParseFrom(pb::ByteString data) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data));
        }
        public static RetObj ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data,er));
        }
        public static RetObj ParseFrom(byte[] data) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data));
        }
        public static RetObj ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data,er));
        }
        public static RetObj ParseFrom(global::System.IO.Stream data) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data));
        }
        public static RetObj ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data,er));
        }
        public static RetObj ParseFrom(pb::CodedInputStream data) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data));
        }
        public static RetObj ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new RetObj(_PBJ_Internal.RetObj.ParseFrom(data,er));
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
            protected _PBJ_Internal.RetObj.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.RetObj.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.RetObj.Builder();}
            public Builder(_PBJ_Internal.RetObj.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(RetObj prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public RetObj BuildPartial() {return new RetObj(super.BuildPartial());}
            public RetObj Build() {if (_HasAllPBJFields) return new RetObj(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return RetObj.Descriptor; }            }
        public Builder ClearObjectReference() { super.ClearObjectReference();return this;}
        public const int ObjectReferenceFieldTag=2;
        public bool HasObjectReference{ get {return super.HasObjectReference&&PBJ._PBJ.ValidateUuid(super.ObjectReference);} }
        public PBJ.UUID ObjectReference{ get {
            if (HasObjectReference) {
                return PBJ._PBJ.CastUuid(super.ObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.ObjectReference=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearLocation() { super.ClearLocation();return this;}
        public const int LocationFieldTag=3;
        public bool HasLocation{ get {return super.HasLocation;} }
        public ObjLoc Location{ get {
            if (HasLocation) {
                return new ObjLoc(super.Location);
            } else {
                return new ObjLoc();
            }
        }
        set {
            super.Location=value._PBJSuper;
        }
        }
        public Builder ClearBoundingSphere() { super.ClearBoundingSphere();return this;}
        public const int BoundingSphereFieldTag=4;
        public bool HasBoundingSphere{ get {return super.BoundingSphereCount>=4;} }
        public PBJ.BoundingSphere3f BoundingSphere{ get  {
            int index=0;
            if (HasBoundingSphere) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBoundingSphere(index*4+0),super.GetBoundingSphere(index*4+1),super.GetBoundingSphere(index*4+2),super.GetBoundingSphere(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        set {
            super.ClearBoundingSphere();
            float[] _PBJtempArray=PBJ._PBJ.ConstructBoundingsphere3f(value);
            super.AddBoundingSphere(_PBJtempArray[0]);
            super.AddBoundingSphere(_PBJtempArray[1]);
            super.AddBoundingSphere(_PBJtempArray[2]);
            super.AddBoundingSphere(_PBJtempArray[3]);
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class DelObj : PBJ.IMessage {
        protected _PBJ_Internal.DelObj super;
        public _PBJ_Internal.DelObj _PBJSuper{ get { return super;} }
        public DelObj() {
            super=new _PBJ_Internal.DelObj();
        }
        public DelObj(_PBJ_Internal.DelObj reference) {
            super=reference;
        }
        public static DelObj defaultInstance= new DelObj (_PBJ_Internal.DelObj.DefaultInstance);
        public static DelObj DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.DelObj.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ObjectReferenceFieldTag=2;
        public bool HasObjectReference{ get {return super.HasObjectReference&&PBJ._PBJ.ValidateUuid(super.ObjectReference);} }
        public PBJ.UUID ObjectReference{ get {
            if (HasObjectReference) {
                return PBJ._PBJ.CastUuid(super.ObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(DelObj prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static DelObj ParseFrom(pb::ByteString data) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data));
        }
        public static DelObj ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data,er));
        }
        public static DelObj ParseFrom(byte[] data) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data));
        }
        public static DelObj ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data,er));
        }
        public static DelObj ParseFrom(global::System.IO.Stream data) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data));
        }
        public static DelObj ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data,er));
        }
        public static DelObj ParseFrom(pb::CodedInputStream data) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data));
        }
        public static DelObj ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new DelObj(_PBJ_Internal.DelObj.ParseFrom(data,er));
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
            protected _PBJ_Internal.DelObj.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.DelObj.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.DelObj.Builder();}
            public Builder(_PBJ_Internal.DelObj.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(DelObj prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public DelObj BuildPartial() {return new DelObj(super.BuildPartial());}
            public DelObj Build() {if (_HasAllPBJFields) return new DelObj(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return DelObj.Descriptor; }            }
        public Builder ClearObjectReference() { super.ClearObjectReference();return this;}
        public const int ObjectReferenceFieldTag=2;
        public bool HasObjectReference{ get {return super.HasObjectReference&&PBJ._PBJ.ValidateUuid(super.ObjectReference);} }
        public PBJ.UUID ObjectReference{ get {
            if (HasObjectReference) {
                return PBJ._PBJ.CastUuid(super.ObjectReference);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.ObjectReference=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class NewProxQuery : PBJ.IMessage {
        protected _PBJ_Internal.NewProxQuery super;
        public _PBJ_Internal.NewProxQuery _PBJSuper{ get { return super;} }
        public NewProxQuery() {
            super=new _PBJ_Internal.NewProxQuery();
        }
        public NewProxQuery(_PBJ_Internal.NewProxQuery reference) {
            super=reference;
        }
        public static NewProxQuery defaultInstance= new NewProxQuery (_PBJ_Internal.NewProxQuery.DefaultInstance);
        public static NewProxQuery DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.NewProxQuery.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int QueryIdFieldTag=2;
        public bool HasQueryId{ get {return super.HasQueryId&&PBJ._PBJ.ValidateUint32(super.QueryId);} }
        public uint QueryId{ get {
            if (HasQueryId) {
                return PBJ._PBJ.CastUint32(super.QueryId);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int StatelessFieldTag=3;
        public bool HasStateless{ get {return super.HasStateless&&PBJ._PBJ.ValidateBool(super.Stateless);} }
        public bool Stateless{ get {
            if (HasStateless) {
                return PBJ._PBJ.CastBool(super.Stateless);
            } else {
                return PBJ._PBJ.CastBool();
            }
        }
        }
        public const int RelativeCenterFieldTag=4;
        public bool HasRelativeCenter{ get {return super.RelativeCenterCount>=3;} }
        public PBJ.Vector3f RelativeCenter{ get  {
            int index=0;
            if (HasRelativeCenter) {
                return PBJ._PBJ.CastVector3f(super.GetRelativeCenter(index*3+0),super.GetRelativeCenter(index*3+1),super.GetRelativeCenter(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int AbsoluteCenterFieldTag=5;
        public bool HasAbsoluteCenter{ get {return super.AbsoluteCenterCount>=3;} }
        public PBJ.Vector3d AbsoluteCenter{ get  {
            int index=0;
            if (HasAbsoluteCenter) {
                return PBJ._PBJ.CastVector3d(super.GetAbsoluteCenter(index*3+0),super.GetAbsoluteCenter(index*3+1),super.GetAbsoluteCenter(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        }
        public const int MaxRadiusFieldTag=6;
        public bool HasMaxRadius{ get {return super.HasMaxRadius&&PBJ._PBJ.ValidateFloat(super.MaxRadius);} }
        public float MaxRadius{ get {
            if (HasMaxRadius) {
                return PBJ._PBJ.CastFloat(super.MaxRadius);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int MinSolidAngleFieldTag=7;
        public bool HasMinSolidAngle{ get {return super.HasMinSolidAngle&&PBJ._PBJ.ValidateAngle(super.MinSolidAngle);} }
        public float MinSolidAngle{ get {
            if (HasMinSolidAngle) {
                return PBJ._PBJ.CastAngle(super.MinSolidAngle);
            } else {
                return PBJ._PBJ.CastAngle();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(NewProxQuery prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static NewProxQuery ParseFrom(pb::ByteString data) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data));
        }
        public static NewProxQuery ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data,er));
        }
        public static NewProxQuery ParseFrom(byte[] data) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data));
        }
        public static NewProxQuery ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data,er));
        }
        public static NewProxQuery ParseFrom(global::System.IO.Stream data) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data));
        }
        public static NewProxQuery ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data,er));
        }
        public static NewProxQuery ParseFrom(pb::CodedInputStream data) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data));
        }
        public static NewProxQuery ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new NewProxQuery(_PBJ_Internal.NewProxQuery.ParseFrom(data,er));
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
            protected _PBJ_Internal.NewProxQuery.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.NewProxQuery.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.NewProxQuery.Builder();}
            public Builder(_PBJ_Internal.NewProxQuery.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(NewProxQuery prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public NewProxQuery BuildPartial() {return new NewProxQuery(super.BuildPartial());}
            public NewProxQuery Build() {if (_HasAllPBJFields) return new NewProxQuery(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return NewProxQuery.Descriptor; }            }
        public Builder ClearQueryId() { super.ClearQueryId();return this;}
        public const int QueryIdFieldTag=2;
        public bool HasQueryId{ get {return super.HasQueryId&&PBJ._PBJ.ValidateUint32(super.QueryId);} }
        public uint QueryId{ get {
            if (HasQueryId) {
                return PBJ._PBJ.CastUint32(super.QueryId);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.QueryId=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearStateless() { super.ClearStateless();return this;}
        public const int StatelessFieldTag=3;
        public bool HasStateless{ get {return super.HasStateless&&PBJ._PBJ.ValidateBool(super.Stateless);} }
        public bool Stateless{ get {
            if (HasStateless) {
                return PBJ._PBJ.CastBool(super.Stateless);
            } else {
                return PBJ._PBJ.CastBool();
            }
        }
        set {
            super.Stateless=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearRelativeCenter() { super.ClearRelativeCenter();return this;}
        public const int RelativeCenterFieldTag=4;
        public bool HasRelativeCenter{ get {return super.RelativeCenterCount>=3;} }
        public PBJ.Vector3f RelativeCenter{ get  {
            int index=0;
            if (HasRelativeCenter) {
                return PBJ._PBJ.CastVector3f(super.GetRelativeCenter(index*3+0),super.GetRelativeCenter(index*3+1),super.GetRelativeCenter(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearRelativeCenter();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddRelativeCenter(_PBJtempArray[0]);
            super.AddRelativeCenter(_PBJtempArray[1]);
            super.AddRelativeCenter(_PBJtempArray[2]);
        }
        }
        public Builder ClearAbsoluteCenter() { super.ClearAbsoluteCenter();return this;}
        public const int AbsoluteCenterFieldTag=5;
        public bool HasAbsoluteCenter{ get {return super.AbsoluteCenterCount>=3;} }
        public PBJ.Vector3d AbsoluteCenter{ get  {
            int index=0;
            if (HasAbsoluteCenter) {
                return PBJ._PBJ.CastVector3d(super.GetAbsoluteCenter(index*3+0),super.GetAbsoluteCenter(index*3+1),super.GetAbsoluteCenter(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3d();
            }
        }
        set {
            super.ClearAbsoluteCenter();
            double[] _PBJtempArray=PBJ._PBJ.ConstructVector3d(value);
            super.AddAbsoluteCenter(_PBJtempArray[0]);
            super.AddAbsoluteCenter(_PBJtempArray[1]);
            super.AddAbsoluteCenter(_PBJtempArray[2]);
        }
        }
        public Builder ClearMaxRadius() { super.ClearMaxRadius();return this;}
        public const int MaxRadiusFieldTag=6;
        public bool HasMaxRadius{ get {return super.HasMaxRadius&&PBJ._PBJ.ValidateFloat(super.MaxRadius);} }
        public float MaxRadius{ get {
            if (HasMaxRadius) {
                return PBJ._PBJ.CastFloat(super.MaxRadius);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.MaxRadius=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearMinSolidAngle() { super.ClearMinSolidAngle();return this;}
        public const int MinSolidAngleFieldTag=7;
        public bool HasMinSolidAngle{ get {return super.HasMinSolidAngle&&PBJ._PBJ.ValidateAngle(super.MinSolidAngle);} }
        public float MinSolidAngle{ get {
            if (HasMinSolidAngle) {
                return PBJ._PBJ.CastAngle(super.MinSolidAngle);
            } else {
                return PBJ._PBJ.CastAngle();
            }
        }
        set {
            super.MinSolidAngle=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class ProxCall : PBJ.IMessage {
        protected _PBJ_Internal.ProxCall super;
        public _PBJ_Internal.ProxCall _PBJSuper{ get { return super;} }
        public ProxCall() {
            super=new _PBJ_Internal.ProxCall();
        }
        public ProxCall(_PBJ_Internal.ProxCall reference) {
            super=reference;
        }
        public static ProxCall defaultInstance= new ProxCall (_PBJ_Internal.ProxCall.DefaultInstance);
        public static ProxCall DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ProxCall.Descriptor; }        }
        public static class Types {
        public enum ProximityEvent {
            EXITED_PROXIMITY=_PBJ_Internal.ProxCall.Types.ProximityEvent.EXITED_PROXIMITY,
            ENTERED_PROXIMITY=_PBJ_Internal.ProxCall.Types.ProximityEvent.ENTERED_PROXIMITY,
            STATELESS_PROXIMITY=_PBJ_Internal.ProxCall.Types.ProximityEvent.STATELESS_PROXIMITY
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int QueryIdFieldTag=2;
        public bool HasQueryId{ get {return super.HasQueryId&&PBJ._PBJ.ValidateUint32(super.QueryId);} }
        public uint QueryId{ get {
            if (HasQueryId) {
                return PBJ._PBJ.CastUint32(super.QueryId);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int ProximateObjectFieldTag=3;
        public bool HasProximateObject{ get {return super.HasProximateObject&&PBJ._PBJ.ValidateUuid(super.ProximateObject);} }
        public PBJ.UUID ProximateObject{ get {
            if (HasProximateObject) {
                return PBJ._PBJ.CastUuid(super.ProximateObject);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int ProximityEventFieldTag=4;
        public bool HasProximityEvent{ get {return super.HasProximityEvent;} }
        public Types.ProximityEvent ProximityEvent{ get {
            if (HasProximityEvent) {
                return (Types.ProximityEvent)super.ProximityEvent;
            } else {
                return new Types.ProximityEvent();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ProxCall prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ProxCall ParseFrom(pb::ByteString data) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data));
        }
        public static ProxCall ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data,er));
        }
        public static ProxCall ParseFrom(byte[] data) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data));
        }
        public static ProxCall ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data,er));
        }
        public static ProxCall ParseFrom(global::System.IO.Stream data) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data));
        }
        public static ProxCall ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data,er));
        }
        public static ProxCall ParseFrom(pb::CodedInputStream data) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data));
        }
        public static ProxCall ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ProxCall(_PBJ_Internal.ProxCall.ParseFrom(data,er));
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
            protected _PBJ_Internal.ProxCall.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ProxCall.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ProxCall.Builder();}
            public Builder(_PBJ_Internal.ProxCall.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ProxCall prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ProxCall BuildPartial() {return new ProxCall(super.BuildPartial());}
            public ProxCall Build() {if (_HasAllPBJFields) return new ProxCall(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ProxCall.Descriptor; }            }
        public Builder ClearQueryId() { super.ClearQueryId();return this;}
        public const int QueryIdFieldTag=2;
        public bool HasQueryId{ get {return super.HasQueryId&&PBJ._PBJ.ValidateUint32(super.QueryId);} }
        public uint QueryId{ get {
            if (HasQueryId) {
                return PBJ._PBJ.CastUint32(super.QueryId);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.QueryId=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearProximateObject() { super.ClearProximateObject();return this;}
        public const int ProximateObjectFieldTag=3;
        public bool HasProximateObject{ get {return super.HasProximateObject&&PBJ._PBJ.ValidateUuid(super.ProximateObject);} }
        public PBJ.UUID ProximateObject{ get {
            if (HasProximateObject) {
                return PBJ._PBJ.CastUuid(super.ProximateObject);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.ProximateObject=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearProximityEvent() { super.ClearProximityEvent();return this;}
        public const int ProximityEventFieldTag=4;
        public bool HasProximityEvent{ get {return super.HasProximityEvent;} }
        public Types.ProximityEvent ProximityEvent{ get {
            if (HasProximityEvent) {
                return (Types.ProximityEvent)super.ProximityEvent;
            } else {
                return new Types.ProximityEvent();
            }
        }
        set {
            super.ProximityEvent=((_PBJ_Internal.ProxCall.Types.ProximityEvent)value);
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class DelProxQuery : PBJ.IMessage {
        protected _PBJ_Internal.DelProxQuery super;
        public _PBJ_Internal.DelProxQuery _PBJSuper{ get { return super;} }
        public DelProxQuery() {
            super=new _PBJ_Internal.DelProxQuery();
        }
        public DelProxQuery(_PBJ_Internal.DelProxQuery reference) {
            super=reference;
        }
        public static DelProxQuery defaultInstance= new DelProxQuery (_PBJ_Internal.DelProxQuery.DefaultInstance);
        public static DelProxQuery DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.DelProxQuery.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int QueryIdFieldTag=2;
        public bool HasQueryId{ get {return super.HasQueryId&&PBJ._PBJ.ValidateUint32(super.QueryId);} }
        public uint QueryId{ get {
            if (HasQueryId) {
                return PBJ._PBJ.CastUint32(super.QueryId);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(DelProxQuery prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static DelProxQuery ParseFrom(pb::ByteString data) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data));
        }
        public static DelProxQuery ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data,er));
        }
        public static DelProxQuery ParseFrom(byte[] data) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data));
        }
        public static DelProxQuery ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data,er));
        }
        public static DelProxQuery ParseFrom(global::System.IO.Stream data) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data));
        }
        public static DelProxQuery ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data,er));
        }
        public static DelProxQuery ParseFrom(pb::CodedInputStream data) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data));
        }
        public static DelProxQuery ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new DelProxQuery(_PBJ_Internal.DelProxQuery.ParseFrom(data,er));
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
            protected _PBJ_Internal.DelProxQuery.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.DelProxQuery.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.DelProxQuery.Builder();}
            public Builder(_PBJ_Internal.DelProxQuery.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(DelProxQuery prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public DelProxQuery BuildPartial() {return new DelProxQuery(super.BuildPartial());}
            public DelProxQuery Build() {if (_HasAllPBJFields) return new DelProxQuery(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return DelProxQuery.Descriptor; }            }
        public Builder ClearQueryId() { super.ClearQueryId();return this;}
        public const int QueryIdFieldTag=2;
        public bool HasQueryId{ get {return super.HasQueryId&&PBJ._PBJ.ValidateUint32(super.QueryId);} }
        public uint QueryId{ get {
            if (HasQueryId) {
                return PBJ._PBJ.CastUint32(super.QueryId);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.QueryId=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class Vector3fProperty : PBJ.IMessage {
        protected _PBJ_Internal.Vector3fProperty super;
        public _PBJ_Internal.Vector3fProperty _PBJSuper{ get { return super;} }
        public Vector3fProperty() {
            super=new _PBJ_Internal.Vector3fProperty();
        }
        public Vector3fProperty(_PBJ_Internal.Vector3fProperty reference) {
            super=reference;
        }
        public static Vector3fProperty defaultInstance= new Vector3fProperty (_PBJ_Internal.Vector3fProperty.DefaultInstance);
        public static Vector3fProperty DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.Vector3fProperty.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ValueFieldTag=10;
        public bool HasValue{ get {return super.ValueCount>=3;} }
        public PBJ.Vector3f Value{ get  {
            int index=0;
            if (HasValue) {
                return PBJ._PBJ.CastVector3f(super.GetValue(index*3+0),super.GetValue(index*3+1),super.GetValue(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(Vector3fProperty prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static Vector3fProperty ParseFrom(pb::ByteString data) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data));
        }
        public static Vector3fProperty ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data,er));
        }
        public static Vector3fProperty ParseFrom(byte[] data) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data));
        }
        public static Vector3fProperty ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data,er));
        }
        public static Vector3fProperty ParseFrom(global::System.IO.Stream data) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data));
        }
        public static Vector3fProperty ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data,er));
        }
        public static Vector3fProperty ParseFrom(pb::CodedInputStream data) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data));
        }
        public static Vector3fProperty ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new Vector3fProperty(_PBJ_Internal.Vector3fProperty.ParseFrom(data,er));
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
            protected _PBJ_Internal.Vector3fProperty.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.Vector3fProperty.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.Vector3fProperty.Builder();}
            public Builder(_PBJ_Internal.Vector3fProperty.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(Vector3fProperty prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public Vector3fProperty BuildPartial() {return new Vector3fProperty(super.BuildPartial());}
            public Vector3fProperty Build() {if (_HasAllPBJFields) return new Vector3fProperty(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return Vector3fProperty.Descriptor; }            }
        public Builder ClearValue() { super.ClearValue();return this;}
        public const int ValueFieldTag=10;
        public bool HasValue{ get {return super.ValueCount>=3;} }
        public PBJ.Vector3f Value{ get  {
            int index=0;
            if (HasValue) {
                return PBJ._PBJ.CastVector3f(super.GetValue(index*3+0),super.GetValue(index*3+1),super.GetValue(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearValue();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddValue(_PBJtempArray[0]);
            super.AddValue(_PBJtempArray[1]);
            super.AddValue(_PBJtempArray[2]);
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class StringProperty : PBJ.IMessage {
        protected _PBJ_Internal.StringProperty super;
        public _PBJ_Internal.StringProperty _PBJSuper{ get { return super;} }
        public StringProperty() {
            super=new _PBJ_Internal.StringProperty();
        }
        public StringProperty(_PBJ_Internal.StringProperty reference) {
            super=reference;
        }
        public static StringProperty defaultInstance= new StringProperty (_PBJ_Internal.StringProperty.DefaultInstance);
        public static StringProperty DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.StringProperty.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ValueFieldTag=10;
        public bool HasValue{ get {return super.HasValue&&PBJ._PBJ.ValidateString(super.Value);} }
        public string Value{ get {
            if (HasValue) {
                return PBJ._PBJ.CastString(super.Value);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(StringProperty prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static StringProperty ParseFrom(pb::ByteString data) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data));
        }
        public static StringProperty ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data,er));
        }
        public static StringProperty ParseFrom(byte[] data) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data));
        }
        public static StringProperty ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data,er));
        }
        public static StringProperty ParseFrom(global::System.IO.Stream data) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data));
        }
        public static StringProperty ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data,er));
        }
        public static StringProperty ParseFrom(pb::CodedInputStream data) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data));
        }
        public static StringProperty ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new StringProperty(_PBJ_Internal.StringProperty.ParseFrom(data,er));
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
            protected _PBJ_Internal.StringProperty.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.StringProperty.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.StringProperty.Builder();}
            public Builder(_PBJ_Internal.StringProperty.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(StringProperty prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public StringProperty BuildPartial() {return new StringProperty(super.BuildPartial());}
            public StringProperty Build() {if (_HasAllPBJFields) return new StringProperty(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return StringProperty.Descriptor; }            }
        public Builder ClearValue() { super.ClearValue();return this;}
        public const int ValueFieldTag=10;
        public bool HasValue{ get {return super.HasValue&&PBJ._PBJ.ValidateString(super.Value);} }
        public string Value{ get {
            if (HasValue) {
                return PBJ._PBJ.CastString(super.Value);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.Value=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class StringMapProperty : PBJ.IMessage {
        protected _PBJ_Internal.StringMapProperty super;
        public _PBJ_Internal.StringMapProperty _PBJSuper{ get { return super;} }
        public StringMapProperty() {
            super=new _PBJ_Internal.StringMapProperty();
        }
        public StringMapProperty(_PBJ_Internal.StringMapProperty reference) {
            super=reference;
        }
        public static StringMapProperty defaultInstance= new StringMapProperty (_PBJ_Internal.StringMapProperty.DefaultInstance);
        public static StringMapProperty DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.StringMapProperty.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int KeysFieldTag=2;
        public int KeysCount { get { return super.KeysCount;} }
        public bool HasKeys(int index) {return PBJ._PBJ.ValidateString(super.GetKeys(index));}
        public string Keys(int index) {
            return (string)PBJ._PBJ.CastString(super.GetKeys(index));
        }
        public const int ValuesFieldTag=3;
        public int ValuesCount { get { return super.ValuesCount;} }
        public bool HasValues(int index) {return PBJ._PBJ.ValidateString(super.GetValues(index));}
        public string Values(int index) {
            return (string)PBJ._PBJ.CastString(super.GetValues(index));
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(StringMapProperty prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static StringMapProperty ParseFrom(pb::ByteString data) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data));
        }
        public static StringMapProperty ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data,er));
        }
        public static StringMapProperty ParseFrom(byte[] data) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data));
        }
        public static StringMapProperty ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data,er));
        }
        public static StringMapProperty ParseFrom(global::System.IO.Stream data) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data));
        }
        public static StringMapProperty ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data,er));
        }
        public static StringMapProperty ParseFrom(pb::CodedInputStream data) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data));
        }
        public static StringMapProperty ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new StringMapProperty(_PBJ_Internal.StringMapProperty.ParseFrom(data,er));
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
            protected _PBJ_Internal.StringMapProperty.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.StringMapProperty.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.StringMapProperty.Builder();}
            public Builder(_PBJ_Internal.StringMapProperty.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(StringMapProperty prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public StringMapProperty BuildPartial() {return new StringMapProperty(super.BuildPartial());}
            public StringMapProperty Build() {if (_HasAllPBJFields) return new StringMapProperty(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return StringMapProperty.Descriptor; }            }
        public Builder ClearKeys() { super.ClearKeys();return this;}
        public Builder SetKeys(int index, string value) {
            super.SetKeys(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int KeysFieldTag=2;
        public int KeysCount { get { return super.KeysCount;} }
        public bool HasKeys(int index) {return PBJ._PBJ.ValidateString(super.GetKeys(index));}
        public string Keys(int index) {
            return (string)PBJ._PBJ.CastString(super.GetKeys(index));
        }
        public Builder AddKeys(string value) {
            super.AddKeys(PBJ._PBJ.Construct(value));
            return this;
        }
        public Builder ClearValues() { super.ClearValues();return this;}
        public Builder SetValues(int index, string value) {
            super.SetValues(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int ValuesFieldTag=3;
        public int ValuesCount { get { return super.ValuesCount;} }
        public bool HasValues(int index) {return PBJ._PBJ.ValidateString(super.GetValues(index));}
        public string Values(int index) {
            return (string)PBJ._PBJ.CastString(super.GetValues(index));
        }
        public Builder AddValues(string value) {
            super.AddValues(PBJ._PBJ.Construct(value));
            return this;
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class PhysicalParameters : PBJ.IMessage {
        protected _PBJ_Internal.PhysicalParameters super;
        public _PBJ_Internal.PhysicalParameters _PBJSuper{ get { return super;} }
        public PhysicalParameters() {
            super=new _PBJ_Internal.PhysicalParameters();
        }
        public PhysicalParameters(_PBJ_Internal.PhysicalParameters reference) {
            super=reference;
        }
        public static PhysicalParameters defaultInstance= new PhysicalParameters (_PBJ_Internal.PhysicalParameters.DefaultInstance);
        public static PhysicalParameters DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.PhysicalParameters.Descriptor; }        }
        public static class Types {
        public enum Mode {
            NONPHYSICAL=_PBJ_Internal.PhysicalParameters.Types.Mode.NONPHYSICAL,
            STATIC=_PBJ_Internal.PhysicalParameters.Types.Mode.STATIC,
            DYNAMICBOX=_PBJ_Internal.PhysicalParameters.Types.Mode.DYNAMICBOX,
            DYNAMICSPHERE=_PBJ_Internal.PhysicalParameters.Types.Mode.DYNAMICSPHERE,
            DYNAMICCYLINDER=_PBJ_Internal.PhysicalParameters.Types.Mode.DYNAMICCYLINDER,
            CHARACTER=_PBJ_Internal.PhysicalParameters.Types.Mode.CHARACTER
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ModeFieldTag=2;
        public bool HasMode{ get {return super.HasMode;} }
        public Types.Mode Mode{ get {
            if (HasMode) {
                return (Types.Mode)super.Mode;
            } else {
                return new Types.Mode();
            }
        }
        }
        public const int DensityFieldTag=3;
        public bool HasDensity{ get {return super.HasDensity&&PBJ._PBJ.ValidateFloat(super.Density);} }
        public float Density{ get {
            if (HasDensity) {
                return PBJ._PBJ.CastFloat(super.Density);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int FrictionFieldTag=4;
        public bool HasFriction{ get {return super.HasFriction&&PBJ._PBJ.ValidateFloat(super.Friction);} }
        public float Friction{ get {
            if (HasFriction) {
                return PBJ._PBJ.CastFloat(super.Friction);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int BounceFieldTag=5;
        public bool HasBounce{ get {return super.HasBounce&&PBJ._PBJ.ValidateFloat(super.Bounce);} }
        public float Bounce{ get {
            if (HasBounce) {
                return PBJ._PBJ.CastFloat(super.Bounce);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int HullFieldTag=6;
        public bool HasHull{ get {return super.HullCount>=3;} }
        public PBJ.Vector3f Hull{ get  {
            int index=0;
            if (HasHull) {
                return PBJ._PBJ.CastVector3f(super.GetHull(index*3+0),super.GetHull(index*3+1),super.GetHull(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int CollideMsgFieldTag=16;
        public bool HasCollideMsg{ get {return super.HasCollideMsg&&PBJ._PBJ.ValidateUint32(super.CollideMsg);} }
        public uint CollideMsg{ get {
            if (HasCollideMsg) {
                return PBJ._PBJ.CastUint32(super.CollideMsg);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int CollideMaskFieldTag=17;
        public bool HasCollideMask{ get {return super.HasCollideMask&&PBJ._PBJ.ValidateUint32(super.CollideMask);} }
        public uint CollideMask{ get {
            if (HasCollideMask) {
                return PBJ._PBJ.CastUint32(super.CollideMask);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        }
        public const int GravityFieldTag=18;
        public bool HasGravity{ get {return super.HasGravity&&PBJ._PBJ.ValidateFloat(super.Gravity);} }
        public float Gravity{ get {
            if (HasGravity) {
                return PBJ._PBJ.CastFloat(super.Gravity);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(PhysicalParameters prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static PhysicalParameters ParseFrom(pb::ByteString data) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data));
        }
        public static PhysicalParameters ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data,er));
        }
        public static PhysicalParameters ParseFrom(byte[] data) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data));
        }
        public static PhysicalParameters ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data,er));
        }
        public static PhysicalParameters ParseFrom(global::System.IO.Stream data) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data));
        }
        public static PhysicalParameters ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data,er));
        }
        public static PhysicalParameters ParseFrom(pb::CodedInputStream data) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data));
        }
        public static PhysicalParameters ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new PhysicalParameters(_PBJ_Internal.PhysicalParameters.ParseFrom(data,er));
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
            protected _PBJ_Internal.PhysicalParameters.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.PhysicalParameters.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.PhysicalParameters.Builder();}
            public Builder(_PBJ_Internal.PhysicalParameters.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(PhysicalParameters prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public PhysicalParameters BuildPartial() {return new PhysicalParameters(super.BuildPartial());}
            public PhysicalParameters Build() {if (_HasAllPBJFields) return new PhysicalParameters(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return PhysicalParameters.Descriptor; }            }
        public Builder ClearMode() { super.ClearMode();return this;}
        public const int ModeFieldTag=2;
        public bool HasMode{ get {return super.HasMode;} }
        public Types.Mode Mode{ get {
            if (HasMode) {
                return (Types.Mode)super.Mode;
            } else {
                return new Types.Mode();
            }
        }
        set {
            super.Mode=((_PBJ_Internal.PhysicalParameters.Types.Mode)value);
        }
        }
        public Builder ClearDensity() { super.ClearDensity();return this;}
        public const int DensityFieldTag=3;
        public bool HasDensity{ get {return super.HasDensity&&PBJ._PBJ.ValidateFloat(super.Density);} }
        public float Density{ get {
            if (HasDensity) {
                return PBJ._PBJ.CastFloat(super.Density);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.Density=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearFriction() { super.ClearFriction();return this;}
        public const int FrictionFieldTag=4;
        public bool HasFriction{ get {return super.HasFriction&&PBJ._PBJ.ValidateFloat(super.Friction);} }
        public float Friction{ get {
            if (HasFriction) {
                return PBJ._PBJ.CastFloat(super.Friction);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.Friction=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearBounce() { super.ClearBounce();return this;}
        public const int BounceFieldTag=5;
        public bool HasBounce{ get {return super.HasBounce&&PBJ._PBJ.ValidateFloat(super.Bounce);} }
        public float Bounce{ get {
            if (HasBounce) {
                return PBJ._PBJ.CastFloat(super.Bounce);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.Bounce=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearHull() { super.ClearHull();return this;}
        public const int HullFieldTag=6;
        public bool HasHull{ get {return super.HullCount>=3;} }
        public PBJ.Vector3f Hull{ get  {
            int index=0;
            if (HasHull) {
                return PBJ._PBJ.CastVector3f(super.GetHull(index*3+0),super.GetHull(index*3+1),super.GetHull(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearHull();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddHull(_PBJtempArray[0]);
            super.AddHull(_PBJtempArray[1]);
            super.AddHull(_PBJtempArray[2]);
        }
        }
        public Builder ClearCollideMsg() { super.ClearCollideMsg();return this;}
        public const int CollideMsgFieldTag=16;
        public bool HasCollideMsg{ get {return super.HasCollideMsg&&PBJ._PBJ.ValidateUint32(super.CollideMsg);} }
        public uint CollideMsg{ get {
            if (HasCollideMsg) {
                return PBJ._PBJ.CastUint32(super.CollideMsg);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.CollideMsg=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearCollideMask() { super.ClearCollideMask();return this;}
        public const int CollideMaskFieldTag=17;
        public bool HasCollideMask{ get {return super.HasCollideMask&&PBJ._PBJ.ValidateUint32(super.CollideMask);} }
        public uint CollideMask{ get {
            if (HasCollideMask) {
                return PBJ._PBJ.CastUint32(super.CollideMask);
            } else {
                return PBJ._PBJ.CastUint32();
            }
        }
        set {
            super.CollideMask=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearGravity() { super.ClearGravity();return this;}
        public const int GravityFieldTag=18;
        public bool HasGravity{ get {return super.HasGravity&&PBJ._PBJ.ValidateFloat(super.Gravity);} }
        public float Gravity{ get {
            if (HasGravity) {
                return PBJ._PBJ.CastFloat(super.Gravity);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.Gravity=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class LightInfoProperty : PBJ.IMessage {
        protected _PBJ_Internal.LightInfoProperty super;
        public _PBJ_Internal.LightInfoProperty _PBJSuper{ get { return super;} }
        public LightInfoProperty() {
            super=new _PBJ_Internal.LightInfoProperty();
        }
        public LightInfoProperty(_PBJ_Internal.LightInfoProperty reference) {
            super=reference;
        }
        public static LightInfoProperty defaultInstance= new LightInfoProperty (_PBJ_Internal.LightInfoProperty.DefaultInstance);
        public static LightInfoProperty DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.LightInfoProperty.Descriptor; }        }
        public static class Types {
        public enum LightTypes {
            POINT=_PBJ_Internal.LightInfoProperty.Types.LightTypes.POINT,
            SPOTLIGHT=_PBJ_Internal.LightInfoProperty.Types.LightTypes.SPOTLIGHT,
            DIRECTIONAL=_PBJ_Internal.LightInfoProperty.Types.LightTypes.DIRECTIONAL
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int DiffuseColorFieldTag=3;
        public bool HasDiffuseColor{ get {return super.DiffuseColorCount>=3;} }
        public PBJ.Vector3f DiffuseColor{ get  {
            int index=0;
            if (HasDiffuseColor) {
                return PBJ._PBJ.CastVector3f(super.GetDiffuseColor(index*3+0),super.GetDiffuseColor(index*3+1),super.GetDiffuseColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int SpecularColorFieldTag=4;
        public bool HasSpecularColor{ get {return super.SpecularColorCount>=3;} }
        public PBJ.Vector3f SpecularColor{ get  {
            int index=0;
            if (HasSpecularColor) {
                return PBJ._PBJ.CastVector3f(super.GetSpecularColor(index*3+0),super.GetSpecularColor(index*3+1),super.GetSpecularColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int PowerFieldTag=5;
        public bool HasPower{ get {return super.HasPower&&PBJ._PBJ.ValidateFloat(super.Power);} }
        public float Power{ get {
            if (HasPower) {
                return PBJ._PBJ.CastFloat(super.Power);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int AmbientColorFieldTag=6;
        public bool HasAmbientColor{ get {return super.AmbientColorCount>=3;} }
        public PBJ.Vector3f AmbientColor{ get  {
            int index=0;
            if (HasAmbientColor) {
                return PBJ._PBJ.CastVector3f(super.GetAmbientColor(index*3+0),super.GetAmbientColor(index*3+1),super.GetAmbientColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int ShadowColorFieldTag=7;
        public bool HasShadowColor{ get {return super.ShadowColorCount>=3;} }
        public PBJ.Vector3f ShadowColor{ get  {
            int index=0;
            if (HasShadowColor) {
                return PBJ._PBJ.CastVector3f(super.GetShadowColor(index*3+0),super.GetShadowColor(index*3+1),super.GetShadowColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int LightRangeFieldTag=8;
        public bool HasLightRange{ get {return super.HasLightRange&&PBJ._PBJ.ValidateDouble(super.LightRange);} }
        public double LightRange{ get {
            if (HasLightRange) {
                return PBJ._PBJ.CastDouble(super.LightRange);
            } else {
                return PBJ._PBJ.CastDouble();
            }
        }
        }
        public const int ConstantFalloffFieldTag=9;
        public bool HasConstantFalloff{ get {return super.HasConstantFalloff&&PBJ._PBJ.ValidateFloat(super.ConstantFalloff);} }
        public float ConstantFalloff{ get {
            if (HasConstantFalloff) {
                return PBJ._PBJ.CastFloat(super.ConstantFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int LinearFalloffFieldTag=10;
        public bool HasLinearFalloff{ get {return super.HasLinearFalloff&&PBJ._PBJ.ValidateFloat(super.LinearFalloff);} }
        public float LinearFalloff{ get {
            if (HasLinearFalloff) {
                return PBJ._PBJ.CastFloat(super.LinearFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int QuadraticFalloffFieldTag=11;
        public bool HasQuadraticFalloff{ get {return super.HasQuadraticFalloff&&PBJ._PBJ.ValidateFloat(super.QuadraticFalloff);} }
        public float QuadraticFalloff{ get {
            if (HasQuadraticFalloff) {
                return PBJ._PBJ.CastFloat(super.QuadraticFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int ConeInnerRadiansFieldTag=12;
        public bool HasConeInnerRadians{ get {return super.HasConeInnerRadians&&PBJ._PBJ.ValidateFloat(super.ConeInnerRadians);} }
        public float ConeInnerRadians{ get {
            if (HasConeInnerRadians) {
                return PBJ._PBJ.CastFloat(super.ConeInnerRadians);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int ConeOuterRadiansFieldTag=13;
        public bool HasConeOuterRadians{ get {return super.HasConeOuterRadians&&PBJ._PBJ.ValidateFloat(super.ConeOuterRadians);} }
        public float ConeOuterRadians{ get {
            if (HasConeOuterRadians) {
                return PBJ._PBJ.CastFloat(super.ConeOuterRadians);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int ConeFalloffFieldTag=14;
        public bool HasConeFalloff{ get {return super.HasConeFalloff&&PBJ._PBJ.ValidateFloat(super.ConeFalloff);} }
        public float ConeFalloff{ get {
            if (HasConeFalloff) {
                return PBJ._PBJ.CastFloat(super.ConeFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        }
        public const int TypeFieldTag=15;
        public bool HasType{ get {return super.HasType;} }
        public Types.LightTypes Type{ get {
            if (HasType) {
                return (Types.LightTypes)super.Type;
            } else {
                return new Types.LightTypes();
            }
        }
        }
        public const int CastsShadowFieldTag=16;
        public bool HasCastsShadow{ get {return super.HasCastsShadow&&PBJ._PBJ.ValidateBool(super.CastsShadow);} }
        public bool CastsShadow{ get {
            if (HasCastsShadow) {
                return PBJ._PBJ.CastBool(super.CastsShadow);
            } else {
                return PBJ._PBJ.CastBool();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(LightInfoProperty prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static LightInfoProperty ParseFrom(pb::ByteString data) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data));
        }
        public static LightInfoProperty ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data,er));
        }
        public static LightInfoProperty ParseFrom(byte[] data) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data));
        }
        public static LightInfoProperty ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data,er));
        }
        public static LightInfoProperty ParseFrom(global::System.IO.Stream data) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data));
        }
        public static LightInfoProperty ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data,er));
        }
        public static LightInfoProperty ParseFrom(pb::CodedInputStream data) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data));
        }
        public static LightInfoProperty ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new LightInfoProperty(_PBJ_Internal.LightInfoProperty.ParseFrom(data,er));
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
            protected _PBJ_Internal.LightInfoProperty.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.LightInfoProperty.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.LightInfoProperty.Builder();}
            public Builder(_PBJ_Internal.LightInfoProperty.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(LightInfoProperty prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public LightInfoProperty BuildPartial() {return new LightInfoProperty(super.BuildPartial());}
            public LightInfoProperty Build() {if (_HasAllPBJFields) return new LightInfoProperty(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return LightInfoProperty.Descriptor; }            }
        public Builder ClearDiffuseColor() { super.ClearDiffuseColor();return this;}
        public const int DiffuseColorFieldTag=3;
        public bool HasDiffuseColor{ get {return super.DiffuseColorCount>=3;} }
        public PBJ.Vector3f DiffuseColor{ get  {
            int index=0;
            if (HasDiffuseColor) {
                return PBJ._PBJ.CastVector3f(super.GetDiffuseColor(index*3+0),super.GetDiffuseColor(index*3+1),super.GetDiffuseColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearDiffuseColor();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddDiffuseColor(_PBJtempArray[0]);
            super.AddDiffuseColor(_PBJtempArray[1]);
            super.AddDiffuseColor(_PBJtempArray[2]);
        }
        }
        public Builder ClearSpecularColor() { super.ClearSpecularColor();return this;}
        public const int SpecularColorFieldTag=4;
        public bool HasSpecularColor{ get {return super.SpecularColorCount>=3;} }
        public PBJ.Vector3f SpecularColor{ get  {
            int index=0;
            if (HasSpecularColor) {
                return PBJ._PBJ.CastVector3f(super.GetSpecularColor(index*3+0),super.GetSpecularColor(index*3+1),super.GetSpecularColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearSpecularColor();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddSpecularColor(_PBJtempArray[0]);
            super.AddSpecularColor(_PBJtempArray[1]);
            super.AddSpecularColor(_PBJtempArray[2]);
        }
        }
        public Builder ClearPower() { super.ClearPower();return this;}
        public const int PowerFieldTag=5;
        public bool HasPower{ get {return super.HasPower&&PBJ._PBJ.ValidateFloat(super.Power);} }
        public float Power{ get {
            if (HasPower) {
                return PBJ._PBJ.CastFloat(super.Power);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.Power=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearAmbientColor() { super.ClearAmbientColor();return this;}
        public const int AmbientColorFieldTag=6;
        public bool HasAmbientColor{ get {return super.AmbientColorCount>=3;} }
        public PBJ.Vector3f AmbientColor{ get  {
            int index=0;
            if (HasAmbientColor) {
                return PBJ._PBJ.CastVector3f(super.GetAmbientColor(index*3+0),super.GetAmbientColor(index*3+1),super.GetAmbientColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearAmbientColor();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddAmbientColor(_PBJtempArray[0]);
            super.AddAmbientColor(_PBJtempArray[1]);
            super.AddAmbientColor(_PBJtempArray[2]);
        }
        }
        public Builder ClearShadowColor() { super.ClearShadowColor();return this;}
        public const int ShadowColorFieldTag=7;
        public bool HasShadowColor{ get {return super.ShadowColorCount>=3;} }
        public PBJ.Vector3f ShadowColor{ get  {
            int index=0;
            if (HasShadowColor) {
                return PBJ._PBJ.CastVector3f(super.GetShadowColor(index*3+0),super.GetShadowColor(index*3+1),super.GetShadowColor(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearShadowColor();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddShadowColor(_PBJtempArray[0]);
            super.AddShadowColor(_PBJtempArray[1]);
            super.AddShadowColor(_PBJtempArray[2]);
        }
        }
        public Builder ClearLightRange() { super.ClearLightRange();return this;}
        public const int LightRangeFieldTag=8;
        public bool HasLightRange{ get {return super.HasLightRange&&PBJ._PBJ.ValidateDouble(super.LightRange);} }
        public double LightRange{ get {
            if (HasLightRange) {
                return PBJ._PBJ.CastDouble(super.LightRange);
            } else {
                return PBJ._PBJ.CastDouble();
            }
        }
        set {
            super.LightRange=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearConstantFalloff() { super.ClearConstantFalloff();return this;}
        public const int ConstantFalloffFieldTag=9;
        public bool HasConstantFalloff{ get {return super.HasConstantFalloff&&PBJ._PBJ.ValidateFloat(super.ConstantFalloff);} }
        public float ConstantFalloff{ get {
            if (HasConstantFalloff) {
                return PBJ._PBJ.CastFloat(super.ConstantFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.ConstantFalloff=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearLinearFalloff() { super.ClearLinearFalloff();return this;}
        public const int LinearFalloffFieldTag=10;
        public bool HasLinearFalloff{ get {return super.HasLinearFalloff&&PBJ._PBJ.ValidateFloat(super.LinearFalloff);} }
        public float LinearFalloff{ get {
            if (HasLinearFalloff) {
                return PBJ._PBJ.CastFloat(super.LinearFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.LinearFalloff=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearQuadraticFalloff() { super.ClearQuadraticFalloff();return this;}
        public const int QuadraticFalloffFieldTag=11;
        public bool HasQuadraticFalloff{ get {return super.HasQuadraticFalloff&&PBJ._PBJ.ValidateFloat(super.QuadraticFalloff);} }
        public float QuadraticFalloff{ get {
            if (HasQuadraticFalloff) {
                return PBJ._PBJ.CastFloat(super.QuadraticFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.QuadraticFalloff=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearConeInnerRadians() { super.ClearConeInnerRadians();return this;}
        public const int ConeInnerRadiansFieldTag=12;
        public bool HasConeInnerRadians{ get {return super.HasConeInnerRadians&&PBJ._PBJ.ValidateFloat(super.ConeInnerRadians);} }
        public float ConeInnerRadians{ get {
            if (HasConeInnerRadians) {
                return PBJ._PBJ.CastFloat(super.ConeInnerRadians);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.ConeInnerRadians=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearConeOuterRadians() { super.ClearConeOuterRadians();return this;}
        public const int ConeOuterRadiansFieldTag=13;
        public bool HasConeOuterRadians{ get {return super.HasConeOuterRadians&&PBJ._PBJ.ValidateFloat(super.ConeOuterRadians);} }
        public float ConeOuterRadians{ get {
            if (HasConeOuterRadians) {
                return PBJ._PBJ.CastFloat(super.ConeOuterRadians);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.ConeOuterRadians=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearConeFalloff() { super.ClearConeFalloff();return this;}
        public const int ConeFalloffFieldTag=14;
        public bool HasConeFalloff{ get {return super.HasConeFalloff&&PBJ._PBJ.ValidateFloat(super.ConeFalloff);} }
        public float ConeFalloff{ get {
            if (HasConeFalloff) {
                return PBJ._PBJ.CastFloat(super.ConeFalloff);
            } else {
                return PBJ._PBJ.CastFloat();
            }
        }
        set {
            super.ConeFalloff=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearType() { super.ClearType();return this;}
        public const int TypeFieldTag=15;
        public bool HasType{ get {return super.HasType;} }
        public Types.LightTypes Type{ get {
            if (HasType) {
                return (Types.LightTypes)super.Type;
            } else {
                return new Types.LightTypes();
            }
        }
        set {
            super.Type=((_PBJ_Internal.LightInfoProperty.Types.LightTypes)value);
        }
        }
        public Builder ClearCastsShadow() { super.ClearCastsShadow();return this;}
        public const int CastsShadowFieldTag=16;
        public bool HasCastsShadow{ get {return super.HasCastsShadow&&PBJ._PBJ.ValidateBool(super.CastsShadow);} }
        public bool CastsShadow{ get {
            if (HasCastsShadow) {
                return PBJ._PBJ.CastBool(super.CastsShadow);
            } else {
                return PBJ._PBJ.CastBool();
            }
        }
        set {
            super.CastsShadow=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class ParentProperty : PBJ.IMessage {
        protected _PBJ_Internal.ParentProperty super;
        public _PBJ_Internal.ParentProperty _PBJSuper{ get { return super;} }
        public ParentProperty() {
            super=new _PBJ_Internal.ParentProperty();
        }
        public ParentProperty(_PBJ_Internal.ParentProperty reference) {
            super=reference;
        }
        public static ParentProperty defaultInstance= new ParentProperty (_PBJ_Internal.ParentProperty.DefaultInstance);
        public static ParentProperty DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ParentProperty.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ValueFieldTag=10;
        public bool HasValue{ get {return super.HasValue&&PBJ._PBJ.ValidateUuid(super.Value);} }
        public PBJ.UUID Value{ get {
            if (HasValue) {
                return PBJ._PBJ.CastUuid(super.Value);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ParentProperty prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ParentProperty ParseFrom(pb::ByteString data) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data));
        }
        public static ParentProperty ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data,er));
        }
        public static ParentProperty ParseFrom(byte[] data) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data));
        }
        public static ParentProperty ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data,er));
        }
        public static ParentProperty ParseFrom(global::System.IO.Stream data) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data));
        }
        public static ParentProperty ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data,er));
        }
        public static ParentProperty ParseFrom(pb::CodedInputStream data) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data));
        }
        public static ParentProperty ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ParentProperty(_PBJ_Internal.ParentProperty.ParseFrom(data,er));
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
            protected _PBJ_Internal.ParentProperty.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ParentProperty.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ParentProperty.Builder();}
            public Builder(_PBJ_Internal.ParentProperty.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ParentProperty prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ParentProperty BuildPartial() {return new ParentProperty(super.BuildPartial());}
            public ParentProperty Build() {if (_HasAllPBJFields) return new ParentProperty(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ParentProperty.Descriptor; }            }
        public Builder ClearValue() { super.ClearValue();return this;}
        public const int ValueFieldTag=10;
        public bool HasValue{ get {return super.HasValue&&PBJ._PBJ.ValidateUuid(super.Value);} }
        public PBJ.UUID Value{ get {
            if (HasValue) {
                return PBJ._PBJ.CastUuid(super.Value);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.Value=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class UUIDListProperty : PBJ.IMessage {
        protected _PBJ_Internal.UUIDListProperty super;
        public _PBJ_Internal.UUIDListProperty _PBJSuper{ get { return super;} }
        public UUIDListProperty() {
            super=new _PBJ_Internal.UUIDListProperty();
        }
        public UUIDListProperty(_PBJ_Internal.UUIDListProperty reference) {
            super=reference;
        }
        public static UUIDListProperty defaultInstance= new UUIDListProperty (_PBJ_Internal.UUIDListProperty.DefaultInstance);
        public static UUIDListProperty DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.UUIDListProperty.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ValueFieldTag=10;
        public int ValueCount { get { return super.ValueCount;} }
        public bool HasValue(int index) {return PBJ._PBJ.ValidateUuid(super.GetValue(index));}
        public PBJ.UUID Value(int index) {
            return (PBJ.UUID)PBJ._PBJ.CastUuid(super.GetValue(index));
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(UUIDListProperty prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static UUIDListProperty ParseFrom(pb::ByteString data) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data));
        }
        public static UUIDListProperty ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data,er));
        }
        public static UUIDListProperty ParseFrom(byte[] data) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data));
        }
        public static UUIDListProperty ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data,er));
        }
        public static UUIDListProperty ParseFrom(global::System.IO.Stream data) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data));
        }
        public static UUIDListProperty ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data,er));
        }
        public static UUIDListProperty ParseFrom(pb::CodedInputStream data) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data));
        }
        public static UUIDListProperty ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new UUIDListProperty(_PBJ_Internal.UUIDListProperty.ParseFrom(data,er));
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
            protected _PBJ_Internal.UUIDListProperty.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.UUIDListProperty.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.UUIDListProperty.Builder();}
            public Builder(_PBJ_Internal.UUIDListProperty.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(UUIDListProperty prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public UUIDListProperty BuildPartial() {return new UUIDListProperty(super.BuildPartial());}
            public UUIDListProperty Build() {if (_HasAllPBJFields) return new UUIDListProperty(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return UUIDListProperty.Descriptor; }            }
        public Builder ClearValue() { super.ClearValue();return this;}
        public Builder SetValue(int index, PBJ.UUID value) {
            super.SetValue(index,PBJ._PBJ.Construct(value));
            return this;
        }
        public const int ValueFieldTag=10;
        public int ValueCount { get { return super.ValueCount;} }
        public bool HasValue(int index) {return PBJ._PBJ.ValidateUuid(super.GetValue(index));}
        public PBJ.UUID Value(int index) {
            return (PBJ.UUID)PBJ._PBJ.CastUuid(super.GetValue(index));
        }
        public Builder AddValue(PBJ.UUID value) {
            super.AddValue(PBJ._PBJ.Construct(value));
            return this;
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class ConnectToSpace : PBJ.IMessage {
        protected _PBJ_Internal.ConnectToSpace super;
        public _PBJ_Internal.ConnectToSpace _PBJSuper{ get { return super;} }
        public ConnectToSpace() {
            super=new _PBJ_Internal.ConnectToSpace();
        }
        public ConnectToSpace(_PBJ_Internal.ConnectToSpace reference) {
            super=reference;
        }
        public static ConnectToSpace defaultInstance= new ConnectToSpace (_PBJ_Internal.ConnectToSpace.DefaultInstance);
        public static ConnectToSpace DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ConnectToSpace.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int SpaceIdFieldTag=1;
        public bool HasSpaceId{ get {return super.HasSpaceId&&PBJ._PBJ.ValidateUuid(super.SpaceId);} }
        public PBJ.UUID SpaceId{ get {
            if (HasSpaceId) {
                return PBJ._PBJ.CastUuid(super.SpaceId);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int ObjectUuidEvidenceFieldTag=2;
        public bool HasObjectUuidEvidence{ get {return super.HasObjectUuidEvidence&&PBJ._PBJ.ValidateUuid(super.ObjectUuidEvidence);} }
        public PBJ.UUID ObjectUuidEvidence{ get {
            if (HasObjectUuidEvidence) {
                return PBJ._PBJ.CastUuid(super.ObjectUuidEvidence);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int RequestedObjectLocFieldTag=3;
        public bool HasRequestedObjectLoc{ get {return super.HasRequestedObjectLoc;} }
        public ObjLoc RequestedObjectLoc{ get {
            if (HasRequestedObjectLoc) {
                return new ObjLoc(super.RequestedObjectLoc);
            } else {
                return new ObjLoc();
            }
        }
        }
        public const int BoundingSphereFieldTag=4;
        public bool HasBoundingSphere{ get {return super.BoundingSphereCount>=4;} }
        public PBJ.BoundingSphere3f BoundingSphere{ get  {
            int index=0;
            if (HasBoundingSphere) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBoundingSphere(index*4+0),super.GetBoundingSphere(index*4+1),super.GetBoundingSphere(index*4+2),super.GetBoundingSphere(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ConnectToSpace prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ConnectToSpace ParseFrom(pb::ByteString data) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data));
        }
        public static ConnectToSpace ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data,er));
        }
        public static ConnectToSpace ParseFrom(byte[] data) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data));
        }
        public static ConnectToSpace ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data,er));
        }
        public static ConnectToSpace ParseFrom(global::System.IO.Stream data) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data));
        }
        public static ConnectToSpace ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data,er));
        }
        public static ConnectToSpace ParseFrom(pb::CodedInputStream data) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data));
        }
        public static ConnectToSpace ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ConnectToSpace(_PBJ_Internal.ConnectToSpace.ParseFrom(data,er));
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
            protected _PBJ_Internal.ConnectToSpace.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ConnectToSpace.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ConnectToSpace.Builder();}
            public Builder(_PBJ_Internal.ConnectToSpace.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ConnectToSpace prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ConnectToSpace BuildPartial() {return new ConnectToSpace(super.BuildPartial());}
            public ConnectToSpace Build() {if (_HasAllPBJFields) return new ConnectToSpace(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ConnectToSpace.Descriptor; }            }
        public Builder ClearSpaceId() { super.ClearSpaceId();return this;}
        public const int SpaceIdFieldTag=1;
        public bool HasSpaceId{ get {return super.HasSpaceId&&PBJ._PBJ.ValidateUuid(super.SpaceId);} }
        public PBJ.UUID SpaceId{ get {
            if (HasSpaceId) {
                return PBJ._PBJ.CastUuid(super.SpaceId);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.SpaceId=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearObjectUuidEvidence() { super.ClearObjectUuidEvidence();return this;}
        public const int ObjectUuidEvidenceFieldTag=2;
        public bool HasObjectUuidEvidence{ get {return super.HasObjectUuidEvidence&&PBJ._PBJ.ValidateUuid(super.ObjectUuidEvidence);} }
        public PBJ.UUID ObjectUuidEvidence{ get {
            if (HasObjectUuidEvidence) {
                return PBJ._PBJ.CastUuid(super.ObjectUuidEvidence);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.ObjectUuidEvidence=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearRequestedObjectLoc() { super.ClearRequestedObjectLoc();return this;}
        public const int RequestedObjectLocFieldTag=3;
        public bool HasRequestedObjectLoc{ get {return super.HasRequestedObjectLoc;} }
        public ObjLoc RequestedObjectLoc{ get {
            if (HasRequestedObjectLoc) {
                return new ObjLoc(super.RequestedObjectLoc);
            } else {
                return new ObjLoc();
            }
        }
        set {
            super.RequestedObjectLoc=value._PBJSuper;
        }
        }
        public Builder ClearBoundingSphere() { super.ClearBoundingSphere();return this;}
        public const int BoundingSphereFieldTag=4;
        public bool HasBoundingSphere{ get {return super.BoundingSphereCount>=4;} }
        public PBJ.BoundingSphere3f BoundingSphere{ get  {
            int index=0;
            if (HasBoundingSphere) {
                return PBJ._PBJ.CastBoundingsphere3f(super.GetBoundingSphere(index*4+0),super.GetBoundingSphere(index*4+1),super.GetBoundingSphere(index*4+2),super.GetBoundingSphere(index*4+3));
            } else {
                return PBJ._PBJ.CastBoundingsphere3f();
            }
        }
        set {
            super.ClearBoundingSphere();
            float[] _PBJtempArray=PBJ._PBJ.ConstructBoundingsphere3f(value);
            super.AddBoundingSphere(_PBJtempArray[0]);
            super.AddBoundingSphere(_PBJtempArray[1]);
            super.AddBoundingSphere(_PBJtempArray[2]);
            super.AddBoundingSphere(_PBJtempArray[3]);
        }
        }
        }
    }
}
namespace Sirikata.Protocol {
    public class CreateObject : PBJ.IMessage {
        protected _PBJ_Internal.CreateObject super;
        public _PBJ_Internal.CreateObject _PBJSuper{ get { return super;} }
        public CreateObject() {
            super=new _PBJ_Internal.CreateObject();
        }
        public CreateObject(_PBJ_Internal.CreateObject reference) {
            super=reference;
        }
        public static CreateObject defaultInstance= new CreateObject (_PBJ_Internal.CreateObject.DefaultInstance);
        public static CreateObject DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.CreateObject.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ObjectUuidFieldTag=1;
        public bool HasObjectUuid{ get {return super.HasObjectUuid&&PBJ._PBJ.ValidateUuid(super.ObjectUuid);} }
        public PBJ.UUID ObjectUuid{ get {
            if (HasObjectUuid) {
                return PBJ._PBJ.CastUuid(super.ObjectUuid);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int SpacePropertiesFieldTag=2;
        public int SpacePropertiesCount { get { return super.SpacePropertiesCount;} }
        public bool HasSpaceProperties(int index) {return true;}
        public ConnectToSpace SpaceProperties(int index) {
            return new ConnectToSpace(super.GetSpaceProperties(index));
        }
        public const int MeshFieldTag=3;
        public bool HasMesh{ get {return super.HasMesh&&PBJ._PBJ.ValidateString(super.Mesh);} }
        public string Mesh{ get {
            if (HasMesh) {
                return PBJ._PBJ.CastString(super.Mesh);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
        public const int ScaleFieldTag=4;
        public bool HasScale{ get {return super.ScaleCount>=3;} }
        public PBJ.Vector3f Scale{ get  {
            int index=0;
            if (HasScale) {
                return PBJ._PBJ.CastVector3f(super.GetScale(index*3+0),super.GetScale(index*3+1),super.GetScale(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        }
        public const int WeburlFieldTag=5;
        public bool HasWeburl{ get {return super.HasWeburl&&PBJ._PBJ.ValidateString(super.Weburl);} }
        public string Weburl{ get {
            if (HasWeburl) {
                return PBJ._PBJ.CastString(super.Weburl);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
        public const int LightInfoFieldTag=6;
        public bool HasLightInfo{ get {return super.HasLightInfo;} }
        public LightInfoProperty LightInfo{ get {
            if (HasLightInfo) {
                return new LightInfoProperty(super.LightInfo);
            } else {
                return new LightInfoProperty();
            }
        }
        }
        public const int CameraFieldTag=7;
        public bool HasCamera{ get {return super.HasCamera&&PBJ._PBJ.ValidateBool(super.Camera);} }
        public bool Camera{ get {
            if (HasCamera) {
                return PBJ._PBJ.CastBool(super.Camera);
            } else {
                return PBJ._PBJ.CastBool();
            }
        }
        }
        public const int PhysicalFieldTag=8;
        public bool HasPhysical{ get {return super.HasPhysical;} }
        public PhysicalParameters Physical{ get {
            if (HasPhysical) {
                return new PhysicalParameters(super.Physical);
            } else {
                return new PhysicalParameters();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(CreateObject prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static CreateObject ParseFrom(pb::ByteString data) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data));
        }
        public static CreateObject ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data,er));
        }
        public static CreateObject ParseFrom(byte[] data) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data));
        }
        public static CreateObject ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data,er));
        }
        public static CreateObject ParseFrom(global::System.IO.Stream data) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data));
        }
        public static CreateObject ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data,er));
        }
        public static CreateObject ParseFrom(pb::CodedInputStream data) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data));
        }
        public static CreateObject ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new CreateObject(_PBJ_Internal.CreateObject.ParseFrom(data,er));
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
            protected _PBJ_Internal.CreateObject.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.CreateObject.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.CreateObject.Builder();}
            public Builder(_PBJ_Internal.CreateObject.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(CreateObject prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public CreateObject BuildPartial() {return new CreateObject(super.BuildPartial());}
            public CreateObject Build() {if (_HasAllPBJFields) return new CreateObject(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return CreateObject.Descriptor; }            }
        public Builder ClearObjectUuid() { super.ClearObjectUuid();return this;}
        public const int ObjectUuidFieldTag=1;
        public bool HasObjectUuid{ get {return super.HasObjectUuid&&PBJ._PBJ.ValidateUuid(super.ObjectUuid);} }
        public PBJ.UUID ObjectUuid{ get {
            if (HasObjectUuid) {
                return PBJ._PBJ.CastUuid(super.ObjectUuid);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.ObjectUuid=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearSpaceProperties() { super.ClearSpaceProperties();return this;}
        public Builder SetSpaceProperties(int index,ConnectToSpace value) {
            super.SetSpaceProperties(index,value._PBJSuper);
            return this;
        }
        public const int SpacePropertiesFieldTag=2;
        public int SpacePropertiesCount { get { return super.SpacePropertiesCount;} }
        public bool HasSpaceProperties(int index) {return true;}
        public ConnectToSpace SpaceProperties(int index) {
            return new ConnectToSpace(super.GetSpaceProperties(index));
        }
        public Builder AddSpaceProperties(ConnectToSpace value ) {
            super.AddSpaceProperties(value._PBJSuper);
            return this;
        }
        public Builder ClearMesh() { super.ClearMesh();return this;}
        public const int MeshFieldTag=3;
        public bool HasMesh{ get {return super.HasMesh&&PBJ._PBJ.ValidateString(super.Mesh);} }
        public string Mesh{ get {
            if (HasMesh) {
                return PBJ._PBJ.CastString(super.Mesh);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.Mesh=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearScale() { super.ClearScale();return this;}
        public const int ScaleFieldTag=4;
        public bool HasScale{ get {return super.ScaleCount>=3;} }
        public PBJ.Vector3f Scale{ get  {
            int index=0;
            if (HasScale) {
                return PBJ._PBJ.CastVector3f(super.GetScale(index*3+0),super.GetScale(index*3+1),super.GetScale(index*3+2));
            } else {
                return PBJ._PBJ.CastVector3f();
            }
        }
        set {
            super.ClearScale();
            float[] _PBJtempArray=PBJ._PBJ.ConstructVector3f(value);
            super.AddScale(_PBJtempArray[0]);
            super.AddScale(_PBJtempArray[1]);
            super.AddScale(_PBJtempArray[2]);
        }
        }
        public Builder ClearWeburl() { super.ClearWeburl();return this;}
        public const int WeburlFieldTag=5;
        public bool HasWeburl{ get {return super.HasWeburl&&PBJ._PBJ.ValidateString(super.Weburl);} }
        public string Weburl{ get {
            if (HasWeburl) {
                return PBJ._PBJ.CastString(super.Weburl);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.Weburl=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearLightInfo() { super.ClearLightInfo();return this;}
        public const int LightInfoFieldTag=6;
        public bool HasLightInfo{ get {return super.HasLightInfo;} }
        public LightInfoProperty LightInfo{ get {
            if (HasLightInfo) {
                return new LightInfoProperty(super.LightInfo);
            } else {
                return new LightInfoProperty();
            }
        }
        set {
            super.LightInfo=value._PBJSuper;
        }
        }
        public Builder ClearCamera() { super.ClearCamera();return this;}
        public const int CameraFieldTag=7;
        public bool HasCamera{ get {return super.HasCamera&&PBJ._PBJ.ValidateBool(super.Camera);} }
        public bool Camera{ get {
            if (HasCamera) {
                return PBJ._PBJ.CastBool(super.Camera);
            } else {
                return PBJ._PBJ.CastBool();
            }
        }
        set {
            super.Camera=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearPhysical() { super.ClearPhysical();return this;}
        public const int PhysicalFieldTag=8;
        public bool HasPhysical{ get {return super.HasPhysical;} }
        public PhysicalParameters Physical{ get {
            if (HasPhysical) {
                return new PhysicalParameters(super.Physical);
            } else {
                return new PhysicalParameters();
            }
        }
        set {
            super.Physical=value._PBJSuper;
        }
        }
        }
    }
}
