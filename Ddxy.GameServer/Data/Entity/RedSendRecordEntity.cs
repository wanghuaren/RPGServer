using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;
using FreeSql.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "red_send_record")]
    public class RedSendRecordEntity
    {
        /// <summary>
        /// id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 所属用户id
        /// </summary>
        [Column(Name = "sid")]
        public uint ServerId { get; set; }

        /// <summary>
        /// 发送角色ID
        /// </summary>
        public uint RoleId { get; set; }

        /// <summary>
        /// 红包类型
        /// </summary>
        public byte RedType { get; set; }

        /// <summary>
        /// 帮派ID（帮派红包有效）
        /// </summary>
        public uint SectId { get; set; }

        /// <summary>
        /// 仙玉
        /// </summary>
        public uint Jade { get; set; }

        /// <summary>
        /// 总计个数
        /// </summary>
        public uint Total { get; set; }

        /// <summary>
        /// 祝福
        /// </summary>
        public string Wish { get; set; }

        /// <summary>
        /// 剩余个数
        /// </summary>
        public uint Left { get; set; }

        /// <summary>
        /// 接收者列表
        /// </summary>
        public string Reciver { get; set; }

        /// <summary>
        /// 发送时间
        /// </summary>
        public uint SendTime { get; set; }

        [Column(IsIgnore = true)] public List<uint> ReciverList { get; set; }
        [Column(IsIgnore = true)] public RoleInfo Sender { get; set; }
        [Column(IsIgnore = true)] public Dictionary<uint, RedGetter> Getter { get; set; }
        [Column(IsIgnore = true)] public List<uint> RedJadeList { get; set; }

        public async Task ParseReciver(string TAG, ILogger logger)
        {
            Getter?.Clear();
            Getter = new();
            ReciverList?.Clear();
            ReciverList = new List<uint>();
            if (Reciver.Length > 0)
            {
                ReciverList = Json.SafeDeserialize<List<uint>>(Reciver);
            }
            RedJadeList?.Clear();
            RedJadeList = new List<uint>();
            int got_jade = (int)(await DbService.Sql.Queryable<RedReciveRecordEntity>().Where(it => it.RedId == Id).SumAsync(it => it.Jade));
            int left_jade = (int)(Jade > got_jade ? Jade - got_jade : 0);
            int total_jade = left_jade;
            int total_player = Math.Max(0, (int)Total - ReciverList.Count);
            int left_player = total_player;
            if (total_player > 0 && left_jade > 0)
            {
                int reserved = (int)((float)left_jade / (total_player * 2));
                var random = new Random();
                for (int i = 0; i < total_player - 1; i++)
                {
                    int amount = random.Next(reserved, (int)(2 * (float)left_jade / left_player) - reserved);
                    left_jade = left_jade > amount ? left_jade - amount : 0;
                    left_player -= 1;
                    RedJadeList.Add((uint)Math.Max(amount, 0));
                }
                RedJadeList.Add((uint)Math.Max(left_jade, 0));
                RedJadeList.Sort((a, b) => (int)(b - a));
                int index = 0;
                for (int i = 0; i < RedJadeList.Count; i++)
                {
                    if (RedJadeList[i] <= 0)
                    {
                        var t = RedJadeList[index];
                        RedJadeList[i] = (uint)random.Next((int)(t / 3), (int)(t / 2));
                        RedJadeList[index] = t - RedJadeList[i];
                        index++;
                    }
                }
                uint sum = 0;
                foreach (var v in RedJadeList)
                {
                    if (v <= 0)
                    {
                        logger.LogError($"{TAG}分配错误，有红包金额小于等于0");
                    }
                    sum += v;
                }
                if (sum != total_jade)
                {
                    logger.LogError($"{TAG}分配错误，总金额不对sum[{sum}]!=[{total_jade}]");
                }
                if (RedJadeList.Count != total_player)
                {
                    logger.LogError($"{TAG}分配错误，红包数量不够total_player[{total_player}]!=RedJadeList[{RedJadeList.Count}]");
                }
            }
            else
            {
                Left = 0;
            }
        }
        public void SyncReciver()
        {
            Reciver = ReciverList == null ? "[]" : Json.SafeSerialize(ReciverList);
        }

        public void CopyFrom(RedSendRecordEntity other)
        {
            Id = other.Id;
            ServerId = other.ServerId;
            RoleId = other.RoleId;
            RedType = other.RedType;
            SectId = other.SectId;
            Jade = other.Jade;
            Total = other.Total;
            Wish = other.Wish;
            Left = other.Left;
            Reciver = other.Reciver;
            SendTime = other.SendTime;
            ReciverList = new List<uint>(other.ReciverList);
        }

        public RedSendRecordEntity MakeCopy()
        {
            return new RedSendRecordEntity()
            {
                Id = Id,
                ServerId = ServerId,
                RoleId = RoleId,
                RedType = RedType,
                SectId = SectId,
                Jade = Jade,
                Total = Total,
                Wish = Wish,
                Left = Left,
                Reciver = Reciver,
                SendTime = SendTime,
                ReciverList = new List<uint>(ReciverList)
            };
        }

        public bool Equals(RedSendRecordEntity other)
        {
            if (other == null) return false;
            return Id == other.Id &&
                   ServerId == other.ServerId &&
                   RoleId == other.RoleId &&
                   RedType == other.RedType &&
                   SectId == other.SectId &&
                   Jade == other.Jade &&
                   Total == other.Total &&
                   Wish.Equals(other.Wish) &&
                   Left == other.Left &&
                   Reciver.Equals(other.Reciver) &&
                   SendTime == other.SendTime;
        }
    }
}