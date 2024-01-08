using System;
using System.Collections.Generic;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.Protocol;
using Google.Protobuf;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Logic.SectWar
{
    public class SectWarSect : IDisposable
    {
        // 一对帮派共用id, 便于地图服务器做视图裁剪
        public uint SectWarId { get; set; }

        /// <summary>
        /// 帮派人数
        /// </summary>
        public uint Total { get; set; }

        /// <summary>
        /// 帮派数据
        /// </summary>
        public SectWarSectData Data { get; private set; }

        /// <summary>
        /// 已入场的玩家
        /// </summary>
        public List<SectWarMember> Members { get; private set; }

        /// <summary>
        /// 敌方帮派
        /// </summary>
        public SectWarSect Enemy { get; set; }

        public SectWarArena Arena { get; set; }

        public SectWarCannon Cannon { get; set; }

        /// <summary>
        /// 帮派id
        /// </summary>
        public uint SectId => Data.Id;

        public string SectName => Data.Name;

        public int Camp
        {
            get => Data.Camp;
            set => Data.Camp = value;
        }

        /// <summary>
        /// 城门体力
        /// </summary>
        public int DoorHp => Data.DoorHp;

        /// <summary>
        /// 是否已经输了
        /// </summary>
        public bool IsDead => Data.DoorHp <= 0;

        public bool HasPlayer => Members.Count > 0;

        private ISectGrain _sectGrain;

        /// <summary>
        /// 入场人数
        /// </summary>
        public int EnterMemeberNum
        {
            get
            {
                var sum = 0;
                foreach (var swm in Members)
                {
                    if (swm == null) continue;
                    sum += 1;
                    if (swm.Members != null) sum += swm.Members.Count;
                }

                return sum;
            }
        }

        public SectWarSect(SectData data, ISectGrain sectGrain)
        {
            Total = data.Total;
            Data = new SectWarSectData
            {
                Id = data.Id,
                Name = data.Name,
                OwnerId = data.OwnerId,
                Contrib = data.Contrib,
                CreateTime = data.CreateTime,

                Camp = 0,
                DoorHp = 5000 //默认5000点体力
            };
            Members = new List<SectWarMember>(100);

            _sectGrain = sectGrain;
            _ = sectGrain.SyncSectWaring(true);
        }

        public void Dispose()
        {
            _ = _sectGrain.SyncSectWaring(false);
            _sectGrain = null;

            SectWarId = 0;
            Data = null;
            Members.Clear();
            Members = null;
            Arena?.Dispose();
            Arena = null;
            Cannon?.Dispose();
            Cannon = null;
        }

        public int AddHp(int value)
        {
            if (value == 0 || Data.DoorHp == 0) return 0;
            if (value > 0)
            {
                Data.DoorHp += value;
                return value;
            }

            var sub = Math.Abs(value);
            if (sub > Data.DoorHp) sub = Data.DoorHp;
            Data.DoorHp -= sub;
            return sub * -1;
        }

        public void Broadcast(GameCmd cmd, IMessage msg)
        {
            if (Members == null || Members.Count == 0) return;
            Broadcast(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
        }

        public void Broadcast(Immutable<byte[]> bytes, uint ignore = 0)
        {
            foreach (var player in Members)
            {
                if (ignore == 0 || player.Id != ignore)
                    player.SendMessage(bytes, false);
            }
        }

        public void OnSectWarResult(bool win)
        {
            foreach (var player in Members)
            {
                player.OnSectWarResult(win, false);
            }
        }
    }
}