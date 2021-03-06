﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;
using LunaCommon.Message.Base;

namespace LunaCommon.Message.Data.ShareProgress
{
    /// <summary>
    /// Wrapper for transmitting the ksp Strategy objects (for the career strategies).
    /// </summary>
    public class StrategyInfo
    {
        public string Title;
        public float Factor;
        public bool IsActive;
        public int NumBytes;
        public byte[] Data = new byte[0];

        public StrategyInfo()
        {
            
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="copyFrom"></param>
        public StrategyInfo(StrategyInfo copyFrom)
        {
            Title = copyFrom.Title;
            NumBytes = copyFrom.NumBytes;
            if (Data.Length < NumBytes)
                Data = new byte[NumBytes];

            Array.Copy(copyFrom.Data, Data, NumBytes);
        }

        public void Serialize(NetOutgoingMessage lidgrenMsg)
        {
            lidgrenMsg.Write(Title);
            lidgrenMsg.Write(Factor);
            lidgrenMsg.Write(IsActive);
            lidgrenMsg.Write(NumBytes);
            lidgrenMsg.Write(Data, 0, NumBytes);
        }

        public void Deserialize(NetIncomingMessage lidgrenMsg)
        {
            Title = lidgrenMsg.ReadString();
            Factor = lidgrenMsg.ReadFloat();
            IsActive = lidgrenMsg.ReadBoolean();

            NumBytes = lidgrenMsg.ReadInt32();
            if (Data.Length < NumBytes)
                Data = new byte[NumBytes];

            lidgrenMsg.ReadBytes(Data, 0, NumBytes);
        }

        public int GetByteCount()
        {
            return Title.GetByteCount() + sizeof(float) + sizeof(bool) + sizeof(int) + sizeof(byte) * NumBytes;
        }
    }
}
