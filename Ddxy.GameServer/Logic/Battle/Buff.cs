using System;
using System.Collections.Generic;
using System.Linq;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Logic.Battle.Skill;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle
{
    public class Buff
    {
        public uint Id { get; }

        public SkillId SkillId { get; }

        public SkillType SkillType { get; }

        public SkillEffectData EffectData { get; }

        public uint Source; //谁加的
        public uint Round;
        public uint CurRound;
        public uint Probability;

        public float Percent; //失心疯、孟婆汤遗忘技能的概率
        public uint AtkTimes;

        private Attrs _attrs; //记录buff带来的属性加成
        private Dictionary<SkillId, bool> _forgetSkills; //记录buff遗忘的技能

        public Buff(uint id, BaseSkill skill, SkillEffectData effectData)
        {
            Id = id;
            SkillId = skill.Id;
            SkillType = skill.Type;
            EffectData = effectData;

            Source = 0;

            Round = (uint) effectData.Round;
            CurRound = 0;
            Probability = 0;

            // buff挂载后增加的属性绝对值
            _attrs = new Attrs(5);
            _forgetSkills = new Dictionary<SkillId, bool>(3);
        }

        public void OnAppend(BattleMember member)
        {
            _attrs ??= new Attrs(5);
            _forgetSkills ??= new Dictionary<SkillId, bool>(3);
            // 统计带来的属性增量
            _attrs.Clear();
            // 乾坤借速、天外飞魔
            if (EffectData.SpdPercent != 0)
            {
                _attrs.Add(AttrType.Spd, member.Attrs.Get(AttrType.Spd) * EffectData.SpdPercent / 100);
            }

            // 兽王神力，魔神附身
            if (EffectData.AtkPercent != 0)
            {
                _attrs.Add(AttrType.Atk, member.Attrs.Get(AttrType.Atk) * EffectData.AtkPercent / 100);
            }

            if (EffectData.Hit != 0)
            {
                _attrs.Add(AttrType.PmingZhong, member.Attrs.Get(AttrType.PmingZhong) * EffectData.Hit / 100);
            }
            // 命中 绝对值
            if (EffectData.MingZhong != 0)
            {
                _attrs.Add(AttrType.PmingZhong, EffectData.MingZhong);
            }
            // 破防率 绝对值
            if (EffectData.PoFangLv != 0)
            {
                _attrs.Add(AttrType.PpoFangLv, EffectData.PoFangLv);
            }
            // 破防程度 绝对值
            if (EffectData.PoFang != 0)
            {
                _attrs.Add(AttrType.PpoFang, EffectData.PoFang);
            }
            // 忽视抗物理
            if (EffectData.HDWuLi != 0)
            {
                _attrs.Add(AttrType.HdwuLi, member.Attrs.Get(AttrType.HdwuLi) * EffectData.HDWuLi / 100);
            }
            // 物理吸收
            if (EffectData.PXiShou != 0)
            {
                _attrs.Add(AttrType.PxiShou, member.Attrs.Get(AttrType.PxiShou) * EffectData.PXiShou / 100);
            }
            // 抗物理
            if (EffectData.DWuLi != 0)
            {
                _attrs.Add(AttrType.DwuLi, member.Attrs.Get(AttrType.DwuLi) * EffectData.DWuLi / 100);
            }
            // 血量上限百分比
            if (EffectData.HpMaxPercent != 0)
            {
                _attrs.Add(AttrType.HpMax, member.Attrs.Get(AttrType.HpMax) * EffectData.HpMaxPercent / 100);
            }

            // 魔神护体,含情脉脉
            if (EffectData.KongZhi != 0)
            {
                var percent = EffectData.KongZhi / 100;
                _attrs.Add(AttrType.DhunLuan, member.Attrs.Get(AttrType.DhunLuan) * percent);
                _attrs.Add(AttrType.DfengYin, member.Attrs.Get(AttrType.DfengYin) * percent);
                _attrs.Add(AttrType.DhunShui, member.Attrs.Get(AttrType.DhunShui) * percent);
                _attrs.Add(AttrType.DyiWang, member.Attrs.Get(AttrType.DyiWang) * percent);
            }

            // 法伤抗性
            if (EffectData.FaShang != 0)
            {
                var percent = EffectData.FaShang / 100;
                _attrs.Add(AttrType.Dfeng, member.Attrs.Get(AttrType.Dfeng) * percent);
                _attrs.Add(AttrType.Dhuo, member.Attrs.Get(AttrType.Dhuo) * percent);
                _attrs.Add(AttrType.Dshui, member.Attrs.Get(AttrType.Dshui) * percent);
                _attrs.Add(AttrType.Dlei, member.Attrs.Get(AttrType.Dlei) * percent);
                _attrs.Add(AttrType.Ddu, member.Attrs.Get(AttrType.Ddu) * percent);
                _attrs.Add(AttrType.DsanShi, member.Attrs.Get(AttrType.DsanShi) * percent);
                _attrs.Add(AttrType.DguiHuo, member.Attrs.Get(AttrType.DguiHuo) * percent);
            }

            // 法伤忽视
            if (EffectData.HFaShang != 0)
            {
                var percent = EffectData.HFaShang / 100;
                _attrs.Add(AttrType.Hdfeng,   member.Attrs.Get(AttrType.Hdfeng) * percent, false);
                _attrs.Add(AttrType.Hdhuo,    member.Attrs.Get(AttrType.Hdhuo) * percent, false);
                _attrs.Add(AttrType.Hdshui,   member.Attrs.Get(AttrType.Hdshui) * percent, false);
                _attrs.Add(AttrType.Hdlei,    member.Attrs.Get(AttrType.Hdlei) * percent, false);
                _attrs.Add(AttrType.Hddu,     member.Attrs.Get(AttrType.Hddu) * percent, false);
                _attrs.Add(AttrType.HdsanShi, member.Attrs.Get(AttrType.HdsanShi) * percent, false);
                _attrs.Add(AttrType.HdguiHuo, member.Attrs.Get(AttrType.HdguiHuo) * percent, false);
            }

            // 秦丝冰雾，倩女幽魂
            if (EffectData.KongZhi2 != 0)
            {
                var percent = EffectData.KongZhi2 / 100;
                _attrs.Add(AttrType.DhunLuan, member.Attrs.Get(AttrType.DhunLuan) * percent * -1);
                _attrs.Add(AttrType.DfengYin, member.Attrs.Get(AttrType.DfengYin) * percent * -1);
                _attrs.Add(AttrType.DhunShui, member.Attrs.Get(AttrType.DhunShui) * percent * -1);
                _attrs.Add(AttrType.DyiWang, member.Attrs.Get(AttrType.DyiWang) * percent * -1);
            }

            if (EffectData.FangYu != 0)
            {
                _attrs.Add(AttrType.DwuLi, member.Attrs.Get(AttrType.DwuLi) * EffectData.FangYu / 100);
            }

            // 五行属性, 比如枯木逢春，如人饮水, 西天净土
            if (EffectData.AttrType > AttrType.Unkown && EffectData.AttrValue != 0)
            {
                _attrs.Add(EffectData.AttrType, EffectData.AttrValue);
            }

            // 有凤来仪
            if (SkillId == SkillId.YouFengLaiYi && EffectData.AttrValue != 0)
            {
                _attrs.Add(AttrType.Jin, EffectData.AttrValue);
                _attrs.Add(AttrType.Mu, EffectData.AttrValue);
                _attrs.Add(AttrType.Shui, EffectData.AttrValue);
                _attrs.Add(AttrType.Huo, EffectData.AttrValue);
                _attrs.Add(AttrType.Tu, EffectData.AttrValue);
            }

            // 六识炽烈 增加命中
            if (SkillId == SkillId.LiuShiChiLie && EffectData.Percent > 0)
            {
                _attrs.Add(AttrType.PmingZhong, EffectData.Percent);
            }

            foreach (var (k, v) in _attrs)
            {
                if (v != 0) member.Attrs.Add(k, v);
            }

            // Buff遗忘技能
            if (SkillType == SkillType.Forget && member.Skills.Count > 0)
            {
                // 先全部恢复所有技能的遗忘状态
                foreach (var (_, v) in member.Skills)
                {
                    v.CanUse = true;
                }

                _forgetSkills.Clear();

                var forgetRate = Percent;
                if (Probability < 10000)
                    forgetRate *= Probability / 10000.0f;

                var rnd = new Random();
                foreach (var (k, v) in member.Skills)
                {
                    // 不能遗忘被动技能
                    if (SkillManager.IsPassiveSkill(k)) continue;
                    if (rnd.Next(0, 100) < forgetRate)
                    {
                        v.CanUse = false;
                        _forgetSkills.Add(k, true);
                    }
                }

                // 随机遗忘3~6个技能, 只能遗忘主动技能
                // var rnd = new Random();
                // var num = rnd.Next(3, 7);
                // if (num > Skills.Count) num = Skills.Count;
                // var keys = Skills.Keys.ToList();
                // for (var i = 0; i < num; i++)
                // {
                //     var idx = rnd.Next(0, keys.Count);
                //     Skills.TryGetValue(keys[idx], out var skill);
                //     if (skill != null) skill.CanUse = false;
                //     keys.RemoveAt(idx);
                // }
            }
        }

        public void OnRemove(BattleMember member)
        {
            // 恢复buff增加的数值
            foreach (var (k, v) in _attrs)
            {
                if (v != 0) member.Attrs.Add(k, -v);
            }

            _attrs.Dispose();
            _attrs = null;

            // 恢复被遗忘的技能
            foreach (var skid in _forgetSkills.Keys)
            {
                member.Skills.TryGetValue(skid, out var sk);
                if (sk != null) sk.CanUse = true;
            }

            _forgetSkills.Clear();
            _forgetSkills = null;
        }

        public void OnResetRound()
        {
        }

        public float StartRound(BattleMember member, Dictionary<uint, BattleMember> members)
        {
            float addHp = 0;
            if (member.HasBuff(SkillType.Seal)) return addHp;

            if (EffectData.Hurt > 0)
            {
                // 伤害会打断昏睡
                var sleepBuff = member.GetBuffByMagicType(SkillType.Sleep);
                if (sleepBuff != null)
                {
                    sleepBuff.AtkTimes++;
                    // 套装技能 醉生梦死-无价
                    var canBreak = true;
                    members.TryGetValue(sleepBuff.Source, out var sender);
                    if (sender != null && sender.OrnamentSkills.ContainsKey(1032) &&
                        sleepBuff.AtkTimes <= 1)
                    {
                        canBreak = false;
                    }

                    if (canBreak)
                    {
                        member.RemoveBuff(SkillType.Sleep);
                    }
                }

                member.AddHp(-EffectData.Hurt);
                addHp -= EffectData.Hurt;
            }

            if (EffectData.Hp != 0)
            {
                member.AddHp(EffectData.Hp);
                addHp += EffectData.Hp;
            }

            // 龙族 治愈技能BUFF
            if (EffectData.HpMaxPercent != 0 && (SkillId == SkillId.PeiRanMoYu || SkillId == SkillId.ZeBeiWanWu))
            {
                addHp += member.AddHp((int)MathF.Ceiling(member.HpMax * EffectData.HpMaxPercent / 100));
            }
            // 雨魄云魂 无价
            if (SkillType == SkillType.Forget)
            {
                members.TryGetValue(Source, out var sender);
                if (sender != null && sender.OrnamentSkills.ContainsKey(4022))
                {
                    var dic = new Dictionary<AttrType, float>
                    {
                        [AttrType.Dfeng] = 0f,
                        [AttrType.Dhuo] = 0f,
                        [AttrType.Dshui] = 0f,
                        [AttrType.Dlei] = 0f,
                        [AttrType.DguiHuo] = 0f,
                        [AttrType.Ddu] = 0f,
                    };
                    foreach (var at in dic.Keys.ToList())
                    {
                        dic[at] = member.Attrs.Get(at) * 0.15f;
                    }

                    foreach (var (k, v) in dic)
                    {
                        if (v <= 0) continue;
                        _attrs.Add(k, -v);
                        member.Attrs.Add(k, -v);
                    }
                }
            }

            return addHp;
        }

        public void NextRound()
        {
            CurRound++;
        }

        public bool IsEnd()
        {
            return CurRound >= Round;
        }
    }
}