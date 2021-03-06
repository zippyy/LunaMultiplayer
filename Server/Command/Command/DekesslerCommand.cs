using LunaCommon.Message.Data.Vessel;
using LunaCommon.Message.Server;
using LunaCommon.Xml;
using Server.Command.Command.Base;
using Server.Context;
using Server.Log;
using Server.Server;
using Server.Settings;
using Server.System;
using System;
using System.Xml;

namespace Server.Command.Command
{
    public class DekesslerCommand : SimpleCommand
    {
        private static long _lastDekesslerTime;

        public static void CheckTimer()
        {
            //0 or less is disabled.
            if (GeneralSettings.SettingsStore.AutoDekessler > 0 &&
                ServerContext.ServerClock.ElapsedMilliseconds - _lastDekesslerTime >
                TimeSpan.FromMinutes(GeneralSettings.SettingsStore.AutoDekessler).TotalMilliseconds)
            {
                _lastDekesslerTime = ServerContext.ServerClock.ElapsedMilliseconds;
                RunDekessler();
            }
        }

        public override void Execute(string commandArgs)
        {
            RunDekessler();
        }

        private static void RunDekessler()
        {
            var removalCount = 0;

            var vesselList = VesselStoreSystem.CurrentVesselsInXmlFormat.ToArray();
            foreach (var vesselKeyVal in vesselList)
            {
                if (IsVesselDebris(vesselKeyVal.Key, vesselKeyVal.Value))
                {
                    LunaLog.Normal($"Removing debris vessel: {vesselKeyVal.Key}");

                    VesselStoreSystem.RemoveVessel(vesselKeyVal.Key);

                    //Send a vessel remove message
                    var msgData = ServerContext.ServerMessageFactory.CreateNewMessageData<VesselRemoveMsgData>();
                    msgData.VesselId = vesselKeyVal.Key;

                    MessageQueuer.SendToAllClients<VesselSrvMsg>(msgData);

                    removalCount++;
                }
            }

            if (removalCount > 0)
                LunaLog.Normal($"Removed {removalCount} debris");
        }

        private static bool IsVesselDebris(Guid vesselId, string vesselData)
        {
            try
            {
                var document = new XmlDocument();
                document.LoadXml(vesselData);

                var typeNode = document.SelectSingleNode($"/{ConfigNodeXmlParser.StartElement}/{ConfigNodeXmlParser.ValueNode}[@name='type']");
                if (typeNode != null) return typeNode.InnerText.ToLower().Contains("debris");
            }
            catch (Exception e)
            {
                LunaLog.Error($"Error while checking if vessel {vesselId} is debris. Details {e}");
            }

            return false;
        }
    }
}