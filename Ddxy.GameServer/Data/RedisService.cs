using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Model.Admin;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Data.Fields;
using Ddxy.GameServer.Data.Vo;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;
using StackExchange.Redis;
using Ddxy.GameServer.Core;
using Microsoft.Extensions.Logging;
// ReSharper disable CA1806
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Grains;
using FreeSql;
using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;
using Orleans.Configuration;

namespace Ddxy.GameServer.Data
{
    // 转盘--物品配置
    public class RDLDRewardItem
    {
        // 物品配置ID
        public uint id { get; set; }
        // 物品数量
        public uint num { get; set; }
    }
    // 转盘--宝箱
    public class RDLDChest
    {
        // 宠物ID
        public uint pet { get; set; }
        // 物品列表
        public List<RDLDRewardItem> itemList { get; set; }
        // 创建时间
        public uint createTime { get; set; }
    }
    public static class RedisService
    {

        private static readonly ILogger Logger = XLogFactory.Create(typeof(RedisService));

        private static ConnectionMultiplexer _multiplexer;
        private static IDatabase _db;

        private const string KeyUserTokenFmt = "token_{0}"; // 用户登录后产生的token
        private static readonly TimeSpan UserTokenExpire = TimeSpan.FromDays(1);

        private const string KeyRoleInfoFmt = "role_{0}"; //角色基础信息
        private const string KeyLevelRankFmt = "rank_lv_{0}"; //等级排行榜
        private const string KeyJadeRankFmt = "rank_jade_{0}"; //仙玉排行榜
        private const string KeySldhRankFmt = "rank_sldh_{0}"; //水路排行榜
        private const string KeyCszlLayerRankFmt = "rank_cszl_layer_{0}"; //成神之路爬塔排行榜
        private const string KeyWzzzRankFmt = "rank_wzzz_{0}"; //王者之战排行榜
        private const string KeySinglePkRankFmt = "rank_singlePk_{0}"; //单人Pk排行榜
        private const string KeyDaLuanDouRankFmt = "rank_DaLuanDou_{0}"; //大乱斗PK排行榜
        private const string KeyPayRankFmt = "rank_pay_{0}"; //累计充值排行榜

        private const string KeyFriendListFmt = "fri_{0}"; //我的好友列表
        private const string KeyFriendApplyListFmt = "fri_apply_{0}"; //我的好友申请列表

        private const string KeyEquipInfoFmt = "equip_{0}"; //分享的equip
        private static readonly TimeSpan EquipInfoExpire = TimeSpan.FromHours(1);

        private const string KeyOrnamentInfoFmt = "ornament_{0}"; //分享的pet
        private static readonly TimeSpan OrnamentInfoExpire = TimeSpan.FromHours(1);
        private const string KeyPetOrnamentInfoFmt = "pet_ornament_{0}"; //分享的宠物配饰
        private static readonly TimeSpan PetOrnamentInfoExpire = TimeSpan.FromHours(1);

        private const string KeyPetInfoFmt = "pet_{0}"; //分享的pet
        private static readonly TimeSpan PetInfoExpire = TimeSpan.FromHours(1);

        private const string KeyNotice = "notice"; //全局公告
        private const string KeyPayEnable = "pay_enable"; //开放充值
        private const string KeyPayRateJade = "pay_rate"; //每1块钱得多少仙玉, 充值比例
        private const string KeyPayRateBindJade = "pay_rate_bind_jade"; //每1块钱得多少积分, 充值比例

        private const string KeyAdminInfoFmt = "admin_{0}"; // admin登录后产生的token
        private const string KeyAgencyInfoFmt = "agency_{0}"; // admin登录后记录的代理等级
        private static readonly TimeSpan AdminInfoExpire = TimeSpan.FromDays(1);

        private const string KeyAgentPayLockFmt = "agpay_lock_{0}";
        private static readonly TimeSpan AgentPayLockExpire = TimeSpan.FromSeconds(5);

        // 神兽降临
        private const string KeyShenShouJiangLinRewardLockFmt = "shen_shou_jiang_lin_lock_{0}";
        private static readonly TimeSpan ShenShouJiangLinLockExpire = TimeSpan.FromSeconds(5);
        private const string KeyShenShouJiangLinRewardFmt = "shen_shou_jiang_lin_value_{0}";
        // 转盘
        private const string KeyLuckyDrawChestFmt = "ld_chest_{0}";
        private const string KeyLuckyDrawRolePoint = "ld_role_point";
        private const string KeyLuckyDrawRoleChest = "ld_role_chest";
        private const string KeyLuckyDrawRoleFreeLastReset = "ld_role_free_last_reset";
        private const string KeyLuckyDrawRoleFree = "ld_role_free";
        // 限时充值排行榜
        private const string KeyLimitChargeRankFmt = "limit_charge_rank_{0}";
        private const string KeyLimitChargeRankStartFmt = "limit_charge_rank_start_{0}";
        private const string KeyLimitChargeRankEndFmt = "limit_charge_rank_end_{0}";
        private const string KeyLimitChargeRankSettleStateFmt = "limit_charge_rank_settle_state_{0}";
        private static ConcurrentDictionary<uint, uint> LimitChargeRankStartTime = new ();
        private static ConcurrentDictionary<uint, uint> LimitChargeRankEndTime = new ();
        // 限时充值排行榜奖励
        private const string KeyLimitChargeRankGift = "limit_charge_rank_gift_{0}";
        private static ConcurrentDictionary<uint, uint> LimitChargeRankRecState = new();
        private static ConcurrentDictionary<uint, uint> LimitChargeRankSettleRank = new();
        // 限时等级排行榜
        private const string KeyLimitLevelRankFmt = "limit_level_rank_{0}";
        private const string KeyLimitLevelRankStartFmt = "limit_level_rank_start_{0}";
        private const string KeyLimitLevelRankEndFmt = "limit_level_rank_end_{0}";
        private const string KeyLimitLevelRankSettleStateFmt = "limit_level_rank_settle_state_{0}";
        private static ConcurrentDictionary<uint, uint> LimitLevelRankStartTime = new ();
        private static ConcurrentDictionary<uint, uint> LimitLevelRankEndTime = new ();
        // 限时等级排行榜奖励
        private const string KeyLimitLevelRankGift = "limit_level_rank_gift_{0}";
        private static ConcurrentDictionary<uint, uint> LimitLevelRankRecState = new();
        private static ConcurrentDictionary<uint, uint> LimitLevelRankSettleRank = new();
        // VIP特权奖励
        private const string KeyVipGiftDaily = "vip_gift_daily";
        private static ConcurrentDictionary<uint, bool> VipGiftDaily = new ();
        // 成神之路副本每天挑战次数
        private const string KeyCszlTimesDaily = "cszl_times_daily";
        private static ConcurrentDictionary<uint, uint> CszlTimesDaily = new ();
        // 双倍经验
        private const string KeyX2ExpLeftKey = "x2exp_role_left";
        private const string KeyX2ExpCurrentGotKey = "x2exp_role_current_got";
        private static ConcurrentDictionary<uint, uint> X2ExpLeft = new ();
        private static ConcurrentDictionary<uint, uint> X2ExpCurrentGot = new ();

        private const string KeyResVersion = "res_v";

        public static async Task Init(string connection, int db = -1)
        {
            await Close();
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(connection);
            _db = _multiplexer.GetDatabase(db);
        }

        public static async Task Close()
        {
            if (_multiplexer != null) await _multiplexer.CloseAsync();
            _multiplexer?.Dispose();
            _multiplexer = null;
            _db = null;
        }

        public static async ValueTask<bool> SetUserToken(uint uid, string token)
        {
            return await _db.StringSetAsync(string.Format(KeyUserTokenFmt, uid), token, UserTokenExpire);
        }

        public static async Task<string> GetUserToken(uint uid)
        {
            return await _db.StringGetAsync(string.Format(KeyUserTokenFmt, uid));
        }

        public static async Task DelUserToken(uint uid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyUserTokenFmt, uid));
        }

        public static async Task<uint> GetServiceTimestamp()
        {
            var ts = await _db.StringGetAsync("ServiceTimestamp");
            if (ts.IsNullOrEmpty)
            {
                return 0;
            }
            return Convert.ToUInt32(ts);
        }

        // 追加过期时间
        public static async ValueTask<bool> AddUserTokenExpire(uint uid)
        {
            var ret = await _db.KeyExpireAsync(string.Format(KeyUserTokenFmt, uid), UserTokenExpire);
            return ret;
        }

        /// <summary>
        /// 设置角色信息, 只存储id,name,relive,level,cfgId,jade,水路大会信息
        /// </summary>
        public static async Task SetRoleInfo(RoleEntity entity)
        {
            if (entity == null) return;           
            var entries = new[]
            {
                new HashEntry(RoleInfoFields.Id, entity.Id),
                new HashEntry(RoleInfoFields.Name, entity.NickName),
                new HashEntry(RoleInfoFields.CfgId, entity.CfgId),
                new HashEntry(RoleInfoFields.Relive, (uint) entity.Relive),
                new HashEntry(RoleInfoFields.Level, (uint) entity.Level),
                new HashEntry(RoleInfoFields.Jade, entity.Jade),
                new HashEntry(RoleInfoFields.TotalPay, entity.TotalPayBS),
                new HashEntry(RoleInfoFields.SldhWin, entity.SldhWin),
                new HashEntry(RoleInfoFields.SldhScore, entity.SldhScore),
                new HashEntry(RoleInfoFields.CszlLayer, entity.CszlLayer),
                new HashEntry(RoleInfoFields.WzzzWin, entity.WzzzWin),
                new HashEntry(RoleInfoFields.WzzzScore, entity.WzzzScore),
                new HashEntry(RoleInfoFields.SinglePkWin, entity.SinglePkWin),
                new HashEntry(RoleInfoFields.SinglePkLost, entity.SinglePkLost),
                new HashEntry(RoleInfoFields.SinglePkScore, entity.SinglePkScore),
                new HashEntry(RoleInfoFields.DaLuanDouWin, entity.DaLuanDouWin),
                new HashEntry(RoleInfoFields.DaLuanDouLost, entity.DaLuanDouLost),
                new HashEntry(RoleInfoFields.DaLuanDouScore, entity.DaLuanDouScore)
            };
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id), entries);
            //更新操作次数
            var dic = Json.SafeDeserialize<Dictionary<uint, uint>>(entity.OperateTimes);
            if (dic != null)
            {
                foreach (var (k, v) in dic)
                {
                    await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id), string.Format(RoleInfoFields.OperateTimes, k), v);
                }
            }

            // 更新排行榜
            await SetRoleLevelRank(entity);
            await SetRoleJadeRank(entity);
            await SetRolePayRank(entity);
            await SetRoleSldhRank(entity.ServerId, entity.Id, entity.SldhScore);
            await SetRoleCszlLayerRank(entity.ServerId, entity.Id, entity.CszlLayer);
            await SetRoleWzzzRank(entity.ServerId, entity.Id, entity.WzzzScore);
            await SetRoleSinglePkRank(entity.ServerId, entity.Id, entity.SinglePkScore);
            // await SetRoleDaLuanDouRank(entity.ServerId, entity.Id, entity.DaLuanDouScore);
            await SetLimitRoleLevelRank(entity);
        }

        /// <summary>
        /// 更新角色皮肤
        /// </summary>
        public static async Task SetRoleSkin(RoleEntity entity, string skin)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.Skin, skin)});
        }

        /// <summary>
        /// 获取角色皮肤
        /// </summary>
        public static async Task<List<int>> GetRoleSkin(uint roleId)
        {
            var r = await _db.HashGetAsync(string.Format(KeyRoleInfoFmt, roleId), RoleInfoFields.Skin);
            if (r.IsNullOrEmpty)
            {
                return new List<int>();
            }
            else
            {
                return Json.SafeDeserialize<List<int>>(r.ToString());
            }
        }

        /// <summary>
        /// 更新角色武器
        /// </summary>
        public static async Task SetRoleWeapon(RoleEntity entity, string weapon)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.Weapon, weapon)});
        }

        /// <summary>
        /// 获取角色武器
        /// </summary>
        public static async Task<MapObjectEquipData> GetRoleWeapon(uint roleId)
        {
            var r = await _db.HashGetAsync(string.Format(KeyRoleInfoFmt, roleId), RoleInfoFields.Weapon);
            if (r.IsNullOrEmpty)
            {
                return new MapObjectEquipData();
            }
            else
            {
                var j = Json.SafeDeserialize<Dictionary<string, int>>(r.ToString());
                return new MapObjectEquipData()
                {
                    CfgId = (uint)j.GetValueOrDefault("cfgId", 0),
                    Category = (EquipCategory)j.GetValueOrDefault("category", 0),
                    Gem = (uint)j.GetValueOrDefault("gem", 0),
                    Level = (uint)j.GetValueOrDefault("level", 0),
                };
            }
        }

        /// <summary>
        /// 更新角色翅膀
        /// </summary>
        public static async Task SetRoleWing(RoleEntity entity, string wing)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.Wing, wing)});
        }

        /// <summary>
        /// 获取角色翅膀
        /// </summary>
        public static async Task<MapObjectEquipData> GetRoleWing(uint roleId)
        {
            var r = await _db.HashGetAsync(string.Format(KeyRoleInfoFmt, roleId), RoleInfoFields.Wing);
            if (r.IsNullOrEmpty)
            {
                return new MapObjectEquipData();
            }
            else
            {
                var j = Json.SafeDeserialize<Dictionary<string, int>>(r.ToString());
                return new MapObjectEquipData()
                {
                    CfgId = (uint)j.GetValueOrDefault("cfgId", 0),
                    Category = (EquipCategory)j.GetValueOrDefault("category", 0),
                    Gem = (uint)j.GetValueOrDefault("gem", 0),
                    Level = (uint)j.GetValueOrDefault("level", 0),
                };
            }
        }

        /// <summary>
        /// 更新角色昵称
        /// </summary>
        public static async Task SetRoleName(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.Name, entity.NickName)});
        }

        /// <summary>
        /// 更新角色的配置id
        /// </summary>
        public static async Task SetRoleCfgId(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.CfgId, entity.CfgId)});
        }

        /// <summary>
        /// 更新角色等级, 自动更新等级排行榜
        /// </summary>
        public static async Task SetRoleLevel(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.Level, (uint) entity.Level)});
            await SetRoleLevelRank(entity);
            await SetLimitRoleLevelRank(entity);
        }

        /// <summary>
        /// 更新成神之路副本爬塔层数
        /// </summary>
        public static async Task SetRoleCszlLayer(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.CszlLayer, (uint) entity.CszlLayer)});
            await SetRoleCszlLayerRank(entity.ServerId, entity.Id, entity.CszlLayer);
        }

        /// <summary>
        /// 更新角色的仙玉, 自动更新等级排行榜
        /// </summary>
        public static async Task SetRoleJade(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.Jade, entity.Jade)});
            await SetRoleJadeRank(entity);
        }

        /// <summary>
        /// 更新角色的总充值, 自动更新充值排行榜
        /// </summary>
        public static async Task SetRolePay(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[] {new HashEntry(RoleInfoFields.TotalPay, entity.TotalPayBS)});
            await SetRolePayRank(entity);
        }

        /// <summary>
        /// 更新角色的水路大会信息, 自动更新水路大会排行榜
        /// </summary>
        public static async Task SetRoleSldh(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[]
                {
                    new HashEntry(RoleInfoFields.SldhScore, entity.SldhScore),
                    new HashEntry(RoleInfoFields.SldhWin, entity.SldhWin)
                });
            await SetRoleSldhRank(entity.ServerId, entity.Id, entity.SldhScore);
        }

        /// <summary>
        /// 更新角色的王者之战信息, 自动更新王者之战排行榜
        /// </summary>
        public static async Task SetRoleWzzz(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[]
                {
                    new HashEntry(RoleInfoFields.WzzzScore, entity.WzzzScore),
                    new HashEntry(RoleInfoFields.WzzzWin, entity.WzzzWin)
                });
            await SetRoleWzzzRank(entity.ServerId, entity.Id, entity.WzzzScore);
        }

        // 重置单人PK排行榜
        public static async Task ResetAllRoleSinglePk(uint serverId)
        {
            await _db.KeyDeleteAsync(string.Format(KeySinglePkRankFmt, serverId));
        }

        /// <summary>
        /// 更新角色的单人Pk信息, 自动更新单人Pk排行榜
        /// </summary>
        public static async Task SetRoleSinglePk(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[]
                {
                    new HashEntry(RoleInfoFields.SinglePkScore, entity.SinglePkScore),
                    new HashEntry(RoleInfoFields.SinglePkWin, entity.SinglePkWin),
                    new HashEntry(RoleInfoFields.SinglePkLost, entity.SinglePkLost)
                });
            await SetRoleSinglePkRank(entity.ServerId, entity.Id, entity.SinglePkScore);
        }

        // 重置大乱斗PK排行榜
        public static async Task ResetAllRoleDaLuanDou(uint serverId)
        {
            await _db.KeyDeleteAsync(string.Format(KeyDaLuanDouRankFmt, serverId));
        }

        /// <summary>
        /// 更新角色的大乱斗Pk信息, 自动更新大乱斗Pk排行榜
        /// </summary>
        public static async Task SetRoleDaLuanDou(RoleEntity entity)
        {
            if (entity == null) return;
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, entity.Id),
                new[]
                {
                    new HashEntry(RoleInfoFields.DaLuanDouScore, entity.DaLuanDouScore),
                    new HashEntry(RoleInfoFields.DaLuanDouWin, entity.DaLuanDouWin),
                    new HashEntry(RoleInfoFields.DaLuanDouLost, entity.DaLuanDouLost)
                });
            // Logger.LogInformation($"SetRoleDaLuanDouRank entity.ServerId={entity.ServerId}, entity.Id={entity.Id}, entity.DaLuanDouScore={entity.DaLuanDouScore}");
            // await SetRoleDaLuanDouRank(entity.ServerId, entity.Id, entity.DaLuanDouScore);
        }

        /// <summary>
        /// 获取角色信息, 如不指定fields就表示获取全部成员
        /// </summary>
        public static async Task<RoleEntity> GetRoleInfo(uint roleId, params RedisValue[] fields)
        {
            HashEntry[] entries;
            if (fields == null || fields.Length == 0)
            {
                entries = await _db.HashGetAllAsync(string.Format(KeyRoleInfoFmt, roleId));
            }
            else
            {
                var values = await _db.HashGetAsync(string.Format(KeyRoleInfoFmt, roleId), fields);
                entries = new HashEntry[fields.Length];
                for (var i = 0; i < fields.Length; i++)
                {
                    entries[i] = new HashEntry(fields[i], values[i]);
                }
            }

            return ParseRoleEntity(entries);
        }

        private static RoleEntity ParseRoleEntity(IEnumerable<HashEntry> entries)
        {
            var entity = new RoleEntity();
            foreach (var entry in entries)
            {
                switch (entry.Name)
                {
                    case RoleInfoFields.Id:
                        entity.Id = uint.Parse(entry.Value);
                        break;
                    case RoleInfoFields.Name:
                        entity.NickName = entry.Value;
                        break;
                    case RoleInfoFields.CfgId:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.CfgId = value;
                    }
                        break;
                    case RoleInfoFields.Relive:
                    {
                        byte.TryParse(entry.Value, out var value);
                        entity.Relive = value;
                    }
                        break;
                    case RoleInfoFields.Level:
                    {
                        byte.TryParse(entry.Value, out var value);
                        entity.Level = value;
                    }
                        break;
                    case RoleInfoFields.Jade:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.Jade = value;
                    }
                        break;
                    case RoleInfoFields.TotalPay:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.TotalPayBS = value;
                    }
                        break;
                    case RoleInfoFields.SldhWin:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.SldhWin = value;
                    }
                        break;
                    case RoleInfoFields.SldhScore:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.SldhScore = value;
                    }
                        break;
                    case RoleInfoFields.CszlLayer:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.CszlLayer = value;
                    }
                        break;
                    case RoleInfoFields.WzzzWin:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.WzzzWin = value;
                    }
                        break;
                    case RoleInfoFields.WzzzScore:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.WzzzScore = value;
                    }
                        break;
                    case RoleInfoFields.SinglePkWin:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.SinglePkWin = value;
                    }
                        break;
                    case RoleInfoFields.SinglePkLost:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.SinglePkLost = value;
                    }
                        break;
                    case RoleInfoFields.SinglePkScore:
                    {
                        uint.TryParse(entry.Value, out var value);
                        entity.SinglePkScore = value;
                    }
                        break;
                }
            }

            return entity;
        }

        /// <summary>
        /// 检查指定角色是否存在
        /// </summary>
        public static async ValueTask<bool> ExistsRole(uint roleId)
        {
            var ret = await _db.KeyExistsAsync(string.Format(KeyRoleInfoFmt, roleId));
            return ret;
        }

        /// <summary>
        /// 更新等级排行榜
        /// </summary>
        public static async Task SetRoleLevelRank(RoleEntity entity)
        {
            // 每转生一次掉20级，但是要确保2转一定比1转高，所以提升转生等级带来的幅度, 20 -> 30
            var score = entity.Level + entity.Relive * 30;
            await _db.SortedSetAddAsync(string.Format(KeyLevelRankFmt, entity.ServerId), entity.Id, score);
        }

        /// <summary>
        /// 获取角色在等级排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRoleLevelRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyLevelRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRoleLevelRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyLevelRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取等级排行榜列表
        /// </summary>
        public static async Task<List<LvRankMemberData>> GetRoleLevelRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyLevelRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return null;

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new LvRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level,
                    Jade = entity.Jade
                })
                .ToList();
        }

        /// <summary>
        /// 移除等级排行榜
        /// </summary>
        public static async Task DelRoleLevelRank(uint sid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyLevelRankFmt, sid));
        }

        /// <summary>
        /// 更新仙玉排行榜
        /// </summary>
        public static async Task SetRoleJadeRank(RoleEntity entity)
        {
            await _db.SortedSetAddAsync(string.Format(KeyJadeRankFmt, entity.ServerId), entity.Id, entity.Jade);
        }

        /// <summary>
        /// 获取角色在仙玉排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRoleJadeRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyJadeRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRoleJadeRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyJadeRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取仙玉排行榜列表
        /// </summary>
        public static async Task<List<JadeRankMemberData>> GetRoleJadeRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyJadeRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return null;

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new JadeRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level,
                    Jade = entity.Jade
                })
                .ToList();
        }

        /// <summary>
        /// 移除仙玉排行榜
        /// </summary>
        public static async Task DelRoleJadeRank(uint sid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyJadeRankFmt, sid));
        }

        /// <summary>
        /// 更新总充值排行榜
        /// </summary>
        public static async Task SetRolePayRank(RoleEntity entity)
        {
            await _db.SortedSetAddAsync(string.Format(KeyPayRankFmt, entity.ServerId), entity.Id, entity.TotalPayBS);
        }

        /// <summary>
        /// 获取角色在充值排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRolePayRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyPayRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRolePayRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyPayRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取充值排行榜列表
        /// </summary>
        public static async Task<List<PayRankMemberData>> GetRolePayRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyPayRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return null;

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new PayRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level,
                    Pay = entity.TotalPayBS
                })
                .ToList();
        }

        /// <summary>
        /// 移除充值排行榜
        /// </summary>
        public static async Task DelRolePayRank(uint sid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyPayRankFmt, sid));
        }

        /// <summary>
        /// 设置限时充值排行榜--开始结束时间
        /// </summary>
        public static async ValueTask<bool> SetLimitChargeStartEndTimestamp(uint serverId, uint stimestamp, uint etimestamp)
        {
            LimitChargeRankStartTime[serverId] = stimestamp;
            if (!await _db.StringSetAsync(string.Format(KeyLimitChargeRankStartFmt, serverId), stimestamp))
            {
                return false;
            }
            LimitChargeRankEndTime[serverId] = etimestamp;
            if (!await _db.StringSetAsync(string.Format(KeyLimitChargeRankEndFmt, serverId), etimestamp))
            {
                return false;
            }
            return true;
        }
        public static async ValueTask<uint> GetLimitChargeStartTimestamp(uint serverId)
        {
            if (LimitChargeRankStartTime.ContainsKey(serverId))
            {
                return LimitChargeRankStartTime.GetValueOrDefault(serverId, (uint)0);
            }
            var ret = await _db.StringGetAsync(string.Format(KeyLimitChargeRankStartFmt, serverId));
            if (ret.IsNullOrEmpty)
            {
                LimitChargeRankStartTime[serverId] = 0;
                return 0;
            }
            ret.TryParse(out int ts);
            LimitChargeRankStartTime[serverId] = (uint)ts;
            return ((uint)ts);
        }
        public static async ValueTask<uint> GetLimitChargeEndTimestamp(uint serverId)
        {
            if (LimitChargeRankEndTime.ContainsKey(serverId))
            {
                return LimitChargeRankEndTime.GetValueOrDefault(serverId, (uint)0);
            }
            var ret = await _db.StringGetAsync(string.Format(KeyLimitChargeRankEndFmt, serverId));
            if (ret.IsNullOrEmpty)
            {
                LimitChargeRankEndTime[serverId] = 0;
                return 0;
            }
            ret.TryParse(out int ts);
            LimitChargeRankEndTime[serverId] = (uint)ts;
            return ((uint)ts);
        }

        /// <summary>
        /// 限时充值排行榜--增加玩家分数
        /// </summary>
        public static async ValueTask<double> AddLimitPayRoleScore(uint serverId, uint roleId, uint score)
        {
            return await _db.SortedSetIncrementAsync(string.Format(KeyLimitChargeRankFmt, serverId), roleId, score);
            //if (await GetLimitChargeEndTimestamp(serverId) >= TimeUtil.TimeStamp)
            //{
            //    return await _db.SortedSetIncrementAsync(string.Format(KeyLimitChargeRankFmt, serverId), roleId, score);
            //}
            //return 0;
        }

        /// <summary>
        /// 获取角色在限时充值排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetLimitChargeRankRoleRank(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyLimitChargeRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetLimitChargeRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyLimitChargeRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取限时充值排行榜列表
        /// </summary>
        public static async Task<List<LimitChargeRankMemberData>> GetLimitChargeRankList(uint serverId, int pageIndex = 1, int pageSize = 10)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyLimitChargeRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return new();

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element))).ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            var list = results.Select(ParseRoleEntity)
                .Select(entity => new LimitChargeRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level,
                })
                .ToList();
            for (int i = 0; i < entrys.Count() && i < list.Count; i++)
            {
                list[i].Pay = (uint)entrys[i].Score;
            }
            return list;
        }

        /// <summary>
        /// 移除限时充值排行榜
        /// </summary>
        public static async Task<List<bool>> DelLimitChargeRank(uint serverId)
        {
            var r1 = true;
            var key = string.Format(KeyLimitChargeRankFmt, serverId);
            if (await _db.KeyExistsAsync(key))
            {
                r1 = await _db.KeyDeleteAsync(key);
            }
            var r2 = true;
            key = string.Format(KeyLimitChargeRankStartFmt, serverId);
            if (await _db.KeyExistsAsync(key))
            {
                r2 = await _db.KeyDeleteAsync(key);
            }
            var r3 = true;
            key = string.Format(KeyLimitChargeRankEndFmt, serverId);
            if (await _db.KeyExistsAsync(key))
            {
                r3 = await _db.KeyDeleteAsync(key);
            }
            LimitChargeRankStartTime.Remove(serverId, out var i);
            LimitChargeRankEndTime.Remove(serverId, out var j);
            return new() { r1, r2, r3 };
        }
        //结算限时充值排行榜 
        public static async Task<Dictionary<uint, uint>> SettleRoleLimitChargeRank(uint serverId)
        {
            var retList = new Dictionary<uint, uint>();
            //如果排行榜已经结算，直接返回
            var ret = await _db.StringGetAsync(string.Format(KeyLimitChargeRankSettleStateFmt, serverId));
            if (!ret.IsNullOrEmpty)
            {
                ret.TryParse(out int value);
                if (value > 0)
                {
                    return retList;
                }
            }
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyLimitChargeRankFmt, serverId),
                0,
                9,
                Order.Descending);
            uint rank = 0;
            foreach (var entry in entrys)
            {
                rank++;
                var roleId = (uint)entry.Element;
                await SetRoleLimitChargeRankRecState(roleId, 2);
                await SetRoleLimitChargeRankSettleRank(roleId, rank);
                //直接发送邮件
                {
                    // 30天过期
                    var now = DateTimeOffset.Now;
                    var createTime = (uint)now.ToUnixTimeSeconds();
                    var expireTime = (uint)now.AddDays(30).ToUnixTimeSeconds();

                    //根据排名发放奖励
                    var config = ConfigService.LimitRankPrizeConfigList[(int)rank];
                    var item = config.prize1;
                    var itemDict = new Dictionary<uint, uint>();
                    if (item != null && item.Count > 0)
                    {
                        foreach (var i in item)
                        {
                            if (i.num > 0)
                            {
                                ConfigService.Items.TryGetValue(i.id, out var iconfig);
                                if (iconfig != null)
                                {
                                    itemDict.Add(i.id, i.num);
                                    // await AddBagItem(i.id, (int)i.num, tag: "限时充值排行领奖");
                                }
                            }
                        }
                    }
                    var items = Json.SafeSerialize(itemDict);

                    // 给推广人发送奖励邮件
                    var entity = new MailEntity
                    {
                        ServerId = serverId,
                        Sender = 0,
                        Recver = roleId,
                        Admin = 0,
                        Type = (byte)MailType.System,
                        Text = $"恭喜您在限时充值榜中排{rank}名，以下是系统给你的奖励!",
                        Items = items,
                        Remark = "",
                        CreateTime = createTime,
                        PickedTime = 0,
                        DeleteTime = 0,
                        ExpireTime = expireTime
                    };

                    using var uow = DbService.Sql.CreateUnitOfWork();
                    var repo = uow.GetRepository<MailEntity>();
                    await repo.InsertAsync(entity);
                    if (entity.Id == 0)
                    {
                        uow.Rollback();
                        break;
                    }

                    // 先提交事务, 否则playerGrain.OnRecvMail中查询不到数据
                    uow.Commit();
                    retList.Add(roleId, entity.Id);
                }

            }
            //标识排行榜已经结算
            await _db.StringSetAsync(string.Format(KeyLimitChargeRankSettleStateFmt, serverId), 1);
            return retList;
        }

        // 限时充值排行榜奖励
        public static async ValueTask<uint> GetRoleLimitChargeRankRecState(uint roleId)
        {
            if (LimitChargeRankRecState.ContainsKey(roleId))
            {
                return LimitChargeRankRecState[roleId];
            }
            var r = await _db.HashGetAsync(string.Format(KeyLimitChargeRankGift, roleId), "MyRecState");
            if (r.IsNullOrEmpty)
            {
                LimitChargeRankRecState[roleId] = 0;
                return 0;
            }
            r.TryParse(out int value);
            LimitChargeRankRecState[roleId] = (uint)value;
            return (uint)value;
        }
        public static async ValueTask<uint> GetRoleLimitChargeRankSettleRank(uint roleId)
        {
            if (LimitChargeRankSettleRank.ContainsKey(roleId))
            {
                return LimitChargeRankSettleRank[roleId];
            }
            var r = await _db.HashGetAsync(string.Format(KeyLimitChargeRankGift, roleId), "MySettleRank");
            if (r.IsNullOrEmpty)
            {
                LimitChargeRankSettleRank[roleId] = 0;
                return 0;
            }
            r.TryParse(out int value);
            LimitChargeRankSettleRank[roleId] = (uint)value;
            return (uint)value;
        }
        public static async Task SetRoleLimitChargeRankRecState(uint roleId, uint recState)
        {
            LimitChargeRankRecState[roleId] = recState;
            await _db.HashSetAsync(string.Format(KeyLimitChargeRankGift, roleId), "MyRecState", recState);
        }
        public static async Task SetRoleLimitChargeRankSettleRank(uint roleId, uint settleRank)
        {
            LimitChargeRankSettleRank[roleId] = settleRank;
            await _db.HashSetAsync(string.Format(KeyLimitChargeRankGift, roleId), "MySettleRank", settleRank);
        }
        //public static async ValueTask<bool> DeleteRoleLimitChargeRankGift()
        //{
        //    LimitChargeRankRecState.Clear();
        //    if (await _db.KeyExistsAsync(KeyLimitChargeRankGift))
        //    {
        //        return await _db.KeyDeleteAsync(KeyLimitChargeRankGift);
        //    }
        //    return true;
        //}

        /// <summary>
        /// 设置限时等级排行榜--开始结束时间
        /// </summary>
        public static async ValueTask<bool> SetLimitLevelStartEndTimestamp(uint serverId, uint stimestamp, uint etimestamp)
        {
            LimitLevelRankStartTime[serverId] = stimestamp;
            if (!await _db.StringSetAsync(string.Format(KeyLimitLevelRankStartFmt, serverId), stimestamp))
            {
                return false;
            }
            LimitLevelRankEndTime[serverId] = etimestamp;
            if (!await _db.StringSetAsync(string.Format(KeyLimitLevelRankEndFmt, serverId), etimestamp))
            {
                return false;
            }
            return true;
        }
        public static async ValueTask<uint> GetLimitLevelStartTimestamp(uint serverId)
        {
            if (LimitLevelRankStartTime.ContainsKey(serverId))
            {
                return LimitLevelRankStartTime.GetValueOrDefault(serverId, (uint)0);
            }
            var ret = await _db.StringGetAsync(string.Format(KeyLimitLevelRankStartFmt, serverId));
            if (ret.IsNullOrEmpty)
            {
                LimitLevelRankStartTime[serverId] = 0;
                return 0;
            }
            ret.TryParse(out int ts);
            LimitLevelRankStartTime[serverId] = (uint)ts;
            return ((uint)ts);
        }
        public static async ValueTask<uint> GetLimitLevelEndTimestamp(uint serverId)
        {
            if (LimitLevelRankEndTime.ContainsKey(serverId))
            {
                return LimitLevelRankEndTime.GetValueOrDefault(serverId, (uint)0);
            }
            var ret = await _db.StringGetAsync(string.Format(KeyLimitLevelRankEndFmt, serverId));
            if (ret.IsNullOrEmpty)
            {
                LimitLevelRankEndTime[serverId] = 0;
                return 0;
            }
            ret.TryParse(out int ts);
            LimitLevelRankEndTime[serverId] = (uint)ts;
            return ((uint)ts);
        }

        /// <summary>
        /// 更新限时等级排行榜
        /// </summary>
        public static async Task SetLimitRoleLevelRank(RoleEntity entity)
        {
            // 每转生一次掉20级，但是要确保2转一定比1转高，所以提升转生等级带来的幅度, 20 -> 30
            var score = entity.Level + entity.Relive * 30;
            await _db.SortedSetAddAsync(string.Format(KeyLimitLevelRankFmt, entity.ServerId), entity.Id, score);
            //if (await GetLimitLevelEndTimestamp(entity.ServerId) >= TimeUtil.TimeStamp)
            //{
            //    // 每转生一次掉20级，但是要确保2转一定比1转高，所以提升转生等级带来的幅度, 20 -> 30
            //    var score = entity.Level + entity.Relive * 30;
            //    await _db.SortedSetAddAsync(string.Format(KeyLimitLevelRankFmt, entity.ServerId), entity.Id, score);
            //}
        }

        /// <summary>
        /// 获取角色在限时等级排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetLimitRoleLevelRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyLimitLevelRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetLimitRoleLevelRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyLimitLevelRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取限时等级排行榜列表
        /// </summary>
        public static async Task<List<LimitLvRankMemberData>> GetLimitRoleLevelRankList(uint serverId, int pageIndex = 1,
            int pageSize = 10)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyLimitLevelRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return new();

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new LimitLvRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level,
                    Jade = entity.Jade
                })
                .ToList();
        }

        /// <summary>
        /// 移除限时等级排行榜
        /// </summary>
        public static async Task<List<bool>> DelLimitRoleLevelRank(uint sid)
        {
            var r1 = true;
            var key = string.Format(KeyLimitLevelRankFmt, sid);
            if (await _db.KeyExistsAsync(key))
            {
                r1 = await _db.KeyDeleteAsync(key);
            }
            var r2 = true;
            key = string.Format(KeyLimitLevelRankStartFmt, sid);
            if (await _db.KeyExistsAsync(key))
            {
                r2 = await _db.KeyDeleteAsync(key);
            }
            var r3 = true;
            key = string.Format(KeyLimitLevelRankEndFmt, sid);
            if (await _db.KeyExistsAsync(key))
            {
                r3 = await _db.KeyDeleteAsync(key);
            }
            LimitLevelRankStartTime.Remove(sid, out var i);
            LimitLevelRankEndTime.Remove(sid, out var j);
            return new() { r1, r2, r3 };
        }
        //结算限时等级排行榜
        public static async Task<Dictionary<uint, uint>> SettleRoleLimitLevelRank(uint serverId)
        {
            var retList = new Dictionary<uint, uint>();
            //如果排行榜已经结算，直接返回
            var ret = await _db.StringGetAsync(string.Format(KeyLimitLevelRankSettleStateFmt, serverId));
            if (!ret.IsNullOrEmpty)
            {
                ret.TryParse(out int value);
                if (value > 0)
                {
                    return retList;
                }
            }
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyLimitLevelRankFmt, serverId),
                0,
                9,
                Order.Descending);
            uint rank = 0;
            foreach (var entry in entrys)
            {
                rank++;
                var roleId = (uint)entry.Element;
                await SetRoleLimitLevelRankRecState(roleId, 2);
                await SetRoleLimitLevelRankSettleRank(roleId, rank);
                //直接发送邮件
                {
                    // 30天过期
                    var now = DateTimeOffset.Now;
                    var createTime = (uint)now.ToUnixTimeSeconds();
                    var expireTime = (uint)now.AddDays(30).ToUnixTimeSeconds();

                    //根据排名发放奖励
                    var config = ConfigService.LimitRankPrizeConfigList[(int)rank];
                    var item = config.prize2;
                    var itemDict = new Dictionary<uint, uint>();
                    if (item != null && item.Count > 0)
                    {
                        foreach (var i in item)
                        {
                            if (i.num > 0)
                            {
                                ConfigService.Items.TryGetValue(i.id, out var iconfig);
                                if (iconfig != null)
                                {
                                    itemDict.Add(i.id, i.num);
                                }
                            }
                        }
                    }
                    var items = Json.SafeSerialize(itemDict);

                    // 给推广人发送奖励邮件
                    var entity = new MailEntity
                    {
                        ServerId = serverId,
                        Sender = 0,
                        Recver = roleId,
                        Admin = 0,
                        Type = (byte)MailType.System,
                        Text = $"恭喜您在限时等级榜中排{rank}名，以下是系统给你的奖励!",
                        Items = items,
                        Remark = "",
                        CreateTime = createTime,
                        PickedTime = 0,
                        DeleteTime = 0,
                        ExpireTime = expireTime
                    };

                    using var uow = DbService.Sql.CreateUnitOfWork();
                    var repo = uow.GetRepository<MailEntity>();
                    await repo.InsertAsync(entity);
                    if (entity.Id == 0)
                    {
                        uow.Rollback();
                        break;
                    }

                    // 先提交事务, 否则playerGrain.OnRecvMail中查询不到数据
                    uow.Commit();
                    retList.Add(roleId, entity.Id);
                }

            }
            //标识排行榜已经结算
            await _db.StringSetAsync(string.Format(KeyLimitLevelRankSettleStateFmt, serverId), 1);
            return retList;
        }

        // 限时等级排行榜奖励
        public static async ValueTask<uint> GetRoleLimitLevelRankRecState(uint roleId)
        {
            if (LimitLevelRankRecState.ContainsKey(roleId))
            {
                return LimitLevelRankRecState[roleId];
            }
            var r = await _db.HashGetAsync(string.Format(KeyLimitLevelRankGift, roleId), "MyRecState");
            if (r.IsNullOrEmpty)
            {
                LimitLevelRankRecState[roleId] = 0;
                return 0;
            }
            r.TryParse(out int value);
            LimitLevelRankRecState[roleId] = (uint)value;
            return (uint)value;
        }
        public static async ValueTask<uint> GetRoleLimitLevelRankSettleRank(uint roleId)
        {
            if (LimitLevelRankSettleRank.ContainsKey(roleId))
            {
                return LimitLevelRankSettleRank[roleId];
            }
            var r = await _db.HashGetAsync(string.Format(KeyLimitLevelRankGift, roleId), "MySettleRank");
            if (r.IsNullOrEmpty)
            {
                LimitLevelRankSettleRank[roleId] = 0;
                return 0;
            }
            r.TryParse(out int value);
            LimitLevelRankSettleRank[roleId] = (uint)value;
            return (uint)value;
        }
        public static async Task SetRoleLimitLevelRankRecState(uint roleId, uint recState)
        {
            LimitLevelRankRecState[roleId] = recState;
            await _db.HashSetAsync(string.Format(KeyLimitLevelRankGift, roleId), "MyRecState", recState);
        }
        public static async Task SetRoleLimitLevelRankSettleRank(uint roleId, uint settleRank)
        {
            LimitLevelRankSettleRank[roleId] = settleRank;
            await _db.HashSetAsync(string.Format(KeyLimitLevelRankGift, roleId), "MySettleRank", settleRank);
        }
        //public static async ValueTask<bool> DeleteRoleLimitLevelRankGift()
        //{
        //    LimitLevelRankGift.Clear();
        //    if (await _db.KeyExistsAsync(KeyLimitLevelRankGift))
        //    {
        //        return await _db.KeyDeleteAsync(KeyLimitLevelRankGift);
        //    }
        //    return true;
        //}

        /// <summary>
        /// 更新水路大会排行榜
        /// </summary>
        public static async Task SetRoleSldhRank(uint serverId, uint roleId, uint sldhScore)
        {
            await _db.SortedSetAddAsync(string.Format(KeySldhRankFmt, serverId), roleId, sldhScore);
        }

        /// <summary>
        /// 获取角色在水路大会排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRoleSldhRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeySldhRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRoleSldhRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeySldhRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取水路大会排行榜列表
        /// </summary>
        public static async Task<List<SldhRankMemberData>> GetRoleSldhRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeySldhRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return null;

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new SldhRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    Win = entity.SldhWin,
                    Score = entity.SldhScore
                })
                .ToList();
        }

        /// <summary>
        /// 移除水路排行榜
        /// </summary>
        public static async Task DelRoleSldhRank(uint sid)
        {
            await _db.KeyDeleteAsync(string.Format(KeySldhRankFmt, sid));
        }

        /// <summary>
        /// 更新成神之路爬塔排行榜
        /// </summary>
        public static async Task SetRoleCszlLayerRank(uint serverId, uint roleId, uint layer)
        {
            await _db.SortedSetAddAsync(string.Format(KeyCszlLayerRankFmt, serverId), roleId, layer);
        }

        /// <summary>
        /// 获取角色在成神之路爬塔排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRoleCszlLayerRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyCszlLayerRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRoleCszlLayerRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyCszlLayerRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取成神之路爬塔排行榜列表
        /// </summary>
        public static async Task<List<CszlRankMemberData>> GetRoleCszlLayerRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyCszlLayerRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return null;

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new CszlRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level,
                    Layer = entity.CszlLayer
                })
                .ToList();
        }

        /// <summary>
        /// 移除成神之路爬塔排行榜
        /// </summary>
        public static async Task DelRoleCszlLayerRank(uint sid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyCszlLayerRankFmt, sid));
        }

        /// <summary>
        /// 更新王者之战排行榜
        /// </summary>
        public static async Task SetRoleWzzzRank(uint serverId, uint roleId, uint wzzzScore)
        {
            await _db.SortedSetAddAsync(string.Format(KeyWzzzRankFmt, serverId), roleId, wzzzScore);
        }

        /// <summary>
        /// 获取角色在王者之战排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRoleWzzzRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyWzzzRankFmt, serverId), roleId, Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRoleWzzzRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyWzzzRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取王者之战排行榜列表
        /// </summary>
        public static async Task<List<WzzzRankMemberData>> GetRoleWzzzRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyWzzzRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return null;

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => {
                    var ret = new WzzzRankMemberData
                    {
                        Id = entity.Id,
                        Name = entity.NickName,
                        Win = entity.WzzzWin,
                        Score = entity.WzzzScore,
                        CfgId = entity.CfgId,
                    };
                    if (ret.Score >= 50) {
                        ret.DuanWei = 1;
                    } else if (ret.Score >= 100) {
                        ret.DuanWei = 2;
                    } else if (ret.Score >= 200) {
                        ret.DuanWei = 3;
                    } else if (ret.Score >= 500) {
                        ret.DuanWei = 4;
                    }
                    return ret;
                }
                )
                .ToList();
        }

        /// <summary>
        /// 移除王者之战排行榜
        /// </summary>
        public static async Task DelRoleWzzzRank(uint sid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyWzzzRankFmt, sid));
        }


        /// <summary>
        /// 更新单人Pk排行榜
        /// </summary>
        public static async Task SetRoleSinglePkRank(uint serverId, uint roleId, uint score)
        {
            await _db.SortedSetAddAsync(string.Format(KeySinglePkRankFmt, serverId), roleId, score);
        }

        /// <summary>
        /// 获取角色在单人Pk排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRoleSinglePkRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeySinglePkRankFmt, serverId), roleId,
                Order.Descending);
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRoleSinglePkRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeySinglePkRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取单人Pk排行榜列表
        /// </summary>
        public static async Task<List<SinglePkRankMemberData>> GetRoleSinglePkRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeySinglePkRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return new();

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new SinglePkRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    Win = entity.SinglePkWin,
                    Lost = entity.SinglePkLost,
                    Score = entity.SinglePkScore
                })
                .ToList();
        }

        /// <summary>
        /// 移除水路排行榜 
        /// </summary>
        public static async Task DelRoleSinglePkRank(uint sid)
        {
            await _db.KeyDeleteAsync(string.Format(KeySinglePkRankFmt, sid));
        }

        // /// <summary>
        // /// 更新大乱斗PK排行榜
        // /// </summary>
        // public static async Task SetRoleDaLuanDouRank(uint serverId, uint roleId, uint score)
        // {
        //     await _db.SortedSetAddAsync(string.Format(KeyDaLuanDouRankFmt, serverId), roleId, score);
        // }

        public static async Task AddDaLuanDouScore(uint serverId, uint roleId, uint score)
        {
            await _db.SortedSetIncrementAsync(string.Format(KeyDaLuanDouRankFmt, serverId), roleId, score);
        }

        /// <summary>
        /// 获取角色在大乱斗PK排行榜中的排名
        /// </summary>
        public static async ValueTask<long> GetRoleDaLuanDouRankIndex(uint serverId, uint roleId)
        {
            var rank = await _db.SortedSetRankAsync(string.Format(KeyDaLuanDouRankFmt, serverId), roleId,
                Order.Descending);
            Logger.LogInformation($"GetRoleDaLuanDouRankIndex serverId={serverId} roleId={roleId} rank={rank}");       
            return rank.GetValueOrDefault();
        }

        public static async Task<long> GetRoleDaLuanDouRankCount(uint serverId)
        {
            var length = await _db.SortedSetLengthAsync(string.Format(KeyDaLuanDouRankFmt, serverId));
            return length;
        }

        /// <summary>
        /// 获取大乱斗PK排行榜列表
        /// </summary>
        public static async Task<List<DaLuanDouRankMemberData>> GetRoleDaLuanDouRank(uint serverId, int pageIndex = 1,
            int pageSize = 20)
        {
            if (pageIndex < 1) pageIndex = 1;
            var entrys = await _db.SortedSetRangeByRankWithScoresAsync(string.Format(KeyDaLuanDouRankFmt, serverId),
                (pageIndex - 1) * pageSize,
                pageIndex * pageSize - 1,
                Order.Descending);
            if (entrys == null || entrys.Length == 0) return new();

            var batch = _db.CreateBatch();
            var tasks = entrys.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry.Element)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            Logger.LogInformation($"GetRoleDaLuanDouRank serverId={serverId} pageIndex={pageIndex} pageSize={pageSize}");
            var list = results.Select(ParseRoleEntity)
                .Select(entity => new DaLuanDouRankMemberData
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    Win = entity.DaLuanDouWin,
                    Lost = entity.DaLuanDouLost,
                    // Score = entity.DaLuanDouScore,
                    CfgId = entity.CfgId
                })
                .ToList();
            for (int i = 0; i < entrys.Count() && i < list.Count; i++)
            {
                list[i].Score = (uint)entrys[i].Score;
            }
            return list;
        }

        /// <summary>
        /// 移除大乱斗排行榜 
        /// </summary>
        public static async Task DelRoleDaLuanDouRank(uint sid)
        {
            Logger.LogInformation($"DelRoleDaLuanDouRank {sid}");
            await _db.KeyDeleteAsync(string.Format(KeyDaLuanDouRankFmt, sid));
        }


        /// <summary>
        /// 获取好友列表
        /// </summary>
        public static async Task<List<RoleInfo>> GetFriendList(uint roleId)
        {
            var values = await _db.ListRangeAsync(string.Format(KeyFriendListFmt, roleId));
            if (values == null || values.Length == 0) return new List<RoleInfo>();

            var batch = _db.CreateBatch();
            var tasks = values.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new RoleInfo
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level
                })
                .ToList();
        }

        public static async ValueTask<int> GetFriendNum(uint roleId)
        {
            var num = await _db.ListLengthAsync(string.Format(KeyFriendListFmt, roleId));
            return (int) num;
        }

        /// <summary>
        /// 添加好友, 由于是双向好友，所以需要双向处理
        /// </summary>
        public static async ValueTask<bool> AddFriend(uint roleId, uint friendRoleId)
        {
            var idx1 = await _db.ListRightPushAsync(string.Format(KeyFriendListFmt, roleId), friendRoleId);
            var idx2 = await _db.ListRightPushAsync(string.Format(KeyFriendListFmt, friendRoleId), roleId);
            return idx1 >= 0 && idx2 >= 0;
        }

        /// <summary>
        /// 删除好友, 由于是双向好友，所以需要双向处理
        /// </summary>
        public static async ValueTask<bool> DelFriend(uint roleId, uint friendRoleId)
        {
            var idx1 = await _db.ListRemoveAsync(string.Format(KeyFriendListFmt, roleId), friendRoleId);
            var idx2 = await _db.ListRemoveAsync(string.Format(KeyFriendListFmt, friendRoleId), roleId);
            return idx1 >= 0 && idx2 >= 0;
        }

        /// <summary>
        /// 检查是否为好友
        /// </summary>
        public static async ValueTask<bool> IsFriend(uint roleId, uint friendRoleId)
        {
            var res = false;
            var list = await _db.ListRangeAsync(string.Format(KeyFriendListFmt, roleId));
            if (list != null && list.Length > 0)
            {
                if (list.Any(rid => rid == friendRoleId))
                {
                    res = true;
                }
            }

            return res;
        }

        /// <summary>
        /// 获取好友申请列表
        /// </summary>
        public static async Task<List<RoleInfo>> GetFriendApplyList(uint roleId)
        {
            var values = await _db.ListRangeAsync(string.Format(KeyFriendApplyListFmt, roleId));
            if (values == null || values.Length == 0) return new List<RoleInfo>();

            var batch = _db.CreateBatch();
            var tasks = values.Select(entry => batch.HashGetAllAsync(string.Format(KeyRoleInfoFmt, entry)))
                .ToList();
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            return results.Select(ParseRoleEntity)
                .Select(entity => new RoleInfo
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level
                })
                .ToList();
        }

        /// <summary>
        /// 添加一个好友申请
        /// </summary>
        public static async ValueTask<bool> AddFriendApply(uint roleId, uint applyRoleId)
        {
            var idx = await _db.ListRightPushAsync(string.Format(KeyFriendApplyListFmt, roleId), applyRoleId);
            return idx >= 0;
        }

        /// <summary>
        /// 删除一个好友申请
        /// </summary>
        public static async ValueTask<bool> DelFriendApply(uint roleId, uint applyRoleId)
        {
            var idx = await _db.ListRemoveAsync(string.Format(KeyFriendApplyListFmt, roleId), applyRoleId);
            return idx >= 0;
        }

        /// <summary>
        /// 检查roleId是否申请过添加targetRoleId为好友
        /// </summary>
        public static async ValueTask<bool> IsFriendApplyed(uint roleId, uint targetRoleId)
        {
            var res = false;
            var list = await _db.ListRangeAsync(string.Format(KeyFriendListFmt, targetRoleId));
            if (list != null && list.Length > 0)
            {
                if (list.Any(rid => rid == roleId))
                {
                    res = true;
                }
            }

            return res;
        }

        public static async ValueTask<bool> SetResVersion(ResVersionVo vo)
        {
            var json = Json.Serialize(vo);
            var ret = await _db.StringSetAsync(KeyResVersion, json);
            return ret;
        }

        public static async Task<ResVersionVo> GetResVersion()
        {
            var json = await _db.StringGetAsync(KeyResVersion);
            if (!json.HasValue) return null;
            var vo = Json.Deserialize<ResVersionVo>((string) json);
            return vo;
        }

        public static async ValueTask<bool> DelResVersion()
        {
            var ret = await _db.KeyDeleteAsync(KeyResVersion);
            return ret;
        }

        public static Task SetEquipInfo(uint id, byte[] bytes)
        {
            return _db.StringSetAsync(string.Format(KeyEquipInfoFmt, id), bytes, EquipInfoExpire);
        }

        public static async Task<byte[]> GetEquipInfo(uint id)
        {
            var bytes = await _db.StringGetAsync(string.Format(KeyEquipInfoFmt, id));
            return (byte[]) bytes;
        }

        public static Task SetOrnamentInfo(uint id, byte[] bytes)
        {
            return _db.StringSetAsync(string.Format(KeyOrnamentInfoFmt, id), bytes, OrnamentInfoExpire);
        }

        public static async Task<byte[]> GetOrnamentInfo(uint id)
        {
            var bytes = await _db.StringGetAsync(string.Format(KeyOrnamentInfoFmt, id));
            return (byte[]) bytes;
        }

        public static Task SetPetOrnamentInfo(uint id, byte[] bytes)
        {
            return _db.StringSetAsync(string.Format(KeyPetOrnamentInfoFmt, id), bytes, PetOrnamentInfoExpire);
        }

        public static async Task<byte[]> GetPetOrnamentInfo(uint id)
        {
            var bytes = await _db.StringGetAsync(string.Format(KeyPetOrnamentInfoFmt, id));
            return (byte[]) bytes;
        }

        //保存玩家的操作次数到redis   1->孩子定制 2->星阵定制 3->配饰炼化
        public static Task SetOperateTimes(uint roleId, uint id, uint times)
        {
            return _db.HashSetAsync(string.Format(KeyRoleInfoFmt, roleId), string.Format(RoleInfoFields.OperateTimes, id), times);
        }
        public static async Task AddOperateTimes(uint roleId, uint id, int times)
        {
            var oldTimes = await _db.HashGetAsync(string.Format(KeyRoleInfoFmt, roleId), string.Format(RoleInfoFields.OperateTimes, id));
            int newTimes = (int) oldTimes + times;
            if (newTimes < 0)
            {
                newTimes = 0;
            }
            await _db.HashSetAsync(string.Format(KeyRoleInfoFmt, roleId), string.Format(RoleInfoFields.OperateTimes, id), newTimes);
            return;
        }
        public static async Task<uint> GetOperateTimes(uint roleId, uint id)
        {
            var times = await _db.HashGetAsync(string.Format(KeyRoleInfoFmt, roleId), string.Format(RoleInfoFields.OperateTimes, id));
            return  (uint)times;
        }

        public static Task SetPetInfo(uint id, byte[] bytes)
        {
            return _db.StringSetAsync(string.Format(KeyPetInfoFmt, id), bytes, PetInfoExpire);
        }

        public static async Task<byte[]> GetPetInfo(uint id)
        {
            var bytes = await _db.StringGetAsync(string.Format(KeyPetInfoFmt, id));
            return (byte[]) bytes;
        }

        public static async ValueTask<bool> SetNotice(string notice)
        {
            var ret = await _db.StringSetAsync(KeyNotice, notice);
            return ret;
        }

        public static async Task<string> GetNotice()
        {
            var res = await _db.StringGetAsync(KeyNotice);
            return res;
        }

        public static async Task<bool> GetPayEnable()
        {
            var res = await _db.StringGetAsync(KeyPayEnable);
            var ret = res.HasValue && res == 1;
            return ret;
        }

        public static async Task<bool> SetPayEnable(bool enable)
        {
            var res = await _db.StringSetAsync(KeyPayEnable, enable ? 1 : 0);
            return res;
        }

        public static async Task<uint> GetPayRateJade()
        {
            var res = await _db.StringGetAsync(KeyPayRateJade);
            if (!res.HasValue) return GameDefine.JadePerYuan;
            var jadePerYuan = Math.Clamp((uint) res, 1, 9999999);
            return jadePerYuan;
        }

        public static async Task<bool> SetPayRateJade(uint jadePerYuan)
        {
            // 防止数据异常
            jadePerYuan = Math.Clamp(jadePerYuan, 1, 9999999);
            var res = await _db.StringSetAsync(KeyPayRateJade, jadePerYuan);
            return res;
        }

        public static async Task<uint> GetPayRateBindJade()
        {
            var res = await _db.StringGetAsync(KeyPayRateBindJade);
            if (!res.HasValue) return GameDefine.BindJadePerYuan;
            var bindjadePerYuan = Math.Clamp((uint) res, 2, 9999999);
            return bindjadePerYuan;
        }

        public static async Task<bool> SetPayRateBindJade(uint bindjadePerYuan)
        {
            // 防止数据异常
            bindjadePerYuan = Math.Clamp(bindjadePerYuan, 2, 9999999);
            var res = await _db.StringSetAsync(KeyPayRateBindJade, bindjadePerYuan);
            return res;
        }

        public static async Task SetAdminInfo(uint uid, AdminCategory category)
        {
            await _db.StringSetAsync(string.Format(KeyAdminInfoFmt, uid), (int) category, AdminInfoExpire);
        }

        public static async Task<AdminCategory> GetAdminInfo(uint uid)
        {
            var res = await _db.StringGetAsync(string.Format(KeyAdminInfoFmt, uid));
            return (AdminCategory) (int) res;
        }

        public static async Task DelAdminInfo(uint uid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyAdminInfoFmt, uid));
        }

        public static async Task SetAdminAgencyInfo(uint uid, uint agency)
        {
            await _db.StringSetAsync(string.Format(KeyAgencyInfoFmt, uid), agency, AdminInfoExpire);
        }

        public static async Task<uint> GetAdminAgencyInfo(uint uid)
        {
            var res = await _db.StringGetAsync(string.Format(KeyAgencyInfoFmt, uid));
            if (res.IsNullOrEmpty)
            {
                return 999999;
            }
            return (uint)res;
        }

        public static async Task DelAdminAgencyInfo(uint uid)
        {
            await _db.KeyDeleteAsync(string.Format(KeyAgencyInfoFmt, uid));
        }

        // public static async ValueTask<bool> SetRegCode(string code, uint adminId)
        // {
        //     var key = string.Format(KeyRegCodeFmt, code);
        //     // 先检查一遍
        //     var exists = await _db.KeyExistsAsync(key);
        //     if (exists) return false;
        //     // 先对key获取锁, 获得锁后进行赋值
        //     var lockKey = string.Format(KeyRegCodeLockFmt, code);
        //     var lockValue = Guid.NewGuid().ToString();
        //     var ret = await _db.LockTakeAsync(lockKey, lockValue, TimeSpan.FromSeconds(5));
        //     if (ret)
        //     {
        //         try
        //         {
        //             // 获取一遍
        //             exists = await _db.KeyExistsAsync(key);
        //             if (exists)
        //             {
        //                 ret = false;
        //             }
        //             else
        //             {
        //                 // 赋值
        //                 ret = await _db.StringSetAsync(key, adminId, RegCodeExpire);
        //             }
        //         }
        //         finally
        //         {
        //             await _db.LockReleaseAsync(lockKey, lockValue);
        //         }
        //     }
        //
        //     return ret;
        // }

        public static async ValueTask<bool> LockAgentPay(uint agentId, string locker)
        {
            var key = string.Format(KeyAgentPayLockFmt, agentId);
            var ret = await _db.LockTakeAsync(key, locker, AgentPayLockExpire);
            return ret;
        }

        public static async ValueTask<bool> UnlockAgentPay(uint agentId, string locker)
        {
            var key = string.Format(KeyAgentPayLockFmt, agentId);
            var ret = await _db.LockReleaseAsync(key, locker);
            return ret;
        }

        // 神兽降临--奖励锁
        public static async ValueTask<bool> LockShenShouJiangLinReward(uint serverId)
        {
            var key = string.Format(KeyShenShouJiangLinRewardLockFmt, serverId);
            var locker = serverId;
            var ret = await _db.LockTakeAsync(key, locker, TimeSpan.FromSeconds(5));
            return ret;
        }

        public static async ValueTask<bool> UnLockShenShouJiangLinReward(uint serverId)
        {
            var key = string.Format(KeyShenShouJiangLinRewardLockFmt, serverId);
            var locker = serverId;
            var ret = await _db.LockReleaseAsync(key, locker);
            return ret;
        }

        // 神兽降临--奖励
        public static async ValueTask<bool> SetShenShouJiangLinReward(uint serverId, int reward)
        {
            var key = string.Format(KeyShenShouJiangLinRewardFmt, serverId);
            var ret = await _db.StringSetAsync(key, reward);
            return ret;
        }

        public static async Task<int> GetShenShouJiangLinReward(uint serverId)
        {
            var key = string.Format(KeyShenShouJiangLinRewardFmt, serverId);
            var ret = await _db.StringGetAsync(key);
            ret.TryParse(out int reward);
            return reward;
        }
        // 转盘--宝箱
        public static async Task<RDLDChest> GetLuckyDrawChest(uint serverId)
        {
            var key = string.Format(KeyLuckyDrawChestFmt, serverId);
            var r = await _db.StringGetAsync(key);
            if (r.IsNullOrEmpty)
            {
                return null;
            }
            else
            {
                return Json.SafeDeserialize<RDLDChest>(r.ToString());
            }
        }
        public static async ValueTask<bool> SetLuckyDrawChest(uint serverId, RDLDChest chest)
        {
            var key = string.Format(KeyLuckyDrawChestFmt, serverId);
            var ret = await _db.StringSetAsync(key, Json.SafeSerialize(chest));
            return ret;
        }
        // 转盘--玩家风雨值
        public static async ValueTask<uint> GetRoleLuckyPoint(uint roleId)
        {
            var r = await _db.HashGetAsync(KeyLuckyDrawRolePoint, roleId);
            if (r.IsNullOrEmpty)
            {
                return 0;
            }
            r.TryParse(out int point);
            return (uint)point;
        }
        public static async Task SetRoleLuckyPoint(uint roleId, uint point)
        {
            await _db.HashSetAsync(KeyLuckyDrawRolePoint, new[] { new HashEntry(roleId, point) });
        }
        public static async ValueTask<bool> DeleteRoleLuckyPointAll()
        {
            if (await _db.KeyExistsAsync(KeyLuckyDrawRolePoint))
            {
                return await _db.KeyDeleteAsync(KeyLuckyDrawRolePoint);
            }
            return true;
        }
        // 转盘--玩家免费次数
        public static async ValueTask<uint> GetLuckyDrawFree(uint roleId)
        {
            var r = await _db.HashGetAsync(KeyLuckyDrawRoleFree, roleId);
            if (r.IsNullOrEmpty)
            {
                return ConfigService.LuckyDrawConfig.freeTimesADay;
            }
            r.TryParse(out int times);
            return (uint)times;
        }
        public static async Task SetLuckyDrawFree(uint roleId, uint times)
        {
            await _db.HashSetAsync(KeyLuckyDrawRoleFree, new[] { new HashEntry(roleId, times) });
        }
        public static async ValueTask<bool> DeleteLuckyDrawChestFree()
        {
            if (await _db.KeyExistsAsync(KeyLuckyDrawRoleFree))
            {
                return await _db.KeyDeleteAsync(KeyLuckyDrawRoleFree);
            }
            return true;
        }
        public static async ValueTask<uint> GetLuckyDrawFreeLastReset()
        {
            var r = await _db.StringGetAsync(KeyLuckyDrawRoleFreeLastReset);
            if (r.IsNullOrEmpty)
            {
                return 0;
            }
            r.TryParse(out int ts);
            return (uint)ts;
        }
        public static async ValueTask<bool> SetLuckyDrawChestFreeLastReset(uint ts)
        {
            return await _db.StringSetAsync(KeyLuckyDrawRoleFreeLastReset, ts);
        }
        // 转盘--玩家本期是否已经领奖
        public static async ValueTask<bool> IsLuckyDrawChestGot(uint roleId)
        {
            return await _db.HashExistsAsync(KeyLuckyDrawRoleChest, roleId);
        }
        public static async Task SetLuckyDrawChestGot(uint roleId)
        {
            await _db.HashSetAsync(KeyLuckyDrawRoleChest, new[] { new HashEntry(roleId, 1) });
        }
        public static async ValueTask<bool> DeleteLuckyDrawChestGot()
        {
            if (await _db.KeyExistsAsync(KeyLuckyDrawRoleChest))
            {
                return await _db.KeyDeleteAsync(KeyLuckyDrawRoleChest);
            }
            return true;
        }
        // VIP特权奖励
        public static async ValueTask<bool> IsRoleVipGiftDailyGet(uint roleId)
        {
            if (VipGiftDaily.ContainsKey(roleId))
            {
                return VipGiftDaily[roleId];
            }
            var r = await _db.HashGetAsync(KeyVipGiftDaily, roleId);
            if (r.IsNullOrEmpty)
            {
                VipGiftDaily[roleId] = false;
                return false;
            }
            r.TryParse(out int value);
            VipGiftDaily[roleId] = (value == 1);
            return value == 1;
        }
        public static async Task SetRoleVipGiftDailyGot(uint roleId)
        {
            VipGiftDaily[roleId] = true;
            await _db.HashSetAsync(KeyVipGiftDaily, new[] { new HashEntry(roleId, 1) });
        }
        public static async ValueTask<bool> DeleteRoleVipGiftDaily()
        {
            VipGiftDaily.Clear();
            if (await _db.KeyExistsAsync(KeyVipGiftDaily))
            {
                return await _db.KeyDeleteAsync(KeyVipGiftDaily);
            }
            return true;
        }
        // 成神之路，每天挑战次数
        public static async ValueTask<uint> GetCszlTimesDaily(uint roleId)
        {
            if (CszlTimesDaily.ContainsKey(roleId))
            {
                return CszlTimesDaily[roleId];
            }
            var r = await _db.HashGetAsync(KeyCszlTimesDaily, roleId);
            if (r.IsNullOrEmpty)
            {
                CszlTimesDaily[roleId] = 0;
                return 0;
            }
            r.TryParse(out int value);
            CszlTimesDaily[roleId] = (uint)value;
            return (uint)value;
        }
        public static async Task AddCszlTimesDaily(uint roleId)
        {
            if (!CszlTimesDaily.ContainsKey(roleId))
            {
                var useTimes = await GetCszlTimesDaily(roleId);
                CszlTimesDaily[roleId] = useTimes;
            }
            var times = CszlTimesDaily[roleId];
            CszlTimesDaily[roleId] = times + 1;
            await _db.HashSetAsync(KeyCszlTimesDaily, new[] { new HashEntry(roleId, times + 1) });
        }
        public static async Task ReduceCszlTimesDaily(uint roleId)
        {
            if (!CszlTimesDaily.ContainsKey(roleId))
            {
                var useTimes = await GetCszlTimesDaily(roleId);
                CszlTimesDaily[roleId] = useTimes;
            }
            var times = CszlTimesDaily[roleId];
            if (times <= 0) {
                return;
            }
            CszlTimesDaily[roleId] = times - 1;
            await _db.HashSetAsync(KeyCszlTimesDaily, new[] { new HashEntry(roleId, times - 1) });
        }
        public static async ValueTask<bool> DeleteCszlTimesDaily()
        {
            CszlTimesDaily.Clear();
            if (await _db.KeyExistsAsync(KeyCszlTimesDaily))
            {
                return await _db.KeyDeleteAsync(KeyCszlTimesDaily);
            }
            return true;
        }
        // 双倍经验--剩余积分
        public static async ValueTask<uint> GetRoleX2ExpLeft(uint roleId)
        {
            if (X2ExpLeft.ContainsKey(roleId))
            {
                return X2ExpLeft[roleId];
            }
            var r = await _db.HashGetAsync(KeyX2ExpLeftKey, roleId);
            if (r.IsNullOrEmpty)
            {
                X2ExpLeft[roleId] = 0;
                return 0;
            }
            r.TryParse(out long value);
            if (value > 0)
            {
                X2ExpLeft[roleId] = (uint)value;
                return (uint)value;
            }
            else
            {
                X2ExpLeft[roleId] = 0;
                return 0;
            }
        }
        public static async Task SetRoleX2ExpLeft(uint roleId, uint left)
        {
            X2ExpLeft[roleId] = left;
            await _db.HashSetAsync(KeyX2ExpLeftKey, new[] { new HashEntry(roleId, left) });
        }
        public static async ValueTask<bool> DeleteRoleX2ExpLeft()
        {
            X2ExpLeft.Clear();
            if (await _db.KeyExistsAsync(KeyX2ExpLeftKey))
            {
                return await _db.KeyDeleteAsync(KeyX2ExpLeftKey);
            }
            return true;
        }
        // 双倍经验--已领取
        public static async ValueTask<uint> GetRoleX2ExpCurrentGot(uint roleId)
        {
            if (X2ExpCurrentGot.ContainsKey(roleId))
            {
                return X2ExpCurrentGot[roleId];
            }
            var r = await _db.HashGetAsync(KeyX2ExpCurrentGotKey, roleId);
            if (r.IsNullOrEmpty)
            {
                X2ExpCurrentGot[roleId] = 0;
                return 0;
            }
            r.TryParse(out long value);
            if (value > 0)
            {
                X2ExpCurrentGot[roleId] = (uint)value;
                return (uint)value;
            }
            else
            {
                X2ExpCurrentGot[roleId] = 0;
                return 0;
            }
        }
        public static async Task SetRoleX2ExpCurrentGot(uint roleId, uint current)
        {
            X2ExpCurrentGot[roleId] = current;
            await _db.HashSetAsync(KeyX2ExpCurrentGotKey, new[] { new HashEntry(roleId, current) });
        }
        public static async ValueTask<bool> DeleteRoleX2ExpCurrentGot()
        {
            X2ExpCurrentGot.Clear();
            if (await _db.KeyExistsAsync(KeyX2ExpCurrentGotKey))
            {
                return await _db.KeyDeleteAsync(KeyX2ExpCurrentGotKey);
            }
            return true;
        }
    }
}