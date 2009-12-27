using pbd = global::Google.ProtocolBuffers.Descriptors;
using pb = global::Google.ProtocolBuffers;
namespace Sirikata.Persistence.Protocol {
    public class StorageKey : PBJ.IMessage {
        protected _PBJ_Internal.StorageKey super;
        public _PBJ_Internal.StorageKey _PBJSuper{ get { return super;} }
        public StorageKey() {
            super=new _PBJ_Internal.StorageKey();
        }
        public StorageKey(_PBJ_Internal.StorageKey reference) {
            super=reference;
        }
        public static StorageKey defaultInstance= new StorageKey (_PBJ_Internal.StorageKey.DefaultInstance);
        public static StorageKey DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.StorageKey.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ObjectUuidFieldTag=9;
        public bool HasObjectUuid{ get {return super.HasObjectUuid&&PBJ._PBJ.ValidateUuid(super.ObjectUuid);} }
        public PBJ.UUID ObjectUuid{ get {
            if (HasObjectUuid) {
                return PBJ._PBJ.CastUuid(super.ObjectUuid);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int FieldIdFieldTag=10;
        public bool HasFieldId{ get {return super.HasFieldId&&PBJ._PBJ.ValidateUint64(super.FieldId);} }
        public ulong FieldId{ get {
            if (HasFieldId) {
                return PBJ._PBJ.CastUint64(super.FieldId);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        }
        public const int FieldNameFieldTag=11;
        public bool HasFieldName{ get {return super.HasFieldName&&PBJ._PBJ.ValidateString(super.FieldName);} }
        public string FieldName{ get {
            if (HasFieldName) {
                return PBJ._PBJ.CastString(super.FieldName);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(StorageKey prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static StorageKey ParseFrom(pb::ByteString data) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data));
        }
        public static StorageKey ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data,er));
        }
        public static StorageKey ParseFrom(byte[] data) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data));
        }
        public static StorageKey ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data,er));
        }
        public static StorageKey ParseFrom(global::System.IO.Stream data) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data));
        }
        public static StorageKey ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data,er));
        }
        public static StorageKey ParseFrom(pb::CodedInputStream data) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data));
        }
        public static StorageKey ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new StorageKey(_PBJ_Internal.StorageKey.ParseFrom(data,er));
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
            protected _PBJ_Internal.StorageKey.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.StorageKey.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.StorageKey.Builder();}
            public Builder(_PBJ_Internal.StorageKey.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(StorageKey prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public StorageKey BuildPartial() {return new StorageKey(super.BuildPartial());}
            public StorageKey Build() {if (_HasAllPBJFields) return new StorageKey(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return StorageKey.Descriptor; }            }
        public Builder ClearObjectUuid() { super.ClearObjectUuid();return this;}
        public const int ObjectUuidFieldTag=9;
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
        public Builder ClearFieldId() { super.ClearFieldId();return this;}
        public const int FieldIdFieldTag=10;
        public bool HasFieldId{ get {return super.HasFieldId&&PBJ._PBJ.ValidateUint64(super.FieldId);} }
        public ulong FieldId{ get {
            if (HasFieldId) {
                return PBJ._PBJ.CastUint64(super.FieldId);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        set {
            super.FieldId=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearFieldName() { super.ClearFieldName();return this;}
        public const int FieldNameFieldTag=11;
        public bool HasFieldName{ get {return super.HasFieldName&&PBJ._PBJ.ValidateString(super.FieldName);} }
        public string FieldName{ get {
            if (HasFieldName) {
                return PBJ._PBJ.CastString(super.FieldName);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.FieldName=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class StorageValue : PBJ.IMessage {
        protected _PBJ_Internal.StorageValue super;
        public _PBJ_Internal.StorageValue _PBJSuper{ get { return super;} }
        public StorageValue() {
            super=new _PBJ_Internal.StorageValue();
        }
        public StorageValue(_PBJ_Internal.StorageValue reference) {
            super=reference;
        }
        public static StorageValue defaultInstance= new StorageValue (_PBJ_Internal.StorageValue.DefaultInstance);
        public static StorageValue DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.StorageValue.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int DataFieldTag=12;
        public bool HasData{ get {return super.HasData&&PBJ._PBJ.ValidateBytes(super.Data);} }
        public pb::ByteString Data{ get {
            if (HasData) {
                return PBJ._PBJ.CastBytes(super.Data);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(StorageValue prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static StorageValue ParseFrom(pb::ByteString data) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data));
        }
        public static StorageValue ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data,er));
        }
        public static StorageValue ParseFrom(byte[] data) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data));
        }
        public static StorageValue ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data,er));
        }
        public static StorageValue ParseFrom(global::System.IO.Stream data) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data));
        }
        public static StorageValue ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data,er));
        }
        public static StorageValue ParseFrom(pb::CodedInputStream data) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data));
        }
        public static StorageValue ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new StorageValue(_PBJ_Internal.StorageValue.ParseFrom(data,er));
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
            protected _PBJ_Internal.StorageValue.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.StorageValue.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.StorageValue.Builder();}
            public Builder(_PBJ_Internal.StorageValue.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(StorageValue prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public StorageValue BuildPartial() {return new StorageValue(super.BuildPartial());}
            public StorageValue Build() {if (_HasAllPBJFields) return new StorageValue(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return StorageValue.Descriptor; }            }
        public Builder ClearData() { super.ClearData();return this;}
        public const int DataFieldTag=12;
        public bool HasData{ get {return super.HasData&&PBJ._PBJ.ValidateBytes(super.Data);} }
        public pb::ByteString Data{ get {
            if (HasData) {
                return PBJ._PBJ.CastBytes(super.Data);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        set {
            super.Data=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class StorageElement : PBJ.IMessage {
        protected _PBJ_Internal.StorageElement super;
        public _PBJ_Internal.StorageElement _PBJSuper{ get { return super;} }
        public StorageElement() {
            super=new _PBJ_Internal.StorageElement();
        }
        public StorageElement(_PBJ_Internal.StorageElement reference) {
            super=reference;
        }
        public static StorageElement defaultInstance= new StorageElement (_PBJ_Internal.StorageElement.DefaultInstance);
        public static StorageElement DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.StorageElement.Descriptor; }        }
        public static class Types {
        public enum ReturnStatus {
            KEY_MISSING=_PBJ_Internal.StorageElement.Types.ReturnStatus.KEY_MISSING,
            INTERNAL_ERROR=_PBJ_Internal.StorageElement.Types.ReturnStatus.INTERNAL_ERROR
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ObjectUuidFieldTag=9;
        public bool HasObjectUuid{ get {return super.HasObjectUuid&&PBJ._PBJ.ValidateUuid(super.ObjectUuid);} }
        public PBJ.UUID ObjectUuid{ get {
            if (HasObjectUuid) {
                return PBJ._PBJ.CastUuid(super.ObjectUuid);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int FieldIdFieldTag=10;
        public bool HasFieldId{ get {return super.HasFieldId&&PBJ._PBJ.ValidateUint64(super.FieldId);} }
        public ulong FieldId{ get {
            if (HasFieldId) {
                return PBJ._PBJ.CastUint64(super.FieldId);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        }
        public const int FieldNameFieldTag=11;
        public bool HasFieldName{ get {return super.HasFieldName&&PBJ._PBJ.ValidateString(super.FieldName);} }
        public string FieldName{ get {
            if (HasFieldName) {
                return PBJ._PBJ.CastString(super.FieldName);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
        public const int DataFieldTag=12;
        public bool HasData{ get {return super.HasData&&PBJ._PBJ.ValidateBytes(super.Data);} }
        public pb::ByteString Data{ get {
            if (HasData) {
                return PBJ._PBJ.CastBytes(super.Data);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        }
        public const int IndexFieldTag=13;
        public bool HasIndex{ get {return super.HasIndex&&PBJ._PBJ.ValidateInt32(super.Index);} }
        public int Index{ get {
            if (HasIndex) {
                return PBJ._PBJ.CastInt32(super.Index);
            } else {
                return PBJ._PBJ.CastInt32();
            }
        }
        }
        public const int ReturnStatusFieldTag=15;
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
        public static Builder CreateBuilder(StorageElement prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static StorageElement ParseFrom(pb::ByteString data) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data));
        }
        public static StorageElement ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data,er));
        }
        public static StorageElement ParseFrom(byte[] data) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data));
        }
        public static StorageElement ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data,er));
        }
        public static StorageElement ParseFrom(global::System.IO.Stream data) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data));
        }
        public static StorageElement ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data,er));
        }
        public static StorageElement ParseFrom(pb::CodedInputStream data) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data));
        }
        public static StorageElement ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new StorageElement(_PBJ_Internal.StorageElement.ParseFrom(data,er));
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
            protected _PBJ_Internal.StorageElement.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.StorageElement.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.StorageElement.Builder();}
            public Builder(_PBJ_Internal.StorageElement.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(StorageElement prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public StorageElement BuildPartial() {return new StorageElement(super.BuildPartial());}
            public StorageElement Build() {if (_HasAllPBJFields) return new StorageElement(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return StorageElement.Descriptor; }            }
        public Builder ClearObjectUuid() { super.ClearObjectUuid();return this;}
        public const int ObjectUuidFieldTag=9;
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
        public Builder ClearFieldId() { super.ClearFieldId();return this;}
        public const int FieldIdFieldTag=10;
        public bool HasFieldId{ get {return super.HasFieldId&&PBJ._PBJ.ValidateUint64(super.FieldId);} }
        public ulong FieldId{ get {
            if (HasFieldId) {
                return PBJ._PBJ.CastUint64(super.FieldId);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        set {
            super.FieldId=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearFieldName() { super.ClearFieldName();return this;}
        public const int FieldNameFieldTag=11;
        public bool HasFieldName{ get {return super.HasFieldName&&PBJ._PBJ.ValidateString(super.FieldName);} }
        public string FieldName{ get {
            if (HasFieldName) {
                return PBJ._PBJ.CastString(super.FieldName);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.FieldName=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearData() { super.ClearData();return this;}
        public const int DataFieldTag=12;
        public bool HasData{ get {return super.HasData&&PBJ._PBJ.ValidateBytes(super.Data);} }
        public pb::ByteString Data{ get {
            if (HasData) {
                return PBJ._PBJ.CastBytes(super.Data);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        set {
            super.Data=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearIndex() { super.ClearIndex();return this;}
        public const int IndexFieldTag=13;
        public bool HasIndex{ get {return super.HasIndex&&PBJ._PBJ.ValidateInt32(super.Index);} }
        public int Index{ get {
            if (HasIndex) {
                return PBJ._PBJ.CastInt32(super.Index);
            } else {
                return PBJ._PBJ.CastInt32();
            }
        }
        set {
            super.Index=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearReturnStatus() { super.ClearReturnStatus();return this;}
        public const int ReturnStatusFieldTag=15;
        public bool HasReturnStatus{ get {return super.HasReturnStatus;} }
        public Types.ReturnStatus ReturnStatus{ get {
            if (HasReturnStatus) {
                return (Types.ReturnStatus)super.ReturnStatus;
            } else {
                return new Types.ReturnStatus();
            }
        }
        set {
            super.ReturnStatus=((_PBJ_Internal.StorageElement.Types.ReturnStatus)value);
        }
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class CompareElement : PBJ.IMessage {
        protected _PBJ_Internal.CompareElement super;
        public _PBJ_Internal.CompareElement _PBJSuper{ get { return super;} }
        public CompareElement() {
            super=new _PBJ_Internal.CompareElement();
        }
        public CompareElement(_PBJ_Internal.CompareElement reference) {
            super=reference;
        }
        public static CompareElement defaultInstance= new CompareElement (_PBJ_Internal.CompareElement.DefaultInstance);
        public static CompareElement DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.CompareElement.Descriptor; }        }
        public static class Types {
        public enum COMPARATOR {
            EQUAL=_PBJ_Internal.CompareElement.Types.COMPARATOR.EQUAL,
            NEQUAL=_PBJ_Internal.CompareElement.Types.COMPARATOR.NEQUAL
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ObjectUuidFieldTag=9;
        public bool HasObjectUuid{ get {return super.HasObjectUuid&&PBJ._PBJ.ValidateUuid(super.ObjectUuid);} }
        public PBJ.UUID ObjectUuid{ get {
            if (HasObjectUuid) {
                return PBJ._PBJ.CastUuid(super.ObjectUuid);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int FieldIdFieldTag=10;
        public bool HasFieldId{ get {return super.HasFieldId&&PBJ._PBJ.ValidateUint64(super.FieldId);} }
        public ulong FieldId{ get {
            if (HasFieldId) {
                return PBJ._PBJ.CastUint64(super.FieldId);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        }
        public const int FieldNameFieldTag=11;
        public bool HasFieldName{ get {return super.HasFieldName&&PBJ._PBJ.ValidateString(super.FieldName);} }
        public string FieldName{ get {
            if (HasFieldName) {
                return PBJ._PBJ.CastString(super.FieldName);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
        public const int DataFieldTag=12;
        public bool HasData{ get {return super.HasData&&PBJ._PBJ.ValidateBytes(super.Data);} }
        public pb::ByteString Data{ get {
            if (HasData) {
                return PBJ._PBJ.CastBytes(super.Data);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        }
        public const int ComparatorFieldTag=14;
        public bool HasComparator{ get {return super.HasComparator;} }
        public Types.COMPARATOR Comparator{ get {
            if (HasComparator) {
                return (Types.COMPARATOR)super.Comparator;
            } else {
                return new Types.COMPARATOR();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(CompareElement prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static CompareElement ParseFrom(pb::ByteString data) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data));
        }
        public static CompareElement ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data,er));
        }
        public static CompareElement ParseFrom(byte[] data) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data));
        }
        public static CompareElement ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data,er));
        }
        public static CompareElement ParseFrom(global::System.IO.Stream data) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data));
        }
        public static CompareElement ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data,er));
        }
        public static CompareElement ParseFrom(pb::CodedInputStream data) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data));
        }
        public static CompareElement ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new CompareElement(_PBJ_Internal.CompareElement.ParseFrom(data,er));
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
            protected _PBJ_Internal.CompareElement.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.CompareElement.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.CompareElement.Builder();}
            public Builder(_PBJ_Internal.CompareElement.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(CompareElement prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public CompareElement BuildPartial() {return new CompareElement(super.BuildPartial());}
            public CompareElement Build() {if (_HasAllPBJFields) return new CompareElement(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return CompareElement.Descriptor; }            }
        public Builder ClearObjectUuid() { super.ClearObjectUuid();return this;}
        public const int ObjectUuidFieldTag=9;
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
        public Builder ClearFieldId() { super.ClearFieldId();return this;}
        public const int FieldIdFieldTag=10;
        public bool HasFieldId{ get {return super.HasFieldId&&PBJ._PBJ.ValidateUint64(super.FieldId);} }
        public ulong FieldId{ get {
            if (HasFieldId) {
                return PBJ._PBJ.CastUint64(super.FieldId);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        set {
            super.FieldId=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearFieldName() { super.ClearFieldName();return this;}
        public const int FieldNameFieldTag=11;
        public bool HasFieldName{ get {return super.HasFieldName&&PBJ._PBJ.ValidateString(super.FieldName);} }
        public string FieldName{ get {
            if (HasFieldName) {
                return PBJ._PBJ.CastString(super.FieldName);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.FieldName=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearData() { super.ClearData();return this;}
        public const int DataFieldTag=12;
        public bool HasData{ get {return super.HasData&&PBJ._PBJ.ValidateBytes(super.Data);} }
        public pb::ByteString Data{ get {
            if (HasData) {
                return PBJ._PBJ.CastBytes(super.Data);
            } else {
                return PBJ._PBJ.CastBytes();
            }
        }
        set {
            super.Data=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearComparator() { super.ClearComparator();return this;}
        public const int ComparatorFieldTag=14;
        public bool HasComparator{ get {return super.HasComparator;} }
        public Types.COMPARATOR Comparator{ get {
            if (HasComparator) {
                return (Types.COMPARATOR)super.Comparator;
            } else {
                return new Types.COMPARATOR();
            }
        }
        set {
            super.Comparator=((_PBJ_Internal.CompareElement.Types.COMPARATOR)value);
        }
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class StorageSet : PBJ.IMessage {
        protected _PBJ_Internal.StorageSet super;
        public _PBJ_Internal.StorageSet _PBJSuper{ get { return super;} }
        public StorageSet() {
            super=new _PBJ_Internal.StorageSet();
        }
        public StorageSet(_PBJ_Internal.StorageSet reference) {
            super=reference;
        }
        public static StorageSet defaultInstance= new StorageSet (_PBJ_Internal.StorageSet.DefaultInstance);
        public static StorageSet DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.StorageSet.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(StorageSet prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static StorageSet ParseFrom(pb::ByteString data) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data));
        }
        public static StorageSet ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data,er));
        }
        public static StorageSet ParseFrom(byte[] data) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data));
        }
        public static StorageSet ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data,er));
        }
        public static StorageSet ParseFrom(global::System.IO.Stream data) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data));
        }
        public static StorageSet ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data,er));
        }
        public static StorageSet ParseFrom(pb::CodedInputStream data) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data));
        }
        public static StorageSet ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new StorageSet(_PBJ_Internal.StorageSet.ParseFrom(data,er));
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
            protected _PBJ_Internal.StorageSet.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.StorageSet.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.StorageSet.Builder();}
            public Builder(_PBJ_Internal.StorageSet.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(StorageSet prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public StorageSet BuildPartial() {return new StorageSet(super.BuildPartial());}
            public StorageSet Build() {if (_HasAllPBJFields) return new StorageSet(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return StorageSet.Descriptor; }            }
        public Builder ClearReads() { super.ClearReads();return this;}
        public Builder SetReads(int index,StorageElement value) {
            super.SetReads(index,value._PBJSuper);
            return this;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public Builder AddReads(StorageElement value ) {
            super.AddReads(value._PBJSuper);
            return this;
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class ReadSet : PBJ.IMessage {
        protected _PBJ_Internal.ReadSet super;
        public _PBJ_Internal.ReadSet _PBJSuper{ get { return super;} }
        public ReadSet() {
            super=new _PBJ_Internal.ReadSet();
        }
        public ReadSet(_PBJ_Internal.ReadSet reference) {
            super=reference;
        }
        public static ReadSet defaultInstance= new ReadSet (_PBJ_Internal.ReadSet.DefaultInstance);
        public static ReadSet DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ReadSet.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ReadSet prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ReadSet ParseFrom(pb::ByteString data) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data));
        }
        public static ReadSet ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data,er));
        }
        public static ReadSet ParseFrom(byte[] data) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data));
        }
        public static ReadSet ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data,er));
        }
        public static ReadSet ParseFrom(global::System.IO.Stream data) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data));
        }
        public static ReadSet ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data,er));
        }
        public static ReadSet ParseFrom(pb::CodedInputStream data) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data));
        }
        public static ReadSet ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ReadSet(_PBJ_Internal.ReadSet.ParseFrom(data,er));
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
            protected _PBJ_Internal.ReadSet.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ReadSet.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ReadSet.Builder();}
            public Builder(_PBJ_Internal.ReadSet.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ReadSet prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ReadSet BuildPartial() {return new ReadSet(super.BuildPartial());}
            public ReadSet Build() {if (_HasAllPBJFields) return new ReadSet(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ReadSet.Descriptor; }            }
        public Builder ClearReads() { super.ClearReads();return this;}
        public Builder SetReads(int index,StorageElement value) {
            super.SetReads(index,value._PBJSuper);
            return this;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public Builder AddReads(StorageElement value ) {
            super.AddReads(value._PBJSuper);
            return this;
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class WriteSet : PBJ.IMessage {
        protected _PBJ_Internal.WriteSet super;
        public _PBJ_Internal.WriteSet _PBJSuper{ get { return super;} }
        public WriteSet() {
            super=new _PBJ_Internal.WriteSet();
        }
        public WriteSet(_PBJ_Internal.WriteSet reference) {
            super=reference;
        }
        public static WriteSet defaultInstance= new WriteSet (_PBJ_Internal.WriteSet.DefaultInstance);
        public static WriteSet DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.WriteSet.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int WritesFieldTag=10;
        public int WritesCount { get { return super.WritesCount;} }
        public bool HasWrites(int index) {return true;}
        public StorageElement Writes(int index) {
            return new StorageElement(super.GetWrites(index));
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(WriteSet prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static WriteSet ParseFrom(pb::ByteString data) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data));
        }
        public static WriteSet ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data,er));
        }
        public static WriteSet ParseFrom(byte[] data) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data));
        }
        public static WriteSet ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data,er));
        }
        public static WriteSet ParseFrom(global::System.IO.Stream data) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data));
        }
        public static WriteSet ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data,er));
        }
        public static WriteSet ParseFrom(pb::CodedInputStream data) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data));
        }
        public static WriteSet ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new WriteSet(_PBJ_Internal.WriteSet.ParseFrom(data,er));
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
            protected _PBJ_Internal.WriteSet.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.WriteSet.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.WriteSet.Builder();}
            public Builder(_PBJ_Internal.WriteSet.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(WriteSet prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public WriteSet BuildPartial() {return new WriteSet(super.BuildPartial());}
            public WriteSet Build() {if (_HasAllPBJFields) return new WriteSet(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return WriteSet.Descriptor; }            }
        public Builder ClearWrites() { super.ClearWrites();return this;}
        public Builder SetWrites(int index,StorageElement value) {
            super.SetWrites(index,value._PBJSuper);
            return this;
        }
        public const int WritesFieldTag=10;
        public int WritesCount { get { return super.WritesCount;} }
        public bool HasWrites(int index) {return true;}
        public StorageElement Writes(int index) {
            return new StorageElement(super.GetWrites(index));
        }
        public Builder AddWrites(StorageElement value ) {
            super.AddWrites(value._PBJSuper);
            return this;
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class ReadWriteSet : PBJ.IMessage {
        protected _PBJ_Internal.ReadWriteSet super;
        public _PBJ_Internal.ReadWriteSet _PBJSuper{ get { return super;} }
        public ReadWriteSet() {
            super=new _PBJ_Internal.ReadWriteSet();
        }
        public ReadWriteSet(_PBJ_Internal.ReadWriteSet reference) {
            super=reference;
        }
        public static ReadWriteSet defaultInstance= new ReadWriteSet (_PBJ_Internal.ReadWriteSet.DefaultInstance);
        public static ReadWriteSet DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.ReadWriteSet.Descriptor; }        }
        public static class Types {
        public enum ReadWriteSetOptions {
            RETURN_READ_NAMES=_PBJ_Internal.ReadWriteSet.Types.ReadWriteSetOptions.RETURN_READ_NAMES
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public const int WritesFieldTag=10;
        public int WritesCount { get { return super.WritesCount;} }
        public bool HasWrites(int index) {return true;}
        public StorageElement Writes(int index) {
            return new StorageElement(super.GetWrites(index));
        }
        public const int OptionsFieldTag=14;
        public bool HasOptions { get {
            if (!super.HasOptions) return false;
            return PBJ._PBJ.ValidateFlags(super.Options,(ulong)Types.ReadWriteSetOptions.RETURN_READ_NAMES);
        } }
        public ulong Options{ get {
            if (HasOptions) {
                return (ulong)PBJ._PBJ.CastFlags(super.Options,(ulong)Types.ReadWriteSetOptions.RETURN_READ_NAMES);
            } else {
                return (ulong)PBJ._PBJ.CastFlags((ulong)Types.ReadWriteSetOptions.RETURN_READ_NAMES);
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(ReadWriteSet prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static ReadWriteSet ParseFrom(pb::ByteString data) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data));
        }
        public static ReadWriteSet ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data,er));
        }
        public static ReadWriteSet ParseFrom(byte[] data) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data));
        }
        public static ReadWriteSet ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data,er));
        }
        public static ReadWriteSet ParseFrom(global::System.IO.Stream data) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data));
        }
        public static ReadWriteSet ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data,er));
        }
        public static ReadWriteSet ParseFrom(pb::CodedInputStream data) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data));
        }
        public static ReadWriteSet ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new ReadWriteSet(_PBJ_Internal.ReadWriteSet.ParseFrom(data,er));
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
            protected _PBJ_Internal.ReadWriteSet.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.ReadWriteSet.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.ReadWriteSet.Builder();}
            public Builder(_PBJ_Internal.ReadWriteSet.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(ReadWriteSet prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public ReadWriteSet BuildPartial() {return new ReadWriteSet(super.BuildPartial());}
            public ReadWriteSet Build() {if (_HasAllPBJFields) return new ReadWriteSet(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return ReadWriteSet.Descriptor; }            }
        public Builder ClearReads() { super.ClearReads();return this;}
        public Builder SetReads(int index,StorageElement value) {
            super.SetReads(index,value._PBJSuper);
            return this;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public Builder AddReads(StorageElement value ) {
            super.AddReads(value._PBJSuper);
            return this;
        }
        public Builder ClearWrites() { super.ClearWrites();return this;}
        public Builder SetWrites(int index,StorageElement value) {
            super.SetWrites(index,value._PBJSuper);
            return this;
        }
        public const int WritesFieldTag=10;
        public int WritesCount { get { return super.WritesCount;} }
        public bool HasWrites(int index) {return true;}
        public StorageElement Writes(int index) {
            return new StorageElement(super.GetWrites(index));
        }
        public Builder AddWrites(StorageElement value ) {
            super.AddWrites(value._PBJSuper);
            return this;
        }
        public Builder ClearOptions() { super.ClearOptions();return this;}
        public const int OptionsFieldTag=14;
        public bool HasOptions { get {
            if (!super.HasOptions) return false;
            return PBJ._PBJ.ValidateFlags(super.Options,(ulong)Types.ReadWriteSetOptions.RETURN_READ_NAMES);
        } }
        public ulong Options{ get {
            if (HasOptions) {
                return (ulong)PBJ._PBJ.CastFlags(super.Options,(ulong)Types.ReadWriteSetOptions.RETURN_READ_NAMES);
            } else {
                return (ulong)PBJ._PBJ.CastFlags((ulong)Types.ReadWriteSetOptions.RETURN_READ_NAMES);
            }
        }
        set {
            super.Options=((value));
        }
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class Minitransaction : PBJ.IMessage {
        protected _PBJ_Internal.Minitransaction super;
        public _PBJ_Internal.Minitransaction _PBJSuper{ get { return super;} }
        public Minitransaction() {
            super=new _PBJ_Internal.Minitransaction();
        }
        public Minitransaction(_PBJ_Internal.Minitransaction reference) {
            super=reference;
        }
        public static Minitransaction defaultInstance= new Minitransaction (_PBJ_Internal.Minitransaction.DefaultInstance);
        public static Minitransaction DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.Minitransaction.Descriptor; }        }
        public static class Types {
        public enum TransactionOptions {
            RETURN_READ_NAMES=_PBJ_Internal.Minitransaction.Types.TransactionOptions.RETURN_READ_NAMES
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public const int WritesFieldTag=10;
        public int WritesCount { get { return super.WritesCount;} }
        public bool HasWrites(int index) {return true;}
        public StorageElement Writes(int index) {
            return new StorageElement(super.GetWrites(index));
        }
        public const int ComparesFieldTag=11;
        public int ComparesCount { get { return super.ComparesCount;} }
        public bool HasCompares(int index) {return true;}
        public CompareElement Compares(int index) {
            return new CompareElement(super.GetCompares(index));
        }
        public const int OptionsFieldTag=14;
        public bool HasOptions { get {
            if (!super.HasOptions) return false;
            return PBJ._PBJ.ValidateFlags(super.Options,(ulong)Types.TransactionOptions.RETURN_READ_NAMES);
        } }
        public ulong Options{ get {
            if (HasOptions) {
                return (ulong)PBJ._PBJ.CastFlags(super.Options,(ulong)Types.TransactionOptions.RETURN_READ_NAMES);
            } else {
                return (ulong)PBJ._PBJ.CastFlags((ulong)Types.TransactionOptions.RETURN_READ_NAMES);
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(Minitransaction prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static Minitransaction ParseFrom(pb::ByteString data) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data));
        }
        public static Minitransaction ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data,er));
        }
        public static Minitransaction ParseFrom(byte[] data) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data));
        }
        public static Minitransaction ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data,er));
        }
        public static Minitransaction ParseFrom(global::System.IO.Stream data) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data));
        }
        public static Minitransaction ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data,er));
        }
        public static Minitransaction ParseFrom(pb::CodedInputStream data) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data));
        }
        public static Minitransaction ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new Minitransaction(_PBJ_Internal.Minitransaction.ParseFrom(data,er));
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
            protected _PBJ_Internal.Minitransaction.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.Minitransaction.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.Minitransaction.Builder();}
            public Builder(_PBJ_Internal.Minitransaction.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(Minitransaction prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public Minitransaction BuildPartial() {return new Minitransaction(super.BuildPartial());}
            public Minitransaction Build() {if (_HasAllPBJFields) return new Minitransaction(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return Minitransaction.Descriptor; }            }
        public Builder ClearReads() { super.ClearReads();return this;}
        public Builder SetReads(int index,StorageElement value) {
            super.SetReads(index,value._PBJSuper);
            return this;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public Builder AddReads(StorageElement value ) {
            super.AddReads(value._PBJSuper);
            return this;
        }
        public Builder ClearWrites() { super.ClearWrites();return this;}
        public Builder SetWrites(int index,StorageElement value) {
            super.SetWrites(index,value._PBJSuper);
            return this;
        }
        public const int WritesFieldTag=10;
        public int WritesCount { get { return super.WritesCount;} }
        public bool HasWrites(int index) {return true;}
        public StorageElement Writes(int index) {
            return new StorageElement(super.GetWrites(index));
        }
        public Builder AddWrites(StorageElement value ) {
            super.AddWrites(value._PBJSuper);
            return this;
        }
        public Builder ClearCompares() { super.ClearCompares();return this;}
        public Builder SetCompares(int index,CompareElement value) {
            super.SetCompares(index,value._PBJSuper);
            return this;
        }
        public const int ComparesFieldTag=11;
        public int ComparesCount { get { return super.ComparesCount;} }
        public bool HasCompares(int index) {return true;}
        public CompareElement Compares(int index) {
            return new CompareElement(super.GetCompares(index));
        }
        public Builder AddCompares(CompareElement value ) {
            super.AddCompares(value._PBJSuper);
            return this;
        }
        public Builder ClearOptions() { super.ClearOptions();return this;}
        public const int OptionsFieldTag=14;
        public bool HasOptions { get {
            if (!super.HasOptions) return false;
            return PBJ._PBJ.ValidateFlags(super.Options,(ulong)Types.TransactionOptions.RETURN_READ_NAMES);
        } }
        public ulong Options{ get {
            if (HasOptions) {
                return (ulong)PBJ._PBJ.CastFlags(super.Options,(ulong)Types.TransactionOptions.RETURN_READ_NAMES);
            } else {
                return (ulong)PBJ._PBJ.CastFlags((ulong)Types.TransactionOptions.RETURN_READ_NAMES);
            }
        }
        set {
            super.Options=((value));
        }
        }
        }
    }
}
namespace Sirikata.Persistence.Protocol {
    public class Response : PBJ.IMessage {
        protected _PBJ_Internal.Response super;
        public _PBJ_Internal.Response _PBJSuper{ get { return super;} }
        public Response() {
            super=new _PBJ_Internal.Response();
        }
        public Response(_PBJ_Internal.Response reference) {
            super=reference;
        }
        public static Response defaultInstance= new Response (_PBJ_Internal.Response.DefaultInstance);
        public static Response DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.Response.Descriptor; }        }
        public static class Types {
        public enum ReturnStatus {
            SUCCESS=_PBJ_Internal.Response.Types.ReturnStatus.SUCCESS,
            DATABASE_LOCKED=_PBJ_Internal.Response.Types.ReturnStatus.DATABASE_LOCKED,
            KEY_MISSING=_PBJ_Internal.Response.Types.ReturnStatus.KEY_MISSING,
            COMPARISON_FAILED=_PBJ_Internal.Response.Types.ReturnStatus.COMPARISON_FAILED,
            INTERNAL_ERROR=_PBJ_Internal.Response.Types.ReturnStatus.INTERNAL_ERROR
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public const int ReturnStatusFieldTag=15;
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
        public static Builder CreateBuilder(Response prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static Response ParseFrom(pb::ByteString data) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data));
        }
        public static Response ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data,er));
        }
        public static Response ParseFrom(byte[] data) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data));
        }
        public static Response ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data,er));
        }
        public static Response ParseFrom(global::System.IO.Stream data) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data));
        }
        public static Response ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data,er));
        }
        public static Response ParseFrom(pb::CodedInputStream data) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data));
        }
        public static Response ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new Response(_PBJ_Internal.Response.ParseFrom(data,er));
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
            protected _PBJ_Internal.Response.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.Response.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.Response.Builder();}
            public Builder(_PBJ_Internal.Response.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(Response prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public Response BuildPartial() {return new Response(super.BuildPartial());}
            public Response Build() {if (_HasAllPBJFields) return new Response(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return Response.Descriptor; }            }
        public Builder ClearReads() { super.ClearReads();return this;}
        public Builder SetReads(int index,StorageElement value) {
            super.SetReads(index,value._PBJSuper);
            return this;
        }
        public const int ReadsFieldTag=9;
        public int ReadsCount { get { return super.ReadsCount;} }
        public bool HasReads(int index) {return true;}
        public StorageElement Reads(int index) {
            return new StorageElement(super.GetReads(index));
        }
        public Builder AddReads(StorageElement value ) {
            super.AddReads(value._PBJSuper);
            return this;
        }
        public Builder ClearReturnStatus() { super.ClearReturnStatus();return this;}
        public const int ReturnStatusFieldTag=15;
        public bool HasReturnStatus{ get {return super.HasReturnStatus;} }
        public Types.ReturnStatus ReturnStatus{ get {
            if (HasReturnStatus) {
                return (Types.ReturnStatus)super.ReturnStatus;
            } else {
                return new Types.ReturnStatus();
            }
        }
        set {
            super.ReturnStatus=((_PBJ_Internal.Response.Types.ReturnStatus)value);
        }
        }
        }
    }
}
