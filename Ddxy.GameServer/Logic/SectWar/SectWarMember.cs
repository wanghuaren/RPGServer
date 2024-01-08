using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.Protocol;
using Google.Protobuf;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Logic.SectWar
{
    public class SectWarMember : IDisposable
    {
        public SectWarRoleData Data { get; private set; }

        public IPlayerGrain Grain { get; private set; }

        public uint SectId { get; private set; }

        public SectWarPlace Place { get; set; }

        public SectWarRoleState State { get; private set; }

        public uint Id => Data.Id;

        public string Name => Data.Name;

        public int Camp => Data.Camp;

        public bool InTeam => Data.Team > 0;

        public bool IsTeamLeader => InTeam && Members != null;

        // 当前如果在战斗中, 战斗对手id
        public uint BattleRoleId { get; set; }

        // 锁定城门后每隔5s进行一次攻击扣点
        public IDisposable LockDoorTimer { get; set; }

        public List<SectWarMember> Members { get; set; }

        public bool Online { get; set; }

        // 上次战斗失败的时间
        public uint LostTime { get; set; }

        public SectWarMember(SectWarRoleData data, uint sectId, IPlayerGrain grain)
        {
            Data = data;
            Grain = grain;
            SectId = sectId;
            Place = SectWarPlace.JiDi;
            State = SectWarRoleState.Idle;
            Online = true;
        }

        public void Dispose()
        {
            if (Grain != null)
                _ = Grain.OnExitSectWar();
            Grain = null;
            Data = null;
            SectId = 0;
            Place = SectWarPlace.JiDi;
            State = SectWarRoleState.Idle;
            BattleRoleId = 0;
            LockDoorTimer?.Dispose();
            LockDoorTimer = null;
        }

        public void BuildTeam(uint teamId)
        {
            Data.Team = teamId;
            Members = new List<SectWarMember>(4);
        }

        public void DestroyTeam()
        {
            Data.Team = 0;
            Members?.Clear();
            Members = null;
        }

        public void AddTeamMember(SectWarMember member)
        {
            member.Data.Team = Data.Team;
            member.Members?.Clear();
            member.Members = null;
            var idx = Members.FindIndex(p => p.Id == member.Id);
            if (idx < 0) Members.Add(member);
        }

        public SectWarMember DelTeamMember(uint roleId)
        {
            var idx = Members.FindIndex(p => p.Id == roleId);
            if (idx < 0) return null;
            var member = Members[idx];
            Members.RemoveAt(idx);
            member.Data.Team = 0;
            member.Members?.Clear();
            member.Members = null;
            return member;
        }

        public void SendMessage(Immutable<byte[]> bytes, bool broadCastTeam)
        {
            if (Grain != null && Online)
            {
                _ = Grain.SendMessage(bytes);
            }

            if (broadCastTeam && IsTeamLeader)
            {
                foreach (var swm in Members)
                {
                    swm.SendMessage(bytes, false);
                }
            }
        }

        public void SendPacket(GameCmd cmd, IMessage msg, bool broadCastTeam)
        {
            SendMessage(new Immutable<byte[]>(Packet.Serialize(cmd, msg)), broadCastTeam);
        }

        public void SendNotice(string notice, bool broadCastTeam)
        {
            if (string.IsNullOrWhiteSpace(notice)) return;
            SendPacket(GameCmd.S2CNotice, new S2C_Notice {Text = notice}, broadCastTeam);
        }

        public ValueTask<int> StartPvp(uint targetRoleId, BattleType type)
        {
            if (Grain == null) return new ValueTask<int>(0);
            return Grain.StartPvp(targetRoleId, (byte) type);
        }

        public void OnSectWarResult(bool win, bool broadCastTeam)
        {
            if (Grain != null) _ = Grain.OnSectWarResult(win);
            if (broadCastTeam && IsTeamLeader)
            {
                foreach (var swm in Members)
                {
                    swm.OnSectWarResult(win, false);
                }
            }
        }

        public void SyncState(SectWarRoleState state, bool broadCastTeam)
        {
            State = state;
            if (Grain != null) _ = Grain.OnSectWarState((byte) state);

            if (broadCastTeam && IsTeamLeader)
            {
                foreach (var swm in Members)
                {
                    swm.SyncState(state, false);
                }
            }
        }

        public void SyncPlace(SectWarPlace place, bool broadCastTeam)
        {
            Place = place;
            if (Grain != null) _ = Grain.OnSectWarPlace((byte) place);

            if (broadCastTeam && IsTeamLeader)
            {
                foreach (var swm in Members)
                {
                    swm.SyncPlace(place, false);
                }
            }
        }

        public void SetLostTime()
        {
            LostTime = TimeUtil.TimeStamp;
            if (IsTeamLeader)
            {
                foreach (var swm in Members)
                {
                    swm.LostTime = LostTime;
                }
            }
        }

        public void ClearLostTime()
        {
            LostTime = 0;
            if (IsTeamLeader)
            {
                foreach (var swm in Members)
                {
                    swm.LostTime = LostTime;
                }
            }
        }

        public uint CheckLostTime()
        {
            if (LostTime == 0) return 0;
            // 要等1分钟
            var expire = LostTime + 60;
            var now = TimeUtil.TimeStamp;
            if (now > expire) return 0;
            return expire - now;
        }
    }
}