using pbd = global::Google.ProtocolBuffers.Descriptors;
using pb = global::Google.ProtocolBuffers;
namespace Sirikata.Subscription.Protocol {
    public class Address : PBJ.IMessage {
        protected _PBJ_Internal.Address super;
        public _PBJ_Internal.Address _PBJSuper{ get { return super;} }
        public Address() {
            super=new _PBJ_Internal.Address();
        }
        public Address(_PBJ_Internal.Address reference) {
            super=reference;
        }
        public static Address defaultInstance= new Address (_PBJ_Internal.Address.DefaultInstance);
        public static Address DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.Address.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false;
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int HostnameFieldTag=1;
        public bool HasHostname{ get {return super.HasHostname&&PBJ._PBJ.ValidateString(super.Hostname);} }
        public string Hostname{ get {
            if (HasHostname) {
                return PBJ._PBJ.CastString(super.Hostname);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
        public const int ServiceFieldTag=2;
        public bool HasService{ get {return super.HasService&&PBJ._PBJ.ValidateString(super.Service);} }
        public string Service{ get {
            if (HasService) {
                return PBJ._PBJ.CastString(super.Service);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(Address prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static Address ParseFrom(pb::ByteString data) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data));
        }
        public static Address ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data,er));
        }
        public static Address ParseFrom(byte[] data) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data));
        }
        public static Address ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data,er));
        }
        public static Address ParseFrom(global::System.IO.Stream data) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data));
        }
        public static Address ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data,er));
        }
        public static Address ParseFrom(pb::CodedInputStream data) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data));
        }
        public static Address ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new Address(_PBJ_Internal.Address.ParseFrom(data,er));
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
            protected _PBJ_Internal.Address.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.Address.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.Address.Builder();}
            public Builder(_PBJ_Internal.Address.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(Address prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public Address BuildPartial() {return new Address(super.BuildPartial());}
            public Address Build() {if (_HasAllPBJFields) return new Address(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return Address.Descriptor; }            }
        public Builder ClearHostname() { super.ClearHostname();return this;}
        public const int HostnameFieldTag=1;
        public bool HasHostname{ get {return super.HasHostname&&PBJ._PBJ.ValidateString(super.Hostname);} }
        public string Hostname{ get {
            if (HasHostname) {
                return PBJ._PBJ.CastString(super.Hostname);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.Hostname=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearService() { super.ClearService();return this;}
        public const int ServiceFieldTag=2;
        public bool HasService{ get {return super.HasService&&PBJ._PBJ.ValidateString(super.Service);} }
        public string Service{ get {
            if (HasService) {
                return PBJ._PBJ.CastString(super.Service);
            } else {
                return PBJ._PBJ.CastString();
            }
        }
        set {
            super.Service=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Subscription.Protocol {
    public class Subscribe : PBJ.IMessage {
        protected _PBJ_Internal.Subscribe super;
        public _PBJ_Internal.Subscribe _PBJSuper{ get { return super;} }
        public Subscribe() {
            super=new _PBJ_Internal.Subscribe();
        }
        public Subscribe(_PBJ_Internal.Subscribe reference) {
            super=reference;
        }
        public static Subscribe defaultInstance= new Subscribe (_PBJ_Internal.Subscribe.DefaultInstance);
        public static Subscribe DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.Subscribe.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=6)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int BroadcastAddressFieldTag=7;
        public bool HasBroadcastAddress{ get {return super.HasBroadcastAddress;} }
        public Address BroadcastAddress{ get {
            if (HasBroadcastAddress) {
                return new Address(super.BroadcastAddress);
            } else {
                return new Address();
            }
        }
        }
        public const int BroadcastNameFieldTag=8;
        public bool HasBroadcastName{ get {return super.HasBroadcastName&&PBJ._PBJ.ValidateUuid(super.BroadcastName);} }
        public PBJ.UUID BroadcastName{ get {
            if (HasBroadcastName) {
                return PBJ._PBJ.CastUuid(super.BroadcastName);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
        public const int UpdatePeriodFieldTag=9;
        public bool HasUpdatePeriod{ get {return super.HasUpdatePeriod&&PBJ._PBJ.ValidateDuration(super.UpdatePeriod);} }
        public PBJ.Duration UpdatePeriod{ get {
            if (HasUpdatePeriod) {
                return PBJ._PBJ.CastDuration(super.UpdatePeriod);
            } else {
                return PBJ._PBJ.CastDuration();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(Subscribe prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static Subscribe ParseFrom(pb::ByteString data) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data));
        }
        public static Subscribe ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data,er));
        }
        public static Subscribe ParseFrom(byte[] data) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data));
        }
        public static Subscribe ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data,er));
        }
        public static Subscribe ParseFrom(global::System.IO.Stream data) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data));
        }
        public static Subscribe ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data,er));
        }
        public static Subscribe ParseFrom(pb::CodedInputStream data) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data));
        }
        public static Subscribe ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new Subscribe(_PBJ_Internal.Subscribe.ParseFrom(data,er));
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
            protected _PBJ_Internal.Subscribe.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.Subscribe.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.Subscribe.Builder();}
            public Builder(_PBJ_Internal.Subscribe.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(Subscribe prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public Subscribe BuildPartial() {return new Subscribe(super.BuildPartial());}
            public Subscribe Build() {if (_HasAllPBJFields) return new Subscribe(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return Subscribe.Descriptor; }            }
        public Builder ClearBroadcastAddress() { super.ClearBroadcastAddress();return this;}
        public const int BroadcastAddressFieldTag=7;
        public bool HasBroadcastAddress{ get {return super.HasBroadcastAddress;} }
        public Address BroadcastAddress{ get {
            if (HasBroadcastAddress) {
                return new Address(super.BroadcastAddress);
            } else {
                return new Address();
            }
        }
        set {
            super.BroadcastAddress=value._PBJSuper;
        }
        }
        public Builder ClearBroadcastName() { super.ClearBroadcastName();return this;}
        public const int BroadcastNameFieldTag=8;
        public bool HasBroadcastName{ get {return super.HasBroadcastName&&PBJ._PBJ.ValidateUuid(super.BroadcastName);} }
        public PBJ.UUID BroadcastName{ get {
            if (HasBroadcastName) {
                return PBJ._PBJ.CastUuid(super.BroadcastName);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.BroadcastName=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearUpdatePeriod() { super.ClearUpdatePeriod();return this;}
        public const int UpdatePeriodFieldTag=9;
        public bool HasUpdatePeriod{ get {return super.HasUpdatePeriod&&PBJ._PBJ.ValidateDuration(super.UpdatePeriod);} }
        public PBJ.Duration UpdatePeriod{ get {
            if (HasUpdatePeriod) {
                return PBJ._PBJ.CastDuration(super.UpdatePeriod);
            } else {
                return PBJ._PBJ.CastDuration();
            }
        }
        set {
            super.UpdatePeriod=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
namespace Sirikata.Subscription.Protocol {
    public class Broadcast : PBJ.IMessage {
        protected _PBJ_Internal.Broadcast super;
        public _PBJ_Internal.Broadcast _PBJSuper{ get { return super;} }
        public Broadcast() {
            super=new _PBJ_Internal.Broadcast();
        }
        public Broadcast(_PBJ_Internal.Broadcast reference) {
            super=reference;
        }
        public static Broadcast defaultInstance= new Broadcast (_PBJ_Internal.Broadcast.DefaultInstance);
        public static Broadcast DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.Broadcast.Descriptor; }        }
        public static class Types {
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=6)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int BroadcastNameFieldTag=7;
        public bool HasBroadcastName{ get {return super.HasBroadcastName&&PBJ._PBJ.ValidateUuid(super.BroadcastName);} }
        public PBJ.UUID BroadcastName{ get {
            if (HasBroadcastName) {
                return PBJ._PBJ.CastUuid(super.BroadcastName);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(Broadcast prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static Broadcast ParseFrom(pb::ByteString data) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data));
        }
        public static Broadcast ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data,er));
        }
        public static Broadcast ParseFrom(byte[] data) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data));
        }
        public static Broadcast ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data,er));
        }
        public static Broadcast ParseFrom(global::System.IO.Stream data) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data));
        }
        public static Broadcast ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data,er));
        }
        public static Broadcast ParseFrom(pb::CodedInputStream data) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data));
        }
        public static Broadcast ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new Broadcast(_PBJ_Internal.Broadcast.ParseFrom(data,er));
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
            protected _PBJ_Internal.Broadcast.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.Broadcast.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.Broadcast.Builder();}
            public Builder(_PBJ_Internal.Broadcast.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(Broadcast prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public Broadcast BuildPartial() {return new Broadcast(super.BuildPartial());}
            public Broadcast Build() {if (_HasAllPBJFields) return new Broadcast(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return Broadcast.Descriptor; }            }
        public Builder ClearBroadcastName() { super.ClearBroadcastName();return this;}
        public const int BroadcastNameFieldTag=7;
        public bool HasBroadcastName{ get {return super.HasBroadcastName&&PBJ._PBJ.ValidateUuid(super.BroadcastName);} }
        public PBJ.UUID BroadcastName{ get {
            if (HasBroadcastName) {
                return PBJ._PBJ.CastUuid(super.BroadcastName);
            } else {
                return PBJ._PBJ.CastUuid();
            }
        }
        set {
            super.BroadcastName=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
