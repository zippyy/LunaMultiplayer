﻿using LunaClient.Utilities;
using LunaCommon;
using LunaCommon.Message.Data.Flag;
using System.IO;

namespace LunaClient.Systems.Flag
{
    public class ExtendedFlagInfo : FlagInfo
    {
        public string ShaSum => Common.CalculateSha256Hash(FlagData);
        public bool Loaded { get; set; }
        public string FlagPath => CommonUtil.CombinePaths(FlagSystem.FlagPath, FlagName);
        public bool FlagExists => File.Exists(FlagPath);

        public ExtendedFlagInfo(FlagInfo flagInfo)
        {
            FlagData = Common.TrimArray(flagInfo.FlagData, flagInfo.NumBytes);
            Owner = flagInfo.Owner;
            FlagName = flagInfo.FlagName;
        }
    }
}