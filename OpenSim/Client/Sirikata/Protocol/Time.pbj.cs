using pbd = global::Google.ProtocolBuffers.Descriptors;
using pb = global::Google.ProtocolBuffers;
namespace Sirikata.Network.Protocol {
    public class TimeSync : PBJ.IMessage {
        protected _PBJ_Internal.TimeSync super;
        public _PBJ_Internal.TimeSync _PBJSuper{ get { return super;} }
        public TimeSync() {
            super=new _PBJ_Internal.TimeSync();
        }
        public TimeSync(_PBJ_Internal.TimeSync reference) {
            super=reference;
        }
        public static TimeSync defaultInstance= new TimeSync (_PBJ_Internal.TimeSync.DefaultInstance);
        public static TimeSync DefaultInstance{
            get {return defaultInstance;}
        }
        public static pbd.MessageDescriptor Descriptor {
            get { return _PBJ_Internal.TimeSync.Descriptor; }        }
        public static class Types {
        public enum ReturnOptions {
            REPLY_RELIABLE=_PBJ_Internal.TimeSync.Types.ReturnOptions.REPLY_RELIABLE,
            REPLY_ORDERED=_PBJ_Internal.TimeSync.Types.ReturnOptions.REPLY_ORDERED
        };
        }
        public static bool WithinReservedFieldTagRange(int field_tag) {
            return false||(field_tag>=1&&field_tag<=8)||(field_tag>=1536&&field_tag<=2560)||(field_tag>=229376&&field_tag<=294912);
        }
        public static bool WithinExtensionFieldTagRange(int field_tag) {
            return false;
        }
        public const int ClientTimeFieldTag=9;
        public bool HasClientTime{ get {return super.HasClientTime&&PBJ._PBJ.ValidateTime(super.ClientTime);} }
        public PBJ.Time ClientTime{ get {
            if (HasClientTime) {
                return PBJ._PBJ.CastTime(super.ClientTime);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        }
        public const int ServerTimeFieldTag=10;
        public bool HasServerTime{ get {return super.HasServerTime&&PBJ._PBJ.ValidateTime(super.ServerTime);} }
        public PBJ.Time ServerTime{ get {
            if (HasServerTime) {
                return PBJ._PBJ.CastTime(super.ServerTime);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        }
        public const int SyncRoundFieldTag=11;
        public bool HasSyncRound{ get {return super.HasSyncRound&&PBJ._PBJ.ValidateUint64(super.SyncRound);} }
        public ulong SyncRound{ get {
            if (HasSyncRound) {
                return PBJ._PBJ.CastUint64(super.SyncRound);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        }
        public const int ReturnOptionsFieldTag=14;
        public bool HasReturnOptions { get {
            if (!super.HasReturnOptions) return false;
            return PBJ._PBJ.ValidateFlags(super.ReturnOptions,(ulong)Types.ReturnOptions.REPLY_RELIABLE|(ulong)Types.ReturnOptions.REPLY_ORDERED);
        } }
        public uint ReturnOptions{ get {
            if (HasReturnOptions) {
                return (uint)PBJ._PBJ.CastFlags(super.ReturnOptions,(ulong)Types.ReturnOptions.REPLY_RELIABLE|(ulong)Types.ReturnOptions.REPLY_ORDERED);
            } else {
                return (uint)PBJ._PBJ.CastFlags((ulong)Types.ReturnOptions.REPLY_RELIABLE|(ulong)Types.ReturnOptions.REPLY_ORDERED);
            }
        }
        }
        public const int RoundTripFieldTag=2561;
        public bool HasRoundTrip{ get {return super.HasRoundTrip&&PBJ._PBJ.ValidateTime(super.RoundTrip);} }
        public PBJ.Time RoundTrip{ get {
            if (HasRoundTrip) {
                return PBJ._PBJ.CastTime(super.RoundTrip);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        }
            public override Google.ProtocolBuffers.IMessage _PBJISuper { get { return super; } }
        public override PBJ.IMessage.IBuilder WeakCreateBuilderForType() { return new Builder(); }
        public static Builder CreateBuilder() { return new Builder(); }
        public static Builder CreateBuilder(TimeSync prototype) {
            return (Builder)new Builder().MergeFrom(prototype);
        }
        public static TimeSync ParseFrom(pb::ByteString data) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data));
        }
        public static TimeSync ParseFrom(pb::ByteString data, pb::ExtensionRegistry er) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data,er));
        }
        public static TimeSync ParseFrom(byte[] data) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data));
        }
        public static TimeSync ParseFrom(byte[] data, pb::ExtensionRegistry er) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data,er));
        }
        public static TimeSync ParseFrom(global::System.IO.Stream data) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data));
        }
        public static TimeSync ParseFrom(global::System.IO.Stream data, pb::ExtensionRegistry er) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data,er));
        }
        public static TimeSync ParseFrom(pb::CodedInputStream data) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data));
        }
        public static TimeSync ParseFrom(pb::CodedInputStream data, pb::ExtensionRegistry er) {
            return new TimeSync(_PBJ_Internal.TimeSync.ParseFrom(data,er));
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
            protected _PBJ_Internal.TimeSync.Builder super;
            public override Google.ProtocolBuffers.IBuilder _PBJISuper { get { return super; } }
            public _PBJ_Internal.TimeSync.Builder _PBJSuper{ get { return super;} }
            public Builder() {super = new _PBJ_Internal.TimeSync.Builder();}
            public Builder(_PBJ_Internal.TimeSync.Builder other) {
                super=other;
            }
            public Builder Clone() {return new Builder(super.Clone());}
            public Builder MergeFrom(TimeSync prototype) { super.MergeFrom(prototype._PBJSuper);return this;}
            public Builder Clear() {super.Clear();return this;}
            public TimeSync BuildPartial() {return new TimeSync(super.BuildPartial());}
            public TimeSync Build() {if (_HasAllPBJFields) return new TimeSync(super.Build());return null;}
            public pbd::MessageDescriptor DescriptorForType {
                get { return TimeSync.Descriptor; }            }
        public Builder ClearClientTime() { super.ClearClientTime();return this;}
        public const int ClientTimeFieldTag=9;
        public bool HasClientTime{ get {return super.HasClientTime&&PBJ._PBJ.ValidateTime(super.ClientTime);} }
        public PBJ.Time ClientTime{ get {
            if (HasClientTime) {
                return PBJ._PBJ.CastTime(super.ClientTime);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        set {
            super.ClientTime=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearServerTime() { super.ClearServerTime();return this;}
        public const int ServerTimeFieldTag=10;
        public bool HasServerTime{ get {return super.HasServerTime&&PBJ._PBJ.ValidateTime(super.ServerTime);} }
        public PBJ.Time ServerTime{ get {
            if (HasServerTime) {
                return PBJ._PBJ.CastTime(super.ServerTime);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        set {
            super.ServerTime=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearSyncRound() { super.ClearSyncRound();return this;}
        public const int SyncRoundFieldTag=11;
        public bool HasSyncRound{ get {return super.HasSyncRound&&PBJ._PBJ.ValidateUint64(super.SyncRound);} }
        public ulong SyncRound{ get {
            if (HasSyncRound) {
                return PBJ._PBJ.CastUint64(super.SyncRound);
            } else {
                return PBJ._PBJ.CastUint64();
            }
        }
        set {
            super.SyncRound=(PBJ._PBJ.Construct(value));
        }
        }
        public Builder ClearReturnOptions() { super.ClearReturnOptions();return this;}
        public const int ReturnOptionsFieldTag=14;
        public bool HasReturnOptions { get {
            if (!super.HasReturnOptions) return false;
            return PBJ._PBJ.ValidateFlags(super.ReturnOptions,(ulong)Types.ReturnOptions.REPLY_RELIABLE|(ulong)Types.ReturnOptions.REPLY_ORDERED);
        } }
        public uint ReturnOptions{ get {
            if (HasReturnOptions) {
                return (uint)PBJ._PBJ.CastFlags(super.ReturnOptions,(ulong)Types.ReturnOptions.REPLY_RELIABLE|(ulong)Types.ReturnOptions.REPLY_ORDERED);
            } else {
                return (uint)PBJ._PBJ.CastFlags((ulong)Types.ReturnOptions.REPLY_RELIABLE|(ulong)Types.ReturnOptions.REPLY_ORDERED);
            }
        }
        set {
            super.ReturnOptions=((value));
        }
        }
        public Builder ClearRoundTrip() { super.ClearRoundTrip();return this;}
        public const int RoundTripFieldTag=2561;
        public bool HasRoundTrip{ get {return super.HasRoundTrip&&PBJ._PBJ.ValidateTime(super.RoundTrip);} }
        public PBJ.Time RoundTrip{ get {
            if (HasRoundTrip) {
                return PBJ._PBJ.CastTime(super.RoundTrip);
            } else {
                return PBJ._PBJ.CastTime();
            }
        }
        set {
            super.RoundTrip=(PBJ._PBJ.Construct(value));
        }
        }
        }
    }
}
