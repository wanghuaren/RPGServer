using System.Collections.Generic;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public static class SkillManager
    {
        private static readonly Dictionary<SkillId, BaseSkill> All = new(100);

        private static readonly Dictionary<SkillType, byte> AtkSkills = new()
        {
            [SkillType.Physics] = 0,
            [SkillType.Chaos] = 0,
            [SkillType.Toxin] = 0,
            [SkillType.Sleep] = 0,
            [SkillType.Seal] = 0,
            [SkillType.Feng] = 0,
            [SkillType.Huo] = 0,
            [SkillType.Lei] = 0,
            [SkillType.Shui] = 0,
            [SkillType.Frighten] = 0,
            [SkillType.ThreeCorpse] = 0,
            [SkillType.Charm] = 0,
            [SkillType.GhostFire] = 0,
            [SkillType.Forget] = 0,
            [SkillType.SubDefense] = 0,
            // 龙族
            [SkillType.SaoJi] = 0,
            [SkillType.ZhenJi] = 0,
        };

        private static readonly Dictionary<SkillType, byte> DefSkills = new()
        {
            [SkillType.Speed] = 0,
            [SkillType.Defense] = 0,
            [SkillType.Attack] = 0,
            [SkillType.YinShen] = 0
        };

        private static readonly Dictionary<SkillType, byte> BuffSkills = new()
        {
            [SkillType.Physics] = 0,
            [SkillType.Chaos] = 0,
            [SkillType.Toxin] = 0,
            [SkillType.Sleep] = 0,
            [SkillType.Seal] = 0,
            [SkillType.Feng] = 0,
            [SkillType.Huo] = 0,
            [SkillType.Lei] = 0,
            [SkillType.Shui] = 0,
            [SkillType.Speed] = 0,
            [SkillType.Defense] = 0,
            [SkillType.Attack] = 0,
            [SkillType.Frighten] = 0,
            [SkillType.ThreeCorpse] = 0,
            [SkillType.Charm] = 0,
            [SkillType.GhostFire] = 0,
            [SkillType.Forget] = 0,
            [SkillType.SubDefense] = 0,
            // 龙族
            [SkillType.SaoJi] = 0,
            [SkillType.ZhenJi] = 0,
        };

        private static readonly Dictionary<SkillType, byte> DebuffSkills = new()
        {
            [SkillType.Chaos] = 0,
            [SkillType.Toxin] = 0,
            [SkillType.Sleep] = 0,
            [SkillType.Seal] = 0,
            [SkillType.Charm] = 0,
            [SkillType.Forget] = 0,
            [SkillType.SubDefense] = 0,
            [SkillType.RaiseHurt] = 0
        };

        /// <summary>
        /// 加防/加攻/加速技能
        /// </summary>
        private static readonly Dictionary<SkillId, byte> FGSSkills = new()
        {
            [SkillId.TianWaiFeiMo] = 0,
            [SkillId.QianKunJieSu] = 0,
            [SkillId.ShouWangShenLi] = 0,
            [SkillId.MoShenFuShen] = 0,
            [SkillId.MoShenHuTi] = 0,
            [SkillId.HanQingMoMo] = 0,
        };

        /// <summary>
        /// 控制技能
        /// </summary>
        private static readonly Dictionary<SkillId, byte> KongZhiSkills = new()
        {
            [SkillId.JieDaoShaRen] = 0,
            [SkillId.MiHunZui] = 0,
            [SkillId.ZuoBiShangGuan] = 0,
            [SkillId.ShiXinKuangLuan] = 0,
            [SkillId.BaiRiMian] = 0,
            [SkillId.SiMianChuGe] = 0,
            [SkillId.MengPoTang] = 0,
            [SkillId.ShiXinFeng] = 0
        };

        /// <summary>
        /// 五行技能
        /// </summary>
        private static readonly Dictionary<SkillId, byte> WuXingSkills = new()
        {
            [SkillId.ChuiJinZhuanYu] = 0,
            [SkillId.KuMuFengChun] = 0,
            [SkillId.RuRenYinShui] = 0,
            [SkillId.FengHuoLiaoYuan] = 0,
            [SkillId.XiTianJingTu] = 0,
            [SkillId.YouFengLaiYi] = 0,
            // [SkillId.YouFengLaiYiJin] = 0,
            // [SkillId.YouFengLaiYiMu] = 0,
            // [SkillId.YouFengLaiYiShui] = 0,
            // [SkillId.YouFengLaiYiHuo] = 0,
            // [SkillId.YouFengLaiYiTu] = 0,
        };

        /// <summary>
        /// 消耗魔法的技能
        /// </summary>
        private static readonly Dictionary<SkillId, byte> ForceMpSkills = new Dictionary<SkillId, byte>
        {
            [SkillId.BingLinChengXia] = 0,
            [SkillId.TianMoJieTi] = 0,
            [SkillId.FenGuangHuaYing] = 0,
            [SkillId.QingMianLiaoYa] = 0,
            [SkillId.XiaoLouYeKu] = 0,
            [SkillId.HighTianMoJieTi] = 0,
            [SkillId.HighFenGuangHuaYing] = 0,
            [SkillId.HighQingMianLiaoYa] = 0,
            [SkillId.HighXiaoLouYeKu] = 0,
            [SkillId.ChunHuiDaDi] = 0
        };

        static SkillManager()
        {
            All.Add(SkillId.NormalAtk, new NormalAttackSkill());
            All.Add(SkillId.NormalDef, new NormalDefendSkill());
            // 人族
            All.Add(SkillId.HeDingHongFen, new HeDingHongFenSkill());
            All.Add(SkillId.WanDuGongXin, new WanDuGongXinSkill());
            All.Add(SkillId.JieDaoShaRen, new JieDaoShaRenSkill());
            All.Add(SkillId.ShiXinKuangLuan, new ShiXinKuangLuanSkill());
            All.Add(SkillId.MiHunZui, new MiHunZuiSkill());
            All.Add(SkillId.BaiRiMian, new BaiRiMianSkill());
            All.Add(SkillId.ZuoBiShangGuan, new ZuoBiShangGuanSkill());
            All.Add(SkillId.SiMianChuGe, new SiMianChuGeSkill());
            // 仙族
            All.Add(SkillId.LieHuoJiaoYang, new LieHuoJiaoYangSkill());
            All.Add(SkillId.JiuYinChunHuo, new JiuYinChunHuoSkill());
            All.Add(SkillId.FengLeiYongDong, new FengLeiYongDongSkill());
            All.Add(SkillId.XiuLiQianKun, new XiuLiQianKunSkill());
            All.Add(SkillId.DianShanLeiMing, new DianShanLeiMingSkill());
            All.Add(SkillId.TianZhuDiMie, new TianZhuDiMieSkill());
            All.Add(SkillId.JiaoLongChuHai, new JiaoLongChuHaiSkill());
            All.Add(SkillId.JiuLongBingFeng, new JiuLongBingFengSkill());
            // 魔族
            All.Add(SkillId.MoShenHuTi, new MoShenHuTiSkill());
            All.Add(SkillId.HanQingMoMo, new HanQingMoMoSkill());
            All.Add(SkillId.TianWaiFeiMo, new TianWaiFeiMoSkill());
            All.Add(SkillId.QianKunJieSu, new QianKunJieSuSkill());
            All.Add(SkillId.ShouWangShenLi, new ShouWangShenLiSkill());
            All.Add(SkillId.MoShenFuShen, new MoShenFuShenSkill());
            All.Add(SkillId.XiaoHunShiGu, new XiaoHunShiGuSkill());
            All.Add(SkillId.YanLuoZhuiMing, new YanLuoZhuiMingSkill());
            // 鬼族
            All.Add(SkillId.QinSiBingWu, new QinSiBingWuSkill());
            All.Add(SkillId.QianNvYouHun, new QianNvYouHunSkill());
            All.Add(SkillId.XueShaZhiGu, new XueShaZhiGuSkill());
            All.Add(SkillId.XiXingDaFa, new XiXingDaFaSkill());
            All.Add(SkillId.LuoRiRongJin, new LuoRiRongJinSkill());
            All.Add(SkillId.XueHaiShenChou, new XueHaiShenChouSkill());
            All.Add(SkillId.ShiXinFeng, new ShiXinFengSkill());
            All.Add(SkillId.MengPoTang, new MengPoTangSkill());
            // 龙族
            All.Add(SkillId.LingXuYuFeng, new LingXuYuFengSkill());
            All.Add(SkillId.FeiJuJiuTian, new FeiJuJiuTianSkill());
            All.Add(SkillId.FengLeiWanYun, new FengLeiWanYunSkill());
            All.Add(SkillId.ZhenTianDongDi, new ZhenTianDongDiSkill());
            All.Add(SkillId.PeiRanMoYu, new PeiRanMoYuSkill());
            All.Add(SkillId.ZeBeiWanWu, new ZeBeiWanWuSkill());
            All.Add(SkillId.BaiLangTaoTian, new BaiLangTaoTianSkill());
            All.Add(SkillId.CangHaiHengLiu, new CangHaiHengLiuSkill());
            All.Add(SkillId.NiLin, new NiLinSkill());

            All.Add(SkillId.HuiHunZhiShu, new HuiHunZhiShuSkill());
            All.Add(SkillId.TianHuoJiangShi, new TianHuoJiangShiSkill());
            All.Add(SkillId.WanQianHuaShen, new WanQianHuaShenSkill());

            // 召唤兽-普通技能
            All.Add(SkillId.FengYin, new FengYinSkill());
            All.Add(SkillId.YuanQuanWanHu, new YuanQuanWanHuSkill());
            All.Add(SkillId.QingMianLiaoYa, new QingMianLiaoYaSkill());
            All.Add(SkillId.PanShan, new PanShanSkill());
            // All.Add(SkillId.QingFengFuMian, new QingFengFuMianSkill());

            All.Add(SkillId.YiChan, new YiChanSkill());
            All.Add(SkillId.XianFengDaoGu, new XianFengDaoGuSkill());

            All.Add(SkillId.ZhangYinDongDu, new ZhangYinDongDuSkill());
            All.Add(SkillId.HighZhangYinDongDu, new HighZhangYinDongDuSkill());
            All.Add(SkillId.SuperZhangYinDongDu, new SuperZhangYinDongDuSkill());

            All.Add(SkillId.HighYuanQuanWanHu, new HighYuanQuanWanHuSkill());
            All.Add(SkillId.SuperYuanQuanWanHu, new SuperYuanQuanWanHuSkill());
            All.Add(SkillId.ShenGongGuiLi, new ShenGongGuiLiSkill());
            All.Add(SkillId.HighShenGongGuiLi, new HighShenGongGuiLiSkill());
            All.Add(SkillId.SuperShenGongGuiLi, new SuperShenGongGuiLiSkill());
            All.Add(SkillId.BeiDaoJianXing, new BeiDaoJianXingSkill());
            All.Add(SkillId.HighBeiDaoJianXing, new HighBeiDaoJianXingSkill());
            All.Add(SkillId.SuperBeiDaoJianXing, new SuperBeiDaoJianXingSkill());

            All.Add(SkillId.HighPanShan, new HighPanShanSkill());
            All.Add(SkillId.GongXingTianFa, new GongXingTianFaSkill());
            All.Add(SkillId.TianGangZhanQi, new TianGangZhanQiSkill());
            All.Add(SkillId.XuanRen, new XuanRenSkill());
            All.Add(SkillId.YiHuan, new YiHuanSkill());
            All.Add(SkillId.ShanXian, new ShanXianSkill());
            All.Add(SkillId.HighShanXian, new HighShanXianSkill());
            All.Add(SkillId.SuperShanXian, new SuperShanXianSkill());
            All.Add(SkillId.YinShen, new YinShenSkill());
            All.Add(SkillId.ChuiJinZhuanYu, new ChuiJinZhuanYuSkill());
            All.Add(SkillId.KuMuFengChun, new KuMuFengChunSkill());
            All.Add(SkillId.XiTianJingTu, new XiTianJingTuSkill());
            All.Add(SkillId.RuRenYinShui, new RuRenYinShuiSkill());
            All.Add(SkillId.FengHuoLiaoYuan, new FengHuoLiaoYuanSkill());
            All.Add(SkillId.MiaoShouHuiChun, new MiaoShouHuiChunSkill());
            All.Add(SkillId.FenHuaFuLiu, new FenHuaFuLiuSkill());
            All.Add(SkillId.JiRenTianXiang, new JiRenTianXiangSkill());
            All.Add(SkillId.FuLuShuangQuan, new FuLuShuangQuanSkill());
            All.Add(SkillId.SheShengQuYi, new SheShengQuYiSkill());
            All.Add(SkillId.TaoMing, new TaoMingSkill());
            All.Add(SkillId.BaoFu, new BaoFuSkill());
            All.Add(SkillId.FenLieGongJi, new FenLieGongJiSkill());
            All.Add(SkillId.HighFenLieGongJi, new HighFenLieGongJiSkill());
            All.Add(SkillId.TianMoJieTi, new TianMoJieTiSkill());
            All.Add(SkillId.HighTianMoJieTi, new HighTianMoJieTiSkill());
            All.Add(SkillId.FenGuangHuaYing, new FenGuangHuaYingSkill());
            All.Add(SkillId.HighFenGuangHuaYing, new HighFenGuangHuaYingSkill());

            All.Add(SkillId.HighQingMianLiaoYa, new HighQingMianLiaoYaSkill());
            All.Add(SkillId.XiaoLouYeKu, new XiaoLouYeKuSkill());
            All.Add(SkillId.HighXiaoLouYeKu, new HighXiaoLouYeKuSkill());
            All.Add(SkillId.GeShanDaNiu, new GeShanDaNiuSkill());
            All.Add(SkillId.HighGeShanDaNiu, new HighGeShanDaNiuSkill());
            All.Add(SkillId.JiQiBuYi, new JiQiBuYiSkill());
            All.Add(SkillId.PoYin, new PoYinSkill());
            All.Add(SkillId.HunLuan, new HunLuanSkill());

            All.Add(SkillId.WuSeWuXiang, new WuSeWuXiangSkill());
            All.Add(SkillId.ZePiTianXia, new ZePiTianXiaSkill());
            All.Add(SkillId.LiSheDaChuan, new LiSheDaChuanSkill());
            All.Add(SkillId.PeiYuGanLin, new PeiYuGanLinSkill());
            All.Add(SkillId.YouLianMeiYin, new YouLianMeiYinSkill());
            All.Add(SkillId.YiHuaJieMu, new YiHuaJieMuSkill());
            All.Add(SkillId.LiuShiChiLie, new LiuShiZhiLieSkill());
            All.Add(SkillId.AnZhiJiangLin, new AnZhiJiangLinSkill());
            All.Add(SkillId.FeiZhuJianYu, new FeiZhuJianYuSkill());
            All.Add(SkillId.HenYuFeiFei, new HenYuFeiFeiSkill());
            All.Add(SkillId.LuoZhiYunYan, new LuoZhiYunYanSkill());
            All.Add(SkillId.NvWaZhouNian, new NvWaZhouNianSkill());
            All.Add(SkillId.NiuZhuanQianKun, new NiuZhuanQianKunSkill());
            All.Add(SkillId.TianJiangTuoTu, new TianJiangTuoTuSkill());
            All.Add(SkillId.PiCaoRouHou, new PiCaoRouHouSkill());
            All.Add(SkillId.AnXingJiDou, new AnXingJiDouSkill());
            All.Add(SkillId.HuanYingLiHun, new HuanYingLiHunSkill());
            All.Add(SkillId.FaTianXiangDi, new FaTianXiangDiSkill());
            All.Add(SkillId.TuMiHuaKai, new TuMiHuaKaiSkill());

            // 四圣兽技能
            All.Add(SkillId.FeiLongZaiTian, new FeiLongZaiTianSkill());
            All.Add(SkillId.FeiLongZaiTianFeng, new FeiLongZaiTianFengSkill());
            All.Add(SkillId.FeiLongZaiTianHuo, new FeiLongZaiTianHuoSkill());
            All.Add(SkillId.FeiLongZaiTianShui, new FeiLongZaiTianShuiSkill());
            All.Add(SkillId.FeiLongZaiTianLei, new FeiLongZaiTianLeiSkill());

            // 金翅大鹏
            All.Add(SkillId.FengJuanCanYunMain, new FengJuanCanYunMainSkill());
            All.Add(SkillId.FengJuanCanYunLeft, new FengJuanCanYunLeftSkill());
            All.Add(SkillId.FengJuanCanYunRight, new FengJuanCanYunRightSkill());
            All.Add(SkillId.ZhuTianMieDi, new ZhuTianMieDiSkill());

            All.Add(SkillId.YouFengLaiYi, new YouFengLaiYiSkill());
            // All.Add(SkillId.YouFengLaiYiJin, new YouFengLaiYiJinSkill());
            // All.Add(SkillId.YouFengLaiYiMu, new YouFengLaiYiMuSkill());
            // All.Add(SkillId.YouFengLaiYiShui, new YouFengLaiYiShuiSkill());
            // All.Add(SkillId.YouFengLaiYiHuo, new YouFengLaiYiHuoSkill());
            // All.Add(SkillId.YouFengLaiYiTu, new YouFengLaiYiTuSkill());

            // 召唤兽-终极技能
            All.Add(SkillId.ZiXuWuYou, new ZiXuWuYouSkill());
            All.Add(SkillId.HuaWu, new HuaWuSkill());
            All.Add(SkillId.JueJingFengSheng, new JueJingFengShengSkill());
            All.Add(SkillId.ZuoNiaoShouSan, new ZuoNiaoShouSanSkill());
            All.Add(SkillId.ShuangGuanQiXia, new ShuangGuanQiXiaSkill());
            All.Add(SkillId.JiangSi, new JiangSiSkill());
            All.Add(SkillId.ChunHuiDaDi, new ChunHuiDaDiSkill());
            All.Add(SkillId.DangTouBangHe, new DangTouBangHeSkill());
            All.Add(SkillId.MingChaQiuHao, new MingChaQiuHaoSkill());

            // 神兽技能
            All.Add(SkillId.BingLinChengXia, new BingLinChengXiaSkill());
            All.Add(SkillId.NiePan, new NiePanSkill());
            All.Add(SkillId.QiangHuaXuanRen, new QiangHuaXuanRenSkill());
            All.Add(SkillId.QiangHuaYiHuan, new QiangHuaYiHuanSkill());
            All.Add(SkillId.ChaoMingDianChe, new ChaoMingDianCheSkill());
            All.Add(SkillId.RuHuTianYi, new RuHuTianYiSkill());

            // 特殊技能
            All.Add(SkillId.StealMoney, new StealMoneySkill());
        }

        public static BaseSkill GetSkill(SkillId id)
        {
            return All.GetValueOrDefault(id, null);
        }

        public static T GetSkill<T>(SkillId id) where T : BaseSkill
        {
            All.TryGetValue(id, out var skill);
            return skill as T;
        }

        public static bool IsAtkSkill(SkillId id)
        {
            All.TryGetValue(id, out var skill);
            if (skill != null)
            {
                if (skill.ActionType == SkillActionType.Passive) return false;
                return AtkSkills.ContainsKey(skill.Type);
            }

            return false;
        }

        public static bool IsPassiveSkill(SkillId id)
        {
            if (!All.TryGetValue(id, out var skill)) return false;
            return skill.ActionType == SkillActionType.Passive;
        }

        public static bool IsSelfBuffSkill(SkillId id)
        {
            All.TryGetValue(id, out var skill);
            if (skill != null)
            {
                if (skill.ActionType == SkillActionType.Passive) return false;
                return skill.TargetType == SkillTargetType.Self;
            }

            return false;
        }

        public static bool IsDebuffSkill(SkillId id)
        {
            All.TryGetValue(id, out var skill);
            if (skill != null)
            {
                if (skill.ActionType == SkillActionType.Passive) return false;
                return DebuffSkills.ContainsKey(skill.Type);
            }

            return false;
        }

        // 是否为控制技能
        public static bool IsKongZhiSkill(SkillId id)
        {
            return KongZhiSkills.ContainsKey(id);
        }

        // 是否为加防/加攻/加速技能
        public static bool IsFGSSkill(SkillId id)
        {
            return FGSSkills.ContainsKey(id);
        }

        public static bool IsWuXingSkill(SkillId id)
        {
            return WuXingSkills.ContainsKey(id);
        }

        public static bool IsForceMpSkills(SkillId id)
        {
            return ForceMpSkills.ContainsKey(id);
        }

        // 是否为闪现
        public static bool IsShanXianSkill(SkillId id)
        {
            return id is SkillId.ShanXian or SkillId.HighShanXian or SkillId.SuperShanXian;
        }

        // 检查技能是否可以闪避
        public static bool CanShanBi(SkillId id)
        {
            return id == SkillId.NormalAtk
            // 龙族
            || id == SkillId.LingXuYuFeng
            || id == SkillId.FeiJuJiuTian
            || id == SkillId.FengLeiWanYun
            || id == SkillId.ZhenTianDongDi
            || id == SkillId.BaiLangTaoTian
            || id == SkillId.CangHaiHengLiu;
        }

        public static bool IsXianFa(SkillType type)
        {
            return type is SkillType.Feng or SkillType.Huo or SkillType.Shui or SkillType.Lei;
        }

        // 检查技能是否已实现
        public static bool IsValid(SkillId id)
        {
            return All.ContainsKey(id);
        }

        public static bool IsPvp(BattleType type)
        {
            return type != BattleType.Normal
                && type != BattleType.DiShaXing
                && type != BattleType.KuLouWang
                && type != BattleType.JinChanSongBao
                && type != BattleType.Cszl
                && type != BattleType.Eagle
                && type != BattleType.TianJiangLingHou
                && type != BattleType.ShenShouJiangLin;
        }
    }
}