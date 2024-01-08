using System;
using System.Collections;
using System.Collections.Generic;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle
{
    public class SkillEffectData : IEnumerable<KeyValuePair<string, float>>, IDisposable
    {
        private Dictionary<string, float> _dic;

        public SkillEffectData()
        {
            _dic = new Dictionary<string, float>();
            TargetNum = 1;
        }

        public SkillEffectData(IDictionary<string, float> dic)
        {
            _dic = new Dictionary<string, float>(dic);
        }

        public float this[string key]
        {
            get => _dic.GetValueOrDefault(key, 0f);
            set => _dic[key] = value;
        }

        /// <summary>
        /// 目标人数
        /// </summary>
        public int TargetNum
        {
            get => (int) _dic.GetValueOrDefault("TargetNum", 1);
            set => _dic["TargetNum"] = value;
        }

        /// <summary>
        /// 持续的回合数
        /// </summary>
        public int Round
        {
            get => (int) _dic.GetValueOrDefault("Round", 0);
            set => _dic["Round"] = value;
        }

        /// <summary>
        /// 攻击增加
        /// </summary>
        public int Atk
        {
            get => (int) _dic.GetValueOrDefault("Atk", 0);
            set => _dic["Atk"] = value;
        }

        /// <summary>
        /// 攻击百分比
        /// </summary>
        public float AtkPercent
        {
            get => (int) _dic.GetValueOrDefault("AtkPercent", 0f);
            set => _dic["AtkPercent"] = value;
        }

        /// <summary>
        /// 速度增加
        /// </summary>
        public int Spd
        {
            get => (int) _dic.GetValueOrDefault("Spd", 0);
            set => _dic["Spd"] = value;
        }

        /// <summary>
        /// 速度百分比
        /// </summary>
        public float SpdPercent
        {
            get => _dic.GetValueOrDefault("SpdPercent", 0f);
            set => _dic["SpdPercent"] = value;
        }

        /// <summary>
        /// 加蓝
        /// </summary>
        public float Mp
        {
            get => _dic.GetValueOrDefault("Mp", 0f);
            set => _dic["Mp"] = value;
        }

        /// <summary>
        /// 加蓝百分比
        /// </summary>
        public float MpPercent
        {
            get => _dic.GetValueOrDefault("MpPercent", 0f);
            set => _dic["MpPercent"] = value;
        }

        /// <summary>
        /// 扣蓝百分比
        /// </summary>
        public float MpPercent2
        {
            get => _dic.GetValueOrDefault("MpPercent2", 0f);
            set => _dic["MpPercent2"] = value;
        }

        /// <summary>
        /// 伤害值
        /// </summary>
        public float Hurt
        {
            get => _dic.GetValueOrDefault("Hurt", 0f);
            set => _dic["Hurt"] = value;
        }

        /// <summary>
        /// 伤害百分比
        /// </summary>
        public float HurtPercent
        {
            get => _dic.GetValueOrDefault("HurtPercent", 0f);
            set => _dic["HurtPercent"] = value;
        }
        
        /// <summary>
        /// 伤害蓝
        /// </summary>
        public float MpHurt
        {
            get => _dic.GetValueOrDefault("MpHurt", 0f);
            set => _dic["MpHurt"] = value;
        }

        /// <summary>
        /// 防御值
        /// </summary>
        public float FangYu
        {
            get => _dic.GetValueOrDefault("FangYu", 0f);
            set => _dic["FangYu"] = value;
        }

        /// <summary>
        /// 控制抗性增加
        /// </summary>
        public float KongZhi
        {
            get => _dic.GetValueOrDefault("KongZhi", 0f);
            set => _dic["KongZhi"] = value;
        }

        /// <summary>
        /// 控制抗性减少
        /// </summary>
        public float KongZhi2
        {
            get => _dic.GetValueOrDefault("KongZhi2", 0f);
            set => _dic["KongZhi2"] = value;
        }

        /// <summary>
        /// 法伤抗性增加
        /// </summary>
        public float FaShang
        {
            get => _dic.GetValueOrDefault("FaShang", 0f);
            set => _dic["FaShang"] = value;
        }

        /// <summary>
        /// 法伤忽视增加
        /// </summary>
        public float HFaShang
        {
            get => _dic.GetValueOrDefault("FaShang", 0f);
            set => _dic["FaShang"] = value;
        }

        /// <summary>
        /// 命中增加
        /// </summary>
        public float Hit
        {
            get => _dic.GetValueOrDefault("Hit", 0f);
            set => _dic["Hit"] = value;
        }

        // 命中 绝对值
        public float MingZhong
        {
            get => _dic.GetValueOrDefault("MingZhong", 0f);
            set => _dic["MingZhong"] = value;
        }
        // 破防 绝对值
        public float PoFang
        {
            get => _dic.GetValueOrDefault("PoFang", 0f);
            set => _dic["PoFang"] = value;
        }
        // 破防率 绝对值
        public float PoFangLv
        {
            get => _dic.GetValueOrDefault("PoFangLv", 0f);
            set => _dic["PoFangLv"] = value;
        }
        // 忽视抗物理
        public float HDWuLi
        {
            get => _dic.GetValueOrDefault("HDWuLi", 0f);
            set => _dic["HDWuLi"] = value;
        }
        // 物理吸收
        public float PXiShou
        {
            get => _dic.GetValueOrDefault("PXiShou", 0f);
            set => _dic["PXiShou"] = value;
        }
        // 抗物理
        public float DWuLi
        {
            get => _dic.GetValueOrDefault("DWuLi", 0f);
            set => _dic["DWuLi"] = value;
        }
        // 增加血量上限百分比
        public int HpMaxPercent
        {
            get => (int) _dic.GetValueOrDefault("HpMaxPercent", 0);
            set => _dic["HpMaxPercent"] = value;
        }

        /// <summary>
        /// 加血量
        /// </summary>
        public int Hp
        {
            get => (int) _dic.GetValueOrDefault("Hp", 0);
            set => _dic["Hp"] = value;
        }

        /// <summary>
        /// 加血百分比
        /// </summary>
        public float HpPercent
        {
            get => _dic.GetValueOrDefault("HpPercent", 0f);
            set => _dic["HpPercent"] = value;
        }

        /// <summary>
        /// 智能回血,用于吸血类技能
        /// </summary>
        public float AiHp
        {
            get => _dic.GetValueOrDefault("AiHp", 0f);
            set => _dic["AiHp"] = value;
        }

        /// <summary>
        /// 增加属性
        /// </summary>
        public float Add
        {
            get => _dic.GetValueOrDefault("Add", 0f);
            set => _dic["Add"] = value;
        }

        /// <summary>
        /// 减少属性
        /// </summary>
        public float Del
        {
            get => _dic.GetValueOrDefault("Del", 0f);
            set => _dic["Del"] = value;
        }

        /// <summary>
        /// 五行属性
        /// </summary>
        public AttrType AttrType
        {
            get => (AttrType) _dic.GetValueOrDefault("AttrType", 0);
            set => _dic["AttrType"] = (int) value;
        }

        /// <summary>
        /// 五行属性值
        /// </summary>
        public float AttrValue
        {
            get => _dic.GetValueOrDefault("AttrValue", 0f);
            set => _dic["AttrValue"] = value;
        }

        /// <summary>
        /// 值, 闪现
        /// </summary>
        public int Value
        {
            get => (int) _dic.GetValueOrDefault("Value", 0);
            set => _dic["Value"] = value;
        }

        /// <summary>
        /// 百分值，分子
        /// </summary>
        public float Percent
        {
            get => _dic.GetValueOrDefault("Percent", 0f);
            set => _dic["Percent"] = value;
        }

        /// <summary>
        /// 隐身
        /// </summary>
        public int YinShen
        {
            get => (int) _dic.GetValueOrDefault("YinShen", 0);
            set => _dic["YinShen"] = value;
        }

        /// <summary>
        /// 技能id, 混乱
        /// </summary>
        public SkillId SkillId
        {
            get => (SkillId) (int) _dic.GetValueOrDefault("SkillId", 0);
            set => _dic["SkillId"] = (int) value;
        }

        /// <summary>
        /// 钱
        /// </summary>
        public int Money
        {
            get => (int) _dic.GetValueOrDefault("Money", 0);
            set => _dic["Money"] = value;
        }

        /// <summary>
        /// 吸血给自己
        /// </summary>
        public float SuckHp
        {
            get => _dic.GetValueOrDefault("SuckHp", 0f);
            set => _dic["SuckHp"] = value;
        }

        /// <summary>
        /// 吸蓝给自己
        /// </summary>
        public float SuckMp
        {
            get => _dic.GetValueOrDefault("SuckMp", 0f);
            set => _dic["SuckMp"] = value;
        }

        /// <summary>
        /// 吸收伤害百分比
        /// </summary>
        public float SuckHurtPercent
        {
            get => _dic.GetValueOrDefault("SuckHurtPercent", 0f);
            set => _dic["SuckHurtPercent"] = value;
        }

        /// <summary>
        /// 伤害衰减百分比
        /// </summary>
        public float HurtDecayPercent
        {
            get => _dic.GetValueOrDefault("HurtDecayPercent", 0f);
            set => _dic["HurtDecayPercent"] = value;
        }
        
        /// <summary>
        /// 伤害增加百分比
        /// </summary>
        public float HurtRaisePercent
        {
            get => _dic.GetValueOrDefault("HurtRaisePercent", 0f);
            set => _dic["HurtRaisePercent"] = value;
        }

        public IEnumerator<KeyValuePair<string, float>> GetEnumerator()
        {
            return _dic.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            _dic.Clear();
            _dic = null;
        }

        public SkillEffectData Clone()
        {
            return new SkillEffectData(_dic);
        }
    }
}