using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Equip
{
    public class PetOrnament
    {
        private PlayerGrain _player;
        public PetOrnamentEntity Entity { get; private set; }
        private PetOrnamentEntity _lastEntity; //上一次更新的Entity

        public uint Id => Entity.Id;

        public byte Grade => Entity.Grade;

        public uint Score { get; private set; }

        public uint Place
        {
            get => Entity.Place;
            set => Entity.Place = value;
        }

        public bool Locked
        {
            get => Entity.Locked;
            set => Entity.Locked = value;
        }

        public Attrs Attrs { get; private set; }

        private List<AttrPair> _baseAttrs;

        public PetOrnament(PlayerGrain player, PetOrnamentEntity entity)
        {
            _player = player;
            Entity = entity;
            _lastEntity = new PetOrnamentEntity();
            _lastEntity.CopyFrom(Entity);

            _baseAttrs = new ();
            Attrs = new Attrs();
            CalculateAttribute();
            RefreshScore();
        }

        public async Task Destroy()
        {
            // 更新数据入库
            await SaveData(false);
            _lastEntity = null;
            Entity = null;

            _player = null;

            _baseAttrs.Clear();
            _baseAttrs = null;
            Attrs.Dispose();
            Attrs = null;
        }

        public PetOrnamentData BuildPbData()
        {
            var pbData = new PetOrnamentData
            {
                Id = Entity.Id,
                Type = (PetOrnamentTpe)Entity.TypeId,
                Locked = (uint)(Entity.Locked ? 2 : 1),
                Grade = Entity.Grade,
                Place = Entity.Place,
                Score = Score,
                BaseAttrs = {_baseAttrs}
            };
            return pbData;
        }

        public Task SendInfo()
        {
            return _player.SendPacket(GameCmd.S2CPetOrnamentInfo, new S2C_PetOrnamentInfo {Data = BuildPbData()});
        }

        // 分享的时候缓存
        public async Task Cache()
        {
            var bytes = Packet.Serialize(BuildPbData());
            await RedisService.SetPetOrnamentInfo(Entity.Id, bytes);
        }

        public async Task SaveData(bool copy = true)
        {
            if (Entity.Equals(_lastEntity)) return;
            var ret = await DbService.UpdateEntity(_lastEntity, Entity);
            if (ret && copy) _lastEntity.CopyFrom(Entity);
        }

        private void RefreshScore()
        {
            Score = CalcOrnamentScore(_baseAttrs);
        }

        private void CalculateAttribute()
        {
            if (Entity.BaseAttrs.StartsWith('[')) {
                _baseAttrs = ParseAttrPairs(Entity.BaseAttrs);
            } else {
                var temp = new Attrs();
                temp.FromJson(Entity.BaseAttrs);
                _baseAttrs = temp.ToList();
                Entity.BaseAttrs = FormatAttrPairs(_baseAttrs);
            }
            // 从基础属性和炼化属性中统计Attr
            Attrs.Clear();
            foreach (var p in _baseAttrs)
            {
                var value = p.Value;
                if (!GameDefine.EquipNumericalAttrType.ContainsKey(p.Key))
                {
                    value = p.Value / 10;
                }

                Attrs.Add(p.Key, value);
            }
        }

        public static uint CalcOrnamentScore(Attrs baseAttrs)
        {
            // 计算得分
            var total = 0f;
            foreach (var (k, v) in baseAttrs)
            {
                var scale = GameDefine.AttrTypeCalcScoreScale.GetValueOrDefault(k, 1f);
                total += MathF.Ceiling(MathF.Abs(v * scale));
            }

            var score = (uint) MathF.Ceiling(total);
            return score;
        }

        public static uint CalcOrnamentScore(List<AttrPair> list)
        {
            var attrs = new Attrs();
            foreach (var pair in list)
            {
                attrs.Add(pair.Key, pair.Value);
            }

            return CalcOrnamentScore(attrs);
        }

        public static List<AttrPair> GetOrnamentRecastAttrs(uint pos, int grade)
        {
            var list = new List<AttrPair>();

            ConfigService.PetOrnamentAttrs.TryGetValue(pos, out var cfgList);
            if (cfgList == null || cfgList.Count == 0) return list;

            var rnd = new Random();

            switch (pos)
            {
                // 兽环：固定属性条，攻击力最大+3000/速度20其一
                case 1:
                    if (rnd.Next(100) < 50)
                    {
                        list.Add(new AttrPair { Key = AttrType.Atk, Value = rnd.Next(750, 3000) + 1 });
                    }
                    else
                    {
                        list.Add(new AttrPair { Key = AttrType.Spd, Value = rnd.Next(5, 20) + 1 });
                    }
                    break;
                // 兽铃：固定属性条，法力最大+20000
                case 2:
                    list.Add(new AttrPair { Key = AttrType.Mp, Value = rnd.Next(5000, 20000) + 1 });
                    break;
                // 兽甲：固定属性条，气血最大+20000
                case 3:
                    list.Add(new AttrPair { Key = AttrType.Hp, Value = rnd.Next(5000, 20000) + 1 });
                    break;
                default:
                    break;
            }
            // 最多再来3条，最多一共4条（包含固定属性）
            var count = 1;
            if (grade == 1)
            {
                count = rnd.Next(1, 4);
            }
            else if (grade == 2)
            {
                count = rnd.Next(2, 4);
            }
            else if (grade == 3)
            {
                count = 3;
            }
            var keys = cfgList.Keys.ToList();
            while(count > 0) {
                var AttrStr = keys[rnd.Next(keys.Count)];
                // 配置出错
                if (!GameDefine.EquipAttrTypeMap.TryGetValue(AttrStr, out var attrType)) continue;
                var arr = cfgList[AttrStr].Range.Split(",");
                int.TryParse(arr[0], out var min);
                int.TryParse(arr[1], out var max);
                var deltaValue = max - min;
                list.Add(new AttrPair { Key = attrType, Value = rnd.Next(min, max + 1) });
                count--;
            }
            return list;
        }
        public static List<AttrPair> ParseAttrPairs(string text)
        {
            var resList = new List<AttrPair>();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var list = Json.Deserialize<List<string>>(text);
                foreach (var line in list)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var arr = line.Split('_');
                    if (arr.Length != 2) continue;
                    byte.TryParse(arr[0], out var key);
                    float.TryParse(arr[1], out var value);
                    resList.Add(new AttrPair { Key = (AttrType)key, Value = value });
                }
            }

            return resList;
        }

        public static string FormatAttrPairs(IReadOnlyCollection<AttrPair> list)
        {
            if (list == null || list.Count == 0)
            {
                return string.Empty;
            }

            var tmpList = new List<string>();
            foreach (var pair in list)
            {
                if (pair.Key != AttrType.Unkown && pair.Value != 0)
                {
                    tmpList.Add($"{(byte)pair.Key}_{pair.Value}");
                }
            }

            if (tmpList.Count == 0)
            {
                return string.Empty;
            }

            return Json.SafeSerialize(tmpList);
        }
    }
}