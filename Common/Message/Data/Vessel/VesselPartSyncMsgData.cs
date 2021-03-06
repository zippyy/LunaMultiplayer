﻿using Lidgren.Network;
using LunaCommon.Message.Base;
using LunaCommon.Message.Types;
using System;

namespace LunaCommon.Message.Data.Vessel
{
    public class VesselPartSyncMsgData : VesselBaseMsgData
    {
        internal VesselPartSyncMsgData() { }

        public Guid VesselId;
        public uint PartFlightId;
        public string ModuleName;
        public string BaseModuleName;
        public string FieldName;
        public string Value;

        public override VesselMessageType VesselMessageType => VesselMessageType.PartSync;

        public override string ClassName { get; } = nameof(VesselPartSyncMsgData);

        internal override void InternalSerialize(NetOutgoingMessage lidgrenMsg)
        {
            base.InternalSerialize(lidgrenMsg);

            GuidUtil.Serialize(VesselId, lidgrenMsg);
            lidgrenMsg.Write(PartFlightId);
            lidgrenMsg.Write(ModuleName);
            lidgrenMsg.Write(BaseModuleName);
            lidgrenMsg.Write(FieldName);
            lidgrenMsg.Write(Value);
        }

        internal override void InternalDeserialize(NetIncomingMessage lidgrenMsg)
        {
            base.InternalDeserialize(lidgrenMsg);

            VesselId = GuidUtil.Deserialize(lidgrenMsg);
            PartFlightId = lidgrenMsg.ReadUInt32();
            ModuleName = lidgrenMsg.ReadString();
            BaseModuleName = lidgrenMsg.ReadString();
            FieldName = lidgrenMsg.ReadString();
            Value = lidgrenMsg.ReadString();
        }

        internal override int InternalGetMessageSize()
        {
            return base.InternalGetMessageSize() + GuidUtil.GetByteSize() + sizeof(uint) + ModuleName.GetByteCount() + BaseModuleName.GetByteCount() + FieldName.GetByteCount() + Value.GetByteCount();
        }
    }
}
