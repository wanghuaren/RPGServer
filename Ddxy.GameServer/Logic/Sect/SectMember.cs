using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.Protocol;
using Google.Protobuf;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Logic.Sect
{
    public class SectMember
    {
        public SectMemberData Data { get; set; }

        public IPlayerGrain Grain { get; set; }

        public uint Id => Data?.Id ?? 0;

        public SectMemberType SectMemberType => (SectMemberType)(Data?.Type);

        public bool Online => Data is {Online: true} && Grain != null;

        public void SendSectData(SectData data)
        {
            if (Online)
            {
                _ = Grain.SendMessage(
                    new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectData, new S2C_SectData {Data = data})));
            }
        }

        public void SendPacket(GameCmd cmd, IMessage msg)
        {
            if (Online)
            {
                _ = Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
            }
        }

        public void OnEnterSect(uint sectId, string sectName, uint ownerId)
        {
            if (Grain != null)
                _ = Grain.OnEnterSect(sectId, sectName, ownerId, (byte) Data.Type);
        }

        public void OnExitSect(uint sectId, string sectName, uint ownerId)
        {
            if (Grain != null)
                _ = Grain.OnExitSect(sectId, sectName, ownerId);
        }

        public void OnSectJob(uint sectId, string sectName, uint targetRoleId, SectMemberType type)
        {
            if (Grain != null)
                _ = Grain.OnSectJob(sectId, sectName, targetRoleId, (byte) type);
        }

        public void OnSectSilent(uint sectId, string sectName, uint opRoleId, string opName, SectMemberType opJob)
        {
            if (Grain != null)
                _ = Grain.OnSectSilent(sectId, sectName, opRoleId, opName, (byte) opJob);
        }
    }
}