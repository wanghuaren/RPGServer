using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ddxy.Common.Model;
using Ddxy.Common.Orleans;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Data.Fields;
using Ddxy.GameServer.Data.Vo;
using Ddxy.GameServer.Logic;
using Ddxy.GameServer.Logic.Battle.Skill;
using Ddxy.GameServer.Logic.Equip;
using Ddxy.GameServer.Logic.Mail;
using Ddxy.GameServer.Logic.Mount;
using Ddxy.GameServer.Logic.Partner;
using Ddxy.GameServer.Logic.Pet;
using Ddxy.GameServer.Logic.Scheme;
using Ddxy.GameServer.Logic.Title;
using Ddxy.GameServer.Logic.XTask;
using Ddxy.GameServer.Option;
using Ddxy.GameServer.Util;
using Ddxy.GrainInterfaces;
using Ddxy.GrainInterfaces.Core;
using Ddxy.Protocol;
using FreeSql;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;
using Orleans.Runtime;
using System.Text.RegularExpressions;
namespace Ddxy.GameServer.Grains
{
    class TianyanceLevelupCost
    {
        public uint jade { get; set; }
        public uint bindJade { get; set; }
        public List<TianceLevelItem> item { get; set; }
    }
    public partial class PlayerGrain
    {
        /// <summary>
        /// 接收来自网关转发的客户端数据包
        /// </summary>
        public async Task HandlePacket(ushort command, byte[] payload)
        {
            var cmd = (GameCmd)command;

            try
            {
                if (!IsActive)
                {
                    LogDebug("尚未激活");
                    if (_packet != null)
                    {
                        await _packet.SendStatus(RoleId, WebSocketCloseStatus.NormalClosure, false);
                    }
                    return;
                }
                if (!IsOnline)
                {
                    LogDebug("尚未连接");
                    await _packet.SendStatus(RoleId, WebSocketCloseStatus.NormalClosure, false);
                    return;
                }

                // 心跳
                if (cmd == GameCmd.C2SHeartBeat)
                {
                    var req = C2S_HeartBeat.Parser.ParseFrom(payload);
                    await ReqHeartBeat(req);
                    return;
                }

                // 进入区服
                if (cmd == GameCmd.C2SEnterServer)
                {
                    // 不能重复进入
                    if (IsEnterServer && !IsEnterBackGround)
                    {
                        LogInformation("已经进入游戏");
                        return;
                    }

                    var req = C2S_EnterServer.Parser.ParseFrom(payload);
                    await ReqEnterServer(req);
                    return;
                }

                // 下面的协议都需要验证是否已经EnterServer
                if (!IsEnterServer)
                {
                    LogInformation("尚未进入游戏");
                    return;
                }

                switch (cmd)
                {
                    case GameCmd.C2SAppPause:
                    {
                        var req = C2S_AppPause.Parser.ParseFrom(payload);
                        await OnApplicationPause(req.Pause);
                        break;
                    }
                    case GameCmd.C2SPrisonFree:
                    {
                        await ReqPrisonFree();
                        break;
                    }
                    case GameCmd.C2SShanXianOrder:
                    {
                        var req = C2S_ShanXianOrder.Parser.ParseFrom(payload);
                        await ReqEnableShanXianOrder(req.Enable);
                        break;
                    }
                    // case GameCmd.C2SLiBao:
                    // {
                    //     await ReqLiBao();
                    //     break;
                    // }
                    case GameCmd.C2SGetWuJiaTao:
                    {
                        var req = C2S_GetWuJiaTao.Parser.ParseFrom(payload);
                        await ReqGetWuJiaTao(req.SuitId);
                        break;
                    }
                    case GameCmd.C2SGetWuJiaBuJian:
                    {
                        var req = C2S_GetWuJiaBuJian.Parser.ParseFrom(payload);
                        await ReqGetWuJiaBuJian(req.SuitId, req.Index);
                        break;
                    }
                    case GameCmd.C2SGetJiNengShu:
                    {
                        var req = C2S_GetJiNengShu.Parser.ParseFrom(payload);
                        await ReqGetJiNengShu(req.CfgId);
                        break;
                    }
                    case GameCmd.C2SGetWuXingCaiLiao:
                    {
                        var req = C2S_GetWuXingCaiLiao.Parser.ParseFrom(payload);
                        await ReqGetWuXingCaiLiao(req.CfgId);
                        break;
                    }
                    case GameCmd.C2SSkillUpdate:
                    {
                        var req = C2S_SkillUpdate.Parser.ParseFrom(payload);
                        await ReqSkillUp(req);
                        break;
                    }
                    case GameCmd.C2SChangeName:
                    {
                        var req = C2S_ChangeName.Parser.ParseFrom(payload);
                        await ReqChangeName(req);
                        break;
                    }
                    case GameCmd.C2SChangeRace:
                    {
                        var req = C2S_ChangeRace.Parser.ParseFrom(payload);
                        await ReqChangeRace(req.CfgId);
                        break;
                    }
                    case GameCmd.C2SRelive:
                    {
                        var req = C2S_Relive.Parser.ParseFrom(payload);
                        await ReqRelive(req.CfgId);
                        break;
                    }
                    case GameCmd.C2SChangeColor:
                    {
                        var req = C2S_ChangeColor.Parser.ParseFrom(payload);
                        await ReqChangeColor(req.Index1, req.Index2);
                        break;
                    }
                    case GameCmd.C2SLevelRewardGet:
                    {
                        var req = C2S_LevelRewardGet.Parser.ParseFrom(payload);
                        await GetLevelReward((byte)req.Level);
                        break;
                    }
                    case GameCmd.C2SXinShouGiftGet:
                    {
                        await GetXinShouGift();
                        break;
                    }
                    case GameCmd.C2SXinShouGiftCheck:
                    {
                        await CheckXinShouGift();
                        break;
                    }
                    case GameCmd.C2SUpgradeXlLevel:
                    {
                        var req = C2S_UpgradeXlLevel.Parser.ParseFrom(payload);
                        await UpgradeXlLevel(req.Add);
                        break;
                    }
                    case GameCmd.C2SSafePassword:
                    {
                        var req = C2S_SafePassword.Parser.ParseFrom(payload);
                        await ReqSafePassword(req.Password, req.OldPassword);
                        break;
                    }
                    case GameCmd.C2SSafeLock:
                    {
                        var req = C2S_SafeLock.Parser.ParseFrom(payload);
                        await ReqSafeLock(req.Lock, req.Password);
                        break;
                    }
                    case GameCmd.C2SBindSpread:
                    {
                        var req = C2S_BindSpread.Parser.ParseFrom(payload);
                        await ReqBindSpread(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SSpreads:
                    {
                        var req = C2S_Spreads.Parser.ParseFrom(payload);
                        await ReqSpreads(req);
                        break;
                    }
                    case GameCmd.C2SOrder:
                    {
                        var req = C2S_Order.Parser.ParseFrom(payload);
                        await ReqOrder(req);
                        break;
                    }
                    // 进入充值页
                    case GameCmd.C2SEnterChargeUi:
                    {
                        var req = C2S_EnterChargeUI.Parser.ParseFrom(payload);
                        await ReqEnterChargeUI();
                        break;
                    }
                    // 物品商店--进入
                    case GameCmd.C2SEnterItemShop:
                    {
                        // var req = C2S_EnterItemShop.Parser.ParseFrom(payload);
                        await ReqEnterItemShop();
                        break;
                    }
                    // 物品商店--下单购买
                    case GameCmd.C2SOrderItemShop:
                    {
                        var req = C2S_OrderItemShop.Parser.ParseFrom(payload);
                        await ReqOrderItemShop(req.Id, (PayType)req.PayType, req.Num);
                        break;
                    }
                    case GameCmd.C2SAutoSkill:
                    {
                        var req = C2S_AutoSkill.Parser.ParseFrom(payload);
                        await ReqAutoSkill(req);
                        break;
                    }
                    case GameCmd.C2SIncenseStop:
                    {
                        await StopIncense();
                        break;
                    }
                    case GameCmd.C2SItemDel:
                    {
                        var req = C2S_ItemDel.Parser.ParseFrom(payload);
                        await ReqDelItem(req.List);
                        break;
                    }
                    case GameCmd.C2SItemUse:
                    {
                        var req = C2S_ItemUse.Parser.ParseFrom(payload);
                        await ReqUseItem(req.Id, req.Num, req.Target);
                        break;
                    }
                    case GameCmd.C2SItemCompose:
                    {
                        var req = C2S_ItemCompose.Parser.ParseFrom(payload);
                        await ReqComposeItem(req.ParentId, req.Num);
                        break;
                    }
                    case GameCmd.C2SItemFusion:
                    {
                        var req = C2S_ItemFusion.Parser.ParseFrom(payload);
                        await ReqFusionWuXing(req.Items);
                        break;
                    }
                    case GameCmd.C2SRepoUpdate:
                    {
                        var req = C2S_RepoUpdate.Parser.ParseFrom(payload);
                        await ReqUpdateRepo(req.IsAdd, req.Type, req.Target);
                        break;
                    }
                    case GameCmd.C2SLotteryOpen:
                    {
                        var req = C2S_LotteryOpen.Parser.ParseFrom(payload);
                        await ReqLotteryOpen(req.CfgId);
                        break;
                    }
                    case GameCmd.C2SRolePays:
                    {
                        await ReqRolePays();
                        break;
                    }
                    case GameCmd.C2STotalPayRewards:
                    {
                        var req = C2S_TotalPayRewards.Parser.ParseFrom(payload);
                        await ReqGetTotalPayRewards(req.Money);
                        break;
                    }
                    case GameCmd.C2SEwaiPayRewards:
                    {
                        var req = C2S_EwaiPayRewards.Parser.ParseFrom(payload);
                        await ReqGetEwaiPayRewards(req.Money);
                        break;
                    }
                    case GameCmd.C2SDailyPayRewards:
                    {
                        var req = C2S_DailyPayRewards.Parser.ParseFrom(payload);
                        await ReqGetDailyPayRewards(req.Money);
                        break;
                    }
                    case GameCmd.C2SDailyPayRewardsReset:
                    {
                        await ReqResetDailyPayRewards();
                        break;
                    }
                    case GameCmd.C2STotalPayRewardsReset:
                    {
                        await ReqResetTotalPayRewards();
                        break;
                    }
                    case GameCmd.C2SEwaiPayRewardsReset:
                    {
                        await ReqResetEwaiPayRewards();
                        break;
                    }
                    case GameCmd.C2SFirstPayReward:
                    {
                        await ReqGetFirstPayRewards();
                        break;
                    }
                    case GameCmd.C2SRecover:
                    {
                        var req = C2S_Recover.Parser.ParseFrom(payload);
                        await ReqRecover(req);
                        break;
                    }
                    case GameCmd.C2SEquipUpgrade:
                    {
                        var req = C2S_EquipUpgrade.Parser.ParseFrom(payload);
                        await ReqEquipUpgrade(req.Id, req.Use1, req.Use2);
                        break;
                    }
                    case GameCmd.C2SEquipInlay:
                    {
                        var req = C2S_EquipInlay.Parser.ParseFrom(payload);
                        await ReqEquipInlay(req.Id, req.Add);
                        break;
                    }
                    case GameCmd.C2SEquipRefine:
                    {
                        var req = C2S_EquipRefine.Parser.ParseFrom(payload);
                        await ReqEquipRefine(req.Id, req.Level, req.Opera);
                        break;
                    }
                    case GameCmd.C2SEquipRefineTimes:
                    {
                        var req = C2S_EquipRefineTimes.Parser.ParseFrom(payload);
                        await ReqEquipRefineTimes(req.Id, req.Level, req.Opera, req.Times, req.ChoiceIndex);
                        break;
                    }
                    case GameCmd.C2SEquipDingZhi:
                    {
                        var req = C2S_EquipDingZhi.Parser.ParseFrom(payload);
                        await ReqEquipDingZhi(req.Id, req.Attrs.ToList());
                        break;
                    }
                    case GameCmd.C2SOrnamentDingZhi:
                    {
                        var req = C2S_OrnamentDingZhi.Parser.ParseFrom(payload);
                        await ReqOrnamentDingZhi(req.Id, req.Attrs.ToList());
                        break;
                    }
                    // 装备升星
                    case GameCmd.C2SEquipStarUpgrade:
                    {
                        var req = C2S_EquipStarUpgrade.Parser.ParseFrom(payload);
                        await ReqEquipStarUpgrade(req.Id, req.UseItemId);
                        break;
                    }
                    // 装备升阶
                    case GameCmd.C2SEquipGradeUpgrade:
                    {
                        var req = C2S_EquipGradeUpgrade.Parser.ParseFrom(payload);
                        await ReqEquipGradeUpgrade(req.Id);
                        break;
                    }
                    case GameCmd.C2SEquipRecast:
                    {
                        var req = C2S_EquipRecast.Parser.ParseFrom(payload);
                        await ReqEquipRecast(req.Id, req.Opera);
                        break;
                    }
                    case GameCmd.C2SEquipDelete:
                    {
                        var req = C2S_EquipDelete.Parser.ParseFrom(payload);
                        await ReqEquipDelete(req.Id);
                        break;
                    }
                    case GameCmd.C2SEquipCombine:
                    {
                        var req = C2S_EquipCombine.Parser.ParseFrom(payload);
                        await ReqEquipCombine(req.Category, req.Index);
                        break;
                    }
                    case GameCmd.C2SEquipProperty:
                    {
                        var req = C2S_EquipProperty.Parser.ParseFrom(payload);
                        await ReqEquipProperty(req.Id, req.Flag);
                        break;
                    }
                    case GameCmd.C2SEquipPropertyList:
                    {
                        var req = C2S_EquipPropertyList.Parser.ParseFrom(payload);
                        await ReqEquipPropertyList(req.Id, req.Flag);
                        break;
                    }
                    case GameCmd.C2SShareEquipInfo:
                    {
                        var req = C2S_ShareEquipInfo.Parser.ParseFrom(payload);
                        await ReqGetEquipShareInfo(req.Id);
                        break;
                    }
                    case GameCmd.C2SOrnamentDel:
                    {
                        var req = C2S_OrnamentDel.Parser.ParseFrom(payload);
                        await EquipMgr.DelOrnament(req.Id);
                        break;
                    }
                    case GameCmd.C2SOrnamentProperty:
                    {
                        var req = C2S_OrnamentProperty.Parser.ParseFrom(payload);
                        await EquipMgr.SendOrnamentProperty(req.Id);
                        break;
                    }
                    case GameCmd.C2SOrnamentRecast:
                    {
                        var req = C2S_OrnamentRecast.Parser.ParseFrom(payload);
                        await EquipMgr.RecastOrnament(req.Id, req.Opera, req.Locks.ToList());
                        break;
                    }
                    case GameCmd.C2SOrnamentDecompose:
                    {
                        var req = C2S_OrnamentDecompose.Parser.ParseFrom(payload);
                        await EquipMgr.DecomposeOrnament(req.Id);
                        break;
                    }
                    case GameCmd.C2SOrnamentAppraisal:
                    {
                        var req = C2S_OrnamentAppraisal.Parser.ParseFrom(payload);
                        await EquipMgr.AppraisalOrnament(req.ItemId);
                        break;
                    }
                    case GameCmd.C2SShareOrnamentInfo:
                    {
                        var req = C2S_ShareOrnamentInfo.Parser.ParseFrom(payload);
                        await EquipMgr.GetShareOrnamentInfo(req.Id);
                        break;
                    }
                    // 宠物配饰--分解
                    case GameCmd.C2SPetOrnamentFenJie:
                    {
                        var req = C2S_PetOrnamentFenJie.Parser.ParseFrom(payload);
                        await EquipMgr.PetOrnamentFenJie(req.List.ToList());
                        break;
                    }
                    // 宠物配饰--打造
                    case GameCmd.C2SPetOrnamentDaZhao:
                    {
                        var req = C2S_PetOrnamentDaZhao.Parser.ParseFrom(payload);
                        await EquipMgr.PetOrnamentDaZhao(req.ItemId);
                        break;
                    }
                    // 宠物配饰--装备
                    case GameCmd.C2SPetOrnamentEquip:
                    {
                        var req = C2S_PetOrnamentEquip.Parser.ParseFrom(payload);
                        await EquipMgr.PetOrnamentEquip(req.Id, req.PetId);
                        break;
                    }
                    // 宠物配饰--卸载
                    case GameCmd.C2SPetOrnamentUnEquip:
                    {
                        var req = C2S_PetOrnamentUnEquip.Parser.ParseFrom(payload);
                        await EquipMgr.PetOrnamentUnEquip(req.Id);
                        break;
                    }
                    // 宠物配饰--锁定
                    case GameCmd.C2SPetOrnamentLock:
                    {
                        var req = C2S_PetOrnamentLock.Parser.ParseFrom(payload);
                        await EquipMgr.PetOrnamentLock(req.Id);
                        break;
                    }
                    case GameCmd.C2SGetOperateTimes:
                    {
                        var req = C2S_GetOperateTimes.Parser.ParseFrom(payload);
                        await EquipMgr.GetOperateTimes(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetAdopt:
                    {
                        var req = C2S_PetAdopt.Parser.ParseFrom(payload);
                        await ReqPetAdopt(req);
                        break;
                    }
                    case GameCmd.C2SPetDel:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetDel.Parser.ParseFrom(payload);
                        await PetMgr.DelPet(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetActive:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetActive.Parser.ParseFrom(payload);
                        await PetMgr.ActivePet(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetWash:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetWash.Parser.ParseFrom(payload);
                        await PetMgr.WashPet(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetSaveWash:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetSaveWash.Parser.ParseFrom(payload);
                        await PetMgr.SaveWash(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetRelive:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetSaveWash.Parser.ParseFrom(payload);
                        await PetMgr.RelivePet(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetCombine:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetCombine.Parser.ParseFrom(payload);
                        await PetMgr.CombinePet(req.CfgId);
                        break;
                    }
                    case GameCmd.C2SPetUnlock:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetUnlock.Parser.ParseFrom(payload);
                        await PetMgr.UnlockSkill(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetLearnSkill:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetLearnSkill.Parser.ParseFrom(payload);
                        await PetMgr.LearnSkill(req.Id, req.Index, req.ItemId);
                        break;
                    }
                    case GameCmd.C2SPetForgetSkill:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetForgetSkill.Parser.ParseFrom(payload);
                        await PetMgr.ForgetSkill(req.Id, req.SkId);
                        break;
                    }
                    case GameCmd.C2SPetLockSkill:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetLockSkill.Parser.ParseFrom(payload);
                        await PetMgr.LockSkill(req.Id, req.SkId, req.Lock);
                        break;
                    }
                    case GameCmd.C2SPetChangeSsSkill:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetChangeSsSkill.Parser.ParseFrom(payload);
                        await PetMgr.ChangeSsSkill(req.Id, req.SkId);
                        break;
                    }
                    case GameCmd.C2SPetFly:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetFly.Parser.ParseFrom(payload);
                        await PetMgr.Fly(req.Id, req.Type);
                        break;
                    }
                    case GameCmd.C2SPetAddPoint:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetRefine.Parser.ParseFrom(payload);
                        var pet = PetMgr.All.FirstOrDefault(p => p.Id == req.Id);
                        // ReSharper disable once PossibleNullReferenceException
                        if (pet != null) await pet.AddPoint(req.Reset, req.Attrs);
                        break;
                    }
                    case GameCmd.C2SPetRefine:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetRefine.Parser.ParseFrom(payload);
                        var pet = PetMgr.All.FirstOrDefault(p => p.Id == req.Id);
                        // ReSharper disable once PossibleNullReferenceException
                        if (pet != null) await pet.Refine(req.Reset, req.Attrs);
                        break;
                    }
                    case GameCmd.C2SPetAutoSkill:
                    {
                        var req = C2S_PetAutoSkill.Parser.ParseFrom(payload);
                        var pet = PetMgr.All.FirstOrDefault(p => p.Id == req.Id);
                        // ReSharper disable once PossibleNullReferenceException
                        if (pet != null) await pet.SetAutoSkill(req.Skill);
                        break;
                    }
                    // 觉醒突破
                    case GameCmd.C2SPetJxTuPo:
                    {  
                        var req = C2S_PetJxTuPo.Parser.ParseFrom(payload);
                        var pet = PetMgr.All.FirstOrDefault(p => p.Id == req.Id);
                        if (pet != null) await pet.JxSkillTuPo(req.Must);
                        break;
                    }
                    case GameCmd.C2SSharePetInfo:
                    {
                        var req = C2S_SharePetInfo.Parser.ParseFrom(payload);
                        await ReqGetPetShareInfo(req.Id);
                        break;
                    }
                    // 分享宠物配饰
                    case GameCmd.C2SSharePetOrnamentInfo:
                    {
                        var req = C2S_SharePetOrnamentInfo.Parser.ParseFrom(payload);
                        await ReqGetPetOrnamentShareInfo(req.Id);
                        break;
                    }
                    case GameCmd.C2SPetName:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetName.Parser.ParseFrom(payload);
                        var pet = PetMgr.All.FirstOrDefault(p => p.Id == req.Id);
                        // ReSharper disable once PossibleNullReferenceException
                        if (pet != null) await pet.ChangeName(req.Name);
                        break;
                    }
                    case GameCmd.C2SPetChangeShanXianOrder:
                    {
                        if (CheckSafeLocked()) return;
                        var req = C2S_PetChangeShanXianOrder.Parser.ParseFrom(payload);
                        await PetMgr.ChangeShanXianOrder(req.Id, req.Order);
                        break;
                    }
                    case GameCmd.C2SPetWashPreview:
                    {
                        var req = C2S_PetWashPreview.Parser.ParseFrom(payload);
                        var pet = PetMgr.All.FirstOrDefault(p => p.Id == req.Id);
                        // ReSharper disable once PossibleNullReferenceException
                        if (pet != null) await pet.SendWashPreview();
                        break;
                    }
                    case GameCmd.C2SMountActive:
                    {
                        var req = C2S_MountActive.Parser.ParseFrom(payload);
                        await MountMgr.SetActive(req.Id, req.Active);
                        break;
                    }
                    case GameCmd.C2SMountUnlock:
                    {
                        var req = C2S_MountUnlock.Parser.ParseFrom(payload);
                        await MountMgr.Unlock(req.Id);
                        break;
                    }
                    // 坐骑定制
                    case GameCmd.C2SMountDingZhi:
                    {
                        var req = C2S_MountDingZhi.Parser.ParseFrom(payload);
                        await MountMgr.DingZhi(req.Id, req.Skills.ToList());
                        break;
                    }
                    case GameCmd.C2SMountWash:
                    {
                        var req = C2S_MountWash.Parser.ParseFrom(payload);
                        await MountMgr.WashMount(req.Id);
                        break;
                    }
                    case GameCmd.C2SMountSaveWash:
                    {
                        var req = C2S_MountSaveWash.Parser.ParseFrom(payload);
                        await MountMgr.SaveWash(req.Id);
                        break;
                    }
                    case GameCmd.C2SMountControl:
                    {
                        var req = C2S_MountControl.Parser.ParseFrom(payload);
                        await MountMgr.ControlPet(req.Id, req.PetId, req.Add);
                        break;
                    }
                    case GameCmd.C2SMountUpgradeSkill:
                    {
                        var req = C2S_MountUpgradeSkill.Parser.ParseFrom(payload);
                        await MountMgr.UpgradeSkill(req.Id, req.Grid);
                        break;
                    }
                    case GameCmd.C2SMountWashPreview:
                    {
                        var req = C2S_MountWashPreview.Parser.ParseFrom(payload);
                        var mount = MountMgr.All.FirstOrDefault(p => p.Id == req.Id);
                        // ReSharper disable once PossibleNullReferenceException
                        if (mount != null) await mount.SendWashPreview();
                        break;
                    }
                    case GameCmd.C2SSchemeActive:
                    {
                        var req = C2S_SchemeActive.Parser.ParseFrom(payload);
                        await SchemeMgr.ActiveScheme(req.Id);
                        break;
                    }
                    case GameCmd.C2SSchemeAddPoint:
                    {
                        var req = C2S_SchemeAddPoint.Parser.ParseFrom(payload);
                        await SchemeMgr.Scheme.AddPoint(req.Attrs);
                        break;
                    }
                    case GameCmd.C2SSchemeResetAddPoint:
                    {
                        await SchemeMgr.Scheme.ResetApAttrs();
                        break;
                    }
                    case GameCmd.C2SSchemeXiuLian:
                    {
                        var req = C2S_SchemeXiuLian.Parser.ParseFrom(payload);
                        await SchemeMgr.Scheme.AddXlPoint(req.Attrs);
                        break;
                    }
                    case GameCmd.C2SSchemeResetXlPoint:
                    {
                        await SchemeMgr.Scheme.ResetXlPoint();
                        break;
                    }
                    case GameCmd.C2SSchemeEquip:
                    {
                        var req = C2S_SchemeEquip.Parser.ParseFrom(payload);
                        await SchemeMgr.Scheme.SetEquip(req.Equip, (int)req.Pos);
                        break;
                    }
                    case GameCmd.C2SSchemeOrnament:
                    {
                        var req = C2S_SchemeOrnament.Parser.ParseFrom(payload);
                        await SchemeMgr.Scheme.SetOrnament(req.Ornament, (int)req.Pos);
                        break;
                    }
                    case GameCmd.C2SSchemeName:
                    {
                        var req = C2S_SchemeName.Parser.ParseFrom(payload);
                        await SchemeMgr.SetSchemeName(req.Id, req.Name);
                        break;
                    }
                    case GameCmd.C2SSchemeResetFix:
                    {
                        var req = C2S_SchemeResetFix.Parser.ParseFrom(payload);
                        await SchemeMgr.Scheme.ResetFix(req.List);
                        break;
                    }
                    case GameCmd.C2SPartnerPos:
                    {
                        var req = C2S_PartnerPos.Parser.ParseFrom(payload);
                        await ReqPartnerPos(req.Id, req.Pos);
                        break;
                    }
                    case GameCmd.C2SPartnerExchange:
                    {
                        var req = C2S_PartnerExchange.Parser.ParseFrom(payload);
                        await ReqPartnerExchange(req);
                        break;
                    }
                    case GameCmd.C2SPartnerRelive:
                    {
                        var req = C2S_PartnerRelive.Parser.ParseFrom(payload);
                        await ReqPartnerRelive(req);
                        break;
                    }
                    case GameCmd.C2STeamCreate:
                    {
                        var req = C2S_TeamCreate.Parser.ParseFrom(payload);
                        await ReqCreateTeam(req.Target);
                        break;
                    }
                    case GameCmd.C2STeamApplyJoin:
                    {
                        var req = C2S_TeamApplyJoin.Parser.ParseFrom(payload);
                        await ReqJoinTeam(req.Id);
                        break;
                    }
                    case GameCmd.C2STeamExit:
                    {
                        await ReqExitTeam();
                        break;
                    }
                    case GameCmd.C2STeamHandleJoinApply:
                    {
                        var req = C2S_TeamHandleJoinApply.Parser.ParseFrom(payload);
                        await ReqHandleTeamJoinApply(req.RoleId, req.Agree);
                        break;
                    }
                    case GameCmd.C2STeamJoinApplyList:
                    {
                        if (!IsTeamLeader) return;
                        _ = TeamGrain.QueryApplyList(RoleId);
                        break;
                    }
                    case GameCmd.C2STeamKickout:
                    {
                        var req = C2S_TeamKickout.Parser.ParseFrom(payload);
                        await ReqKickoutTeamPlayer(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SReqTeamHandOver:
                    {
                        if (!IsTeamLeader) return;
                        if (InBattle)
                        {
                            SendNotice("战斗中不允许交换队长");
                            return;
                        }

                        var req = C2S_ReqTeamHandOver.Parser.ParseFrom(payload);
                        _ = TeamGrain.ReqHandOver(RoleId, req.RoleId);
                        break;
                    }
                    case GameCmd.C2STeamHandOver:
                    {
                        if (!InTeam || IsTeamLeader) return;
                        if (InBattle)
                        {
                            SendNotice("战斗中不允许交换队长");
                            return;
                        }

                        var req = new HandOverTeamRequest { RoleId = RoleId };
                        req.List.AddRange(PartnerMgr.Actives.Select(p => p.BuildTeamObjectData()));
                        _ = TeamGrain.HandOver(new Immutable<byte[]>(Packet.Serialize(req)));
                        break;
                    }
                    case GameCmd.C2STeamList:
                    {
                        var req = C2S_TeamList.Parser.ParseFrom(payload);
                        await ReqListTeam(req.Target, (int)req.PageIndex);
                        break;
                    }
                    case GameCmd.C2STeamFindAndApplyJoin:
                    {
                        var req = C2S_TeamFindAndApplyJoin.Parser.ParseFrom(payload);
                        await ReqFindAndJoinTeam(req.Target);
                        break;
                    }
                    case GameCmd.C2STeamInvite:
                    {
                        var req = C2S_TeamInvite.Parser.ParseFrom(payload);
                        // 30s以内不再发起邀请
                        if (!C2S_TeamInviteDict.ContainsKey(req.RoleId))
                        {
                            C2S_TeamInviteDict.Add(req.RoleId, TimeUtil.TimeStamp);
                            await ReqInvitePlayerJoinTeam(req.RoleId);
                        }
                        else
                        {
                            SendNotice("已经邀请，请等待");
                        }
                        break;
                    }
                    case GameCmd.C2STeamHandleInvite:
                    {
                        var req = C2S_TeamHandleInvite.Parser.ParseFrom(payload);
                        await ReqHandleTeamInvite(req.TeamId, req.Agree);
                        break;
                    }
                    case GameCmd.C2STeamFind:
                    {
                        var req = C2S_TeamFind.Parser.ParseFrom(payload);
                        await ReqFindTeam(req.TeamId);
                        break;
                    }
                    case GameCmd.C2STeamRecruit:
                    {
                        var req = C2S_TeamRecruit.Parser.ParseFrom(payload);
                        await ReqTeamRecruit(req);
                        break;
                    }
                    case GameCmd.C2STeamTarget:
                    {
                        var req = C2S_TeamTarget.Parser.ParseFrom(payload);
                        await ReqTeamTarget(req);
                        break;
                    }
                    case GameCmd.C2STeamApplyLeader:
                    {
                        await ReqTeamLeader();
                        break;
                    }
                    case GameCmd.C2STeamHandleApplyLeader:
                    {
                        var req = C2S_TeamHandleApplyLeader.Parser.ParseFrom(payload);
                        await ReqHandleTeamLeader(req);
                        break;
                    }
                    // 开启组队--暂离、归队、邀请归队
                    /*
                    case GameCmd.C2STeamLeave:
                    {
                        if (!InTeam || IsTeamLeader || _teamLeave) return;
                        await TeamGrain.Leave(RoleId);
                        break;
                    }
                    case GameCmd.C2STeamBack:
                    {
                        if (!InTeam || IsTeamLeader || !_teamLeave) return;
                        await TeamGrain.Back(RoleId);
                        break;
                    }
                    case GameCmd.C2STeamBackInvite:
                    {
                        if (!IsTeamLeader) return;
                        var req = C2S_TeamBackInvite.Parser.ParseFrom(payload);
                        await TeamGrain.InviteBack(RoleId, req.RoleId);
                        break;
                    }
                    */
                    case GameCmd.C2SFriendList:
                    {
                        await SendPacket(GameCmd.S2CFriendList, new S2C_FriendList { List = { _friendList } });
                        break;
                    }
                    case GameCmd.C2SFriendApply:
                    {
                        var req = C2S_FriendApply.Parser.ParseFrom(payload);
                        await ReqAddFriend(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SFriendHandleApply:
                    {
                        var req = C2S_FriendHandleApply.Parser.ParseFrom(payload);
                        await ReqHandleFriendApply(req.RoleId, req.Agree);
                        break;
                    }
                    case GameCmd.C2SFriendDel:
                    {
                        var req = C2S_FriendDel.Parser.ParseFrom(payload);
                        await ReqDelFriend(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SFriendSearch:
                    {
                        var req = C2S_FriendSearch.Parser.ParseFrom(payload);
                        await ReqSearchRole(req.PageIndex, req.Search);
                        break;
                    }
                    case GameCmd.C2SFriendInfo:
                    {
                        var req = C2S_FriendInfo.Parser.ParseFrom(payload);
                        await ReqFriendInfo(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SSectList:
                    {
                        var req = C2S_SectList.Parser.ParseFrom(payload);
                        await ReqSectList(req.Search, (int)req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SSectMemberList:
                    {
                        if (!InSect) return;
                        var req = C2S_SectMemberList.Parser.ParseFrom(payload);
                        _ = SectGrain.GetMemberList(RoleId, (int)req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SSectJoinApplyList:
                    {
                        if (!InSect) return;
                        _ = SectGrain.GetJoinApplyList(RoleId);
                        break;
                    }
                    case GameCmd.C2SSectCreate:
                    {
                        var req = C2S_SectCreate.Parser.ParseFrom(payload);
                        await ReqCreateSect(req.Name, req.Desc);
                        break;
                    }
                    case GameCmd.C2SSectExit:
                    {
                        if (InSect)
                            _ = SectGrain.Exit(RoleId);
                        break;
                    }
                    case GameCmd.C2SSectApplyJoin:
                    {
                        var req = C2S_SectApplyJoin.Parser.ParseFrom(payload);
                        await ReqJoinSect(req.Id);
                        break;
                    }
                    case GameCmd.C2SSectHandleJoinApply:
                    {
                        if (!InSect) return;
                        var req = C2S_SectHandleJoinApply.Parser.ParseFrom(payload);
                        _ = SectGrain.HandleJoinApply(RoleId, req.RoleId, req.Agree);
                        break;
                    }
                    case GameCmd.C2SSectKickout:
                    {
                        if (!InSect) return;
                        var req = C2S_SectKickout.Parser.ParseFrom(payload);
                        _ = SectGrain.Kickout(RoleId, req.Id);
                        break;
                    }
                    case GameCmd.C2SSectContrib:
                    {
                        if (!InSect) return;
                        var req = C2S_SectContrib.Parser.ParseFrom(payload);
                        await ReqContribSect(req.Jade);
                        break;
                    }
                    case GameCmd.C2SSectAppoint:
                    {
                        if (!InSect) return;
                        var req = C2S_SectAppoint.Parser.ParseFrom(payload);
                        await ReqSectAppoint(req.RoleId, req.Type);
                        break;
                    }
                    case GameCmd.C2SSectSilent:
                    {
                        if (!InSect) return;
                        var req = C2S_SectSilent.Parser.ParseFrom(payload);
                        await ReqSectSilent(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SSectChangeDesc:
                    {
                        if (!InSect) return;
                        var req = C2S_SectChangeDesc.Parser.ParseFrom(payload);
                        await ReqSectChangeDesc(req.Desc);
                        break;
                    }
                    case GameCmd.C2SChat:
                    {
                        var req = C2S_Chat.Parser.ParseFrom(payload);
                        // 弹幕
                        if (req.Type == ChatMessageType.DanMu)
                        {
                            await ReqDanMu(req);
                        }
                        // 其他
                        else
                        {
                            await ReqChat(req);
                        }
                        break;
                    }
                    case GameCmd.C2SChatSilent:
                    {
                        var req = C2S_ChatSilent.Parser.ParseFrom(payload);
                        await ReqChatSilent(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SChatFroze:
                    {
                        var req = C2S_ChatFroze.Parser.ParseFrom(payload);
                        await ReqChatFroze(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SRankLevel:
                    {
                        var req = C2S_RankLevel.Parser.ParseFrom(payload);
                        await ReqLevelRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SRankJade:
                    {
                        var req = C2S_RankJade.Parser.ParseFrom(payload);
                        await ReqJadeRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SRankPay:
                    {
                        var req = C2S_RankPay.Parser.ParseFrom(payload);
                        await ReqPayRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SRankSldh:
                    {
                        var req = C2S_RankSldh.Parser.ParseFrom(payload);
                        await ReqSldhRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SRankWzzz:
                    {
                        var req = C2S_RankWzzz.Parser.ParseFrom(payload);
                        await ReqWzzzRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SRankSect:
                    {
                        var req = C2S_RankSect.Parser.ParseFrom(payload);
                        await ReqSectRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SRankSinglePk:
                    {
                        var req = C2S_RankSinglePk.Parser.ParseFrom(payload);
                        await ReqSinglePkRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2SRankDaLuanDou:
                    {
                        var req = C2S_RankDaLuanDou.Parser.ParseFrom(payload);
                        await ReqDaLuanDouRank(req.PageIndex);
                        break;
                    }
                    case GameCmd.C2STitleChange:
                    {
                        var req = C2S_TitleChange.Parser.ParseFrom(payload);
                        await ReqChangeTitle(req);
                        break;
                    }
                    case GameCmd.C2SMapChange:
                    {
                        var req = C2S_MapChange.Parser.ParseFrom(payload);
                        await ReqChangeMap(req);
                        break;
                    }
                    case GameCmd.C2SMapMove:
                    {
                        if (InTeam && !IsTeamLeader && !_teamLeave) return;
                        var req = C2S_MapMove.Parser.ParseFrom(payload);
                        await ReqMove(req.X, req.Y);
                        break;
                    }
                    case GameCmd.C2SMapStop:
                    {
                        var req = C2S_MapStop.Parser.ParseFrom(payload);
                        await ReqStopMove(req);
                        break;
                    }
                    case GameCmd.C2SNpcShopItems:
                    {
                        var req = C2S_NpcShopItems.Parser.ParseFrom(payload);
                        await ReqNpcShopItems(req.NpcCfgId);
                        break;
                    }
                    case GameCmd.C2SNpcShopBuy:
                    {
                        var req = C2S_NpcShopBuy.Parser.ParseFrom(payload);
                        await ReqNpcShopBuy(req.NpcCfgId, req.CfgId, req.Num);
                        break;
                    }
                    case GameCmd.C2SShopItems:
                    {
                        var req = C2S_ShopItems.Parser.ParseFrom(payload);
                        await ReqShopItems(req.Type);
                        break;
                    }
                    case GameCmd.C2SShopBuy:
                    {
                        var req = C2S_ShopBuy.Parser.ParseFrom(payload);
                        await ReqShopBuy(req.CfgId, req.Num);
                        break;
                    }
                    case GameCmd.C2SMallItems:
                    {
                        _ = C2S_MallItems.Parser.ParseFrom(payload);
                        await ReqMallItems(payload);
                        break;
                    }
                    case GameCmd.C2SMallAddItem:
                    {
                        var req = C2S_MallAddItem.Parser.ParseFrom(payload);
                        await ReqMallAddItem(req);
                        break;
                    }
                    case GameCmd.C2SMallDelItem:
                    {
                        var req = C2S_MallDelItem.Parser.ParseFrom(payload);
                        await ReqMallDelItem(req.Id);
                        break;
                    }
                    case GameCmd.C2SMallBuy:
                    {
                        var req = C2S_MallBuy.Parser.ParseFrom(payload);
                        await ReqMallBuy(req.Id, req.Num);
                        break;
                    }
                    case GameCmd.C2SMallItemDetail:
                    {
                        var req = C2S_MallItemDetail.Parser.ParseFrom(payload);
                        await ReqMallItemDetail(req.Id);
                        break;
                    }
                    case GameCmd.C2SMallMyItems:
                    {
                        await ReqMallMyItems();
                        break;
                    }
                    case GameCmd.C2SMallUpdateMyItem:
                    {
                        var req = C2S_MallUpdateMyItem.Parser.ParseFrom(payload);
                        await ReqMallUpdateMyItem(req.Id, req.Price);
                        break;
                    }
                    case GameCmd.C2SMailList:
                    {
                        if (MailMgr != null)
                            await MailMgr.SendList();
                        break;
                    }
                    case GameCmd.C2SMailPick:
                    {
                        var req = C2S_MailPick.Parser.ParseFrom(payload);
                        if (MailMgr != null)
                            await MailMgr.Pick(req.Ids);
                        break;
                    }
                    case GameCmd.C2SMailDel:
                    {
                        var req = C2S_MailDel.Parser.ParseFrom(payload);
                        if (MailMgr != null)
                            await MailMgr.Delete(req.Ids);
                        break;
                    }
                    case GameCmd.C2STaskTriggerNpcBomb:
                    {
                        var req = C2S_TaskTriggerNpcBomb.Parser.ParseFrom(payload);
                        await ReqTriggerNpcBoomb(req);
                        break;
                    }
                    case GameCmd.C2STaskSubmitTalkNpc:
                    {
                        var req = C2S_TaskSubmitTalkNpc.Parser.ParseFrom(payload);
                        await ReqSubmitTalkNpcTask(req);
                        break;
                    }
                    case GameCmd.C2STaskSubmitGatherNpc:
                    {
                        var req = C2S_TaskSubmitGatherNpc.Parser.ParseFrom(payload);
                        await ReqSubmitGatherNpcTask(req);
                        break;
                    }
                    case GameCmd.C2STaskSubmitDoAction:
                    {
                        var req = C2S_TaskSubmitDoAction.Parser.ParseFrom(payload);
                        await ReqSubmitDoActionTask(req);
                        break;
                    }
                    case GameCmd.C2STaskInceptDaily:
                    {
                        var req = C2S_TaskInceptDaily.Parser.ParseFrom(payload);
                        await ReqInceptDailyTask(req);
                        break;
                    }
                    case GameCmd.C2STaskInceptInstance:
                    {
                        var req = C2S_TaskInceptInstance.Parser.ParseFrom(payload);
                        await ReqInceptInstanceTask(req);
                        break;
                    }
                    case GameCmd.C2STaskAbort:
                    {
                        var req = C2S_TaskAbort.Parser.ParseFrom(payload);
                        await ReqAbortTask(req.TaskId);
                        break;
                    }
                    case GameCmd.C2STaskDailyData:
                    {
                        await TaskMgr.SendDailyData();
                        break;
                    }
                    case GameCmd.C2STaskActivePrize:
                    {
                        var req = C2S_TaskActivePrize.Parser.ParseFrom(payload);
                        await TaskMgr.GetActivePrize((int)req.Index);
                        break;
                    }
                    case GameCmd.C2SBattleAttack:
                    {
                        // 转发给BattleGrain处理
                        if (!InBattle || _battleGrain == null) return;
                        await _battleGrain.Attack(new Immutable<byte[]>(payload));
                        break;
                    }
                    //观战--进入
                    case GameCmd.C2SEnterWatchBattle:
                    {
                        if (InTeam && !IsTeamLeader/* && !_teamLeave*/)
                        {
                            SendNotice("队员不能独自进入观战");
                            break;
                        }
                        if (InTeam && await TeamGrain.CheckSldhSigned(RoleId))
                        {
                            SendNotice("当前已报名水陆大会活动，不能进入观战");
                            break;
                        }
                        if (InTeam && await TeamGrain.CheckWzzzSigned(RoleId))
                        {
                            SendNotice("当前已报名王者之战活动，不能进入观战");
                            break;
                        }
                        if (await IsSignedSinglePk())
                        {
                            SendNotice("当前已报名比武大会，不能进入观战");
                            break;
                        }
                        if (await IsSignedDaLuanDou())
                        {
                            SendNotice("当前已参加大乱斗，不能进入观战");
                            break;
                        }
                        if (_ssjl.Signed)
                        {
                            SendNotice("当前已报名神兽降临，不能进入观战");
                            break;
                        }
                        if (_battleGrain != null)
                        {
                            SendNotice("当前战斗中，不能进入观战");
                            break;
                        }
                        var req = C2S_EnterWatchBattle.Parser.ParseFrom(payload);
                        if (req.IsSectWar && (_sectWarId == 0 || (_sectWarCamp != 1 && _sectWarCamp != 2)))
                        {
                            SendNotice("没有参加帮战，不能进入观战");
                            break;
                        }
                        if (!req.IsSectWar && _sectWarId > 0)
                        {
                            SendNotice("当前参加帮战，不能进入观战");
                            break;
                        }
                        await ReqEnterWatchBattle(req);
                        break;
                    }
                    //观战--退出
                    case GameCmd.C2SExitWatchBattle:
                    {
                        if (InTeam && !IsTeamLeader/* && !_teamLeave*/)
                        {
                            SendNotice("队员不能独自退出观战");
                            break;
                        }
                        await ReqExitWatchBattle();
                        break;
                    }
                    case GameCmd.C2STianJiangLingHouFight:
                    {
                        var req = C2S_TianJiangLingHouFight.Parser.ParseFrom(payload);
                        await ReqTianJiangLingHouFight(req.NpcOnlyId);
                        break;
                    }
                    case GameCmd.C2SPk:
                    {
                        var req = C2S_PK.Parser.ParseFrom(payload);
                        await ReqPk(req.TargetRoleId);
                        break;
                    }
                    case GameCmd.C2SHcPk:
                    {
                        var req = C2S_HcPk.Parser.ParseFrom(payload);
                        await ReqHcPk(req.TargetRoleId, req.Text);
                        break;
                    }
                    case GameCmd.C2SHcHandleApply:
                    {
                        var req = C2S_HcHandleApply.Parser.ParseFrom(payload);
                        await ReqHcHandle(req.Agree);
                        break;
                    }
                    case GameCmd.C2SHcRoleList:
                    {
                        await ReqHcRoleList();
                        break;
                    }
                    case GameCmd.C2SChallengeNpc:
                    {
                        var req = C2S_ChallengeNpc.Parser.ParseFrom(payload);
                        await ReqChallengeNpc(req.OnlyId, req.CfgId);
                        break;
                    }
                    case GameCmd.C2SSldhSign:
                    {
                        await ReqSldhSign();
                        break;
                    }
                    case GameCmd.C2SWzzzSign:
                    {
                        await ReqWzzzSign();
                        break;
                    }
                    case GameCmd.C2SSldhUnSign:
                    {
                        await ReqSldhUnSign();
                        break;
                    }
                    case GameCmd.C2SWzzzUnSign:
                    {
                        await ReqWzzzUnSign();
                        break;
                    }
                    case GameCmd.C2SSldhInfo:
                    {
                        await ReqSldhInfo();
                        break;
                    }
                    case GameCmd.C2SWzzzInfo:
                    {
                        await ReqWzzzInfo();
                        break;
                    }
                    case GameCmd.C2SSinglePkSign:
                    {
                        await ReqSinglePkSign();
                        break;
                    }
                    case GameCmd.C2SSinglePkUnSign:
                    {
                        await ReqSinglePkUnSign();
                        break;
                    }
                    case GameCmd.C2SSinglePkInfo:
                    {
                        await ReqSinglePkInfo();
                        break;
                    }
                    case GameCmd.C2SDaLuanDouSign:
                    {
                        await ReqDaLuanDouSign();
                        break;
                    }
                    case GameCmd.C2SDaLuanDouUnSign:
                    {
                        await ReqDaLuanDouUnSign();
                        break;
                    }
                    case GameCmd.C2SDaLuanDouInfo:
                    {
                        await ReqDaLuanDouInfo();
                        break;
                    }
                    case GameCmd.C2SDaLuanDouPk:
                    {
                        var req = C2S_DaLuanDouPk.Parser.ParseFrom(payload);
                        await ReqDaLuanDouPk(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SSectWarEnter:
                    {
                        await ReqSectWarEnter();
                        break;
                    }
                    case GameCmd.C2SSectWarExit:
                    {
                        await ReqSectWarExit();
                        break;
                    }
                    case GameCmd.C2SSectWarChangePlace:
                    {
                        var req = C2S_SectWarChangePlace.Parser.ParseFrom(payload);
                        await ReqSectWarChangePlace(req.Place);
                        break;
                    }
                    case GameCmd.C2SSectWarReadyPk:
                    {
                        await ReqSectWarReadyPk();
                        break;
                    }
                    case GameCmd.C2SSectWarCancelPk:
                    {
                        await ReqSectWarCancelPk();
                        break;
                    }
                    case GameCmd.C2SSectWarGrabCannon:
                    {
                        await ReqSectWarGrabCannon();
                        break;
                    }
                    case GameCmd.C2SSectWarLockDoor:
                    {
                        await ReqSectWarLockDoor();
                        break;
                    }
                    case GameCmd.C2SSectWarCancelDoor:
                    {
                        await ReqSectWarCancelDoor();
                        break;
                    }
                    case GameCmd.C2SSectWarBreakDoor:
                    {
                        var req = C2S_SectWarBreakDoor.Parser.ParseFrom(payload);
                        await ReqSectWarBreakDoor(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SSectWarFreePk:
                    {
                        var req = C2S_SectWarFreePk.Parser.ParseFrom(payload);
                        await ReqSectWarFreePk(req.RoleId);
                        break;
                    }
                    case GameCmd.C2SSectWarInfo:
                    {
                        await ReqSectWarInfo();
                        break;
                    }
                    case GameCmd.C2SSkinInfo:
                    {
                        var req = C2S_SkinInfo.Parser.ParseFrom(payload);
                        await ReqSkinInfo(req.Use.ToList());
                        break;
                    }
                    // 获取变身信息
                    case GameCmd.C2SBianshenInfo:
                    {
                        await ReqBianshenInfo();
                        break;
                    }
                    // 五行升级
                    case GameCmd.C2SBianshenWuxingLevelUp:
                    {
                        var req = C2S_BianshenWuxingLevelUp.Parser.ParseFrom(payload);
                        await ReqBianshenWuxingLevelUp(req.Type);
                        break;
                    }
                    // 使用变身卡
                    case GameCmd.C2SBianshenUse:
                    {
                        var req = C2S_BianshenUse.Parser.ParseFrom(payload);
                        await ReqBianshenUse(req.Id, req.Avatar);
                        break;
                    }
                    // 变身还原
                    case  GameCmd.C2SBianshenReset:
                    {
                        await ReqBianshenReset();
                        break;
                    }
                    // 获取星阵信息
                    case GameCmd.C2SXingzhenInfo:
                    {
                        await ReqXingzhenInfo();
                        break;
                    }
                    // 星阵--解锁
                    case GameCmd.C2SXingzhenUnlock:
                    {
                        var req = C2S_XingzhenUnlock.Parser.ParseFrom(payload);
                        await ReqXingzhenUnlock(req.Id);
                        break;
                    }
                    // 星阵--装备
                    case GameCmd.C2SXingzhenUse:
                    {
                        var req = C2S_XingzhenUse.Parser.ParseFrom(payload);
                        await ReqXingzhenUse(req.Id);
                        break;
                    }
                    // 星阵--升级
                    case GameCmd.C2SXingzhenUpgrade:
                    {
                        var req = C2S_XingzhenUpgrade.Parser.ParseFrom(payload);
                        await ReqXingzhenUpgrade(req.Id, req.ItemId);
                        break;
                    }
                    // 星阵--洗炼
                    case GameCmd.C2SXingzhenRefine:
                    {
                        var req = C2S_XingzhenRefine.Parser.ParseFrom(payload);
                        await ReqXingzhenRefine(req.Id);
                        break;
                    }
                    // 星阵--替换
                    case GameCmd.C2SXingzhenReplace:
                    {
                        var req = C2S_XingzhenReplace.Parser.ParseFrom(payload);
                        await ReqXingzhenReplace(req.Id);
                        break;
                    }
                    // 星阵--定制
                    case GameCmd.C2SXingzhenDingZhi:
                    {
                        var req = C2S_XingzhenDingZhi.Parser.ParseFrom(payload);
                        await ReqXingzhenDingZhi(req.Id, req.Attrs.ToList());
                        break;
                    }
                    // 获取孩子信息
                    case GameCmd.C2SChildInfo:
                    {
                        var req = C2S_ChildInfo.Parser.ParseFrom(payload);
                        await ReqChildInfo();
                        break;
                    }
                    // 孩子--领养
                    case GameCmd.C2SChildAdopt:
                    {
                        var req = C2S_ChildAdopt.Parser.ParseFrom(payload);
                        await ReqChildAdopt(req.Sex);
                        break;
                    }
                    // 孩子--起名
                    case GameCmd.C2SChildRename:
                    {
                        var req = C2S_ChildRename.Parser.ParseFrom(payload);
                        await ReqChildRename(req.Name);
                        break;
                    }
                    // 孩子--培养
                    case GameCmd.C2SChildUpgrade:
                    {
                        var req = C2S_ChildUpgrade.Parser.ParseFrom(payload);
                        await ReqChildUpgrade(req.ItemId);
                        break;
                    }
                    // 孩子--更换形象
                    case GameCmd.C2SChildChangeShape:
                    {
                        var req = C2S_ChildChangeShape.Parser.ParseFrom(payload);
                        await ReqChildChangeShape(req.Shape);
                        break;
                    }
                    // 孩子--洗炼
                    case GameCmd.C2SChildRefine:
                    {
                        // var req = C2S_ChildRefine.Parser.ParseFrom(payload);
                        await ReqChildRefine();
                        break;
                    }
                    // 孩子--替换
                    case GameCmd.C2SChildReplace:
                    {
                        // var req = C2S_ChildReplace.Parser.ParseFrom(payload);
                        await ReqChildReplace();
                        break;
                    }
                    // 孩子--定制
                    case GameCmd.C2SChildDingZhi:
                    {
                        var req = C2S_ChildDingZhi.Parser.ParseFrom(payload);
                        await ReqChildDingZhi(req.Skills.ToList());
                        break;
                    }
                    // 神兽降临--报名
                    case GameCmd.C2SSsjlSign:
                    {
                        await ReqSsjlSign();
                        break;
                    }
                    // 神兽降临--退赛
                    case GameCmd.C2SSsjlUnSign:
                    {
                    await ReqSsjlUnSign();
                        break;
                    }
                    // 转盘--信息
                    case GameCmd.C2SLuckyDrawInfo:
                    {
                        await ReqLuckyDrawInfo();
                        break;
                    }
                    // 转盘--抽奖
                    case GameCmd.C2SLuckyDrawTurn:
                    {
                        var req = C2S_LuckyDrawTurn.Parser.ParseFrom(payload);
                        await ReqLuckyDrawTurn(req.Count);
                        break;
                    }
                    // 转盘--开宝箱
                    case GameCmd.C2SLuckyDrawOpenChest:
                    {
                        await ReqLuckyDrawOpenChest();
                        break;
                    }
                    // 经验兑换潜能--信息
                    case GameCmd.C2SExpExchangeInfo:
                    {
                        await ReqExpExchangeInfo();
                        break;
                    }
                    // 经验兑换潜能--兑换
                    case GameCmd.C2SExpExchange:
                    {
                        await ReqExpExchange();
                        break;
                    }
                    // 限时充值排行榜--排行榜信息
                    case GameCmd.C2SLimitChargeRankInfo:
                    {
                        // var req = C2S_LimitChargeRankInfo.Parser.ParseFrom(payload);
                        await ReqLimitChargeRankInfo();
                        break;
                    }
                    // 限时充值排行榜--获取排行奖励
                    case GameCmd.C2SLimitChargeRankGiftGet:
                    {
                        // var req = C2S_LimitChargeRankGiftGet.Parser.ParseFrom(payload);
                        await ReqLimitChargeRankGiftGet();
                        break;
                    }
                    // 限时等级排行榜--排行榜信息
                    case GameCmd.C2SLimitLevelRankInfo:
                    {
                        // var req = C2S_LimitLevelRankInfo.Parser.ParseFrom(payload);
                        await ReqLimitLevelRankInfo();
                        break;
                    }
                    // 限时等级排行榜--获取排行奖励
                    case GameCmd.C2SLimitLevelRankGiftGet:
                    {
                        // var req = C2S_LimitLevelRankGiftGet.Parser.ParseFrom(payload);
                        await ReqLimitLevelRankGiftGet();
                        break;
                    }
                    // 获取VIP信息
                    case GameCmd.C2SVipInfo:
                    {
                        // var req = C2S_VipInfo.Parser.ParseFrom(payload);
                        await ReqVipInfo();
                        break;
                    }
                    // 获取VIP奖励
                    case GameCmd.C2SVipGiftGet:
                    {
                        // var req = C2S_VipGiftGet.Parser.ParseFrom(payload);
                        await ReqVipGiftGet();
                        break;
                    }
                    // 双倍经验--信息
                    case GameCmd.C2SX2ExpInfo:
                    {
                        // var req = C2S_X2ExpInfo.Parser.ParseFrom(payload);
                        await ReqX2ExpInfo();
                        break;
                    }
                    // 双倍经验--领取
                    case GameCmd.C2SX2ExpGet:
                    {
                        // var req = C2S_X2ExpGet.Parser.ParseFrom(payload);
                        await ReqX2ExpGet();
                        break;
                    }
                    //天策符--列表
                    case GameCmd.C2STianceFuGetList:
                    {
                        // var req = C2S_TianceFuGetList.Parser.ParseFrom(payload);
                        await ReqTianceFuGetList();
                        break;
                    }
                    //天策符--合成
                    case GameCmd.C2STianceFuHeCheng:
                    {
                        var req = C2S_TianceFuHeCheng.Parser.ParseFrom(payload);
                        await ReqTianceFuHeCheng(req.Num);
                        break;
                    }
                    //天策符--鉴定
                    case GameCmd.C2STianceFuJianDing:
                    {
                        var req = C2S_TianceFuJianDing.Parser.ParseFrom(payload);
                        await ReqTianceFuJianDing(req.Type, req.Num);
                        break;
                    }
                    //天策符--分解
                    case GameCmd.C2STianceFuFengJie:
                    {
                        var req = C2S_TianceFuFengJie.Parser.ParseFrom(payload);
                        await ReqTianceFuFengJie(req.List.ToList());
                        break;
                    }
                    //天策符--使用
                    case GameCmd.C2STianceFuUse:
                    {
                        var req = C2S_TianceFuUse.Parser.ParseFrom(payload);
                        await ReqTianceFuUse(req.Id, req.State);
                        break;
                    }
                    //打开天演策界面
                    case GameCmd.C2STianYanCeOpen:
                    {
                        // var req = C2S_TianYanCeOpen.Parser.ParseFrom(payload);
                        await ReqTianYanCeOpen();
                        break;
                    }
                    //天演策升级
                    case GameCmd.C2STianYanCeUpgrade:
                    {
                        var req = C2S_TianYanCeUpgrade.Parser.ParseFrom(payload);
                        await ReqTianYanCeUpgrade(req.Level);
                        break;
                    }
                    //天策符--百变鉴定
                    case GameCmd.C2STianceFuJianDingBaiBian:
                    {
                        var req = C2S_TianceFuJianDingBaiBian.Parser.ParseFrom(payload);
                        await ReqTiancFuJianDingBaiBian(req.ItemId, req.TianCeFuId);
                        break;
                    }
                    //切割--进入
                    case GameCmd.C2SQieGeEnter:
                    {
                        //var req = C2S_QieGeEnter.Parser.ParseFrom(payload);
                        await ReqQieGeEnter();
                        break;
                    }
                    //切割--升级
                    case GameCmd.C2SQieGeUpgrade:
                    {
                        var req = C2S_QieGeUpgrade.Parser.ParseFrom(payload);
                        await ReqQieGeUpgrade(req.Quick);
                        break;
                    }
                    //神之力--查询信息
                    case GameCmd.C2SShenZhiLiInfo:
                    {
                        await ReqShenZhiLiInfo();
                        break;
                    }
                    //神之力--升级
                    case GameCmd.C2SShenZhiLiUpgrade:
                    {
                        var req = C2S_ShenZhiLiUpgrade.Parser.ParseFrom(payload);
                        await ReqShenZhiLiUpgrade(req.LvType);
                        break;
                    }
                    //成神之路副本信息--查询
                    case GameCmd.C2SCszlInfo:
                    {
                        await ReqCszlInfo();
                        break;
                    }
                    //成神之路--挑战副本
                    case GameCmd.C2SCszlChallenge:
                    {
                        // var req = C2S_ShenZhiLiUpgrade.Parser.ParseFrom(payload);
                        await ReqCszlChallenge();
                        break;
                    }
                    //成神之路--积分跳过本层
                    case GameCmd.C2SCszlScoreSkip:
                    {
                        // var req = C2S_ShenZhiLiUpgrade.Parser.ParseFrom(payload);
                        await ReqCszlScoreSkip();
                        break;
                    }
                    //成神之路--积分重置
                    case GameCmd.C2SCszlScoreReset:
                    {
                        await ReqCszlScoreReset();
                        break;
                    }
                    //成神之路--爬塔层数排行榜
                    case GameCmd.C2SRankCszlLayer:
                    {
                        var req = C2S_RankCszlLayer.Parser.ParseFrom(payload);
                        await ReqRankCszlLayer(req.PageIndex);
                        break;
                    }
                    // 红包--进入主界面
                    case GameCmd.C2SRedEnterMain:
                    {
                        // var req = C2S_RedEnterMain.Parser.ParseFrom(payload);
                        await ReqRedEnterMain();
                        break;
                    }
                    // 红包--详情
                    case GameCmd.C2SRedDetail:
                    {
                        var req = C2S_RedDetail.Parser.ParseFrom(payload);
                        await ReqRedDetail(req.Id);
                        break;
                    }
                    // 红包--历史记录
                    case GameCmd.C2SRedHistory:
                    {
                        var req = C2S_RedHistory.Parser.ParseFrom(payload);
                        await ReqRedHistory(req.Type, req.Recived);
                        break;
                    }
                    // 红包--发送
                    case GameCmd.C2SRedSend:
                    {
                        var req = C2S_RedSend.Parser.ParseFrom(payload);
                        await ReqRedSend(req);
                        break;
                    }
                    // 红包--抢包
                    case GameCmd.C2SRedGet:
                    {
                        var req = C2S_RedGet.Parser.ParseFrom(payload);
                        await ReqRedGet(req.Id);
                        break;
                    }
                    default:
                        LogError($"非法的指令[{command}]");
                        await _packet.SendStatus(RoleId, WebSocketCloseStatus.ProtocolError, false);
                        await Offline();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError($"处理数据包出错[{ex.Message}][{ex.StackTrace}]");
                await _packet.SendStatus(RoleId, WebSocketCloseStatus.InternalServerError, false);
                await Offline();
            }
        }

        private async Task OnApplicationPause(bool pause)
        {
            if (!IsEnterServer || IsEnterBackGround == pause) return;
            // 标记进入后台
            IsEnterBackGround = pause;
            if (pause)
            {
                _enterBackGroundTime = TimeUtil.TimeStamp;
            }
            else
            {
                _enterBackGroundTime = 0;
            }

            // 通知地图服务器
            if (_mapGrain != null)
            {
                await _mapGrain.PlayerPause(OnlyId, pause);
            }

            // 通知战斗
            // if (InBattle)
            // {
            //     var battleId = (uint) _battleGrain.GetPrimaryKeyLong();
            //     var ret = await GlobalGrain.CheckBattle(battleId);
            //     if (ret)
            //     {
            //         if (pause) _ = _battleGrain.Offline(RoleId);
            //         else _ = _battleGrain.Online(RoleId);
            //     }
            //     else
            //     {
            //         _battleGrain = null;
            //     }
            // }

            // 重置上次心跳时间
            _lastHeartBeatTime = TimeUtil.TimeStamp;
            await Task.CompletedTask;
        }

        private async Task ReqHeartBeat(C2S_HeartBeat req)
        {
            // 记录本次收到的时间, 在Update中检测，如果时间超过了就认为掉线
            _lastHeartBeatTime = TimeUtil.TimeStamp;

            var resp = new S2C_HeartBeat();
            // 检查资源版本
            if (!string.IsNullOrWhiteSpace(req.ResVersion))
            {
                var bytes = await GlobalGrain.GetResVersion();
                if (bytes.Value != null)
                {
                    var vo = Json.Deserialize<ResVersionVo>(bytes.Value);
                    if (vo != null && !string.IsNullOrWhiteSpace(vo.Version))
                    {
                        resp.ResVersion = vo.Version;
                        resp.Force = vo.Force;
                    }
                }
            }

            await SendPacket(GameCmd.S2CHeartBeat, resp);
        }

        private async Task ReqEnterServer(C2S_EnterServer req)
        {
            _deviceWidth = req.DeviceWidth;
            _deviceHeight = req.DeviceHeight;

            // 检查区服是否已经启动
            if (!await ServerGrain.CheckActive())
            {
                await _packet.SendStatus(RoleId, WebSocketCloseStatus.NormalClosure, true);
                return;
            }
            // 标记上线
            IsEnterServer = true;
            IsEnterBackGround = false;
            _enterBackGroundTime = 0;

            // 检查战斗是否还存在
            if (InBattle)
            {
                if (_battleGrain != null)
                {
                    var ret = await GlobalGrain.CheckBattle((uint)_battleGrain.GetPrimaryKeyLong());
                    if (!ret)
                    {
                        _battleGrain = null;
                        _battleId = 0;
                        _campId = 0;
                    }
                }
            }

            // 不能私自逗留在帮战地图上
            if (Entity.MapId == 5001 && _sectWarCamp == 0)
            {
                await ChangeMap(1011, 230, 20);
            }

            // 不能私自逗留在水路地图上
            if (Entity.MapId == 3001 && _sldh.Group == 0)
            {
                await ChangeMap(1011, 999999, 999999);
            }

            // 不能私自逗留在大乱斗地图上
            if (Entity.MapId == 3004)
            {
                await ChangeMap(1011, 999999, 999999);
            }

            // 不能私自逗留在王者地图上
            // if (Entity.MapId == 3001 && _wzzz.Group == 0)
            // {
            //     await ChangeMap(1011, 999999, 999999);
            // }

            // 单人PK, 需要检测状态
            if (_singlePkGrain != null)
            {
                _singlePkVo.Sign = await _singlePkGrain.Online(RoleId);
                if (_singlePkVo.Sign)
                {
                    _singlePkVo.Sign = true;
                }
                else
                {
                    _singlePkVo.Sign = false;
                }
            }
            else
            {
                _singlePkVo.Sign = false;
            }
            // 不能私自逗留在PK地图上
            if (Entity.MapId == 3003 && !_singlePkVo.Sign)
            {
                await ChangeMap(1011, 999999, 999999);
            }

            // // 大乱斗PK, 需要检测状态
            // if (_daLuanDouGrain != null)
            // {
            //     _daLuanDouVo.Sign = await _daLuanDouGrain.Online(RoleId);
            //     if (_daLuanDouVo.Sign)
            //     {
            //         _daLuanDouVo.Sign = true;
            //     }
            //     else
            //     {
            //         _daLuanDouVo.Sign = false;
            //     }
            // }
            // else
            // {
            //     _daLuanDouVo.Sign = false;
            // }
            // // 不能私自逗留在PK地图上
            // if (Entity.MapId == 3003 && !_daLuanDouVo.Sign)
            // {
            //     await ChangeMap(1011, 999999, 999999);
            // }

            // 不能私自逗留在神兽降临地图上
            if (Entity.MapId == 6001 && !_ssjl.Signed)
            {
                await ChangeMap(1011, 999999, 999999);
            }

            // 下发玩家数据
            await SendRoleInfo();
            // 下发道具数据
            await SendItemList();
            // 下发称谓数据
            await TitleMgr.SendList();
            // 下发装备数据
            await EquipMgr.SendList();
            // 下发方案数据
            await SchemeMgr.SendList();
            // 下发宠物数据
            var newCreate = await PetMgr.SendList();
            // 全新创建用户送物品
            // 赠送财大气粗
            {
                var xxx = await TitleMgr.AddTitle(45);
                // 默认穿戴财大气粗
                if (xxx != null)
                {
                    await ReqChangeTitle(new C2S_TitleChange { Active = true, Id = xxx.Id });
                }
            }
            //梦回好服礼包直接送
            if (!GetFlag(FlagType.GongYiHaoFuGift))
            {
                await GetGongYiHaoFuGift();
            }

            //zyj-fix 在这里就不送了
            if (false) {
                // 赠送一套神兵
#if false
                await Task.WhenAll(
                    EquipMgr.AddEquip(EquipCategory.Shen, 1, 1),
                    EquipMgr.AddEquip(EquipCategory.Shen, 2, 1),
                    EquipMgr.AddEquip(EquipCategory.Shen, 3, 1),
                    EquipMgr.AddEquip(EquipCategory.Shen, 4, 1),
                    EquipMgr.AddEquip(EquipCategory.Shen, 5, 1)
                );
// #endif
                // 变身卡
                await AddBagItem(9904, 1, tag: "新用户注册");
                // 仙玉
                await AddMoney(MoneyType.Jade, 1, "新用户注册");
                // 宠物浪淘沙
                await AddItem(90061, 1, tag: "新用户注册");

                // todo 注册新号在这里添加赠送道具

                //  赠送一个蝉翼翅膀
                {
                    ConfigService.Wings.TryGetValue(5001, out var cfg);
                    if (cfg != null)
                    {
                        await EquipMgr.AddEquip(cfg, false);
                    }
                }

                // 送1000充值
// #if false
                {
                    // 发货
                    var payRate = await RedisService.GetPayRateJade();
                    await OnPayed(1000, 1000 * (int)payRate);
                }
#endif
            }
            // 下发宠物闪现支援列表
            await PetMgr.SendShanXianOrderList();
            // 下发坐骑数据
            await MountMgr.SendList();
            // 下发伙伴数据
            await PartnerMgr.SendList();

            // 进入游戏成功
            await SendPacket(GameCmd.S2CEnterServer);

            // 下发任务数据
            await TaskMgr.SendList();

            // 通知Server
            await ServerGrain.Online(RoleId);

            // 通知Map玩家正式上线
            await _mapGrain.PlayerOnline(OnlyId, _deviceWidth, _deviceHeight);

            // 通知队伍我已上线
            if (InTeam)
            {
                _ = TeamGrain.Online(RoleId);
            }
            else
            {
                await SendPacket(GameCmd.S2CTeamData, new S2C_TeamData { Data = null });
            }

            // 帮派
            if (Entity.SectId > 0)
            {
                var ret = await ServerGrain.ExistsSect(Entity.SectId);
                if (ret)
                {
                    // 不排除数据同步出现问题, 以SectGrain为准
                    SectGrain = GrainFactory.GetGrain<ISectGrain>(Entity.SectId);
                    ret = await SectGrain.Online(RoleId);
                }

                if (!ret)
                {
                    Entity.SectId = 0;
                    Entity.SectContrib = 0;
                    Entity.SectJob = 0;
                    SectGrain = null;
                }
            }

            // 其实这条协议是完全多余的
            if (Entity.SectId == 0)
            {
                await SendPacket(GameCmd.S2CSectData, new S2C_SectData { Data = null });
            }

            // 好友
            await SendPacket(GameCmd.S2CFriendList, new S2C_FriendList { List = { _friendList } });
            await SendPacket(GameCmd.S2CFriendApplyList, new S2C_FriendList { List = { _friendApplyList } });

            // 等级奖励
            await SendLevelRewardList();

            // 邮件
            await MailMgr.Reload();

            // 引妖香时间
            await SendPacket(GameCmd.S2CIncenseTime, new S2C_IncenseTime { Time = _incenseTime });

            // 检查是否在天牢
            _inPrison = Entity.MapId == 1201;
            if (await CheckShanEChange()) return;

            // 通知战斗，恢复上线
            if (InBattle && _battleGrain != null) _ = _battleGrain.Online(RoleId);

            LogDebug("进入游戏");

            // FIXME: 老旧BUG补丁，当前帮战活动是关闭的，但是玩家带帮战信息，则强制退出帮战
            SectWarState state = (SectWarState)await _sectWarGrain.State();
            if (state == SectWarState.Close)
            {
                if (_sectWarId != 0 || _sectWarCamp != 0 || _sectWarPlace != SectWarPlace.JiDi || _sectWarState != SectWarRoleState.Idle)
                {
                    LogDebug($"玩家[{RoleId}] 帮战状态不正确，{_sectWarId}, {_sectWarCamp}, {_sectWarPlace}, {_sectWarState}");
                    await OnExitSectWar();
                }
                // 帮战退出状态补丁
                await SendPacket(GameCmd.S2CSectWarStateFix, new S2C_SectWarStateFix());
            }
            else
            {
                // 通知帮战
                _ = _sectWarGrain.Online(RoleId, TeamLeader, Entity.SectId);
            }
            // 水陆大会
            _ = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(Entity.ServerId).Online(RoleId, TeamId, _sldh.Season);
            // 大乱斗
            _ = GrainFactory.GetGrain<IDaLuanDouGrain>(Entity.ServerId).Online(RoleId, TeamId, _sldh.Season);
            // 王者之战
            _ = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(Entity.ServerId).Online(RoleId, TeamId, _wzzz.Season);
            // 甄不亏
            _ = _zhenBuKuiGrain.Online(RoleId);
            // 神兽降临
            _ = GrainFactory.GetGrain<IShenShouJiangLinGrain>(Entity.ServerId).Online(RoleId, TeamId, _ssjl.Season);
        }

        private async Task ReqPrisonFree()
        {
            if (!_inPrison)
            {
                SendNotice("你当前并未监禁");
                return;
            }

            // 耗费10000仙玉
            var ret = await CostMoney(MoneyType.Jade, 10000, tag: "解除监禁");
            if (!ret) return;
            _shane = 0;
            Entity.Shane = 0;
            await CheckShanEChange();
        }

        private async Task ReqEnableShanXianOrder(bool enable)
        {
            SetFlag(FlagType.ShanXianOrder, enable);
            await SendPacket(GameCmd.S2CShanXianOrder, new S2C_ShanXianOrder { Enable = enable });
        }

        // 兑换可选无价配饰套
        private async Task ReqGetWuJiaTao(uint suitId)
        {
            // 检查套装id
            ConfigService.OrnamentSuits.TryGetValue(suitId, out var suitCfg);
            if (suitCfg == null) return;

            var ret = await AddBagItem(500000, -1, tag: "兑换可选无价配饰套");
            if (!ret) return;

            // 获得指定套装的全部配饰
            for (var i = 1; i <= 5; i++)
            {
                await EquipMgr.AddOrnaments(i, suitId, 3);
            }
        }

        private async Task ReqGetWuJiaBuJian(uint suitId, uint index)
        {
            // 检查套装id
            ConfigService.OrnamentSuits.TryGetValue(suitId, out var suitCfg);
            if (suitCfg == null) return;

            var ret = await AddBagItem(500003, -1, tag: "兑换可选无价配饰部件");
            if (!ret) return;

            // 获得指定套装的全部配饰
            var pos = (int)Math.Clamp(index, 1, 5);
            await EquipMgr.AddOrnaments(pos, suitId, 3);
        }

        private async Task ReqGetJiNengShu(uint cfgId)
        {
            ConfigService.Items.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return;
            if (cfg.Type == 10 && cfg.Level == 3)
            {
                var ret = await AddBagItem(500001, -1, tag: "兑换可选终极技能书");
                if (!ret) return;
                await AddBagItem(cfgId, 1, tag: "兑换可选终极技能书");
            }
        }

        private async Task ReqGetWuXingCaiLiao(uint cfgId)
        {
            if (cfgId < 20006 || cfgId > 20010) return;
            ConfigService.Items.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return;

            var ret = await AddBagItem(500002, -1, tag: "兑换可选二级五行材料");
            if (!ret) return;
            await AddBagItem(cfgId, 1, tag: "兑换可选二级五行材料");
        }

        private async Task ReqChangeMap(C2S_MapChange req)
        {
            if (InBattle) return;
            if (InTeam && !IsTeamLeader && !_teamLeave) return;

            // 帮战中不允许直接跳地图
            if (Entity.MapId == 5001 && _sectWarCamp != 0)
            {
                return;
            }

            // 水路大会地图不允许直接跳出地图
            if (Entity.MapId == 3001 && _sldh.Group != 0)
            {
                return;
            }

            // // 王者之战地图不允许直接跳出地图
            // if (Entity.MapId == 3001 && _wzzz.Group != 0)
            // {
            //     return;
            // }

            // 神兽降临地图不允许直接跳出地图
            if (Entity.MapId == 6001 && _ssjl.Signed)
            {
                return;
            }

            // 参数校验
            ConfigService.Maps.TryGetValue(req.Map, out var mapCfg);
            if (mapCfg == null)
            {
                BadRequest();
                return;
            }

            var terrainCfg = ConfigService.Terrains[mapCfg.Terrain];
            if (req.X < 0 || req.X >= terrainCfg.Cols || req.Y < 0 || req.Y >= terrainCfg.Rows)
            {
                BadRequest();
                return;
            }

            await ChangeMap(req.Map, req.X, req.Y);
        }

        private async Task ReqMove(int x, int y, bool stop = false, bool immediate = false)
        {
            // 参数修正
            if (x < 0) x = 0;
            else if (x >= _terrainCfg.Cols) x = _terrainCfg.Cols - 1;
            if (y < 0) y = 0;
            else if (y >= _terrainCfg.Rows) y = _terrainCfg.Rows - 1;

            if (Entity.MapX == x && Entity.MapY == y) return;

            // 检查杀人香是否触发了暗雷怪
            if (_incenseTime > 0)
            {
                _anleiCnt--;
                if (_anleiCnt <= 0)
                {
                    TriggerAnlei();
                    _anleiCnt = (uint)Random.Next(15, 31);
                }
            }
            // 神兽降临
            if (IsTeamLeader && _ssjl.Signed && _ssjl.Started && _ssjl.EndTime > 0)
            {
                _ssjl.NextTime--;
                if (_ssjl.NextTime <= 0)
                {
                    ConfigService.Maps.TryGetValue(Entity.MapId, out var mapCfg);
                    var array = mapCfg?.Anlei;
                    if (array == null || array.Length == 0) return;

                    // 进入战斗
                    var grain = GrainFactory.GetGrain<IShenShouJiangLinGrain>(Entity.ServerId);
                    _ = grain.OnBattleStart(TeamId);
                    // 随机一个怪物组
                    var group = array[Random.Next(0, array.Length)];
                    // 10%几率遇到神兽
                    var canCatch = (Random.Next(100) + 1) <= 10;
                    _ = StartPve(0, group, BattleType.ShenShouJiangLin, canCatch);
                    _ssjl.NextTime = (uint)Random.Next(15, 31);
                }
            }

            // 更新数据
            Entity.MapX = x;
            Entity.MapY = y;

            if (IsTeamLeader)
            {
                if (!stop)
                {
                    // 让TeamGrain打包统一提交给MapGrain
                    _ = TeamGrain.UpdatePos(Entity.MapX, Entity.MapY, immediate);
                }
            }
            else
            {
                // 通知地图
                _ = _mapGrain.PlayerMove(OnlyId, x, y, immediate);
            }

            await Task.CompletedTask;
        }

        private async Task ReqStopMove(C2S_MapStop req)
        {
            await ReqMove(req.X, req.Y, true);
            if (IsTeamLeader)
            {
                _ = TeamGrain.SetPathList(new Immutable<byte[]>(Packet.Serialize(req)));
            }
        }

        private Task ReqSubmitTalkNpcTask(C2S_TaskSubmitTalkNpc req)
        {
            return TaskMgr.SubmitTalkNpcTask(req.TaskId, req.TaskStep, req.OnlyId, req.CfgId);
        }

        private Task ReqSubmitGatherNpcTask(C2S_TaskSubmitGatherNpc req)
        {
            return TaskMgr.SubmitGatherNpcTask(req.OnlyId);
        }

        private Task ReqTriggerNpcBoomb(C2S_TaskTriggerNpcBomb req)
        {
            return TaskMgr.TriggerNpcBoomb(req.OnlyId, req.CfgId);
        }

        private Task ReqSubmitDoActionTask(C2S_TaskSubmitDoAction req)
        {
            return TaskMgr.SubmitDoActionTask(req.MapId, req.MapX, req.MapY);
        }

        private Task ReqInceptDailyTask(C2S_TaskInceptDaily req)
        {
            return TaskMgr.InceptDailyTask(req.Group, req.OnlyId);
        }

        private Task ReqInceptInstanceTask(C2S_TaskInceptInstance req)
        {
            return TaskMgr.InceptInstanceTask(req.TaskId, req.OnlyId);
        }

        private Task ReqAbortTask(uint taskId)
        {
            return TaskMgr.AbortTask(taskId);
        }

        private async Task ReqPetAdopt(C2S_PetAdopt req)
        {
            // 检查是否已经领取过了
            if (GetFlag(FlagType.AdoptPet)) return;
            // 只能是2个宠物
            if (req.Id != 1004 && req.Id != 1005) return;
            // 创建宠物
            await CreatePet(req.Id);
            // 创建成功了, 就标记为已领取过
            if (PetMgr.Pet != null)
            {
                SetFlag(FlagType.AdoptPet, true);
                // 防止自动入库失败，这里先把flag强行入库
                await DbService.Sql.Update<RoleEntity>()
                    .Where(it => it.Id == RoleId)
                    .Set(it => it.Flags, Entity.Flags)
                    .ExecuteAffrowsAsync();
            }

            // 发放物资
            if (AppOptions.AllItems)
            {
                // 赠送一套5阶仙器
                await Task.WhenAll(
                    EquipMgr.AddEquip(EquipCategory.Xian, 1, 5),
                    EquipMgr.AddEquip(EquipCategory.Xian, 2, 5),
                    EquipMgr.AddEquip(EquipCategory.Xian, 3, 5),
                    EquipMgr.AddEquip(EquipCategory.Xian, 4, 5),
                    EquipMgr.AddEquip(EquipCategory.Xian, 5, 5)
                );

                //  赠送所有翅膀
                {
                    foreach (var wingCfg in ConfigService.Wings.Values)
                    {
                        await EquipMgr.AddEquip(wingCfg, false);
                    }
                }

                // 赠送所有称号
                {
                    foreach (var titleId in ConfigService.Titles.Keys)
                    {
                        await TitleMgr.AddTitle(titleId);
                    }
                }
            }
            else
            {
                // 赠送一套神兵
                // await Task.WhenAll(
                //     EquipMgr.AddEquip(EquipCategory.Shen, 1, 1),
                //     EquipMgr.AddEquip(EquipCategory.Shen, 2, 1),
                //     EquipMgr.AddEquip(EquipCategory.Shen, 3, 1),
                //     EquipMgr.AddEquip(EquipCategory.Shen, 4, 1),
                //     EquipMgr.AddEquip(EquipCategory.Shen, 5, 1)
                // );

                //  赠送一个蝉翼翅膀
                // {
                //     ConfigService.Wings.TryGetValue(5001, out var cfg);
                //     if (cfg != null)
                //     {
                //         await EquipMgr.AddEquip(cfg, false);
                //     }
                // }

                // 赠送相伴15载
                {
                    var xxx = await TitleMgr.AddTitle(105);
                    // 默认穿戴相伴十五载
                    if (xxx != null)
                    {
                        await ReqChangeTitle(new C2S_TitleChange { Active = true, Id = xxx.Id });
                    }
                }
            }
        }

        // 修改伙伴的出战和休息
        private Task ReqPartnerPos(uint id, uint pos)
        {
            if (CheckSafeLocked()) return Task.CompletedTask;
            return PartnerMgr.ActivePartner(id, pos);
        }

        // 传功
        private Task ReqPartnerExchange(C2S_PartnerExchange req)
        {
            if (CheckSafeLocked()) return Task.CompletedTask;
            return PartnerMgr.ExchangePartner(req.Id1, req.Id2, req.Cost);
        }

        // 转生
        private Task ReqPartnerRelive(C2S_PartnerRelive req)
        {
            if (CheckSafeLocked()) return Task.CompletedTask;
            return PartnerMgr.RelivePartner(req.Id);
        }

        // 创建队伍
        private async Task ReqCreateTeam(TeamTarget target)
        {
            if (InTeam)
            {
                SendNotice("您已经加入其它队伍，请刷新");
                return;
            }

            if (_inPrison)
            {
                SendNotice("天牢中无法创建队伍");
                return;
            }

            if (await IsSignedSinglePk())
            {
                SendNotice("当前已报名比武大会，无法创建队伍");
                return;
            }

            if (Entity.MapId == 5001) target = TeamTarget.SectWar;
            else if (target == TeamTarget.SectWar) target = TeamTarget.Unkown;

            if (target == TeamTarget.SectWar && (Entity.SectId == 0 || _sectWarCamp == 0))
            {
                SendNotice("请先进入帮战");
                return;
            }

            var takeRet = false;
            try
            {
                TeamId = await ServerGrain.CreateTeam((uint)target);
                TeamLeader = RoleId;
                TeamGrain = GrainFactory.GetGrain<ITeamGrain>($"{Entity.ServerId}_{TeamId}");
                await TeamGrain.StartUp();
                // 接管该队伍, 上传自己的当前的地图信息和角色+伙伴信息
                var req = new TakeOverTeamRequest
                {
                    Target = target,
                    MapId = Entity.MapId,
                    MapX = Entity.MapX,
                    MapY = Entity.MapY
                };
                if (req.Target == TeamTarget.SectWar) req.SectId = Entity.SectId;
                req.List.AddRange(BuildTeamMembers(req.Target != TeamTarget.SectWar));
                takeRet = await TeamGrain.TakeOver(new Immutable<byte[]>(Packet.Serialize(req)));
                // 通知地图服务, 由于我刚创建，所以肯定是1
                TeamMemberCount = 1;
                _ = _mapGrain.SetPlayerTeam(OnlyId, TeamId, TeamLeader, TeamMemberCount);
            }
            finally
            {
                if (!takeRet)
                {
                    if (TeamId > 0)
                    {
                        try
                        {
                            await ServerGrain.DeleteTeam(TeamId);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    TeamId = 0;
                    TeamLeader = 0;
                    TeamGrain = null;
                }
            }
        }

        private Task ReqExitTeam()
        {
            if (!InTeam)
            {
                SendNotice("您不在任何队伍中");
                return Task.CompletedTask;
            }

            if (InBattle)
            {
                SendNotice("战斗中不允许离队");
                return Task.CompletedTask;
            }

            _ = TeamGrain.Exit(RoleId);
            return Task.CompletedTask;
        }

        private async Task ReqListTeam(TeamTarget target, int pageIndex)
        {
            var respBytes = await ServerGrain.QueryTeams((byte)target, pageIndex, TeamId);
            var resp = S2C_TeamList.Parser.ParseFrom(respBytes.Value);
            _ = SendPacket(GameCmd.S2CTeamList, resp);
        }

        private async Task ReqFindAndJoinTeam(TeamTarget target)
        {
            if (CheckSafeLocked()) return;
            if (InTeam)
            {
                SendNotice("您已经加入其它队伍，请刷新");
                return;
            }

            var respBytes = await ServerGrain.QueryTeams((byte)target, 1, TeamId);
            var resp = S2C_TeamList.Parser.ParseFrom(respBytes.Value);
            if (resp?.List == null || resp.List.Count == 0)
            {
                SendNotice("没有找到匹配的队伍");
                return;
            }

            var teamData = resp.List[Random.Next(0, resp.List.Count)];
            await ReqJoinTeam(teamData.Id);
        }

        private async Task ReqFindTeam(uint teamId)
        {
            var respBytes = await ServerGrain.QueryTeam(teamId);
            if (respBytes.Value == null)
            {
                await SendPacket(GameCmd.S2CTeamFind, new S2C_TeamFind());
                return;
            }

            var td = TeamData.Parser.ParseFrom(respBytes.Value);
            // 过滤掉伙伴数据
            for (var i = td.Members.Count - 1; i >= 0; i--)
            {
                if (td.Members[i].Type != TeamObjectType.Player)
                {
                    td.Members.RemoveAt(i);
                }
            }

            await SendPacket(GameCmd.S2CTeamFind, new S2C_TeamFind { Data = td });
        }

        private async Task ReqTeamRecruit(C2S_TeamRecruit req)
        {
            if (!IsTeamLeader)
            {
                SendNotice("请先创建队伍");
                return;
            }

            // 检查队伍是否为帮战目标, 帮战目标只能发布到帮派频道
            var teamTarget = (TeamTarget)(await TeamGrain.GetTarget());
            if (teamTarget == TeamTarget.SectWar)
            {
                req.Type = ChatMessageType.Sect;
            }

            if (string.IsNullOrWhiteSpace(req.Msg))
                req.Msg = "招募队员, 速来!";

            // 在帮派频道广播聊天消息
            var chatReq = new C2S_Chat
            {
                Type = req.Type,
                Msg = req.Msg,
                TeamId = TeamId,
                Extras = { req.Extras }
            };
            if (req.Type != ChatMessageType.Sect) req.Type = ChatMessageType.World;

            await ReqChat(chatReq);
        }

        private async Task ReqTeamTarget(C2S_TeamTarget req)
        {
            if (!IsTeamLeader)
            {
                SendNotice("请先创建队伍");
                return;
            }

            _ = TeamGrain.ChangeTarget(RoleId, (byte)req.Target);
            await Task.CompletedTask;
        }

        private async Task ReqTeamLeader()
        {
            if (!InTeam)
            {
                SendNotice("你不在队伍中");
                return;
            }

            if (IsTeamLeader)
            {
                SendNotice("你已经是队长");
                return;
            }

            if (InBattle)
            {
                SendNotice("战斗中不允许申请队长");
                return;
            }
            // 队长在战场上，不允许申请
            var leaderGrain = GrainFactory.GetGrain<IPlayerGrain>(TeamLeader);
            if (leaderGrain != null)
            {
                if (await leaderGrain.IsInBattle())
                {
                    SendNotice("队长观战中不允许申请队长");
                    return;
                }
            }
            else
            {
                LogError("申请队长失败");
                return;
            }

            _ = TeamGrain.ApplyLeader(RoleId);
            await Task.CompletedTask;
        }

        private async Task ReqHandleTeamLeader(C2S_TeamHandleApplyLeader req)
        {
            if (RoleId == req.RoleId || req.Agree) return;
            if (!InTeam)
            {
                SendNotice("你不在队伍中");
                return;
            }
            // 自己在战场上，不允许统一交接
            if (InBattle)
            {
                SendNotice("观战中，不允许更换队长");
                return;
            }
            _ = TeamGrain.HandleApplyLeader(RoleId, req.RoleId, req.Agree);
            await Task.CompletedTask;
        }

        private async Task ReqJoinTeam(uint teamId)
        {
            if (InTeam)
            {
                SendNotice("您已经加入其它队伍，请刷新");
                return;
            }

            if (InBattle)
            {
                SendNotice("战斗中不允许加入队伍");
                return;
            }

            // 如果已报名单人PK
            if (await IsSignedSinglePk())
            {
                SendNotice("已报名比武大会，不允许加入队伍");
                return;
            }

            // 检测team是否存在
            var exists = await ServerGrain.ExistsTeam(teamId);
            if (!exists)
            {
                SendNotice("队伍已解散，请选择其他队伍");
                return;
            }

            var req = new TeamApplyJoinData
            {
                RoleId = RoleId,
                RoleName = Entity.NickName,
                CfgId = Entity.CfgId,
                Relive = Entity.Relive,
                Level = Entity.Level,
                SectId = Entity.SectId,
                SectWarCamp = _sectWarCamp
            };
            var grain = GrainFactory.GetGrain<ITeamGrain>($"{Entity.ServerId}_{teamId}");
            _ = grain.ApplyJoin(new Immutable<byte[]>(Packet.Serialize(req)));
        }

        private async Task ReqHandleTeamJoinApply(uint applyId, bool agree)
        {
            if (!InTeam) return;
            if (InBattle && agree)
            {
                SendNotice("战斗中不允许处理入队申请");
                return;
            }

            _ = TeamGrain.HandleJoinApply(applyId, agree);
            await Task.CompletedTask;
        }

        private async Task ReqKickoutTeamPlayer(uint roleId)
        {
            if (!IsTeamLeader) return;
            if (InBattle)
            {
                SendNotice("战斗中不允许踢除成员");
                return;
            }

            _ = TeamGrain.Kickout(RoleId, roleId);
            await Task.CompletedTask;
        }

        private async Task ReqInvitePlayerJoinTeam(uint roleId)
        {
            if (InBattle)
            {
                SendNotice("战斗中不允许邀请入队");
                return;
            }

            // 检查是否在线
            var ret = await GlobalGrain.CheckPlayer(roleId);
            if (!ret)
            {
                SendNotice("对方不在线, 无法接受邀请");
                return;
            }

            _ = TeamGrain.InvitePlayer(RoleId, roleId);
        }

        private async Task ReqHandleTeamInvite(uint teamId, bool agree)
        {
            if (InTeam && agree)
            {
                SendNotice("已经在队伍中");
                return;
            }

            // 检查队伍是否存在
            var ret = await ServerGrain.ExistsTeam(teamId);
            if (agree && !ret)
            {
                SendNotice("队伍不存在或已解散");
                return;
            }

            if (agree && InBattle)
            {
                SendNotice("你正在战斗中，暂时不能入队！");
                return;
            }

            if (agree && await IsSignedSinglePk())
            {
                SendNotice("你已报名比武大会，暂时不能入队！");
                return;
            }

            if (ret)
            {
                var grain = GrainFactory.GetGrain<ITeamGrain>($"{Entity.ServerId}_{teamId}");
                _ = grain.HandleInvite(RoleId, agree, Entity.SectId, _sectWarCamp);
            }
        }

        /// <summary>
        /// 主动申请添加roleId为好友
        /// </summary>
        private async Task ReqAddFriend(uint roleId)
        {
            if (CheckSafeLocked()) return;
            if (RoleId == roleId) return;

            // 不能超过50个
            if (_friendList.Count >= GameDefine.FriendMaxNum)
            {
                SendNotice("好友数已达上限，无法添加好友");
                return;
            }

            // 检查用户id是否存在
            var ret = await RedisService.ExistsRole(roleId);
            if (!ret)
            {
                SendNotice("玩家不存在");
                return;
            }

            // 检查是否已经是自己的好友
            ret = _friendList.Exists(p => p.Id == roleId);
            if (ret)
            {
                SendNotice("申请失败，已经是好友");
                return;
            }

            // 检查对方是否已经好友上限
            var fn = await RedisService.GetFriendNum(roleId);
            if (fn >= GameDefine.FriendMaxNum)
            {
                SendNotice("申请失败，对方好友数已达上限");
                return;
            }

            ret = await RedisService.IsFriendApplyed(RoleId, roleId);
            if (ret)
            {
                SendNotice("申请失败，已经是好友或已申请");
                return;
            }

            // 检查目标是否在线
            ret = await GlobalGrain.CheckPlayer(roleId);
            if (ret)
            {
                // 通知好友处理申请
                var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                _ = grain.OnFriendApply(new Immutable<byte[]>(Packet.Serialize(BuildRoleInfo())));
            }
            else
            {
                // 在他的好友申请中插入一条记录
                await RedisService.AddFriendApply(roleId, RoleId);
            }

            SendNotice("申请成功，等待对方的处理");
        }

        private async Task ReqDelFriend(uint roleId)
        {
            if (CheckSafeLocked()) return;
            if (RoleId == roleId) return;
            // 检查是否是我的好友
            var idx = _friendList.FindIndex(p => p.Id == roleId);
            if (idx < 0)
            {
                SendNotice("不是好友, 不能删除");
                return;
            }

            var ret = await RedisService.DelFriend(RoleId, roleId);
            if (!ret)
            {
                SendNotice("删除好友出错");
                return;
            }

            _friendList.RemoveAt(idx);
            await SendPacket(GameCmd.S2CFriendDel, new S2C_FriendDel { RoleId = roleId });

            // 如果我的好友在线，通知他处理
            ret = await GlobalGrain.CheckPlayer(roleId);
            if (ret)
            {
                var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                _ = grain.OnFriendDel(RoleId);
            }
        }

        private async Task ReqHandleFriendApply(uint roleId, bool agree)
        {
            if (CheckSafeLocked()) return;
            if (roleId == RoleId) return;
            // 检查是否在我的申请列表中
            var idx = _friendApplyList.FindIndex(p => p.Id == roleId);
            if (idx < 0) return;
            // 移除请求
            _friendApplyList.RemoveAt(idx);
            await RedisService.DelFriendApply(RoleId, roleId);
            // 通知前端删除请求
            await SendPacket(GameCmd.S2CFriendApplyDel, new S2C_FriendApplyDel { RoleId = roleId });

            // 建交
            if (agree)
            {
                var ret = await RedisService.AddFriend(RoleId, roleId);
                if (!ret)
                {
                    SendNotice("添加好友出错");
                    return;
                }

                // 获取好友信息
                var entity = await RedisService.GetRoleInfo(roleId);
                var info = new RoleInfo
                {
                    Id = entity.Id,
                    Name = entity.NickName,
                    CfgId = entity.CfgId,
                    Relive = entity.Relive,
                    Level = entity.Level
                };
                _friendList.Add(info);
                // 推送新建立的好友
                await SendPacket(GameCmd.S2CFriendAdd, new S2C_FriendAdd
                {
                    Data = info
                });

                // 如果申请者在线，也推送给他
                ret = await GlobalGrain.CheckPlayer(roleId);
                if (ret)
                {
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                    _ = grain.OnFriendAdd(new Immutable<byte[]>(Packet.Serialize(BuildRoleInfo())));
                }
            }
        }

        private async Task ReqSearchRole(uint pageIndex, string search)
        {
            if (pageIndex < 1) pageIndex = 1;
            // 角色id是从100000开始的
            uint.TryParse(search, out var rid);
            if (rid < 100000) rid = 0;

            // 如果搜索的是我自己
            if (rid == RoleId)
            {
                return;
            }

            // 5s间隔, 防止高频请求
            var now = TimeUtil.TimeStamp;
            if (_lastSearchRoleTime > 0 && now - _lastSearchRoleTime < 5)
            {
                SendNotice("搜索玩家过于频繁, 请5秒钟后再试");
                return;
            }

            var resp = new S2C_FriendSearch { PageIndex = pageIndex, Search = search };
            if (rid > 0)
            {
                // 相同区服下的玩家才可以查找
                var resultEntity = await DbService.Sql.Queryable<RoleEntity>()
                    .Where(it => it.Id == rid && it.ServerId == Entity.ServerId).FirstAsync();
                if (resultEntity != null)
                {
                    if (await GlobalGrain.CheckPlayer(rid))
                    {
                        var bytes = await GrainFactory.GetGrain<IPlayerGrain>(rid).QueryRoleInfo();
                        var info2 = RoleInfo.Parser.ParseFrom(bytes.Value);
                        resp.List.Add(info2);
                    }
                    else
                    {
                        var rinfo = new RoleInfo
                        {
                            Id = resultEntity.Id,
                            Name = resultEntity.NickName,
                            CfgId = resultEntity.CfgId,
                            Relive = resultEntity.Relive,
                            Level = resultEntity.Level,
                        };
                        // 解析皮肤
                        var _o = Json.Deserialize<Dictionary<string, List<int>>>(resultEntity.Skins);
                        var _use = _o.GetValueOrDefault("use", new List<int>());
                        foreach (var i in _use)
                        {
                            var cfg = ConfigService.SkinConfigs.GetValueOrDefault(i, null);
                            if (cfg != null && (cfg.index == 6 || cfg.index == 8))
                            {
                                rinfo.Skins.Add(i);
                            }
                        }
                        resp.List.Add(rinfo);
                    }
                }

                resp.Total = (uint)resp.List.Count;
            }
            else if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                // 模糊匹配，分页查询
                var rows = await DbService.Sql.Queryable<RoleEntity>()
                    .Where(it => it.ServerId == Entity.ServerId && it.NickName.Contains(search))
                    .Count(out var total)
                    .Page((int)pageIndex, 10)
                    .ToListAsync();
                foreach (var r in rows)
                {
                    var rinfo = new RoleInfo
                    {
                        Id = r.Id,
                        Name = r.NickName,
                        CfgId = r.CfgId,
                        Relive = r.Relive,
                        Level = r.Level,
                    };
                    // 解析皮肤
                    var _o = Json.Deserialize<Dictionary<string, List<int>>>(r.Skins);
                    var _use = _o.GetValueOrDefault("use", new List<int>());
                    foreach (var i in _use)
                    {
                        var cfg = ConfigService.SkinConfigs.GetValueOrDefault(i, null);
                        if (cfg != null && (cfg.index == 6 || cfg.index == 8))
                        {
                            rinfo.Skins.Add(i);
                        }
                    }
                    resp.List.Add(rinfo);
                }
                    // .ToListAsync(it => new RoleInfo
                    // {
                    //     Id = it.Id,
                    //     Name = it.NickName,
                    //     CfgId = it.CfgId,
                    //     Relive = it.Relive,
                    //     Level = it.Level
                    // });

                resp.Total = (uint)total;
                // resp.List.AddRange(rows);
            }

            await SendPacket(GameCmd.S2CFriendSearch, resp);
            _lastSearchRoleTime = now;
        }

        private async Task ReqFriendInfo(uint roleId)
        {
            // 如果搜索的是我自己
            if (roleId == RoleId) return;

            var resp = new S2C_FriendInfo();
            if (await GlobalGrain.CheckPlayer(roleId))
            {
                var bytes = await GrainFactory.GetGrain<IPlayerGrain>(roleId).QueryRoleInfo();
                resp.Data = RoleInfo.Parser.ParseFrom(bytes.Value);
            }
            else
            {
                var entity = await RedisService.GetRoleInfo(roleId);
                if (entity != null)
                {
                    resp.Data = new RoleInfo
                    {
                        Id = roleId,
                        Name = entity.NickName,
                        Relive = entity.Relive,
                        Level = entity.Level,
                        CfgId = entity.CfgId
                    };
                }
            }

            await SendPacket(GameCmd.S2CFriendInfo, resp);
        }

        private async Task ReqSectList(string search, int pageIndex)
        {
            var respBytes = await ServerGrain.QuerySects(search, pageIndex);
            if (respBytes.Value != null)
            {
                var resp = S2C_SectList.Parser.ParseFrom(respBytes.Value);
                await SendPacket(GameCmd.S2CSectList, resp);
            }
        }

        private async Task ReqCreateSect(string name, string desc)
        {
            if (CheckSafeLocked()) return;

            if (InSect)
            {
                SendNotice("已经加入帮派");
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                SendNotice("请输入帮派名");
                return;
            }

            // 检查充值额度 (真实的充值额度要大于198)
            // if (!IsGm && Entity.TotalPayBS < 198*2 + 1000)
            if (!IsGm && Entity.TotalPayBS < 1000 + 1000)
            {
                SendNotice("充值额度不足1000");
                return;
            }

            // 检查仙玉
            const uint costJade = 200000U;
            if (!CheckMoney(MoneyType.Jade, costJade)) return;

            name = name.Trim();
            if (name.Length > 10)
            {
                SendNotice("帮派名不允许超过10个字");
                return;
            }

            if (TextFilter.HasDirty(name))
            {
                SendNotice("帮派名中包含非法字符");
                return;
            }

            // 检查帮派名是否非法
            var ret = TextFilter.CheckLimitWord(name);
            if (!ret)
            {
                SendNotice("帮派名包含非法字符");
                return;
            }

            // 宗旨
            if (desc == null)
            {
                desc = string.Empty;
            }
            else
            {
                if (desc.Length > 100)
                {
                    SendNotice("帮派宗旨不允许超过100个字");
                    return;
                }

                ret = TextFilter.CheckLimitWord(desc);
                if (!ret)
                {
                    SendNotice("帮派宗旨包含非法字符");
                    return;
                }

                desc = TextFilter.Filte(desc);
            }

            // 1个区服中帮派的数量不能超过10个
            // var sectNum = await ServerGrain.QuerySectNum();
            // if (sectNum >= 10)
            // {
            //     SendNotice("帮派数量超过10个");
            //     return;
            // }

            // 检查帮派名是否存在
            ret = await DbService.ExistsSect(name);
            if (ret)
            {
                SendNotice("帮派已经存在，请换个名字");
                return;
            }

            // 插入数据库
            var sectEntity = new SectEntity
            {
                ServerId = Entity.ServerId,
                Name = name,
                Desc = desc,
                OwnerId = RoleId,
                MemberNum = 1,
                Contrib = 0,
                CreateTime = TimeUtil.TimeStamp
            };
            await DbService.InsertEntity(sectEntity);
            if (sectEntity.Id == 0)
            {
                SendNotice("创建帮派失败");
                return;
            }

            // 扣款
            await CostMoney(MoneyType.Jade, costJade, tag: "创建帮派消耗");

            // SectGrain启动的时候就要获取所有成员, 所以这里立即将自己的SectId入库
            Entity.SectId = sectEntity.Id;
            Entity.SectContrib = 0;
            Entity.SectJob = (byte)SectMemberType.BangZhu;
            Entity.SectJoinTime = TimeUtil.TimeStamp;
            // 防止tick入库，对LastEntity进行拷贝赋值
            LastEntity.SectId = Entity.SectId;
            LastEntity.SectContrib = Entity.SectContrib;
            LastEntity.SectJob = Entity.SectJob;
            LastEntity.SectJoinTime = Entity.SectJoinTime;

            ret = await DbService.UpdateRoleSect(RoleId, Entity.SectId, Entity.SectContrib, Entity.SectJob,
                Entity.SectJoinTime);
            if (!ret)
            {
                // 恢复数据
                Entity.SectId = 0;
                Entity.SectContrib = 0;
                Entity.SectJob = 0;
                Entity.SectJoinTime = 0;
                LastEntity.SectId = Entity.SectId;
                LastEntity.SectContrib = Entity.SectContrib;
                LastEntity.SectJob = Entity.SectJob;
                LastEntity.SectJoinTime = Entity.SectJoinTime;

                SendNotice("创建帮派失败");
                return;
            }

            SectGrain = GrainFactory.GetGrain<ISectGrain>(Entity.SectId);
            await SectGrain.StartUp();
            // 激活SectGrain并传递自己的信息, 等待SectGrain回调OnEnterSect
            _ = SectGrain.Join(new Immutable<byte[]>(Packet.Serialize(BuildSectMemberData())));
        }

        private async Task ReqJoinSect(uint sectId)
        {
            if (CheckSafeLocked()) return;

            if (InSect)
            {
                SendNotice("已经加入帮派");
                return;
            }

            if (sectId == 0)
            {
                // 一键加入帮派
                sectId = await ServerGrain.FindRandomSect();
                if (sectId == 0)
                {
                    SendNotice("暂无帮派,赶紧创建一个吧");
                    return;
                }
            }
            else
            {
                var exists = await ServerGrain.ExistsSect(sectId);
                if (!exists)
                {
                    SendNotice("帮派不存在");
                    return;
                }
            }

            var req = new SectApplyJoinData
            {
                RoleId = RoleId,
                RoleName = Entity.NickName,
                CfgId = Entity.CfgId,
                Relive = Entity.Relive,
                Level = Entity.Level
            };
            var grain = GrainFactory.GetGrain<ISectGrain>(sectId);
            _ = grain.ApplyJoin(new Immutable<byte[]>(Packet.Serialize(req)));
        }

        private async Task ReqContribSect(uint jade)
        {
            if (CheckSafeLocked()) return;

            // jade最低1000，只能是整千
            if (jade < 1000 || jade % 1000 != 0) return;
            // 检测自己的jade是否足够
            if (!CheckMoney(MoneyType.Jade, jade)) return;

            var ret = await SectGrain.Contrib(RoleId, jade);
            if (ret)
            {
                await CostMoney(MoneyType.Jade, jade);
                Entity.SectContrib += jade;
                Entity.Contrib += jade;
            }

            SendNotice($"帮派贡献+{jade}");
        }

        private async Task ReqSectAppoint(uint roleId, SectMemberType type)
        {
            if (type < SectMemberType.BangZhu || type > SectMemberType.BangZhong) return;

            if (!InSect)
            {
                SendNotice("请先加入一个帮派");
                return;
            }

            if (CheckSafeLocked()) return;

            var error = await SectGrain.Appoint(RoleId, roleId, (byte)type);
            if (!string.IsNullOrWhiteSpace(error))
                SendNotice(error);
        }

        private async Task ReqSectSilent(uint roleId)
        {
            if (!InSect)
            {
                SendNotice("请先加入一个帮派");
                return;
            }

            if (CheckSafeLocked()) return;

            var error = await SectGrain.Silent(RoleId, roleId);
            if (!string.IsNullOrWhiteSpace(error))
            {
                SendNotice(error);
                return;
            }

            await SendPacket(GameCmd.S2CSectSilent, new S2C_SectSilent
            {
                RoleId = roleId,
                Silent = true,
                OpRoleId = RoleId,
                OpName = Entity.NickName,
                OpJob = (SectMemberType)Entity.SectJob
            });
        }

        private async Task ReqSectChangeDesc(string desc)
        {
            if (!InSect)
            {
                SendNotice("请先加入一个帮派");
                return;
            }

            if (CheckSafeLocked()) return;

            desc ??= "";
            if (desc.Length > 100)
            {
                SendNotice("帮派宗旨不允许超过100个字");
                return;
            }

            var ret = TextFilter.CheckLimitWord(desc);
            if (!ret)
            {
                SendNotice("帮派宗旨包含非法字符");
                return;
            }

            desc = TextFilter.Filte(desc);

            var error = await SectGrain.ChangeDesc(RoleId, desc);
            if (!string.IsNullOrWhiteSpace(error))
            {
                SendNotice(error);
            }
            else
            {
                await SendPacket(GameCmd.S2CSectChangeDesc, new S2C_SectChangeDesc
                {
                    Id = Entity.SectId,
                    Desc = desc
                });
            }
        }

        public static bool CheckBanWord(string text)
        {
            var numberList = ConfigService.BanChat.numberList;
            var wordList = ConfigService.BanChat.wordList;
            if (numberList != null)
            {
                var numCnt = 0;
                foreach (var c in text)
                {
                    if (numberList.Contains(c.ToString()))
                    {
                        // 超过XX个 封号
                        numCnt++;
                        if (numCnt >= ConfigService.BanChat.numberLimit)
                        {
                            return true;
                        }
                        // return true;
                    }
                }
            }
            if (wordList != null)
            {
                text = Regex.Replace(text, @"\s", "");
                if (wordList.Any(text.Contains)) return true;
            }
            return false;
        }

        private async Task ReqDanMu(C2S_Chat req)
        {
            if (string.IsNullOrWhiteSpace(req.Msg) && req.Voice.IsEmpty) return;

            if (CheckSafeLocked()) return;

            // 必须在战场
            if (!InBattle)
            {
                SendNotice("参战/观战才能发弹幕");
                return;
            }
            // 弹幕频率限制
            var now = TimeUtil.TimeStamp;
            if (now - _lastDanMuTimestamp < GameDefine.DanMuSendDelayLimit)
            {
                SendNotice($"两次弹幕时间间隔必须大于{GameDefine.DanMuSendDelayLimit}秒");
                return;
            }
            _lastDanMuTimestamp = now;

            // 检测到直接封号
            if (!IsGm)
            {
                if (CheckBanWord(req.Msg))
                {
                    await ReqChatFrozeSelf();
                    return;
                }
            }

            // 检查非法字符, GM不过滤
            if (!IsGm && !TextFilter.CheckLimitWord(req.Msg))
            {
                SendNotice("消息中包含非法字符");
                return;
            }
            // 消息长度检查
            if (!IsGm && req.Msg != null)
            {
                if (req.Type == ChatMessageType.Bell)
                {
                    if (req.Msg.Length > 45)
                    {
                        SendNotice("消息过长");
                        return;
                    }
                }
                else
                {
                    if (req.Msg.Length > 20)
                    {
                        SendNotice("消息过长");
                        return;
                    }
                }
            }
            // 充值金额检查
            if (Entity.TotalPayBS < 100)
            {
                SendNotice("充值金额达到100元，才能弹幕");
                return;
            }
            // 构造消息
            var msg = new ChatMessage
            {
                Type = req.Type,
                // GM的消息不过滤
                Msg = IsGm ? req.Msg : TextFilter.Filte(req.Msg),
                Voice = req.Voice,
                From = BuildRoleInfo(),
                To = req.ToRoleId,
                Extras = { req.Extras }
            };
            if (_battleGrain != null)
            {
                _ = _battleGrain.SendDanMu(RoleId, new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
            }
            if (_battleGrainWatched != null)
            {
                _ = _battleGrainWatched.SendDanMu(RoleId, new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
            }
        }

        private async Task ReqChat(C2S_Chat req)
        {
            if (string.IsNullOrWhiteSpace(req.Msg) && req.Voice.IsEmpty) return;
            if (req.ToRoleId == RoleId) return;

            if (CheckSafeLocked()) return;

            // 检测到直接封号
            if (!IsGm)
            {
                if (CheckBanWord(req.Msg))
                {
                    await ReqChatFrozeSelf();
                    return;
                }
            }

            // 检查非法字符, GM不过滤
            if (!IsGm && !TextFilter.CheckLimitWord(req.Msg))
            {
                SendNotice("消息中包含非法字符");
                return;
            }

            if (!IsGm && req.Msg != null)
            {
                if (req.Type == ChatMessageType.Bell)
                {
                    if (req.Msg.Length > 45)
                    {
                        SendNotice("消息过长");
                        return;
                    }
                }
                else
                {
                    if (req.Msg.Length > 20)
                    {
                        SendNotice("消息过长");
                        return;
                    }
                }
                //消息10分钟之内连续发10次，直接封号
                var now = TimeUtil.TimeStamp;
                if (req.Msg == _lastChatStr && now - _lastChatTime < 60 * 10 ) {
                    _lastChatRepeatTimes++;
                    if (_lastChatRepeatTimes > 11 ) {
                        await ReqChatFrozeSelf();
                        return;
                    }
                } else {
                    _lastChatStr = req.Msg;
                    _lastChatTime = now;
                    _lastChatRepeatTimes = 1;
                }
            }

            var msg = new ChatMessage
            {
                Type = req.Type,
                // GM的消息不过滤
                Msg = IsGm ? req.Msg : TextFilter.Filte(req.Msg),
                Voice = req.Voice,
                From = BuildRoleInfo(),
                To = req.ToRoleId,
                Extras = { req.Extras }
            };
            if (req.Type == ChatMessageType.Bell)
            {
                msg.BellTimes = 3;
            }

            if (req.TeamId > 0) msg.TeamId = req.TeamId;

            Equip shareEquip = null;
            Ornament shareOrnament = null;
            Pet sharePet = null;
            PetOrnament sharePetOrnament = null;
            if (req.ShareEquip is { Id: > 0 })
            {
                // 检查一下装备是否为我自己的
                shareEquip = EquipMgr.FindEquip(req.ShareEquip.Id);
                if (shareEquip == null)
                {
                    SendNotice("装备不存在");
                    return;
                }
            }
            else if (req.ShareOrnament is { Id: > 0 })
            {
                shareOrnament = EquipMgr.FindOrnament(req.ShareOrnament.Id);
                if (shareOrnament == null)
                {
                    SendNotice("配饰不存在");
                    return;
                }
            }
            else if (req.SharePet is { Id: > 0 })
            {
                sharePet = PetMgr.FindPet(req.SharePet.Id);
                if (sharePet == null)
                {
                    SendNotice("宠物不存在");
                    return;
                }
            }
            else if (req.SharePetOrnament is { Id: > 0 })
            {
                sharePetOrnament = EquipMgr.FindPetOrnament(req.SharePetOrnament.Id);
                if (sharePetOrnament == null)
                {
                    SendNotice("宠物配饰不存在");
                    return;
                }
            }


            switch (req.Type)
            {
                case ChatMessageType.Friend:
                {
                    // 好友
                    if (!_friendList.Exists(p => p.Id == req.ToRoleId))
                    {
                        SendNotice("发送失败，不是我的好友");
                        return;
                    }

                    break;
                }
                case ChatMessageType.Stranger:
                {
                    // 检查该角色是否在线
                    var exists = await GlobalGrain.CheckPlayer(req.ToRoleId);
                    if (!exists)
                    {
                        SendNotice("发送失败，目标角色未上线");
                        return;
                    }
                    if (Entity.TotalPayBS < 30)
                    {
                        SendNotice("充值金额不足30元");
                        return;
                    }
                    // 陌生人
                    if (_friendList.Exists(p => p.Id == req.ToRoleId))
                    {
                        msg.Type = ChatMessageType.Friend;
                    }

                    break;
                }
                case ChatMessageType.World:
                {
                    if (GetFlag(FlagType.WorldSilent))
                    {
                        SendNotice("你已被禁言, 发送失败");
                        return;
                    }

                    if (Entity.Type != UserType.Gm)
                    {
                        // 1、1转120并且充值30元
                        // 2、充值98
                        // var x = Entity.TotalPayBS >= 98;
                        // if (!x)
                        // {
                        //     x = (Entity.Relive > 1 || Entity.Relive == 1 && Entity.Level >= 120) &&
                        //         Entity.TotalPayBS >= 30;
                        // }
                        var len = 0;
                        for (int mk = 0; mk < req.Msg.Length; mk++)
                        {
                            if (req.Msg[mk] >= '0' && req.Msg[mk] <= '9')
                            {
                                len++;
                            }
                        }

                        if (len > 4)
                        {
                            SendNotice("内容数字超过上限");
                            return;
                        }

                        //zyj-fix
                        //上线免开口费，能直接世界发言  上线如果没有首充，只能发4个字
                        //var x = Entity.TotalPayBS >= 1120;
                        //if (!x)
                        //{
                        //  if(req.Msg.Length > 4)
                        //  {
                        //      SendNotice("充值额度不足");
                        //      return;
                        //  }
                        //}

                        if (Entity.Level < 100)
                        {
                            SendNotice("等级100才可开启聊天");
                            return;
                        }


                            // 每10s钟才能发一次世界聊天
                            var now = TimeUtil.TimeStamp;
                        if (now - _lastWorldChatTime < 10)
                        {
                            SendNotice("聊天间隔不足10秒");
                            return;
                        }

                        // 消耗3000银币
                        //var ret = await CostMoney(MoneyType.Silver, 3000, tag: "发送世界聊天");
                        //if (!ret) return;
                    }

                    _lastWorldChatTime = TimeUtil.TimeStamp;
                    msg.To = 0;
                }
                    break;
                case ChatMessageType.Team:
                {
                    if (!InTeam)
                    {
                        SendNotice("请先加入队伍");
                        return;
                    }

                    msg.To = 0;
                    msg.TeamId = TeamId;
                }
                    break;
                // 帮派聊天
                case ChatMessageType.Sect:
                {
                    if (!InSect)
                    {
                        SendNotice("请先加入帮派");
                        return;
                    }

                    if (GetFlag(FlagType.SectSilent))
                    {
                        SendNotice("你已被禁言, 发送失败");
                        return;
                    }

                     if (Entity.Type != UserType.Gm)
                     {
                         // 充值30
                         if (Entity.TotalPayBS < 30)
                         {
                             SendNotice("充值金额不足30元");
                             return;
                         }
                     }

                    msg.To = 0;
                    msg.SectId = Entity.SectId;
                    msg.SectJob = (SectMemberType)Entity.SectJob;
                }
                    break;
                case ChatMessageType.Bell:
                {
                    if (!IsGm)
                    {
                        // 世界铃铛限制3转120以上玩家才可以使用
                        if (Entity.Relive < 3)
                        {
                            SendNotice("需要3转120才能使用世界铃铛");
                            return;
                        }

                        // 每30s钟才能发一次铃铛
                        if (TimeUtil.TimeStamp - _lastBellChatTime < 30)
                        {
                            SendNotice("使用铃铛的时间间隔不足30秒");
                            return;
                        }
                    }

                    var ret = await AddBagItem(10205, -1, tag: "世界铃铛");
                    if (!ret) return;

                    _lastBellChatTime = TimeUtil.TimeStamp;
                }
                    break;
                default:
                    return;
            }
            _ = ServerGrain.RecordChatMsg(RoleId, msg.To, (byte)req.Type, msg.Msg, TimeUtil.TimeStamp);
            switch (req.Type)
            {
                case ChatMessageType.Friend:
                case ChatMessageType.Stranger:
                {
                    // 转播给自己
                    await SendPacket(GameCmd.S2CChat, new S2C_Chat { Msg = msg });
                    // 转播给好友
                    var ret = await GlobalGrain.CheckPlayer(req.ToRoleId);
                    if (ret)
                    {
                        var grain = GrainFactory.GetGrain<IPlayerGrain>(req.ToRoleId);
                        _ = grain.OnRecvChat(new Immutable<byte[]>(Packet.Serialize(msg)));
                    }
                }
                    break;
                case ChatMessageType.World:
                {
                    _ = ServerGrain.Broadcast(
                        new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
                }
                    break;
                case ChatMessageType.Team:
                {
                    _ = TeamGrain.Broadcast(
                        new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
                }
                    break;
                case ChatMessageType.Sect:
                {
                    _ = SectGrain.Broadcast(
                        new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
                }
                    break;
                case ChatMessageType.Bell:
                {
                    _ = ServerGrain.Broadcast(
                        new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
                }
                    break;
            }

            if (shareEquip != null)
            {
                await shareEquip.Cache();
            }

            if (shareOrnament != null)
            {
                await shareOrnament.Cache();
            }

            if (sharePet != null)
            {
                await sharePet.Cache();
            }

            if (sharePetOrnament != null)
            {
                await sharePetOrnament.Cache();
            }
        }

        private async Task ReqChatSilent(uint roleId)
        {
            // 检查角色id是否存在
            var ret = await RedisService.ExistsRole(roleId);
            if (!ret)
            {
                SendNotice("玩家不存在");
                return;
            }
            // 检查自己是否是GM
            if (Entity.Type != UserType.Gm)
            {
                SendNotice("权限不足");
                return;
            }
            var grain = FindPlayer(roleId);
            if (grain == null)
            {
                SendNotice("玩家不存在");
                return;
            }
            if (!await grain.GmSetRoleFlag((byte)FlagType.WorldSilent, true))
            {
                SendNotice("禁言失败--世界");
                return;
            }
            if (!await grain.GmSetRoleFlag((byte)FlagType.SectSilent, true))
            {
                SendNotice("禁言失败--帮派");
                return;
            }
            SendNotice("禁言成功");
        }

        // 自己封号自己 todo
        private async Task ReqChatFrozeSelf()
        {
            // 删除用户的token
            await RedisService.DelUserToken(Entity.UserId);
            // 玩家在线，强制下线
            await Shutdown();
            // 更新数据库
            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.Online, false)
                .Set(it => it.Status, RoleStatus.Frozen)
                .ExecuteAffrowsAsync();
        }

        //todo
        private async Task ReqChatFroze(uint roleId)
        {
            // 检查自己是否是GM
            if (Entity.Type != UserType.Gm)
            {
                SendNotice("权限不足");
                return;
            }

            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync(it => new
                {
                    it.UserId,
                    it.ParentId,
                    it.Status
                });
            if (role == null)
            {
                SendNotice("角色不存在");
                return;
            }
            if (role.Status == RoleStatus.Frozen) {
                SendNotice("角色已被封禁");
                return;
            }
            // 删除用户的token
            await RedisService.DelUserToken(role.UserId);
            // 玩家在线，强制下线
            if (await GrainFactory.GetGrain<IGlobalGrain>(0).CheckPlayer(roleId))
            {
                var player = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                if (player != null)
                {
                    await player.Shutdown();
                }
            }
            // 更新数据库
            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == roleId)
                .Set(it => it.Online, false)
                .Set(it => it.Status, RoleStatus.Frozen)
                .ExecuteAffrowsAsync();
            SendNotice("封禁成功");
        }

        private async Task ReqLevelRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔5s才重新刷新数据
            if (_lastRankLevelResp == null || TimeUtil.TimeStamp - _lastFetchLevelRankTime > 5)
            {
                var list = await RedisService.GetRoleLevelRank(Entity.ServerId, (int)pageIndex);
                var myRank = await RedisService.GetRoleLevelRankIndex(Entity.ServerId, Entity.Id);
                _lastRankLevelResp = new S2C_RankLevel
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchLevelRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankLevel, _lastRankLevelResp);
        }

        private async Task ReqJadeRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔3s才重新刷新数据
            if (_lastRankJadeResp == null || TimeUtil.TimeStamp - _lastFetchJadeRankTime > 5)
            {
                var list = await RedisService.GetRoleJadeRank(Entity.ServerId, (int)pageIndex);
                var myRank = await RedisService.GetRoleJadeRankIndex(Entity.ServerId, Entity.Id);
                _lastRankJadeResp = new S2C_RankJade
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchJadeRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankJade, _lastRankJadeResp);
        }

        private async Task ReqPayRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔3s才重新刷新数据
            if (_lastRankPayResp == null || TimeUtil.TimeStamp - _lastFetchPayRankTime > 5)
            {
                var list = await RedisService.GetRolePayRank(Entity.ServerId, (int)pageIndex);
                // 不下发金额
                foreach (var item in list)
                {
                    item.Pay = 0;
                }

                var myRank = await RedisService.GetRolePayRankIndex(Entity.ServerId, Entity.Id);
                _lastRankPayResp = new S2C_RankPay
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchPayRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankPay, _lastRankPayResp);
        }

        private async Task ReqSldhRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔3s才重新刷新数据
            if (_lastRankSldhResp == null || TimeUtil.TimeStamp - _lastFetchSldhRankTime > 5)
            {
                var list = await RedisService.GetRoleSldhRank(Entity.ServerId, (int)pageIndex);
                var myRank = await RedisService.GetRoleSldhRankIndex(Entity.ServerId, Entity.Id);
                _lastRankSldhResp = new S2C_RankSldh
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchSldhRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankSldh, _lastRankSldhResp);
        }
        
        private async Task ReqWzzzRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔3s才重新刷新数据
            if (_lastRankWzzzResp == null || TimeUtil.TimeStamp - _lastFetchWzzzRankTime > 5)
            {
                var list = await RedisService.GetRoleWzzzRank(Entity.ServerId, (int)pageIndex);
                var myRank = await RedisService.GetRoleWzzzRankIndex(Entity.ServerId, Entity.Id);
                _lastRankWzzzResp = new S2C_RankWzzz
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchWzzzRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankWzzz, _lastRankWzzzResp);
        }

        private async Task ReqSectRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔3s才重新刷新数据
            if (_lastRankSectResp == null || TimeUtil.TimeStamp - _lastFetchSectRankTime > 5)
            {
                var respBytes = await ServerGrain.GetSectRank((int)pageIndex);
                if (respBytes.Value != null)
                {
                    _lastRankSectResp = S2C_RankSect.Parser.ParseFrom(respBytes.Value);
                    _lastFetchSectRankTime = TimeUtil.TimeStamp;
                }
            }

            await SendPacket(GameCmd.S2CRankSect, _lastRankSectResp);
        }

        private async Task ReqSinglePkRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔3s才重新刷新数据
            if (_lastRankSinglePkResp == null || TimeUtil.TimeStamp - _lastFetchSinglePkRankTime > 5)
            {
                var list = await RedisService.GetRoleSinglePkRank(Entity.ServerId, (int)pageIndex);
                var myRank = await RedisService.GetRoleSinglePkRankIndex(Entity.ServerId, Entity.Id);
                _lastRankSinglePkResp = new S2C_RankSinglePk
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchSinglePkRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankSinglePk, _lastRankSinglePkResp);
        }

        private async Task ReqDaLuanDouRank(uint pageIndex)
        {
            // 防止恶意请求, 间隔3s才重新刷新数据
            if (_lastRankDaLuanDouResp == null || TimeUtil.TimeStamp - _lastFetchDaLuanDouRankTime > 5)
            {
                var list = await RedisService.GetRoleDaLuanDouRank(Entity.ServerId, (int)pageIndex);
                var myRank = await RedisService.GetRoleDaLuanDouRankIndex(Entity.ServerId, Entity.Id);
                _lastRankDaLuanDouResp = new S2C_RankDaLuanDou
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchDaLuanDouRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankDaLuanDou, _lastRankDaLuanDouResp);
        }

        private async Task ReqChangeTitle(C2S_TitleChange req)
        {
            await TitleMgr.ActiveTitle(req.Id, req.Active);
        }

        private async Task ReqChangeName(C2S_ChangeName req)
        {
            if (string.IsNullOrWhiteSpace(req.Nickname) || req.Nickname.Length > 8 || req.Nickname.Length < 2)
            {
                SendNotice("请输入2-8个字符的昵称");
                return;
            }

            var nickname = req.Nickname.Trim();

            // 相同的话直接返回成功
            if (nickname.Equals(Entity.NickName))
            {
                SendNotice("请输入不同的昵称");
                return;
            }

            if (!IsGm && !TextFilter.CheckLimitWord(nickname))
            {
                SendNotice("昵称中包含非法字符");
                return;
            }

            // 检测是否为脏词
            if (TextFilter.HasDirty(nickname))
            {
                SendNotice("昵称中包含非法字符");
                return;
            }

            // 检查货币
            if (!CheckMoney(MoneyType.Jade, 8888)) return;

            // 通过插入数据库来判断是否成功
            var ret = await DbService.UpdateRoleName(Entity.Id, nickname);
            if (!ret)
            {
                SendNotice("昵称已存在");
                return;
            }

            // 已经修改了数据库, 不要引起后续的自动更新
            Entity.NickName = nickname;
            LastEntity.NickName = nickname;
            await RedisService.SetRoleName(Entity);

            await CostMoney(MoneyType.Jade, 8888, tag: "角色改名");

            // 通知地图
            _ = _mapGrain.SetPlayerName(OnlyId, nickname);
            // 通知队伍
            if (InTeam) _ = TeamGrain.SetPlayerName(RoleId, nickname);
            // 通知帮派
            if (InSect) _ = SectGrain.SetPlayerName(RoleId, nickname);

            await SendPacket(GameCmd.S2CChangeName, new S2C_ChangeName
            {
                Nickname = nickname
            });
        }

        private async Task ReqChangeColor(uint index1, uint index2)
        {
            ConfigService.RoleColors.TryGetValue(Entity.CfgId, out var cfg);
            if (cfg == null)
            {
                SendNotice("该角色不支持变色");
                return;
            }

            var changed = false;

            if (index1 != Entity.Color1)
            {
                if (index1 != 0)
                {
                    cfg.TryGetValue($"color1_{index1}", out var colorCfg1);
                    if (colorCfg1 == null)
                    {
                        SendNotice("颜色1不存在");
                        return;
                    }

                    // 扣除道具
                    var ret = await AddBagItem(colorCfg1.ItemId, -(int)colorCfg1.Count, tag: "染色");
                    if (!ret) return;
                }

                Entity.Color1 = index1;
                changed = true;
            }

            if (index2 != Entity.Color2)
            {
                if (index2 != 0)
                {
                    cfg.TryGetValue($"color2_{index2}", out var colorCfg2);
                    if (colorCfg2 == null)
                    {
                        SendNotice("颜色2不存在");
                        return;
                    }

                    // 扣除道具
                    var ret = await AddBagItem(colorCfg2.ItemId, -(int)colorCfg2.Count, tag: "染色");
                    if (!ret) return;
                }

                Entity.Color2 = index2;
                changed = true;
            }

            if (changed)
            {
                SendNotice("染色成功");
                await SendPacket(GameCmd.S2CChangeColor, new S2C_ChangeColor
                {
                    Color1 = index1,
                    Color2 = index2
                });
                // 同步给地图
                _ = _mapGrain.SetPlayerColor(OnlyId, Entity.Color1, Entity.Color2);
            }
        }

        private async Task ReqChangeRace(uint cfgId)
        {
            ConfigService.Roles.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return;

            var roles = ConfigService.GetRolesCanBeUsed(Entity.Relive);
            if (!roles.Contains(cfgId))
            {
                SendNotice("无法使用该角色");
                return;
            }

            // 需要1000仙玉
            var ret = await CostMoney(MoneyType.Jade, 1000, tag: "换种族消耗");
            if (!ret) return;

            var oldRace = Entity.Race;
            Entity.CfgId = cfgId;
            Entity.Race = cfg.Race;
            Entity.Sex = cfg.Sex;
            await RedisService.SetRoleCfgId(Entity);

            // 技能换掉，但是技能经验保持, 技能id可能会变换, 所以按索引来, key为索引
            var skillExps = new Dictionary<uint, uint>();
            foreach (var sk in _skills)
            {
                skillExps[sk.Idx] = sk.Exp;
            }

            Entity.Skills = string.Empty;
            InitSkills();
            foreach (var sk in _skills)
            {
                skillExps.TryGetValue(sk.Idx, out var exp);
                sk.Exp = exp;
            }

            // 同步给Entity, 准备入库
            SyncSkills();

            // 脱下方案所有的装备
            foreach (var scheme in SchemeMgr.All)
            {
                await scheme.Reset(false);
            }

            // 重新生成坐骑
            if (oldRace != Entity.Race)
            {
                MountMgr.OnRaceChanged();

                // 切换自动技能
                await ReqAutoSkill(
                    new C2S_AutoSkill { Skill = (uint)SkillId.NormalAtk, AutoSync = Entity.AutoSyncSkill });
            }

            // 通知前端
            await SendPacket(GameCmd.S2CChangeRace, new S2C_ChangeRace
            {
                CfgId = Entity.CfgId,
                Race = Entity.Race,
                Sex = Entity.Sex,
                Skills = { _skills }
            });

            // 通知地图
            _ = _mapGrain.SetPlayerCfgId(OnlyId, Entity.CfgId);
            // 通知队伍
            if (InTeam) _ = TeamGrain.SetPlayerCfgId(RoleId, Entity.CfgId);
            // 通知帮派
            if (InSect) _ = SectGrain.SetPlayerCfgId(RoleId, Entity.CfgId);
        }

        private async Task ReqRelive(uint cfgId)
        {
            ConfigService.Roles.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return;

            var roles = ConfigService.GetRolesCanBeUsed((byte)(Entity.Relive + 1));
            if (!roles.Contains(cfgId))
            {
                SendNotice("无法使用该角色");
                return;
            }

            var nextRelive = (byte)(Entity.Relive + 1);
            if (nextRelive > 4)
            {
                SendNotice("已到最高转生等级");
                return;
            }

            var minLevel = ConfigService.GetRoleMinLevel(nextRelive);
            if (Entity.Level < minLevel)
            {
                SendNotice("等级不足");
                return;
            }

            // 需要100仙玉
            // var ret = await CostMoney(MoneyType.Jade, 100, tag: "转生消耗");
            // if (!ret) return;

            // 记录转生前的种族和性别
            Relives.Add(new ReliveRecord { Race = (Race)Entity.Race, Sex = (Sex)Entity.Sex });
            SyncRelives();

            var oldRace = Entity.Race;
            var oldExp = Entity.Exp;
            Entity.CfgId = cfgId;
            Entity.Race = cfg.Race;
            Entity.Sex = cfg.Sex;
            Entity.Relive = nextRelive;
            Entity.Level = ConfigService.GetRoleMinLevel(Entity.Relive);
            Entity.Exp = 0;
            ExpMax = ConfigService.GetRoleUpgradeExp(Entity.Relive, Entity.Level);
            // 之前的经验有用
            await AddExp(oldExp);
            await RedisService.SetRoleInfo(Entity);

            // 技能换掉，但是技能经验保持
            var skillExps = new Dictionary<uint, uint>();
            foreach (var sk in _skills)
            {
                skillExps[sk.Idx] = sk.Exp;
            }

            Entity.Skills = string.Empty;
            InitSkills();
            foreach (var sk in _skills)
            {
                skillExps.TryGetValue(sk.Idx, out var exp);
                sk.Exp = exp;
            }

            // 同步给Entity, 准备入库
            SyncSkills();

            // 脱下方案所有的装备
            foreach (var scheme in SchemeMgr.All)
            {
                await scheme.Reset(true);
            }

            // 重新生成坐骑
            if (oldRace != Entity.Race)
            {
                MountMgr.OnRaceChanged();

                // 切换自动技能
                await ReqAutoSkill(
                    new C2S_AutoSkill { Skill = (uint)SkillId.NormalAtk, AutoSync = Entity.AutoSyncSkill });
            }

            // 通知前端
            await SendPacket(GameCmd.S2CRelive, new S2C_Relive
            {
                CfgId = Entity.CfgId,
                Race = Entity.Race,
                Sex = Entity.Sex,
                Skills = { _skills },
                Relive = Entity.Relive,
                Level = Entity.Level,
                Exp = Entity.Exp,
                ExpMax = ExpMax
            });

            // 通知地图
            if (_mapGrain != null)
            {
                _ = _mapGrain.SetPlayerLevel(OnlyId, Entity.Relive, Entity.Level);
                _ = _mapGrain.SetPlayerCfgId(OnlyId, Entity.CfgId);
            }

            // 通知队伍
            if (InTeam)
            {
                _ = TeamGrain.SetPlayerLevel(RoleId, Entity.Relive, Entity.Level);
                _ = TeamGrain.SetPlayerCfgId(RoleId, Entity.CfgId);
            }

            // 通知帮派
            if (InSect)
            {
                _ = SectGrain.SetPlayerLevel(RoleId, Entity.Relive, Entity.Level);
                _ = SectGrain.SetPlayerCfgId(RoleId, Entity.CfgId);
            }
        }

        private async Task GetLevelReward(byte level)
        {
            if (!_rewards.ContainsKey(level))
            {
                SendNotice("等级未达到");
                return;
            }

            var val = _rewards[level];
            if (val > 0)
            {
                SendNotice("奖励已领取");
                return;
            }

            ConfigService.LevelRewards.TryGetValue(level, out var cfg);
            if (cfg?.Rewards == null || cfg.Rewards.Num == 0)
            {
                SendNotice("该等级没有奖励");
                return;
            }

            ConfigService.Items.TryGetValue(cfg.Rewards.Item, out var itemCfg);
            if (itemCfg == null)
            {
                SendNotice("奖励配置错误");
                return;
            }

            // 检查背包
            if (IsBagFull)
            {
                SendNotice("背包已满");
                return;
            }

            var ret = await AddItem(cfg.Rewards.Item, (int)cfg.Rewards.Num, tag: "等级奖励领取");
            if (!ret)
            {
                SendNotice("领取奖励失败");
                return;
            }

            _rewards[level] = 1;
            SyncRewards();
            await SendLevelRewardList();
        }

        private async Task GetXinShouGift()
        {
            if (GetFlag(FlagType.XinShouGift))
            {
                SendNotice("已经领取过");
                return;
            }

            SetFlag(FlagType.XinShouGift, true);

            await AddMoney(MoneyType.Jade, 20000000, "新手礼包");
            await AddItem(90061, 1, tag: "新手礼包"); //浪淘沙
			await AddItem(9905, 1, tag: "新手礼包"); //累计重置券
			await AddItem(9904, 10, tag: "新手礼包"); //随机变身卡
            await EquipMgr.AddEquip(EquipCategory.Xian, 1, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 2, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 3, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 4, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 5, 1);
        }

        //获取公益好服礼包
        private async Task GetGongYiHaoFuGift()
        {
            // 检查是否已经领取过了
            if (GetFlag(FlagType.GongYiHaoFuGift)) return;
            SetFlag(FlagType.GongYiHaoFuGift, true);
            // 防止自动入库失败，这里先把flag强行入库
            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.Flags, Entity.Flags)
                .ExecuteAffrowsAsync();

            //一阶仙器一套
            await EquipMgr.AddEquip(EquipCategory.Xian, 1, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 2, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 3, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 4, 1);
            await EquipMgr.AddEquip(EquipCategory.Xian, 5, 1);

            //蝉翼翅膀一个
            {
                ConfigService.Wings.TryGetValue(5001, out var cfg);
                if (cfg != null)
                {
                    await EquipMgr.AddEquip(cfg, false);
                }
            }
            await AddItem(90061, 1, tag: "公益好服"); //浪淘沙一只
			await AddItem(9904, 10, tag: "公益好服"); //随机变身卡10张
            //仙玉2千万
            await AddMoney(MoneyType.Jade, 20000000, "公益好服");
            await AddMoney(MoneyType.BindJade, 3000, "公益好服");
            // 送1000充值
            {
            //var payRate = await RedisService.GetPayRateJade();
            await OnPayedBindJade(1000, 1000000, 0, false);
            }
        }

        private async Task CheckXinShouGift()
        {
            await SendPacket(GameCmd.S2CXinShouGiftCheck, new S2C_XinShouGiftCheck
            {
                Valid = !GetFlag(FlagType.XinShouGift)
            });
        }

        private async Task UpgradeXlLevel(uint add)
        {
            if (add <= 0) return;

            var maxLevel = RoleRefine.GetMaxRefineLevel(Entity.Relive);
            if (Entity.XlLevel + add > maxLevel)
            {
                SendNotice("超过修炼等级上限");
                return;
            }

            uint totalContrib = 1;
            for (var i = 0; i < add; i++)
            {
                totalContrib += RoleRefine.GetContribPrice((uint)(Entity.XlLevel + i));
            }

            var ret = await CostMoney(MoneyType.Contrib, totalContrib, tag: "升级修炼等级消耗");
            if (!ret) return;

            Entity.XlLevel += add;
            await SendPacket(GameCmd.S2CUpgradeXlLevel, new S2C_UpgradeXlLevel
            {
                XlLevel = Entity.XlLevel
            });
        }

        // 设置/修改 安全锁密码
        private async Task ReqSafePassword(string password, string oldPassword)
        {
            if (!string.IsNullOrWhiteSpace(Entity.SafeCode) && !Entity.SafeCode.Equals(oldPassword))
            {
                SendNotice("旧密码错误");
                return;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Trim().Length < 4 ||
                password.Trim().Length > 12)
            {
                SendNotice("密码长度限定4-12个字符");
                return;
            }

            Entity.SafeCode = password;
            await SendPacket(GameCmd.S2CSafePassword, new S2C_SafePassword { LockSeted = true });
        }

        private async Task ReqSafeLock(bool safeLock, string password)
        {
            if (safeLock == Entity.SafeLocked) return;

            if (string.IsNullOrWhiteSpace(Entity.SafeCode))
            {
                SendNotice("请先设置安全锁密码");
                return;
            }

            if (!Entity.SafeCode.Equals(password))
            {
                SendNotice("密码错误");
                return;
            }

            Entity.SafeLocked = safeLock;
            await SendPacket(GameCmd.S2CSafeLock, new S2C_SafeLock { Lock = Entity.SafeLocked });
        }

        private async Task ReqBindSpread(uint roleId)
        {
            if (CheckSafeLocked()) return;
            if (roleId == 0 || roleId == RoleId) return;
            if (Entity.Spread > 0)
            {
                SendNotice("已经绑定过推广人, 不能再次绑定");
                return;
            }

            // 检查roleId是否存在, 是否和自己在同一个区服
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync(it => new
                {
                    it.ServerId, it.Status, it.ParentId
                });

            if (role == null)
            {
                SendNotice("角色不存在");
                return;
            }

            if (role.ServerId != Entity.ServerId)
            {
                SendNotice("待绑定的角色和你不在同一区服");
                return;
            }

            if (role.ParentId != Entity.ParentId)
            {
                SendNotice("待绑定的角色和你不属于同一个代理");
                return;
            }

            if (role.Status != RoleStatus.Normal)
            {
                SendNotice("待绑定的角色已被冻结");
                return;
            }

            Entity.Spread = roleId;
            Entity.SpreadTime = TimeUtil.TimeStamp;
            LastEntity.Spread = Entity.Spread;
            LastEntity.SpreadTime = Entity.SpreadTime;
            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.Spread, Entity.Spread)
                .Set(it => it.SpreadTime, Entity.SpreadTime)
                .ExecuteAffrowsAsync();

            await SendPacket(GameCmd.S2CBindSpread, new S2C_BindSpread
            {
                RoleId = roleId,
                Time = Entity.SpreadTime
            });
        }

        private async Task ReqSpreads(C2S_Spreads req)
        {
            var now = TimeUtil.TimeStamp;
            if (_lastSpreadsTime > 0 && now - _lastSpreadsTime < 10)
            {
                return;
            }

            _lastSpreadsTime = now;
            if (req.PageIndex <= 0) req.PageIndex = 1;

            var ids = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Spread == RoleId)
                .Count(out var total)
                .Page(req.PageIndex, 15)
                .ToListAsync(it => it.Id);

            var resp = new S2C_Spreads { PageIndex = req.PageIndex, Total = (int)total };
            foreach (var id in ids)
            {
                var entity = await RedisService.GetRoleInfo(id);
                if (entity != null && entity.NickName != null)
                {
                    resp.List.Add(new RoleInfo
                    {
                        Id = entity.Id,
                        CfgId = entity.CfgId,
                        Name = entity.NickName,
                        Level = entity.Level,
                        Relive = entity.Relive
                    });
                }
            }

            await SendPacket(GameCmd.S2CSpreads, resp);

            // 以发送完后为准, 上面那个时间不要删掉, 防止获取数据过程中报错
            _lastSpreadsTime = TimeUtil.TimeStamp;
        }

        private async Task ReqAutoSkill(C2S_AutoSkill req)
        {
            // 检查skill是否合法
            var skId = (SkillId)req.Skill;
            if (skId == 0) skId = SkillId.NormalAtk;

            // 检查是否已注册，是否为主动技能
            var skInfo = SkillManager.GetSkill(skId);
            if (skInfo is not { ActionType: SkillActionType.Initiative }) return;

            if (skId != SkillId.NormalAtk && skId != SkillId.NormalDef &&
                !_skills.Exists(p => p.Id == (SkillId)req.Skill)) return;

            Entity.AutoSkill = (uint)skId;
            Entity.AutoSyncSkill = req.AutoSync;

            await SendPacket(GameCmd.S2CAutoSkill, new S2C_AutoSkill
            {
                Skill = (uint)skId,
                AutoSync = req.AutoSync
            });
        }

        private async Task ReqOrder(C2S_Order req)
        {
            if (!CheckSafeLocked() && req.Money != 0)
            {
                if (req.Money > 10000)
                {
                    SendNotice("单笔充值金额限额");
                }
                else if (req.Sdk == 1)
                {
                    // 积分充值
                    await XinOrderBindJade((PayType)req.PayType, req.Money);
                    //if (req.ChargeType == 4)
                    //{
                    //    await XinOrderBindJade((PayType)req.PayType, req.Money);
                    //}
                    //// 仙玉充值
                    //else
                    //{
                    //    // await XinOrder((PayType)req.PayType, req.Money);
                    //    await XinOrderJade((PayType)req.PayType, req.Money);
                    //}
                }
                else
                {
                    SendNotice("不支持的SDK");
                }
            }
        }

        // 进入充值页
        private async Task ReqEnterChargeUI()
        {
            var resp = new S2C_EnterChargeUI() { JadeRate = await RedisService.GetPayRateJade(), BindJadeRate = await RedisService.GetPayRateBindJade(), ChargeType = GameDefine.ChargeType };
            resp.AmountList4Jade.AddRange(ConfigService.ChargeItemConfig.jade);
            resp.AmountList4BindJade.AddRange(ConfigService.ChargeItemConfig.bindJade);
            await SendPacket(GameCmd.S2CEnterChargeUi, resp);
        }
        // 物品商店--进入
        private async Task ReqEnterItemShop()
        {
            var resp = new S2C_EnterItemShop() { GoodList = { ConfigService.ItemShopGoods.Values.ToList() } };
            await SendPacket(GameCmd.S2CEnterItemShop, resp);
        }
        // 物品商店--下单购买   ->这里修改为云鼎支付
        private async Task ReqOrderItemShop(uint id, PayType payType, uint count)
        {
            if (CheckSafeLocked()) return;
            var good = ConfigService.ItemShopGoods.GetValueOrDefault(id, null);
            if (good == null || ConfigService.Items.GetValueOrDefault(good.Item, null) == null)
            {
                var giftGood = ConfigService.GiftShopGoods.GetValueOrDefault(id, null);
                if (giftGood != null) {
                    await ReqOrderGiftShop(giftGood, payType);
                    return;
                }
                SendNotice("商品已售罄");
                return;
            }
            if (string.IsNullOrWhiteSpace(_xinPayOptions.BindJadeNotifyUrl))
            {
                SendNotice("下单失败");
                return;
            }
            if (string.IsNullOrWhiteSpace(_xinPayOptions.ReturnUrl))
            {
                SendNotice("下单失败");
                return;
            }
            string payTypeStr = XinPayUtil.GetPayType(payType);
            string payTypeUrl = XinPayUtil.GetPayType2(payType);
            if (string.IsNullOrWhiteSpace(payTypeStr) || string.IsNullOrWhiteSpace(payTypeUrl))
            {
                SendNotice("暂不支持该支付方式");
                return;
            }
#if false
            if (good.Price < 30 || good.Price > 5000)
            {
                SendNotice("金额异常，请稍候再试");
                return;
            }
#endif
            if (count <= 0) {
                SendNotice($"购买数量{count}...异常");
                return;
            }
            uint amount = good.Price * count;
            IBaseRepository<PayEntity> repository = DbService.Sql.GetRepository<PayEntity>();
            PayEntity entity = new PayEntity
            {
                Rid = RoleId,
                Money = amount,
                Jade = 0,
                BindJade = 0,
                PayChannel = PayChannel.Xin,
                PayType = payType,
                Remark = Json.SafeSerialize(new PayRemark
                {
                    type = 100,
                    content = Json.SafeSerialize(new JItemShopGood()
                    {
                        id = good.Id,
                        item = good.Item,
                        num = good.Num * count,
                        price = good.Price
                    })
                }),
                Order = String.Format("{0}{1}", TimeUtil.MilliTimeStamp, RoleId),
                Status = OrderStatus.Created,
                CreateTime = TimeUtil.TimeStamp,
                UpdateTime = 0u,
                DelivTime = 0u
            };
            await repository.InsertAsync(entity);
            if (entity.Id == 0)
            {
                SendNotice("下单失败, 请再次尝试");
                return;
            }
            //string orderId = _xinPayOptions.OrderPrefix + entity.Id;
            string orderdatetime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string url = XinPayUtil.YunDingOrder(_xinPayOptions.MemberId, entity.Order, amount.ToString(), orderdatetime, payTypeStr, payTypeUrl,
             _xinPayOptions.BindJadeNotifyUrl, _xinPayOptions.ReturnUrl, _xinPayOptions.SignMd5Key, RoleId);
            var resp = new S2C_OrderItemShop() { Url = url };
            await SendPacket(GameCmd.S2COrderItemShop, resp);
        }
        
        private async Task ReqOrderGiftShop(GiftShopGood giftGood, PayType payType)
        {
            if (CheckSafeLocked()) return;
            if (giftGood == null) {
                return;
            }
            if (string.IsNullOrWhiteSpace(_xinPayOptions.BindJadeNotifyUrl))
            {
                SendNotice("下单失败");
                return;
            }
            if (string.IsNullOrWhiteSpace(_xinPayOptions.ReturnUrl))
            {
                SendNotice("下单失败");
                return;
            }
            string payTypeStr = XinPayUtil.GetPayType(payType);
            string payTypeUrl = XinPayUtil.GetPayType2(payType);
            if (string.IsNullOrWhiteSpace(payTypeStr) || string.IsNullOrWhiteSpace(payTypeUrl))
            {
                SendNotice("暂不支持该支付方式");
                return;
            }
            uint amount = giftGood.price;
            IBaseRepository<PayEntity> repository = DbService.Sql.GetRepository<PayEntity>();
            PayEntity entity = new PayEntity
            {
                Rid = RoleId,
                Money = amount,
                Jade = 0,
                BindJade = 0,
                PayChannel = PayChannel.Xin,
                PayType = payType,
                Remark = Json.SafeSerialize(new PayRemark
                {
                    type = 1000,
                    content = Json.SafeSerialize(new GiftShopGood()
                    {
                        id = giftGood.id,
                        group = giftGood.group,
                        order = giftGood.order,
                        items = giftGood.items,
                        price = giftGood.price
                    })
                }),
                Order = String.Format("{0}{1}", TimeUtil.MilliTimeStamp, RoleId),
                Status = OrderStatus.Created,
                CreateTime = TimeUtil.TimeStamp,
                UpdateTime = 0u,
                DelivTime = 0u
            };
            await repository.InsertAsync(entity);
            if (entity.Id == 0)
            {
                SendNotice("下单失败, 请再次尝试");
                return;
            }
            //string orderId = _xinPayOptions.OrderPrefix + entity.Id;
            string orderdatetime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string url = XinPayUtil.YunDingOrder(_xinPayOptions.MemberId, entity.Order, amount.ToString(), orderdatetime, payTypeStr, payTypeUrl,
             _xinPayOptions.BindJadeNotifyUrl, _xinPayOptions.ReturnUrl, _xinPayOptions.SignMd5Key, RoleId);
            var resp = new S2C_OrderItemShop() { Url = url };
            await SendPacket(GameCmd.S2COrderItemShop, resp);
        }
        
        private async Task XinOrderBindJade(PayType payType, uint money)
        {
            if (string.IsNullOrWhiteSpace(_xinPayOptions.BindJadeNotifyUrl))
            {
                SendNotice("下单失败");
                return;
            }
            if (string.IsNullOrWhiteSpace(_xinPayOptions.ReturnUrl))
            {
                SendNotice("下单失败");
                return;
            }
            string payTypeStr = XinPayUtil.GetPayType(payType);
            string payTypeUrl = XinPayUtil.GetPayType2(payType);
            if (string.IsNullOrWhiteSpace(payTypeStr) || string.IsNullOrWhiteSpace(payTypeUrl))
            {
                SendNotice("暂不支持该支付方式");
                return;
            }
            if (money < 30 || money > 10000)
            {
                SendNotice("金额异常");
                return;
            }
            uint jade = 0;
            uint bindjade = 0;
            var cfg = ConfigService.ChargeItemConfig;
            for (int i=0; i<cfg.jade.Count; i++)
            {
                if (cfg.jade[i] == money)
                {
                    jade = cfg.bindJade[i];
                    bindjade = cfg.jade[i] * 100;
                }
            }
            //修改为双倍积分
            // jade *= GameDefine.BindJadeMulti;  
            // bindjade *= GameDefine.BindJadeMulti;  
            IBaseRepository<PayEntity> repository = DbService.Sql.GetRepository<PayEntity>();
            PayEntity entity = new PayEntity
            {
                Rid = RoleId,
                Money = money,
                Jade = jade,
                BindJade = bindjade,
                PayChannel = PayChannel.Xin,
                PayType = payType,
                Remark = "",
                Order = String.Format("{0}{1}", TimeUtil.MilliTimeStamp, RoleId),
                Status = OrderStatus.Created,
                CreateTime = TimeUtil.TimeStamp,
                UpdateTime = 0u,
                DelivTime = 0u
            };
            await repository.InsertAsync(entity);
            if (entity.Id == 0)
            {
                SendNotice("下单失败, 请再次尝试");
                return;
            }
            uint amount = money;
            //uint money = amount * 10;
            //string orderId = _xinPayOptions.OrderPrefix + entity.Id;
            string orderdatetime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string url = XinPayUtil.YunDingOrder(_xinPayOptions.MemberId, entity.Order, amount.ToString(), orderdatetime, payTypeStr, payTypeUrl,
             _xinPayOptions.BindJadeNotifyUrl, _xinPayOptions.ReturnUrl, _xinPayOptions.SignMd5Key, RoleId);
            await SendPacket(GameCmd.S2COrder, new S2C_Order
            {
                Url = url
            });
        }

        private async Task ReqSkillUp(C2S_SkillUpdate req)
        {
            if (CheckSafeLocked()) return;
            var skData = _skills.FirstOrDefault(p => p.Id == req.Id);
            if (skData == null) return;
            // 判断是否到达经验上限
            var maxSkillLExp = ConfigService.GetRoleSkillMaxExp(Entity.Relive);
            if (skData.Exp >= maxSkillLExp) return;

            // 判断剩余银币是否足够
            var (cost1, cost2) = GameDefine.SkillUpgradeConsume[(int)MathF.Floor(skData.Exp / 100f)];
            var costMoney = skData.Idx % 2 == 0 ? cost1 : cost2;
            if (!CheckMoney(MoneyType.Silver, costMoney)) return;

            // 扣银币
            await CostMoney(MoneyType.Silver, costMoney, tag: "升级角色技能");

            // 技能的经验增加
            skData.Exp = Math.Clamp(skData.Exp + 100, 0, maxSkillLExp);
            UpdateSkillCost(skData);
            // 同步给Entity, 准备入库
            SyncSkills();

            // 下发给客户端
            await SendPacket(GameCmd.S2CSkillUpdate, new S2C_SkillUpdate { Data = skData });
        }

        private async Task ReqDelItem(IEnumerable<ItemData> list)
        {
            if (CheckSafeLocked()) return;
            var dic = new Dictionary<uint, uint>();
            foreach (var itemData in list)
            {
                if (GetBagItemNum(itemData.Id) < itemData.Num) continue;
                if (dic.ContainsKey(itemData.Id)) dic[itemData.Id] += itemData.Num;
                else dic[itemData.Id] = itemData.Num;
            }

            if (dic.Count == 0) return;

            foreach (var (k, v) in dic)
            {
                await AddBagItem(k, -(int)v, tag: "丢弃物品");
            }
        }

        private async Task ReqUseItem(uint itemId, uint num, uint target)
        {
            if (CheckSafeLocked()) return;
            // 检查配置表
            ConfigService.Items.TryGetValue(itemId, out var cfg);
            if (cfg == null) return;
            if (num == 0) num = 1;

            // 检查物品
            if (GetBagItemNum(itemId) < num)
            {
                SendNotice("物品不足");
                return;
            }

            await UseItem(cfg, num, target);
        }

        private async Task ReqComposeItem(uint parentId, uint num)
        {
            if (CheckSafeLocked()) return;
            if (num <= 0) return;
            // 检查是否能合成
            GameDefine.ItemComposes.TryGetValue(parentId, out var list);
            if (list == null || list.Count == 0) return;
            // 统计所需材料的总和
            var dic = new Dictionary<uint, uint>();
            foreach (var (k, v) in list)
            {
                if (dic.ContainsKey(k))
                    dic[k] += v * num;
                else
                    dic[k] = v * num;
            }

            // 检查材料是否足够
            foreach (var (k, v) in dic)
            {
                if (GetBagItemNum(k) < v)
                {
                    SendNotice("物品不足");
                    return;
                }
            }

            // 先扣材料
            foreach (var (k, v) in dic)
            {
                await AddBagItem(k, -(int)v, true, "物品合成消耗");
            }

            // 合成物品
            await AddBagItem(parentId, (int)num, true, "物品合成");
        }

        // 五行熔炼
        private async Task ReqFusionWuXing(IReadOnlyCollection<uint> items)
        {
            if (CheckSafeLocked()) return;
            if (items == null || items.Count != 5)
            {
                SendNotice("五行书数量不足");
                return;
            }

            // 只能是20001-20005 或者 20006-20010
            var isLevel1 = items.All(p => p is >= 20001 and <= 20005);
            var isLevel2 = items.All(p => p is >= 20006 and <= 20010);

            if (!isLevel1 && !isLevel2)
            {
                SendNotice("请提供正确的五行书");
                return;
            }

            // 检查背包中是否有足够的数量
            var dic = new Dictionary<uint, uint>();
            foreach (var id in items)
            {
                if (dic.ContainsKey(id))
                    dic[id] += 1;
                else
                    dic[id] = 1;
            }

            var enough = true;
            foreach (var (k, v) in dic)
            {
                if (GetBagItemNum(k) < v)
                {
                    enough = false;
                    break;
                }
            }

            if (!enough)
            {
                SendNotice("五行书数量不足");
                return;
            }

            // 随机高阶五行
            uint newItem;
            if (isLevel1)
            {
                newItem = (uint)Random.Next(20006, 20011);
            }
            else
            {
                newItem = (uint)Random.Next(20011, 20016);
            }

            if (newItem > 0)
            {
                // 扣除道具r
                foreach (var (k, v) in dic)
                {
                    await AddBagItem(k, -(int)v, tag: "五行熔炼消耗");
                }

                await AddBagItem(newItem, 1, tag: "五行熔炼获得");
            }
        }

        private async Task ReqUpdateRepo(bool isAdd, uint type, uint targetId)
        {
            if (CheckSafeLocked()) return;
            if (isAdd)
            {
                if (type == 1)
                {
                    // 把装备从背包放入仓库
                    var equip = EquipMgr.FindEquip(targetId);
                    if (equip == null)
                    {
                        SendNotice("装备不存在");
                        return;
                    }

                    if (equip.Place == EquipPlace.Repo)
                    {
                        SendNotice("装备已经在仓库内");
                        return;
                    }

                    if (IsRepoFull)
                    {
                        SendNotice("仓库已满");
                        return;
                    }

                    equip.Place = EquipPlace.Repo;
                }
                else if (type == 2)
                {
                    // 把配饰从背包放入仓库
                    var ornament = EquipMgr.FindOrnament(targetId);
                    if (ornament == null)
                    {
                        SendNotice("佩饰不存在");
                        return;
                    }

                    if (ornament.Place == EquipPlace.Repo)
                    {
                        SendNotice("配饰已经在仓库内");
                        return;
                    }

                    if (IsRepoFull)
                    {
                        SendNotice("仓库已满");
                        return;
                    }

                    ornament.Place = EquipPlace.Repo;
                }
                else
                {
                    // 把物品从背包放入仓库
                    Items.TryGetValue(targetId, out var bagNum);
                    if (bagNum == 0)
                    {
                        SendNotice("物品不存在");
                        return;
                    }

                    Repos.TryGetValue(targetId, out var repoNum);
                    if (repoNum == 0 && IsRepoFull)
                    {
                        SendNotice("仓库已满");
                        return;
                    }

                    repoNum += bagNum;
                    Repos[targetId] = repoNum;
                    Items.Remove(targetId);

                    SyncItems();
                    SyncRepos();
                }
            }
            else
            {
                if (type == 1)
                {
                    // 把装备从仓库放入背包
                    var equip = EquipMgr.FindEquip(targetId);
                    if (equip == null)
                    {
                        SendNotice("装备不存在");
                        return;
                    }

                    if (equip.Place != EquipPlace.Repo)
                    {
                        SendNotice("装备不在仓库内");
                        return;
                    }

                    if (IsBagFull)
                    {
                        SendNotice("背包已满");
                        return;
                    }

                    equip.Place = EquipPlace.Bag;
                    if (SchemeMgr.All.Exists(p => p.Equips.Contains(equip.Id))) equip.Place = EquipPlace.Wear;
                }
                else if (type == 2)
                {
                    // 把配饰从仓库放入背包
                    var ornament = EquipMgr.FindOrnament(targetId);
                    if (ornament == null)
                    {
                        SendNotice("配饰不存在");
                        return;
                    }

                    if (ornament.Place != EquipPlace.Repo)
                    {
                        SendNotice("配饰不在仓库内");
                        return;
                    }

                    if (IsBagFull)
                    {
                        SendNotice("背包已满");
                        return;
                    }

                    ornament.Place = EquipPlace.Bag;
                    if (SchemeMgr.All.Exists(p => p.Ornaments.Contains(ornament.Id))) ornament.Place = EquipPlace.Wear;
                }
                else
                {
                    // 把物品从仓库放入背包
                    Repos.TryGetValue(targetId, out var repoNum);
                    if (repoNum == 0)
                    {
                        SendNotice("物品不存在");
                        return;
                    }

                    Items.TryGetValue(targetId, out var bagNum);
                    if (bagNum == 0 && IsBagFull)
                    {
                        SendNotice("仓库已满");
                        return;
                    }

                    bagNum += repoNum;
                    Items[targetId] = bagNum;
                    Repos.Remove(targetId);

                    SyncItems();
                    SyncRepos();
                }
            }

            // 响应给前端，让前端本地修改数据
            await SendPacket(GameCmd.S2CRepoUpdate, new S2C_RepoUpdate
            {
                IsAdd = isAdd,
                Type = type,
                Target = targetId
            });
        }

        private async Task ReqLotteryOpen(uint cfgId)
        {
            if (CheckSafeLocked()) return;
            if (cfgId != 50004 && cfgId != 50005 && cfgId != 50006 && cfgId != 50007) return;

            if (GetBagItemNum(cfgId) <= 0)
            {
                SendNotice("物品不足");
                return;
            }

            if (IsBagFull)
            {
                SendNotice("背包已满，无法挖宝");
                return;
            }

            List<LotteryConfig> cfgList = null;
            if (50004 == cfgId)
            {
                cfgList = new List<LotteryConfig>(ConfigService.Lotterys);
            }
            else if (50005 == cfgId)
            {
                cfgList = new List<LotteryConfig>(ConfigService.ShanHeTus);
            }
            else if (50006 == cfgId)
            {
                cfgList = new List<LotteryConfig>(ConfigService.BlindBoxs);
            }
            else if (50007 == cfgId)
            {
                cfgList = new List<LotteryConfig>(ConfigService.BlindBoxsPet);
            }

            if (cfgList == null) return;

            // 直接通过概率来命中抽中的奖品
            var rateSum = cfgList.Sum(p => p.Rate);
            var rateRnd = Random.Next(0, rateSum);
            var hitIndex = -1; //命中的索引
            for (var i = 0; i < cfgList.Count; i++)
            {
                rateRnd -= cfgList[i].Rate;
                if (rateRnd <= 0)
                {
                    hitIndex = i;
                    break;
                }
            }

            // 理论上不会出现, 但是为了以防万一
            if (hitIndex < 0) hitIndex = Random.Next(0, cfgList.Count);
            // 将抽中的物品放入, 这里注意是放在第一个位置上
            var hitCfg = cfgList[hitIndex];
            var resp = new S2C_LotteryOpen { CfgId = cfgId, Index = 0 };
            resp.Items.Add(new LotteryItem { ItemId = hitCfg.ItemId, ItemNum = hitCfg.Num });
            // 移除, 防止产生重复元素
            cfgList.RemoveAt(hitIndex);

            // 再挑选14个用来展示
            var count = Math.Min(14, cfgList.Count);
            for (var i = 0; i < count; i++)
            {
                var idx = Random.Next(0, cfgList.Count);
                var cfg = cfgList[idx];
                resp.Items.Add(new LotteryItem { ItemId = cfg.ItemId, ItemNum = cfg.Num });
                // 移除, 防止产生重复的元素
                cfgList.RemoveAt(idx);
            }

            // 抽中的物品，随机打乱一下顺序
            if (resp.Items.Count > 1)
            {
                var newIdx = Random.Next(0, resp.Items.Count);
                if (newIdx > 0)
                {
                    (resp.Items[0], resp.Items[newIdx]) = (resp.Items[newIdx], resp.Items[0]);
                    resp.Index = newIdx;
                }
            }

            // 扣除道具
            await AddBagItem(cfgId, -1, tag: "Lottery");


            if (50006 == cfgId || 50007 == cfgId) {
                //发送跑马灯公告
                if (hitCfg.Notice == 1) {
                    var text =
                        $"<color=#00ff00 > {Entity.NickName}</c > <color=#ffffff> 获得了</c ><color=#0fffff > {hitCfg.Desc}</color >，<color=#ffffff > 真是太幸运了</c >";
                    BroadcastScreenNotice(text, 0);
                }
                await AddBagItem(hitCfg.ItemId, (int)hitCfg.Num, true, "Lottery");
                return;
            } else {
                // 获得道具, 这里不send, 前端自行add
                await AddBagItem(hitCfg.ItemId, (int)hitCfg.Num, false, "Lottery");
            }

            if (resp.Items.Count > 0)
            {
                await SendPacket(GameCmd.S2CLotteryOpen, resp);
            }
        }

        private async Task ReqRolePays()
        {
            await RefreshDailyPays();

            var resp = new S2C_RolePays
            {
                TotalPay = Entity.TotalPay,
                TotalPayRewards = { _totalPayRewards },
                DailyPay = Entity.DailyPay,
                DailyPayRewards = { _dailyPayRewards },
                FirstPayReward = -1,
                EwaiPayRewards = { _ewaiPayRewards },
                EwaiPay = Entity.EwaiPay,
            };

            //LogInformation($"ReqRolePays   TotalPay={Entity.TotalPay}  EwaiPay={Entity.EwaiPay} EwaiPayRewards={Entity.EwaiPayRewards}");

            if (GetFlag(FlagType.FirstPayReward))
            {
                // 已经领取过了
                resp.FirstPayReward = 1;
            }
            else if (Entity.TotalPayBS >= 1120)
            {
                // 可以领取但未领取
                resp.FirstPayReward = 0;
            }

            await SendPacket(GameCmd.S2CRolePays, resp);
        }

        private async Task ReqGetTotalPayRewards(uint money)
        {
            var cfg = ConfigService.TotalPays.FirstOrDefault(p => p.Pay == money);
            if (cfg == null) return;

            if (Entity.TotalPay < money)
            {
                SendNotice("累计充值额度不足, 请前往充值");
                return;
            }

            if (_totalPayRewards.Contains(money))
            {
                SendNotice("该段位奖励已领取过, 不能重复领取");
                return;
            }

            // 尝试发放奖励
            if (cfg.Rewards == null || cfg.Rewards.Count == 0)
            {
                SendNotice("该段位没有奖励，请联系群主");
                return;
            }

            foreach (var rew in cfg.Rewards)
            {
                if (rew == null || rew.Id == 0 || rew.Num <= 0) continue;
                if (rew.Wing)
                {
                    ConfigService.Wings.TryGetValue(rew.Id, out var wingCfg);
                    if (wingCfg != null)
                    {
                        await EquipMgr.AddEquip(wingCfg);
                    }
                }
                else if (rew.Title)
                {
                    ConfigService.Titles.TryGetValue(rew.Id, out var titleCfg);
                    if (titleCfg != null)
                    {
                        await TitleMgr.AddTitle(titleCfg.Id);
                    }
                }
                else
                {
                    ConfigService.Items.TryGetValue(rew.Id, out var itemCfg);
                    if (itemCfg != null)
                    {
                        await AddItem(itemCfg.Id, rew.Num, tag: "获取累计充值奖励");
                    }
                }
            }

            _totalPayRewards.Add(money);
            Entity.TotalPayRewards = Json.Serialize(_totalPayRewards);
            LastEntity.TotalPayRewards = Entity.TotalPayRewards;

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.TotalPayRewards, Entity.TotalPayRewards)
                .ExecuteAffrowsAsync();

            var resp = new S2C_TotalPayRewards
            {
                TotalPay = Entity.TotalPay,
                TotalPayRewards = { _totalPayRewards }
            };

            await SendPacket(GameCmd.S2CTotalPayRewards, resp);
        }

        private async Task ReqResetTotalPayRewards()
        {
            if (Entity.TotalPay == 0)
            {
                SendNotice("累计充值为0，无需重置");
                return;
            }
            //LogInformation($"ReqGetTotalPayRewards");
            // 20w仙玉
            var ret = await CostMoney(MoneyType.Jade, 200000, false, "重置累计充值奖励");
            if (!ret) return;

            // 清空累计充值和累计充值领取记录
            _totalPayRewards.Clear();
            LastEntity.TotalPay = Entity.TotalPay = 0;
            LastEntity.TotalPayRewards = Entity.TotalPayRewards = "";

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.TotalPay, Entity.TotalPay)
                .Set(it => it.TotalPayRewards, Entity.TotalPayRewards)
                .ExecuteAffrowsAsync();

            var resp = new S2C_TotalPayRewards
            {
                TotalPay = Entity.TotalPay,
                TotalPayRewards = { _totalPayRewards }
            };

            await SendPacket(GameCmd.S2CTotalPayRewards, resp);
        }

        private async Task ReqGetEwaiPayRewards(uint money)
        {
            var cfg = ConfigService.EwaiPays.FirstOrDefault(p => p.Pay == money);
            if (cfg == null) return;

            if (Entity.EwaiPay < money)
            {
                SendNotice("累计充值额度不足, 请前往充值");
                return;
            }

            if (_ewaiPayRewards.Contains(money))
            {
                SendNotice("该段位奖励已领取过, 不能重复领取");
                return;
            }

            // 尝试发放奖励
            if (cfg.Rewards == null || cfg.Rewards.Count == 0)
            {
                SendNotice("该段位没有奖励，请联系群主");
                return;
            }

            foreach (var rew in cfg.Rewards)
            {
                if (rew == null || rew.Id == 0 || rew.Num <= 0) continue;
                if (rew.Wing)
                {
                    ConfigService.Wings.TryGetValue(rew.Id, out var wingCfg);
                    if (wingCfg != null)
                    {
                        await EquipMgr.AddEquip(wingCfg);
                    }
                }
                else if (rew.Title)
                {
                    ConfigService.Titles.TryGetValue(rew.Id, out var titleCfg);
                    if (titleCfg != null)
                    {
                        await TitleMgr.AddTitle(titleCfg.Id);
                    }
                }
                else
                {
                    ConfigService.Items.TryGetValue(rew.Id, out var itemCfg);
                    if (itemCfg != null)
                    {
                        await AddItem(itemCfg.Id, rew.Num, tag: "获取额外累计充值奖励");
                    }
                }
            }

            _ewaiPayRewards.Add(money);
            Entity.EwaiPayRewards = Json.Serialize(_ewaiPayRewards);
            LastEntity.EwaiPayRewards = Entity.EwaiPayRewards;

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.EwaiPayRewards, Entity.EwaiPayRewards)
                .ExecuteAffrowsAsync();

            var resp = new S2C_EwaiPayRewards
            {
                EwaiPay = Entity.EwaiPay,
                EwaiPayRewards = { _ewaiPayRewards }
            };

            await SendPacket(GameCmd.S2CEwaiPayRewards, resp);
        }

        private async Task ReqResetEwaiPayRewards()
        {
            if (Entity.EwaiPay == 0)
            {
                SendNotice("累计充值为0，无需重置...");
                return;
            }
            //LogInformation($"ReqResetEwaiPayRewards");
            // 20w仙玉
            var ret = await CostMoney(MoneyType.Jade, 200000, false, "重置额外福利奖励");
            if (!ret) return;

            // 清空累计充值和累计充值领取记录
            _ewaiPayRewards.Clear();
            LastEntity.EwaiPay = Entity.EwaiPay = 0;
            LastEntity.EwaiPayRewards = Entity.EwaiPayRewards = "";

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.EwaiPay, Entity.EwaiPay)
                .Set(it => it.EwaiPayRewards, Entity.EwaiPayRewards)
                .ExecuteAffrowsAsync();

            var resp = new S2C_EwaiPayRewards
            {
                EwaiPay = Entity.EwaiPay,
                EwaiPayRewards = { _ewaiPayRewards }
            };

            await SendPacket(GameCmd.S2CEwaiPayRewards, resp);
        }

        private async Task ReqGetDailyPayRewards(uint money)
        {
            var cfg = ConfigService.DailyPays.FirstOrDefault(p => p.Pay == money);
            if (cfg == null) return;

            await RefreshDailyPays();

            if (Entity.DailyPay < money)
            {
                SendNotice("今日充值额度不足, 请前往充值");
                return;
            }

            if (_dailyPayRewards.Contains(money))
            {
                SendNotice("该段位奖励已领取过, 不能重复领取");
                return;
            }

            // 尝试发放奖励
            if (cfg.Rewards == null || cfg.Rewards.Count == 0)
            {
                SendNotice("该段位没有奖励，请联系群主");
                return;
            }

            foreach (var rew in cfg.Rewards)
            {
                if (rew == null || rew.Id == 0 || rew.Num <= 0) continue;
                if (rew.Wing)
                {
                    ConfigService.Wings.TryGetValue(rew.Id, out var wingCfg);
                    if (wingCfg != null)
                    {
                        await EquipMgr.AddEquip(wingCfg);
                    }
                }
                else
                {
                    ConfigService.Items.TryGetValue(rew.Id, out var itemCfg);
                    if (itemCfg != null)
                    {
                        await AddItem(itemCfg.Id, rew.Num, tag: "获取每日充值奖励");
                    }
                }
            }

            _dailyPayRewards.Add(money);
            Entity.DailyPayRewards = Json.Serialize(_dailyPayRewards);
            LastEntity.DailyPayRewards = Entity.DailyPayRewards;

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.DailyPayRewards, Entity.DailyPayRewards)
                .ExecuteAffrowsAsync();

            var resp = new S2C_DailyPayRewards
            {
                DailyPay = Entity.DailyPay,
                DailyPayRewards = { _dailyPayRewards }
            };

            await SendPacket(GameCmd.S2CDailyPayRewards, resp);
        }

        private async Task ReqResetDailyPayRewards()
        {
            await RefreshDailyPays();
            if (Entity.DailyPay == 0)
            {
                SendNotice("今日充值为0，无需重置");
                return;
            }

            // 20w仙玉
            var ret = await CostMoney(MoneyType.Jade, 200000, false, "重置每日充值奖励");
            if (!ret) return;

            // 清空今日充值和今日充值领取记录
            _dailyPayRewards.Clear();
            Entity.DailyPay = 0;
            Entity.DailyPayTime = TimeUtil.TimeStamp;
            Entity.DailyPayRewards = "";

            LastEntity.DailyPay = Entity.DailyPay;
            LastEntity.DailyPayTime = Entity.DailyPayTime;
            LastEntity.DailyPayRewards = Entity.DailyPayRewards;

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.DailyPay, Entity.DailyPay)
                .Set(it => it.DailyPayTime, Entity.DailyPayTime)
                .Set(it => it.DailyPayRewards, Entity.DailyPayRewards)
                .ExecuteAffrowsAsync();

            var resp = new S2C_DailyPayRewards
            {
                DailyPay = Entity.DailyPay,
                DailyPayRewards = { _dailyPayRewards }
            };

            await SendPacket(GameCmd.S2CDailyPayRewards, resp);
        }

        private async Task RefreshDailyPays()
        {
            var now = DateTimeOffset.Now;
            var last = DateTimeOffset.FromUnixTimeSeconds(Entity.DailyPayTime).AddHours(8);
            if (now.Year != last.Year || now.DayOfYear != last.DayOfYear)
            {
                // 清空今日充值记录
                _dailyPayRewards.Clear();

                Entity.DailyPay = 0;
                Entity.DailyPayTime = (uint)now.ToUnixTimeSeconds();
                Entity.DailyPayRewards = "";
                LastEntity.DailyPay = Entity.DailyPay;
                LastEntity.DailyPayTime = Entity.DailyPayTime;
                LastEntity.DailyPayRewards = Entity.DailyPayRewards;

                await DbService.Sql.Update<RoleEntity>()
                    .Where(it => it.Id == RoleId)
                    .Set(it => it.DailyPay, Entity.DailyPay)
                    .Set(it => it.DailyPayTime, Entity.DailyPayTime)
                    .Set(it => it.DailyPayRewards, Entity.DailyPayRewards)
                    .ExecuteAffrowsAsync();
            }
        }

        private async Task ReqGetFirstPayRewards()
        {
            if (ConfigService.FirstPay == null || ConfigService.FirstPay.Pay == 0 ||
                ConfigService.FirstPay.Rewards == null) return;

            // 只能领取首充奖励
            if (GetFlag(FlagType.FirstPayReward))
            {
                SendNotice("你已经领取过首充奖励");
                return;
            }

            if (Entity.TotalPayBS < 1120)
            {
                SendNotice("请前往充值");
                return;
            }

            if (ConfigService.FirstPay.Rewards.Count == 0)
            {
                SendNotice("没有首充奖励，请联系群主");
                return;
            }

            var isFirst = !GetFlag(FlagType.FirstPayReward);
            foreach (var rew in ConfigService.FirstPay.Rewards)
            {
                if (rew == null || rew.Id == 0 || rew.Num <= 0) continue;
                if (rew.Wing)
                {
                    ConfigService.Wings.TryGetValue(rew.Id, out var wingCfg);
                    if (wingCfg != null)
                    {
                        await EquipMgr.AddEquip(wingCfg);
                    }
                }
                else
                {
                    ConfigService.Items.TryGetValue(rew.Id, out var itemCfg);
                    if (itemCfg != null)
                    {
                        await AddItem(itemCfg.Id, rew.Num, tag: isFirst ? "获取首充奖励" : "获取次充奖励");
                    }
                }
            }

            SetFlag(FlagType.FirstPayReward, true);

            // 通知前端，已经领取过了
            await SendPacket(GameCmd.S2CFirstPayReward, new S2C_FirstPayReward
            {
                FirstPayReward = 1
            });
        }

        private async Task ReqRecover(C2S_Recover req)
        {
            if (CheckSafeLocked()) return;

            switch (req.Type)
            {
                case 0:
                {
                    // 物品, 检查
                    ConfigService.Items.TryGetValue(req.Id, out var itemCfg);
                    if (itemCfg == null || itemCfg.GuoShi <= 0)
                    {
                        SendNotice("该物品不能回收");
                        return;
                    }

                    Items.TryGetValue(req.Id, out var num);
                    if (num == 0) return;
                    if (req.Num > num) req.Num = num;
                    // 获得郭氏积分
                    await AddMoney(MoneyType.GuoShi, (int)req.Num * itemCfg.GuoShi, "回收");
                    // 移除物品
                    await AddBagItem(req.Id, -(int)req.Num, tag: "回收");
                    break;
                }
                case 1:
                {
                    // 装备, 检查，只能是1级神兵
                    EquipMgr.Equips.TryGetValue(req.Id, out var equip);
                    if (equip == null || equip.Category != EquipCategory.Shen || equip.Grade != 1)
                    {
                        SendNotice("该装备不能回收");
                        return;
                    }

                    // 必须先脱下
                    if (equip.Place != EquipPlace.Bag)
                    {
                        SendNotice("请先将该装备脱下放入背包中");
                        return;
                    }

                    var ret = await EquipMgr.DelEquip(req.Id);
                    if (ret)
                    {
                        // 获得郭氏积分
                        await AddMoney(MoneyType.GuoShi, 50, "回收");
                    }
                }
                    break;
                default:
                {
                    SendNotice("该物品不能回收");
                    return;
                }
            }

            await SendPacket(GameCmd.S2CRecover, new S2C_Recover
            {
                Type = req.Type,
                Id = req.Id,
                Num = req.Num
            });
        }

        private async Task ReqEquipUpgrade(uint equipId, uint use1, uint use2)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null) return;
            if (equip.Category == EquipCategory.Shen)
                await equip.ShenBingUpgrade(use1);
            else if (equip.Category == EquipCategory.Xian)
                await equip.XianQiUpgrade(use1, use2);
        }

        private async Task ReqEquipInlay(uint equipId, bool add)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null) return;
            await equip.Inlay(add);
        }

        private async Task ReqEquipRefine(uint equipId, uint level, uint opera)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null) return;
            await equip.Refine(level, opera > 0);
        }
        private async Task ReqEquipRefineTimes(uint equipId, uint level, uint opera, uint times, uint choiceIndex)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null) return;
            await equip.RefineTimes(level, opera > 0, times, choiceIndex);
        }

        private async Task ReqEquipDingZhi(uint equipId, List<int> attrs)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null)
            {
                SendNotice("装备不存在");
                return;
            }
            await equip.DingZhi(attrs);
        }

        private async Task ReqOrnamentDingZhi(uint ornamentId, List<int> attrs)
        {
            if (CheckSafeLocked()) return;
            var ornament = EquipMgr.FindOrnament(ornamentId);
            if (ornament == null)
            {
                SendNotice("配饰不存在");
                return;
            }
            await ornament.DingZhi(attrs);
        }

        private async Task ReqEquipStarUpgrade(uint equipId, uint itemId)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null)
            {
                SendNotice("装备不存在");
                return;
            }
            await equip.StarUpgrade(itemId);
        }

        private async Task ReqEquipGradeUpgrade(uint equipId)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null)
            {
                SendNotice("装备不存在");
                return;
            }
            await equip.GradeUpgrade();
        }

        private async Task ReqEquipRecast(uint equipId, uint opera)
        {
            if (CheckSafeLocked()) return;
            var equip = EquipMgr.FindEquip(equipId);
            if (equip == null) return;
            await equip.Recast(opera > 0);
        }

        private Task ReqEquipCombine(EquipCategory category, uint index)
        {
            if (CheckSafeLocked()) return Task.CompletedTask;
            return EquipMgr.Combine(category, (int)index);
        }

        private Task ReqEquipProperty(uint id, uint flag)
        {
            if (CheckSafeLocked()) return Task.CompletedTask;
            return EquipMgr.SendEquipPreviewData(id, flag);
        }
        private Task ReqEquipPropertyList(uint id, uint flag)
        {
            if (CheckSafeLocked()) return Task.CompletedTask;
            return EquipMgr.SendEquipPreviewDataList(id, flag);
        }

        private async Task ReqEquipDelete(uint equipId)
        {
            if (CheckSafeLocked()) return;
            await EquipMgr.DelEquip(equipId);
        }

        private async Task ReqNpcShopItems(uint npcCfgId)
        {
            var resp = new S2C_NpcShopItems();
            if (npcCfgId == GameDefine.ZhenBuKuiNpcCfgId)
            {
                var respBytes = await _zhenBuKuiGrain.GetItems();
                if (respBytes.Value == null)
                {
                    SendNotice("甄不亏已离开");
                    return;
                }

                resp = S2C_NpcShopItems.Parser.ParseFrom(respBytes.Value);
            }
            else
            {
                // 水陆大会地图上的水路使者和金銮殿中的魏征同样处理
                if (npcCfgId == 72000) npcCfgId = 10242;
                ConfigService.NpcShopItems.TryGetValue(npcCfgId, out var cfgList);
                if (cfgList != null)
                {
                    foreach (var cfg in cfgList)
                    {
                        resp.List.Add(new ShopItem
                        {
                            CfgId = cfg.ItemId,
                            Price = cfg.Price,
                            Num = -1,
                            Type = (NpcShopItemType)cfg.Type,
                            CostType = (MoneyType)cfg.Cost
                        });
                    }
                }
            }

            await SendPacket(GameCmd.S2CNpcShopItems, resp);
        }

        private async Task ReqNpcShopBuy(uint npcCfgId, uint itemId, uint itemNum)
        {
            if (CheckSafeLocked()) return;
            if (itemNum <= 0) return;

            // 检测背包数量
            if (IsBagFull)
            {
                SendNotice("背包已满，无法购买");
                return;
            }

            if (npcCfgId == GameDefine.ZhenBuKuiNpcCfgId)
            {
                var respBytes = await _zhenBuKuiGrain.GetItem(itemId);
                if (respBytes.Value == null)
                {
                    SendNotice("商品已售罄");
                    return;
                }

                var resp = ShopItem.Parser.ParseFrom(respBytes.Value);
                // 提前计算价钱
                if (resp.Num <= 0)
                {
                    SendNotice("商品已售罄");
                    return;
                }

                // 检查货币是否足够
                if (!CheckMoney(resp.CostType, resp.Price * itemNum)) return;

                // 尝试去购买, 最终购买的数量以返回的数据为准
                respBytes = await _zhenBuKuiGrain.BuyItem(itemId, itemNum);
                if (respBytes.Value == null)
                {
                    SendNotice("商品已售罄");
                    return;
                }

                resp = ShopItem.Parser.ParseFrom(respBytes.Value);
                if (resp.Num <= 0)
                {
                    SendNotice("商品已售罄");
                    return;
                }

                // 扣钱, 这里理论上是不可能出现不足的情况
                await CostMoney(resp.CostType, (uint)resp.Num * resp.Price, tag: "甄不亏商店购买物品");

                // 获得物品
                await AddBagItem(resp.CfgId, resp.Num, tag: "甄不亏商店购买");
            }
            else
            {
                // 水陆大会地图上的水路使者和金銮殿中的魏征同样处理
                if (npcCfgId == 72000) npcCfgId = 10242;
                ConfigService.NpcShopItems.TryGetValue(npcCfgId, out var cfgList);
                NpcShopItemConfig itemCfg = null;
                if (cfgList != null) itemCfg = cfgList.FirstOrDefault(p => p.ItemId == itemId);
                if (itemCfg == null)
                {
                    SendNotice("商品不存在，无法购买");
                    return;
                }

                // 检查货币
                if (!CheckMoney((MoneyType)itemCfg.Cost, itemCfg.Price * itemNum)) return;

                var ret = false;
                uint realNum;
                if (itemCfg.Type == (byte)NpcShopItemType.Weapon)
                {
                    // 装备一次只能买一把，防止大量的插入数据库
                    realNum = 1;
                    var equip = await EquipMgr.AddEquip(itemCfg.ItemId);
                    if (equip != null) ret = true;
                }
                else
                {
                    ret = await AddBagItem(itemId, (int)itemNum, tag: $"NPC({npcCfgId})商店购买");
                    realNum = itemNum;
                }

                if (ret && realNum > 0)
                {
                    await CostMoney((MoneyType)itemCfg.Cost, itemCfg.Price * realNum, tag: $"NPC({npcCfgId})商店消费");
                }
            }

            await Task.CompletedTask;
        }

        private async Task ReqShopItems(uint type)
        {
            var resp = new S2C_ShopItems();
            var costType = type == 6 || type == 7 ? MoneyType.BindJade : MoneyType.Jade;
            if (type == 8) {
                costType = MoneyType.WzzzJiFen;
            }
            foreach (var v in ConfigService.ShopItems.Values.Where(it => it.Type == type))
            {
                resp.List.Add(new ShopItem { CfgId = v.ItemId, Price = v.Price, CostType = costType });
            }

            await SendPacket(GameCmd.S2CShopItems, resp);
        }

        private async Task ReqSkinInfo(List<int> use) {
            var resp = new S2C_SkinInfo();
            // 查询皮肤
            if (use.Count <= 0)
            {
                foreach (var i in SkinUse)
                {
                    resp.Use.Add(i);
                }
            }
            // 穿卸皮肤
            else
            {
                // 校验皮肤是否有效
                var comfirmed = new List<int>();
                foreach (var i in use)
                {
                    if (SkinHas.Contains(i))
                    {
                        comfirmed.Add(i);
                    }
                }
                // 更新实体
                SkinUse.Clear();
                foreach (var i in comfirmed)
                {
                    resp.Use.Add(i);
                    SkinUse.Add(i);
                }
                SyncSkins();
                // 属性方案刷新潜能和属性
                await FreshAllSchemeAttrs();
            }
            // 补充拥有的皮肤信息
            foreach (var i in SkinHas)
            {
                resp.Has.Add(i);
            }
            await SendPacket(GameCmd.S2CSkinInfo, resp);
        }

        // 获取变身信息
        private async Task ReqBianshenInfo()
        {
            if (CheckBianshen()) {
                SyncBianshen();
            }
            var resp = new S2C_BianshenInfo() { Info = Entity.Bianshen };
            await SendPacket(GameCmd.S2CBianshenInfo, resp);
        }

        // 五行升级
        private async Task ReqBianshenWuxingLevelUp(int type)
        {
            if (type < (int)WuXingType.Jin && type > (int)WuXingType.Wuxing)
            {
                return;
            }
            // ------------------------------------------------------------------------------------------
            // 指定修炼--开始
            // ------------------------------------------------------------------------------------------
            // // 当前等级
            // var current = Bianshen.wuxing.GetValueOrDefault(type, 0);
            // // 下一等级
            // var next = current + 1;
            // // 等级升级配置，五行为空，其他有配置
            // var levels = ConfigService.BianShenLevels.GetValueOrDefault(type, null);
            // // 五行名称
            // var wname = ConfigService.BianShenNameConfig.GetValueOrDefault(type, "");
            // // 金木水火土满级为等级配置数量，五行最高等级为100
            // if ((levels != null && next > levels.Count) || (next > 100 && levels == null))
            // {
            //     SendNotice(string.Format("{0}属性已满级", wname));
            //     return;
            // }
            // // 升级所需材料ID
            // uint itemCfgId = levels != null ? (uint)levels.GetValueOrDefault(next, null).itemid : 9903;
            // ------------------------------------------------------------------------------------------
            // 指定修炼--结束
            // ------------------------------------------------------------------------------------------
            // ------------------------------------------------------------------------------------------
            // 随机修炼--开始
            // ------------------------------------------------------------------------------------------
            var canXiuLian = new List<KeyValuePair<int, int>>();
            // 暂时只修炼金木水火土
            for (int t = 1; t <= 5; t++)
            {
                var current = Bianshen.wuxing.GetValueOrDefault(t, 0);
                if ((t != 6 && current < 50) || (t == 6 && current < 200))
                {
                    canXiuLian.Add(KeyValuePair.Create<int, int>(t, current));
                }
            }
            if (canXiuLian.Count == 0)
            {
                SendNotice("五行修炼已经满级！");
                return;
            }
            uint itemCfgId = 9903;
            if (GetBagItemNum(itemCfgId) < 1)
            {
                SendNotice("缺少材料，无法升级");
                return;
            }
            int index = Random.Next(canXiuLian.Count);
            var model = canXiuLian[index];
            var next = model.Value + 1;
            type = model.Key;
            var wname = ConfigService.BianShenNameConfig.GetValueOrDefault(type, "");
            // ------------------------------------------------------------------------------------------
            // 随机修炼--结束
            // ------------------------------------------------------------------------------------------
            // 扣除材料
            await AddBagItem(itemCfgId, -1, true, string.Format("{0}属性升级", wname));
            // 升级
            Bianshen.wuxing[type] = next;
            // 检查过期
            CheckBianshen();
            // 同步
            SyncBianshen();
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            // 发送新的五行等级
            var resp = new S2C_BianshenWuxingLevelUp() { Type = type, Wuxing = JsonSerializer.Serialize(Bianshen.wuxing) };
            await SendPacket(GameCmd.S2CBianshenWuxingLevelUp, resp);
        }

        // 使用变身卡
        private async Task ReqBianshenUse(int id, bool avatar)
        {
            // 没有找到配置
            var config = ConfigService.BianShenCards.GetValueOrDefault(id, null);
            if (config == null)
            {
                SendNotice("变身卡暂时不可使用");
                return;
            }
            // 变身卡数量为0
            var count = Bianshen.cards.GetValueOrDefault(id, 0);
            if (count < 1)
            {
                SendNotice(string.Format("没有可使用的变身卡--{0}", config.name));
                return;
            }
            // 减少变身卡
            Bianshen.cards[id] = count - 1;
            // 没有了则删除
            if ((count - 1) <= 0) {
                Bianshen.cards.Remove(id);
            }
            // 使用中变身卡有效期
            var cardTimestamp = Bianshen.current.timestamp.Length > 0 ? Convert.ToInt64(Bianshen.current.timestamp) : 0;
            // 当前时间戳
            var nowTimestamp = DateTimeUtil.GetTimestamp();
            // 使用的是同一种变身卡，并且当前没有过有效期的话，则延长有效期
            if (Bianshen.current.id == id && cardTimestamp > nowTimestamp)
            {
                cardTimestamp += config.time * 1000;
                Bianshen.current.timestamp = Convert.ToString(cardTimestamp);
                LogInformation(string.Format("延期变身卡，当前{0}, 过期{1}, 时长{2}秒", nowTimestamp, Bianshen.current.timestamp, (cardTimestamp - nowTimestamp) / 1000));
            }
            else
            {
                Bianshen.current.id = id;
                Bianshen.current.timestamp = Convert.ToString(nowTimestamp + config.time * 1000);
                LogInformation(string.Format("新用变身卡，当前{0}, 过期{1}, 时长{2}秒", nowTimestamp, Bianshen.current.timestamp, config.time));
            }
            // 是否改变形象？
            Bianshen.current.avatar = avatar;
            // 同步
            SyncBianshen();
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            // 响应
            var resp = new S2C_BianshenUse() { Id = Bianshen.current.id, Avatar = Bianshen.current.avatar, Timestamp = Bianshen.current.timestamp };
            await SendPacket(GameCmd.S2CBianshenUse, resp);
        }

        // 变身还原
        private async Task ReqBianshenReset()
        {
            uint itemCfgId = 9906;
            // 还原丹卡数量为0
            if (GetBagItemNum(itemCfgId) < 1)
            {
                SendNotice("缺少还原丹，无法还原");
                return;
            }
            // 当前未变身
            if (Bianshen.current.id == 0)
            {
                SendNotice("当前未变身，无需还原");
                return;
            }
            // 扣除材料
            await AddBagItem(itemCfgId, -1, true, "变身还原");
            Bianshen.current.id = 0;
            Bianshen.current.avatar = false;
            Bianshen.current.timestamp = "0";
            // 同步
            SyncBianshen();
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            // 响应
            var resp = new S2C_BianshenUse() { Id = Bianshen.current.id, Avatar = Bianshen.current.avatar, Timestamp = Bianshen.current.timestamp };
            await SendPacket(GameCmd.S2CBianshenUse, resp);
        }
        // 获取星阵信息
        private async Task ReqXingzhenInfo()
        {
            var resp = new S2C_XingzhenInfo() { Info = Entity.Xingzhen };
            await SendPacket(GameCmd.S2CXingzhenInfo, resp);
        }
        // 星阵--解锁
        private async Task ReqXingzhenUnlock(int id)
        {
            var config = ConfigService.XingZhenItems.GetValueOrDefault(id, null);
            if (config == null)
            {
                SendNotice("错误的星阵阵型");
                return;
            }
            var itemId = (uint)config.unlockItemId;
            var itemNum = Math.Abs(config.unlockItemNum);
            if (GetBagItemNum(itemId) < itemNum)
            {
                SendNotice(string.Format("解锁失败，<color=#ff0000>{0}</c>不足", config.name));
                return;
            }
            if (Xingzhen.unlocked.ContainsKey(id))
            {
                SendNotice("已经解锁，无需再次解锁");
                return;
            }
            var result = await AddBagItem(itemId, -itemNum, true, string.Format("解锁{0}", config.name));
            if (!result)
            {
                SendNotice(string.Format("扣除<color=#ff0000>{0}</c>失败", ConfigService.Items[itemId].Name));
                return;
            }
            Xingzhen.unlocked.Add(id, new() { exp = 0, level = 1, refine = new(), preview = new() });
            Xingzhen.used = id;
            SyncXingzhen();
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            SendNotice(string.Format("恭喜成功解锁阵型--<color=#ff0000>{0}</c>", config.name));
            var resp = new S2C_XingzhenInfo() { Info = Entity.Xingzhen };
            await SendPacket(GameCmd.S2CXingzhenInfo, resp);
        }
        // 检查阵型是否解锁？
        private bool checkXingzhenLocked(int id) {
            var config = ConfigService.XingZhenItems.GetValueOrDefault(id, null);
            if (config == null)
            {
                SendNotice("错误的星阵阵型");
                return true;
            }
            if (!Xingzhen.unlocked.ContainsKey(id))
            {
                SendNotice(string.Format("请先解锁阵型--<color=#ff0000>{0}</c>", config.name));
                return true;
            }
            return false;
        }
        // 星阵--装备
        private async Task ReqXingzhenUse(int id)
        {
            if (checkXingzhenLocked(id))return;
            var config = ConfigService.XingZhenItems[id];
            Xingzhen.used = id;
            SyncXingzhen();
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            var resp = new S2C_XingzhenInfo() { Info = Entity.Xingzhen };
            await SendPacket(GameCmd.S2CXingzhenInfo, resp);
        }
        // 星阵--升级
        private async Task ReqXingzhenUpgrade(int id, int itemId)
        {
            if (checkXingzhenLocked(id)) return;
            var config = ConfigService.XingZhenItems[id];
            if (!config.upgradeItemId.Contains(itemId))
            {
                SendNotice("升级材料错误");
                return;
            }
            var itemCfg = ConfigService.Items.GetValueOrDefault((uint)itemId, null);
            if (GetBagItemNum((uint)itemId) < 1 || itemCfg == null)
            {
                SendNotice(string.Format("升级<color=#ff0000>{0}</c>失败，<color=#ff0000>{1}</c>材料不足", config.name, itemCfg.Name));
                return;
            }
            var xz = Xingzhen.unlocked[id];
            var lcount = ConfigService.XingZhenLevels.Count;
            if (xz.level >= lcount)
            {
                SendNotice(string.Format("升级<color=#ff0000>{0}</c>失败，已经满级", config.name));
                return;
            }
            var lconfig = ConfigService.XingZhenLevels.GetValueOrDefault(xz.level, null);
            if (lconfig == null)
            {
                SendNotice(string.Format("升级<color=#ff0000>{0}</c>失败，错误的等级", config.name));
                return;
            }
            var itemExp = itemCfg.Num;
            xz.exp += itemExp;
            var oldLevel = xz.level;
            while (lconfig != null && xz.exp >= lconfig.exp)
            {
                xz.exp -= lconfig.exp;
                xz.level += 1;
                if (xz.level >= lcount)
                {
                    break;
                }
                lconfig = ConfigService.XingZhenLevels.GetValueOrDefault(xz.level, null);
                if (lconfig == null)
                {
                    SendNotice(string.Format("升级<color=#ff0000>{0}</c>失败，错误的等级", config.name));
                    return;
                }
            }
            var result = await AddBagItem((uint)itemId, -1, true, string.Format("升级{0}", config.name));
            if (!result)
            {
                SendNotice(string.Format("扣除<color=#ff0000>{0}</c>失败", itemCfg.Name));
                return;
            }
            Xingzhen.unlocked[id] = xz;
            SyncXingzhen();
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            // 推送升级结果
            if (oldLevel != xz.level)
            {
                SendNotice(string.Format("<color=#ff0000>{0}</c>增加经验<color=#008f00>{1}</c>，升<color=#ff0000>{2}</c>级", config.name, itemExp, xz.level - oldLevel));
            }
            else
            {
                SendNotice(string.Format("<color=#ff0000>{0}</c>增加经验<color=#008f00>{1}</c>", config.name, itemExp));
            }
            var resp = new S2C_XingzhenInfo() { Info = Entity.Xingzhen };
            await SendPacket(GameCmd.S2CXingzhenInfo, resp);
        }
        // 星阵--洗炼
        private async Task ReqXingzhenRefine(int id)
        {
            if (checkXingzhenLocked(id)) return;
            var config = ConfigService.XingZhenItems[id];
            var itemId = (uint)config.refineItemId;
            var itemNum = Math.Abs(config.refineItemNum);
            if (GetBagItemNum(itemId) < itemNum)
            {
                SendNotice(string.Format("洗炼<color=#ff0000>{0}</c>失败，<color=#ff0000>{1}</c>材料不足", config.name, ConfigService.Items[itemId].Name));
                return;
            }
            var xz = Xingzhen.unlocked[id];
            var result = await AddBagItem(itemId, -itemNum, true, string.Format("洗炼{0}", config.name));
            if (!result)
            {
                SendNotice(string.Format("扣除<color=#ff0000>{0}</c>失败", ConfigService.Items[itemId].Name));
                return;
            }
            // 洗炼
            var preview = new List<string>();
            var count = 1;
            // 80% 1
            // 60% 2
            // 20% 3
            // 哈哈？？？？
            do
            {
                // 80%
                if (Random.Next(100) < 80)
                {
                    count = 1;
                    break;
                }
                // 12%
                if (Random.Next(100) < 60)
                {
                    count = 2;
                    break;
                }
                // 2.4%
                if (Random.Next(100) < 20)
                {
                    count = 3;
                    break;
                }
                count = 1;
                break;
            } while (true);
            var keys = config.refineAttr.Keys.ToList();
            while (preview.Count < count)
            {
                var index = Random.Next(keys.Count);
                var skey = keys[index];
                var vals = config.refineAttr[skey];
                if (vals.Length == 2)
                {
                    var attr = GameDefine.EquipAttrTypeMap.GetValueOrDefault(skey, AttrType.Unkown);
                    if (attr != AttrType.Unkown)
                    {
                        var min = Math.Min(vals[0], vals[1]);
                        var max = Math.Max(vals[0], vals[1]);
                        var delta = max - min;
                        int val = (int)(min + Random.Next(delta + 1));
                        preview.Add(string.Format("{0}_{1}", (int)attr, val));
                    }
                    else
                    {
                        LogError("星阵洗炼配置错误--洗炼属性");
                    }
                }
                else
                {
                    LogError("星阵洗炼配置错误--最大最小值");
                }
            }
            xz.preview = preview;
            Xingzhen.unlocked[id] = xz;
            SyncXingzhen();
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();

            //星阵洗练成功，加计数
            await RedisService.AddOperateTimes(RoleId, RoleInfoFields.OperateXingZhenXiLian, 1);

            SendNotice(string.Format("洗炼<color=#ff0000>{0}</c>成功", config.name, ConfigService.Items[itemId].Name));
            var resp = new S2C_XingzhenInfo() { Info = Entity.Xingzhen };
            await SendPacket(GameCmd.S2CXingzhenInfo, resp);
        }
        // 星阵--替换
        private async Task ReqXingzhenReplace(int id)
        {
            if (checkXingzhenLocked(id)) return;
            var config = ConfigService.XingZhenItems[id];
            var xz = Xingzhen.unlocked[id];
            if (xz.preview.Count <= 0)
            {
                SendNotice(string.Format("请先洗炼阵型--<color=#ff0000>{0}</c>", config.name));
            }
            // 替换
            xz.refine = xz.preview;
            xz.preview = new();
            Xingzhen.unlocked[id] = xz;
            SyncXingzhen();
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            var resp = new S2C_XingzhenInfo() { Info = Entity.Xingzhen };
            await SendPacket(GameCmd.S2CXingzhenInfo, resp);
        }
        // 星阵--定制
        private async Task ReqXingzhenDingZhi(int id, List<string> attrs)
        {
            if (checkXingzhenLocked(id)) return;
            //星阵定制按钮 全部更换为满500次炼化 保底，点击保底之后可以选择定制属性。
            // uint times = await RedisService.GetOperateTimes(RoleId, RoleInfoFields.OperateXingZhenXiLian);
            // if (times < 500) {
            //     return;
            // }

            var config = ConfigService.XingZhenItems[id];
            var _attrs = new List<string>();
            foreach (var skey in attrs)
            {
                config.refineAttr.TryGetValue(skey, out var vals);
                if (vals != null && vals.Length == 2)
                {
                    var attr = GameDefine.EquipAttrTypeMap.GetValueOrDefault(skey, AttrType.Unkown);
                    if (attr != AttrType.Unkown)
                    {
                        var max = Math.Max(vals[0], vals[1]);
                        _attrs.Add(string.Format("{0}_{1}", (int)attr, max));
                    }
                    else
                    {
                        LogError("星阵洗炼配置错误--洗炼属性");
                    }
                }
                if (vals != null && vals.Length != 2)
                {
                    LogError("星阵洗炼配置错误--最大最小值");
                }
            }
            if (_attrs.Count != 3)
            {
                SendNotice("请先选择3个属性");
                return;
            }
            uint itemCfgId = 500056;
            if (GetBagItemNum(itemCfgId) < 1)
            {
               ConfigService.Items.TryGetValue(itemCfgId, out var icfg);
               if (icfg != null)
               {
                   SendNotice("缺少" + icfg.Name);
               }
               else
               {
                   SendNotice("缺少定制券");
               }
               return;
            }
            // 减少一个定制券
            await AddItem(itemCfgId, -1, true, String.Format("定制星阵"));

            var xz = Xingzhen.unlocked[id];
            // 替换
            xz.refine = _attrs;
            Xingzhen.unlocked[id] = xz;
            SyncXingzhen();
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
            var resp = new S2C_XingzhenInfo() { Info = Entity.Xingzhen };
            await SendPacket(GameCmd.S2CXingzhenInfo, resp);

            //星阵定制成功后，洗炼次数置零。
            await RedisService.SetOperateTimes(RoleId, RoleInfoFields.OperateXingZhenXiLian, 0);
        }
        // 获取孩子信息
        private async Task ReqChildInfo()
        {
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
        }
        // 孩子--领养
        private async Task ReqChildAdopt(Sex sex)
        {
            if (HasChild(false))
            {
                SendNotice("每人只能领养一个孩子");
                return;
            }
            uint itemId = 9910;
            if (GetBagItemNum(itemId) <= 0)
            {
                SendNotice($"缺少{ConfigService.Items[itemId].Name}");
                return;
            }
            var result = await AddBagItem(itemId, -1, true, "领养孩子");
            if (!result)
            {
                SendNotice(string.Format("扣除<color=#ff0000>{0}</c>失败", ConfigService.Items[itemId].Name));
                return;
            }
            Child = new()
            {
                sex = sex,
                name = "未起名",
                skill = new(),
                preview = new(),
                exp = 0,
                level = 0,
                shape = sex == Sex.Male ? 6158 : 6159
            };
            SyncChild();
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
            SendNotice("领养成功");
        }
        // 孩子--起名
        private async Task ReqChildRename(string name)
        {
            // 没有孩子
            if (!HasChild()) return;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 8 || name.Length < 2)
            {
                SendNotice("请输入2-8个字符的名字");
                return;
            }
            name = name.Trim();
            // 相同的话直接返回成功
            if (name.Equals(Child.name))
            {
                SendNotice("请输入不同的名字");
                return;
            }
            if (!IsGm && !TextFilter.CheckLimitWord(name))
            {
                SendNotice("名字中包含非法字符");
                return;
            }
            // 检测是否为脏词
            if (TextFilter.HasDirty(name))
            {
                SendNotice("名字中包含非法字符");
                return;
            }
            uint itemId = 9913;
            if (GetBagItemNum(itemId) <= 0)
            {
                SendNotice("更名费不够");
                return;
            }
            var result = await AddBagItem(itemId, -1, true, "孩子起名更名");
            if (!result)
            {
                SendNotice(string.Format("扣除<color=#ff0000>{0}</c>失败", ConfigService.Items[itemId].Name));
                return;
            }
            Child.name = name;
            SyncChild();
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
            SendNotice("起名更名成功");
        }
        // 孩子--培养
        private async Task ReqChildUpgrade(uint itemId)
        {
            // 没有孩子
            if (!HasChild()) return;
            int maxLevel = ConfigService.ChildLevels.Count - 1;
            if (Child.level >= maxLevel)
            {
                SendNotice("已满级，无需培养");
                return;
            }
            List<uint> canUseItemList = new () { 9911, 9917 };
            if (!canUseItemList.Contains(itemId))
            {
                SendNotice("培养材料错误");
                return;
            }
            var itemCfg = ConfigService.Items.GetValueOrDefault(itemId, null);
            if (GetBagItemNum(itemId) <= 0 || itemCfg == null)
            {
                SendNotice("培养材料不足");
                return;
            }
            var lconfig = ConfigService.ChildLevels.GetValueOrDefault(Child.level, null);
            if (lconfig == null)
            {
                SendNotice("培养孩子失败，错误的等级[code: 1]");
                return;
            }
            var itemExp = itemCfg.Num;
            Child.exp += itemExp;
            var oldLevel = Child.level;
            while (lconfig != null && Child.exp >= lconfig.exp)
            {
                Child.exp -= lconfig.exp;
                Child.level += 1;
                if (Child.level >= maxLevel)
                {
                    break;
                }
                lconfig = ConfigService.ChildLevels.GetValueOrDefault(Child.level, null);
                if (lconfig == null)
                {
                    SendNotice("培养孩子失败，错误的等级[code: 2]");
                    return;
                }
            }
            var result = await AddBagItem(itemId, -1, true, "孩子培养");
            if (!result)
            {
                SendNotice(string.Format("扣除<color=#ff0000>{0}</c>失败", itemCfg.Name));
                return;
            }
            SyncChild();
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
            // 推送升级结果
            if (oldLevel != Child.level)
            {
                SendNotice(string.Format("孩子增加经验<color=#008f00>{0}</c>，升<color=#ff0000>{1}</c>级", itemExp, Child.level - oldLevel));
            }
            else
            {
                SendNotice(string.Format("孩子增加经验<color=#008f00>{0}</c>", itemExp));
            }
        }
        // 孩子--更换形象
        private async Task ReqChildChangeShape(int shape)
        {
            // 没有孩子
            if (!HasChild()) return;
            if (Child.level < 10)
            {
                SendNotice("10级以后才可以更换形象");
                return;
            }
            Child.shape = shape;
            SyncChild();
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
            SendNotice("孩子换形象成功");
        }
        // 孩子--洗炼
        private async Task ReqChildRefine()
        {
            // 没有孩子
            if (!HasChild()) return;
            uint itemId = 9912;
            if (GetBagItemNum(itemId) <= 0)
            {
                SendNotice("洗炼材料不足");
                return;
            }
            var preview = new List<int>();
            // 20级以后60 35 5，以前65 35 0
            var count = 1;
            var rate = Random.Next(100) + 1;
            // if (Child.level >= 20)
            // {
                // 5%
                if (rate <= 5)
                {
                    count = 3;
                }
                // 35%
                else if (rate <= 40)
                {
                    count = 2;
                }
                // 60%
                else
                {
                    count = 1;
                }
            // }
            // else
            // {
            //     // 35%
            //     if (rate <= 35)
            //     {
            //         count = 2;
            //     }
            //     // 65%
            //     else
            //     {
            //         count = 1;
            //     }
            // }
            var idQList = new Dictionary<int, List<int>>();
            foreach (var item in ConfigService.ChildSkillQualityList)
            {
                idQList.Add(item.Key, new List<int>(item.Value));
            }
            // var idList = ConfigService.ChildSkillItems.Keys.ToList();
            for (int i = 0; i < count; i++)
            {
                // 选品质
                int quality = 1;
                int rq = Random.Next(100) + 1;
                // 50  30  20
                // 20% 高级
                if (rq <= 20)
                {
                    quality = 3;
                }
                // 30% 中级
                else if (rq <= 50)
                {
                    quality = 2;
                }
                // 20% 高级
                else
                {
                    quality = 1;
                }
                if (idQList.GetValueOrDefault(quality, null) == null) {
                    LogError($"孩子洗炼，随机技能取不到品质{quality}");
                    SendNotice("孩子洗炼失败");
                    return;
                }
                // 选ID
                var index = Random.Next(idQList[quality].Count);
                var selectedId = idQList[quality][index];
                preview.Add(selectedId);
                // 删除该品质下的这个ID
                idQList[quality].RemoveAt(index);
                // 删除其他品质下的相同index技能
                var sIndex = ConfigService.ChildSkillItems[selectedId].index;
                foreach (var item in idQList)
                {
                    // 跳过选中的品质，因为已经删除了
                    if (item.Key == quality) continue;
                    for (int idx = 0; idx < item.Value.Count; idx++)
                    {
                        var sid = item.Value[idx];
                        // 找到相同index，这个index一个品质只有1个
                        if (sIndex == ConfigService.ChildSkillItems[sid].index)
                        {
                            item.Value.RemoveAt(idx);
                            break;
                        }
                    }
                }
            }
            var result = await AddBagItem(itemId, -1, true, "孩子洗炼");
            if (!result)
            {
                SendNotice(string.Format("扣除<color=#ff0000>{0}</c>失败", ConfigService.Items[itemId].Name));
                return;
            }
            Child.preview = preview;
            SyncChild();
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
            SendNotice("洗炼成功");

            //孩子洗练成功，加计数
            await RedisService.AddOperateTimes(RoleId, RoleInfoFields.OperateHaiZiXiLian, 1);

        }
        // 孩子--替换
        private async Task ReqChildReplace()
        {
            // 没有孩子
            if (!HasChild()) return;
            if (Child.preview.Count <= 0)
            {
                SendNotice("先洗炼，才能替换");
                return;
            }
            Child.skill = Child.preview;
            Child.preview = new();
            SyncChild();
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
            SendNotice("替换成功");
        }

        // 孩子--定制   // 孩子定制 星阵定制按钮 全部更换为满500次炼化 保底，点击保底之后可以选择定制属性。
        private async Task ReqChildDingZhi(List<int> skills)
        {
            // 没有孩子
            if (!HasChild()) return;
            //孩子定制按钮 全部更换为满500次炼化 保底，点击保底之后可以选择定制属性。
            // uint times = await RedisService.GetOperateTimes(RoleId, RoleInfoFields.OperateHaiZiXiLian);
            // if (times < 500)
            // {
            //     return;
            // }

            var _skills = new List<int>();
            foreach (var s in skills)
            {
                ConfigService.ChildSkillItems.TryGetValue(s, out var sc);
                if (sc == null)
                {
                    SendNotice("请先选择3个技能");
                    return;
                }
                if (!_skills.Contains(s))
                {
                    _skills.Add(s);
                }
            }
            if (_skills.Count != 3)
            {
                SendNotice("请先选择3个技能");
                return;
            }
            uint itemCfgId = 500055;
            if (GetBagItemNum(itemCfgId) < 1)
            {
               ConfigService.Items.TryGetValue(itemCfgId, out var icfg);
               if (icfg != null)
               {
                   SendNotice("缺少" + icfg.Name);
               }
               else
               {
                   SendNotice("缺少定制券");
               }
               return;
            }
            // 减少一个定制券
            await AddItem(itemCfgId, -1, true, String.Format("定制孩子"));
            Child.skill = _skills;
            SyncChild();
            var resp = new S2C_ChildInfo() { Info = Entity.Child };
            await SendPacket(GameCmd.S2CChildInfo, resp);
            SendNotice("定制成功");

            //孩子定制成功后，洗炼次数置零。
            await RedisService.SetOperateTimes(RoleId, RoleInfoFields.OperateHaiZiXiLian, 0);
        }

        // 转盘--信息
        private async Task ReqLuckyDrawInfo()
        {
            var respBytes = await ServerGrain.QueryLuckyDrawChest();
            if (respBytes.Value != null)
            {
                var chest = LuckyDrawChest.Parser.ParseFrom(respBytes.Value);
                await SendPacket(GameCmd.S2CLuckyDrawInfo, new S2C_LuckyDrawInfo()
                {
                    Chest = chest,
                    CurrentPoint = LuckyDrawPoint,
                    Cost = GameDefine.LuckyDrawTurnCost,
                    Free = await RedisService.GetLuckyDrawFree(RoleId),
                    CanOpenChest = true //!await RedisService.IsLuckyDrawChestGot(RoleId)
                });
            }
        }
        // 转盘--抽奖
        private async Task ReqLuckyDrawTurn(uint count)
        {
            // 响应
            var resp = new S2C_LuckyDrawTurn();
            if (count <= 0)
            {
                count = 1;
            }
            if (count > 5)
            {
                count = 5;
            }
            var cost = count * GameDefine.LuckyDrawTurnCost;
            // 免费只可以一次一次的来
            if (count == 1)
            {
                var free = await RedisService.GetLuckyDrawFree(RoleId);
                // 免费次数够用？
                if (free >= count)
                {
                    // 设置新的免费次数
                    await RedisService.SetLuckyDrawFree(RoleId, free - count);
                    cost = 0;
                }
            }
            // 消耗大于0？
            if (cost > 0)
            {
                //消耗开宝箱钥匙（同舟之匙）
                uint costItemId = 100329;
                if (GetBagItemNum(costItemId) < cost)
                {
                    resp.ErrorMsg = "同舟之匙数量不足";
                    await SendPacket(GameCmd.S2CLuckyDrawTurn, resp);
                    return;
                }
                // 消耗开宝箱钥匙
                var ret = await AddBagItem(costItemId, -(int)cost, tag: "转盘-开宝箱钥匙消耗");
                if (!ret)
                {
                    resp.ErrorMsg = "同舟之匙数量不足";
                    await SendPacket(GameCmd.S2CLuckyDrawTurn, resp);
                    return;
                }
            }
            resp.CurrentPoint = LuckyDrawPoint;
            // 转盘奖励
            var itemList = ConfigService.LuckyDrawConfig.drawItems;
            // 抽count次
            // 转盘物品
            var circleList = new List<DrawItem>();
            // 扩充的物品索引列表
            var circleIndexList = new List<uint>();
            for (int j = 0; j < count; j++)
            {
                circleList.Clear();
                circleIndexList.Clear();
                var one = new LuckyDrawOneTurn();
                // 风雨值
                resp.CurrentPoint += ConfigService.LuckyDrawConfig.drawPoint;
                for (uint i = 0; i < 8; i++)
                {
                    var ri = itemList[Random.Next(itemList.Count)];
                    circleList.Add(ri);
                    circleIndexList.AddRange(Enumerable.Repeat(i, (int)ri.rate).ToList());
                    one.ItemList.Add(new LuckyDrawItem() { CfgId = ri.id, Num = ri.num, Index = i });
                }
                one.HitIndex = circleIndexList[Random.Next(0, circleIndexList.Count)];
                var item = circleList[(int)one.HitIndex];
                await AddBagItem(item.id, (int)item.num, tag: "转盘抽奖");
                resp.TurnList.Add(one);
            }
            LuckyDrawPoint = resp.CurrentPoint;
            await RedisService.SetRoleLuckyPoint(RoleId, LuckyDrawPoint);
            await SendPacket(GameCmd.S2CLuckyDrawTurn, resp);
        }
        // 转盘--开宝箱
        private async Task ReqLuckyDrawOpenChest()
        {
            var resp = new S2C_LuckyDrawOpenChest();
            if (LuckyDrawPoint < ConfigService.LuckyDrawConfig.fullPoint)
            {
                resp.ErrorMsg = "风雨值不够";
                await SendPacket(GameCmd.S2CLuckyDrawOpenChest, resp);
                return;
            }
            // var isGot = await RedisService.IsLuckyDrawChestGot(RoleId);
            // if (isGot)
            // {
            //     resp.ErrorMsg = "本期宝箱已经打开";
            //     await SendPacket(GameCmd.S2CLuckyDrawOpenChest, resp);
            //     return;
            // }
            var respBytes = await ServerGrain.QueryLuckyDrawChest();
            if (respBytes.Value == null)
            {
                resp.ErrorMsg = "活动未开启";
                await SendPacket(GameCmd.S2CLuckyDrawOpenChest, resp);
                return;
            }
            var chest = LuckyDrawChest.Parser.ParseFrom(respBytes.Value);
            var itemList = chest.ItemList;
            uint index = 0;
            foreach (var item in itemList)
            {
                await AddBagItem(item.CfgId, (int)item.Num, tag: "转盘宝箱");
                index++;
            }
            await CreatePet(chest.PetCfgId);
            // await RedisService.SetLuckyDrawChestGot(RoleId);
            //扣减风雨值
            LuckyDrawPoint -= ConfigService.LuckyDrawConfig.fullPoint;
            await RedisService.SetRoleLuckyPoint(RoleId, LuckyDrawPoint);
            resp.Chest = chest;
            await SendPacket(GameCmd.S2CLuckyDrawOpenChest, resp);
        }
        private ExpExchangeInfo GetExpExchangeInfo()
        {
            var times = Entity.ExpExchangeTimes;
            Exp2PotentialConfig config = times >= ConfigService.Exp2PotentialList.Count ? null : ConfigService.Exp2PotentialList[(int)times];
            return new ExpExchangeInfo()
            {
                // 需要转生等级
                Relive = 3,
                // 需要等级
                Level = 180,
                // 已经兑换次数
                CurrentTimes = times,
                // 总共可兑换次数
                TotalTimes = ConfigService.Exp2PotentialTotalTimes,
                // 当前兑换到的潜能
                CurrentPotential = ExchangedPotential,
                // 总共可以兑换的潜能
                TotalPotential = ConfigService.Exp2PotentialTotalPotential,
                // 本次兑换所需经验
                NeedExp = config == null ? 0 : config.exp,
                // 本次兑换得到潜能
                EarnPotential = config == null ? 0 : config.potential,
                // 当前经验
                CurrentExp = Entity.Exp,
                // 仙玉消耗
                Cost = (times + 1) * 100000,
            };
        }
        // 经验兑换潜能--信息
        private async Task ReqExpExchangeInfo()
        {
            var resp = new S2C_ExpExchangeInfo() { Info = GetExpExchangeInfo() };
            await SendPacket(GameCmd.S2CExpExchangeInfo, resp);
        }
        // 经验兑换潜能--兑换
        private async Task ReqExpExchange()
        {
            var resp = new S2C_ExpExchange();
            var info = GetExpExchangeInfo();
            if (Entity.Relive >= 4 || (Entity.Relive >= info.Relive && Entity.Level >= info.Level))
            {
                if (Entity.Exp >= info.NeedExp)
                {
                    if (GetMoney(MoneyType.Jade) >= info.Cost)
                    {
                        if (await CostMoney(MoneyType.Jade, info.Cost, tag: "经验兑换潜能消耗"))
                        {
                            Entity.Exp -= info.NeedExp;
                            Entity.ExpExchangeTimes += 1;
                            CalcExchangePotential();
                            await FreshAllSchemeAttrs();
                            await SendRoleExp();
                            resp.Info = GetExpExchangeInfo();
                        }
                        else
                        {
                            resp.Msg = "兑换无法完成";
                        }
                    }
                    else
                    {
                        resp.Msg = "当前仙玉不足，无法完成兑换";
                    }
                }
                else
                {
                    resp.Msg = "当前经验不足，无法完成兑换";
                }
            }
            else
            {
                resp.Msg = $"兑换需要{info.Relive}转{info.Level}级以上";
            }
            await SendPacket(GameCmd.S2CExpExchange, resp);
        }

        // 限时充值排行榜--排行榜信息
        private async Task ReqLimitChargeRankInfo()
        {
            S2C_LimitChargeRankInfo resp = null;
            var startTimestamp = await RedisService.GetLimitChargeStartTimestamp(Entity.ServerId);
            var endTimestamp = await RedisService.GetLimitChargeEndTimestamp(Entity.ServerId);
            var iconTimestamp = endTimestamp + GameDefine.LimitChargeIconDelay;
            var now = TimeUtil.TimeStamp;
            // 已经不用显示图标了
            // if (startTimestamp <= 0 || endTimestamp <= 0 || now >= iconTimestamp || now < startTimestamp)
            // {
            //     resp = new S2C_LimitChargeRankInfo();
            //     resp.Enabled = false;
            //     resp.StartTimestamp = startTimestamp;
            //     resp.EndTimestamp = endTimestamp;
            // }
            // else
            // {
                // 防止恶意请求, 间隔5s才重新刷新数据
                if (_lastLimitRankInfoResp == null || now - _lastFetchLimitPayRankTime > 5)
                {
                    var list = await RedisService.GetLimitChargeRankList(Entity.ServerId);
                    var myRank = await RedisService.GetLimitChargeRankRoleRank(Entity.ServerId, Entity.Id);
                    var mySettleRank = await RedisService.GetRoleLimitChargeRankSettleRank(Entity.Id);
                    var myRecState = await RedisService.GetRoleLimitChargeRankRecState(Entity.Id);
                    _lastLimitRankInfoResp = new S2C_LimitChargeRankInfo
                    {
                        Enabled = true,
                        StartTimestamp = startTimestamp,
                        EndTimestamp = endTimestamp,
                        List = { list },
                        MyRank = (uint)(myRank + 1),
                        MySettleRank = mySettleRank,
                        MyRecState = myRecState
                    };
                    _lastFetchLimitPayRankTime = now;
                }
                resp = _lastLimitRankInfoResp;
            // }
            await SendPacket(GameCmd.S2CLimitChargeRankInfo, resp);
        }

        // 限时充值排行榜--获取排行奖励
        private async Task ReqLimitChargeRankGiftGet()
        {
            var resp = new S2C_LimitChargeRankGiftGet();
            //检查限时排行榜是否结束
            var end = await RedisService.GetLimitChargeEndTimestamp(Entity.ServerId);
            var now = TimeUtil.TimeStamp;
            if (end >= 0)
            {
                if (TimeUtil.TimeStamp < end) {
                    resp.Msg = "排行榜还没有结束";
                    await SendPacket(GameCmd.S2CLimitChargeRankGiftGet, resp);
                    return;
                }
            }
            //检查玩家的排名
            var settleRank = await RedisService.GetRoleLimitChargeRankSettleRank(Entity.Id);
            if (settleRank == 0 || settleRank > 10)
            {
                resp.Msg = "没有进入排行榜前十名";
                await SendPacket(GameCmd.S2CLimitChargeRankGiftGet, resp);
                return;
            }
            //检查玩家的奖励领取状态
            var recState = await RedisService.GetRoleLimitChargeRankRecState(RoleId);
            if (recState <= 0)
            {
                resp.Msg = "限时排行榜奖没有获得奖励";
            } 
            else if (recState >= 2)
            {
                resp.Msg = "限时排行榜奖励已领取";
            }
            else
            {
                await RedisService.SetRoleLimitChargeRankRecState(RoleId, 2);
                //根据排名发放奖励
                var config = ConfigService.LimitRankPrizeConfigList[(int)settleRank];
                var item = config.prize1;
                if (item != null && item.Count > 0)
                {
                    foreach (var i in item)
                    {
                        if (i.num > 0)
                        {
                            ConfigService.Items.TryGetValue(i.id, out var iconfig);
                            if (iconfig != null)
                            {
                                await AddBagItem(i.id, (int)i.num, tag: "限时充值排行领奖");
                            }
                        }
                    }
                }
                resp.Msg = "排行榜奖励领取成功";
            }
            await SendPacket(GameCmd.S2CLimitChargeRankGiftGet, resp);
        }

        // 限时等级排行榜--排行榜信息
        private async Task ReqLimitLevelRankInfo()
        {
            S2C_LimitLevelRankInfo resp = null;
            var startTimestamp = await RedisService.GetLimitLevelStartTimestamp(Entity.ServerId);
            var endTimestamp = await RedisService.GetLimitLevelEndTimestamp(Entity.ServerId);
            var iconTimestamp = endTimestamp + GameDefine.LimitLevelIconDelay;
            var now = TimeUtil.TimeStamp;
            // // 已经不用显示图标了
            // if (startTimestamp <= 0 || endTimestamp <= 0 || now >= iconTimestamp || now < startTimestamp)
            // {
            //     resp = new S2C_LimitLevelRankInfo();
            //     resp.Enabled = false;
            //     resp.StartTimestamp = startTimestamp;
            //     resp.EndTimestamp = endTimestamp;
            // }
            // else
            // {
                // 防止恶意请求, 间隔5s才重新刷新数据
                if (_lastLimitLevelRankInfoResp == null || now - _lastFetchLimitLevelRankTime > 5)
                {
                    var list = await RedisService.GetLimitRoleLevelRankList(Entity.ServerId);
                    var myRank = await RedisService.GetLimitRoleLevelRankIndex(Entity.ServerId, Entity.Id);
                    var mySettleRank = await RedisService.GetRoleLimitLevelRankSettleRank(Entity.Id);
                    var myRecState = await RedisService.GetRoleLimitLevelRankRecState(Entity.Id);
                    _lastLimitLevelRankInfoResp = new S2C_LimitLevelRankInfo
                    {
                        Enabled = true,
                        StartTimestamp = startTimestamp,
                        EndTimestamp = endTimestamp,
                        List = { list },
                        MyRank = (uint)(myRank + 1),
                        MySettleRank = mySettleRank,
                        MyRecState = myRecState
                    };
                    _lastFetchLimitLevelRankTime = now;
                }
                resp = _lastLimitLevelRankInfoResp;
            // }
            await SendPacket(GameCmd.S2CLimitLevelRankInfo, resp);
        }

        // 限时等级排行榜--获取排行奖励
        private async Task ReqLimitLevelRankGiftGet()
        {
            var resp = new S2C_LimitLevelRankGiftGet();
            //检查限时排行榜是否结束
            var end = await RedisService.GetLimitLevelEndTimestamp(Entity.ServerId);
            var now = TimeUtil.TimeStamp;
            if (end >= 0)
            {
                if (TimeUtil.TimeStamp < end)
                {
                    resp.Msg = "排行榜还没有结束";
                    await SendPacket(GameCmd.S2CLimitLevelRankGiftGet, resp);
                    return;
                }
            }
            //检查玩家的排名
            var settleRank = await RedisService.GetRoleLimitLevelRankSettleRank(Entity.Id);
            if (settleRank == 0 || settleRank > 10)
            {
                resp.Msg = "没有进入排行榜前十名";
                await SendPacket(GameCmd.S2CLimitLevelRankGiftGet, resp);
                return;
            }
            //检查玩家的奖励领取状态
            var recState = await RedisService.GetRoleLimitLevelRankRecState(RoleId);
            if (recState <= 0)
            {
                resp.Msg = "限时排行榜奖没有获得奖励";
            }
            else if (recState >= 2)
            {
                resp.Msg = "限时排行榜奖励已领取";
            }
            else
            {
                await RedisService.SetRoleLimitLevelRankRecState(RoleId, 2);
                //await AddMoney(MoneyType.Jade, 999, "限时排行榜奖励");
                //根据排名发放奖励
                var config = ConfigService.LimitRankPrizeConfigList[(int)settleRank];
                var item = config.prize2;
                if (item != null && item.Count > 0)
                {
                    foreach (var i in item)
                    {
                        if (i.num > 0)
                        {
                            ConfigService.Items.TryGetValue(i.id, out var iconfig);
                            if (iconfig != null)
                            {
                                await AddBagItem(i.id, (int)i.num, tag: "限时等级排行领奖");
                            }
                        }
                    }
                }
                resp.Msg = "排行榜奖励领取成功";
            }
            await SendPacket(GameCmd.S2CLimitLevelRankGiftGet, resp);
        }

        // 获取VIP信息
        private async Task ReqVipInfo()
        {
            var resp = new S2C_VipInfo();
            resp.VipLevel = VipLevel;
            resp.VipExp = Entity.TotalPayBS;
            resp.CanGet = !await RedisService.IsRoleVipGiftDailyGet(RoleId);
            await SendPacket(GameCmd.S2CVipInfo, resp);
        }
        // 获取VIP奖励      每天均可领取
        private async Task ReqVipGiftGet()
        {
            var resp = new S2C_VipGiftGet();
            if (VipLevel <= 0) {
                resp.Msg = "VIP1级后才有特权";
                await SendPacket(GameCmd.S2CVipGiftGet, resp);
                return;
            }

            // 0点到1点 无法领取
            var now = DateTimeOffset.Now;
            if (now.Hour == 0 && now.Minute <= 59)
            {
                SendNotice("0点到1点 无法领取");
                return;
            }

            var got = await RedisService.IsRoleVipGiftDailyGet(RoleId);
            if (got)
            {
                resp.Msg = "今日已领取";
            }
            else
            {
                await RedisService.SetRoleVipGiftDailyGot(RoleId);
                var config = ConfigService.VipConfigList[(int)VipLevel];
                int money = (int)config.gift.money;
                if (money > 0)
                {
                    var payRate = await RedisService.GetPayRateJade();
                    var ret = await OnPayed(money, money * (int)payRate);
                }
                var item = config.gift.item;
                if (item != null && item.Count > 0)
                {
                    foreach (var i in item)
                    {
                        if (i.num > 0)
                        {
                            ConfigService.Items.TryGetValue(i.id, out var iconfig);
                            if (iconfig != null)
                            {
                                await AddBagItem(i.id, (int)i.num, tag: "VIP领奖");
                            }
                        }
                    }
                }
                resp.Msg = "领取成功";
            }
            await SendPacket(GameCmd.S2CVipGiftGet, resp);
        }

        // 双倍经验信息
        private async ValueTask<X2ExpInfo> GetX2ExpInfo()
        {
            var config = ConfigService.X2ExpConfigList[(int)VipLevel];
            return new X2ExpInfo()
            {
                Left = await RedisService.GetRoleX2ExpLeft(RoleId),
                Current = await RedisService.GetRoleX2ExpCurrentGot(RoleId),
                Total = config.total,
                GetOnce = config.get_once,
            };
        }
        // 双倍经验--信息
        private async Task ReqX2ExpInfo()
        {
            var resp = new S2C_X2ExpInfo() { Info = await GetX2ExpInfo() };
            await SendPacket(GameCmd.S2CX2ExpInfo, resp);
        }

        // 双倍经验--领取
        private async Task ReqX2ExpGet()
        {
            var resp = new S2C_X2ExpGet();
            var info = await GetX2ExpInfo();
            if (info.Current >= info.Total)
            {
                resp.Msg = "双倍经验，今日已领完";
            }
            else
            {
                await RedisService.SetRoleX2ExpCurrentGot(RoleId, info.Current + info.GetOnce);
                await RedisService.SetRoleX2ExpLeft(RoleId, info.Left + info.GetOnce);
                resp.Info = await GetX2ExpInfo();
            }
            await SendPacket(GameCmd.S2CX2ExpGet, resp);
        }

        //天策符--列表
        private async Task ReqTianceFuGetList()
        {
            var resp = new S2C_TianceFuGetList();
            resp.List.AddRange(Tiance.list);
            await SendPacket(GameCmd.S2CTianceFuGetList, resp);
        }
        //天策符--合成
        private async Task ReqTianceFuHeCheng(uint num)
        {
            if (num > 10)
            {
                SendNotice("一次最多合成10个");
                return;
            }
            // 最少合成1个
            if (num <= 0)
            {
                num = 1;
            }
            // 检查碎片配置
            uint itemId = GameDefine.TianceFuSuiPianItemId;
            ConfigService.Items.TryGetValue(itemId, out var config);
            if (config == null)
            {
                SendNotice("内部错误，请稍候再试");
                return;
            }
            // 检查需要碎片数量
            uint needNum = num * (uint)(config.Num > 0 ? config.Num : 10);
            if (GetBagItemNum(itemId) < needNum)
            {
                SendNotice("碎片数量不足");
                return;
            }
            // 合成待鉴定天策符
            var list = GameDefine.TianceFuDaiJianDingItemId;
            var dic = new Dictionary<uint, int>();
            for (int i = 0; i < num; i++)
            {
                var id = list[Random.Next(list.Count)];
                if (dic.ContainsKey(id))
                {
                    dic[id] += 1;
                }
                else
                {
                    dic[id] = 1;
                }
            }
            // 扣除碎片
            await AddItem(itemId, -(int)needNum, tag: "天策符合成--消耗");
            // 添加待鉴定天策符
            foreach (var kv in dic)
            {
                await AddItem(kv.Key, kv.Value, tag: "天策符合成--生成");
            }
            SendNotice($"成功合成{num}张待鉴定天策符，消耗{needNum}张碎片");
        }
        //天策符--鉴定
        private async Task ReqTianceFuJianDing(TianceFuType type, uint num)
        {
            if (num > 10)
            {
                SendNotice("一次最多鉴定10个");
                return;
            }
            // 最少鉴定1个
            if (num <= 0)
            {
                num = 1;
            }
            // 检查类型对应的天策符
            ConfigService.TianceFuListByType.TryGetValue(type, out var list);
            if (list == null || list.Count <= 0)
            {
                SendNotice("内部错误，请稍候再试");
                return;
            }
            // 检查待鉴定物品ID
            GameDefine.TianceFuType2DaiJianDingItemId.TryGetValue(type, out var itemId);
            if (GetBagItemNum(itemId) < num)
            {
                SendNotice("待鉴定天策符数量不足");
                return;
            }
            // 消耗仙玉数量检查
            var needJade = num * 10000;
            if (GetMoney(MoneyType.Jade) < needJade)
            {
                SendNotice($"仙玉不足，需要消耗{needJade}仙玉！");
                return;
            }
            await CostMoney(MoneyType.Jade, needJade, false, "天策符鉴定--消耗");
            var shouldSkip = false;
            var resp = new S2C_TianceFuJianDing();
            // 开始鉴定
            for (int i = 0; i < num; i++)
            {
                var config = list[Random.Next(list.Count)];
                SkillId skillid = config.skillId;
                var sconfig = ConfigService.TianceSkillList.GetValueOrDefault(skillid, null);
                shouldSkip = false;
                // 暂时屏蔽没有配置技能属性的天策符
                if (sconfig == null)
                {
                    shouldSkip = true;
                }
                // 千面符--跳过
                if (skillid >= SkillId.QianMian1 && skillid <= SkillId.QianMian3)
                {
                    shouldSkip = true;
                }
                // 度厄符--跳过
                else if (skillid >= SkillId.DuE1 && skillid <= SkillId.DuE3)
                {
                    shouldSkip = true;
                }
                // 是否应该跳过？
                if (shouldSkip)
                {
                    i--;
                    continue;
                }
                var id = $"{TimeUtil.TimeStamp}_{Random.Next(1000, 10000)}";
                var addition = (uint)Random.Next(0, GameDefine.TianCeFuMaxLevel) + 1;
                uint grade = 1;
                if (config.tier >= 3 && config.tier < 5 && addition >= 8)
                {
                    grade = (uint)Random.Next(1, 4);
                }
                else if (config.tier == 5 && addition >= 6)
                {
                    grade = (uint)Random.Next(1, 4);
                }
                else
                {
                    grade = (uint)Random.Next(1, 2);
                }
                if (grade == 0)
                {
                    grade = 1;
                }
                if (grade == 2)
                {
                    skillid += 1;
                }
                else if (grade == 3)
                {
                    skillid += 2;
                }
                // 新天策符
                var fu = new TianceFu()
                {
                    Id = id,
                    Type = type,
                    Name = config.name,
                    Grade = grade,
                    Addition = addition,
                    SkillId = skillid,
                    Tier = config.tier,
                    State = 0,
                };
                Tiance.list.Add(fu);
                resp.List.Add(fu);
            }
            await SyncTiance();
            await AddItem(itemId, -(int)num, tag: "天策符鉴定--消耗");
            await SendPacket(GameCmd.S2CTianceFuJianDing, resp);
        }
        //天策符--分解
        private async Task ReqTianceFuFengJie(List<string> list)
        {
            if (list.Count <= 0)
            {
                SendNotice("至少要选择1个天策符才能分解");
                return;
            }
            var itemId = GameDefine.TianceFuSuiPianItemId;
            List<TianceFu> toDelete = new();
            foreach (var id in list)
            {
                foreach (var fu in Tiance.list)
                {
                    if (fu.Id.Equals(id))
                    {
                        toDelete.Add(fu);
                        if (fu.Grade == 1)
                        {
                            _ = await AddItem(itemId, 1, tag: "天策符分解--获得");
                        }
                        if (fu.Grade == 2)
                        {
                            _ = await AddItem(itemId, 5, tag: "天策符分解--获得");
                        }
                        if (fu.Grade == 3)
                        {
                            _ = await AddItem(itemId, 10, tag: "天策符分解--获得");
                        }
                    }
                }
            }
            foreach (var fu in toDelete)
            {
                Tiance.list.Remove(fu);
            }
            await SyncTiance();
            await ReqTianceFuGetList();
        }
        //天策符--使用
        private async Task ReqTianceFuUse(string id, TianceFuState state)
        {
            TianceFu fu = null;
            foreach (var f in Tiance.list)
            {
                if (f.Id.Equals(id))
                {
                    fu = f;
                    break;
                }
            }
            if (fu == null)
            {
                SendNotice("你没有这个天策符");
                return;
            }
            // 不是通用位置，并且不是卸载，则需要检查等级限制
            if (state != TianceFuState.PosCommon && state != TianceFuState.Unknown)
            {
                var typeDic = ConfigService.tianceLevelLimit.GetValueOrDefault(fu.Type, null);
                if (typeDic == null)
                {
                    SendNotice($"系统错误，请稍候再试（找到不到天策符类型{fu.Type}）");
                    return;
                }
                var tierDic = typeDic.GetValueOrDefault(fu.Tier, null);
                if (tierDic == null)
                {
                    SendNotice($"系统错误，请稍候再试（找到不到天策符层数{fu.Tier}）");
                    return;
                }
                var limit = tierDic.GetValueOrDefault(state, (uint)0);
                if (limit <= 0)
                {
                    SendNotice($"系统错误，请稍候再试（找到不到天策符位置{state}）");
                    return;
                }
                if (Tiance.level < limit)
                {
                    SendNotice($"天演策{limit}级后才能使用此符");
                    return;
                }
            }
            foreach (var f in Tiance.list)
            {
                //卸下通用位置的天策符
                if (state == TianceFuState.PosCommon && fu.Type == f.Type && state == f.State)
                {
                    f.State = TianceFuState.Unknown;
                    break;
                }
                //根据层数和类型卸下天策符
                if (fu.Type == f.Type && fu.Tier == f.Tier && state == f.State)
                {
                    f.State = TianceFuState.Unknown;
                }
                //卸下相同的天策符 （相同的天策符只能装备一个）
                if (fu.Name.Equals(f.Name) && f.State > TianceFuState.Unknown)
                {
                    f.State = TianceFuState.Unknown;
                }
            }
            fu.State = state;
            await SyncTiance(true);
            await ReqTianceFuGetList();
        }
        //打开天演策界面
        private async Task ReqTianYanCeOpen()
        {
            var resp = new S2C_TianYanCeOpen() { Level = Tiance.level };
            await SendPacket(GameCmd.S2CTianYanCeOpen, resp);
        }
        //天演策升级消耗
        private TianyanceLevelupCost tianyanceLevelupNeeded(uint level)
        {
            TianyanceLevelupCost cost = new() { jade = 0, bindJade = 0, item = new() };
            if (Tiance.level >= GameDefine.TianYanCeMaxLevel || level <= 0)
            {
                return cost;
            }
            level = Math.Min(level, GameDefine.TianYanCeMaxLevel - Tiance.level);
            for (var i = Tiance.level; i < Tiance.level + level; i++)
            {
                var c = ConfigService.TianceLevelups[i];
                cost.jade += c.jade;
                cost.bindJade += c.bindJade;
                if (c.item != null && c.item.id > 0 && c.item.num > 0)
                {
                    var index = cost.item.FindIndex(a => a.id == c.item.id);
                    if (index >= 0)
                    {
                        cost.item[index].num += c.item.num;
                    }
                    else
                    {
                        cost.item.Add(new() { id = c.item.id, num = c.item.num });
                    }
                }
            }
            return cost;
        }
        //天演策升级
        private async Task ReqTianYanCeUpgrade(uint level)
        {
            if (Tiance.level >= GameDefine.TianYanCeMaxLevel)
            {
                SendNotice("等级已达到上限");
                return;
            }
            var target = Tiance.level + level;
            if (target > GameDefine.TianYanCeMaxLevel)
            {
                SendNotice($"最大等级150，最多能升{GameDefine.TianYanCeMaxLevel - Tiance.level}级");
                return;
            }
            var cost = tianyanceLevelupNeeded(level);
            if (GetMoney(MoneyType.Jade) < cost.jade)
            {
                SendNotice("升级失败，仙玉不足");
                return;
            }
            if (GetMoney(MoneyType.BindJade) < cost.bindJade)
            {
                SendNotice("升级失败，积分不足");
                return;
            }
            foreach (var i in cost.item)
            {
                if (GetBagItemNum(i.id) < i.num)
                {
                    SendNotice($"升级失败，{ConfigService.Items[i.id].Name}不够");
                    return;
                }
            }
            await CostMoney(MoneyType.Jade, cost.jade, false, tag: "天演策升级--消耗");
            await CostMoney(MoneyType.BindJade, cost.bindJade, false, tag: "天演策升级--消耗");
            foreach (var i in cost.item)
            {
                await AddItem(i.id, -(int)i.num, tag: "天演策升级--消耗");
            }
            Tiance.level += level;
            await SyncTiance(true);
            SendNotice($"天演策等级提升到{Tiance.level}级");
            await ReqTianYanCeOpen();
        }
        //天策符--百变鉴定
        private async Task ReqTiancFuJianDingBaiBian(uint itemId, uint tianCeFuId)
        {
            if (itemId >= 100324 && itemId <= 100326)
            {
                if (GetBagItemNum(itemId) < 1)
                {
                    SendNotice("没有找到百变符！");
                    return;
                }
                var resp = new S2C_TianceFuJianDing();
                var config = ConfigService.TianceFuListAll.GetValueOrDefault(tianCeFuId, null);
                if (config == null)
                {
                    SendNotice("鉴定失败，请稍候再试");
                    return;
                }
                if ((uint)config.type != (itemId - 100324 + 1))
                {
                    SendNotice("鉴定失败，请稍候再试");
                    return;
                }
                uint addition = 10;
                uint grade = 3;
                SkillId skillid = config.skillId + 2;
                var sconfig = ConfigService.TianceSkillList.GetValueOrDefault(skillid, null);
                if (sconfig == null)
                {
                    SendNotice("鉴定失败，请稍候再试");
                    return;
                }
                // 千面符--跳过
                if (skillid >= SkillId.QianMian1 && skillid <= SkillId.QianMian3)
                {
                    SendNotice("鉴定失败，请稍候再试");
                    return;
                }
                // 度厄符--跳过
                else if (skillid >= SkillId.DuE1 && skillid <= SkillId.DuE3)
                {
                    SendNotice("鉴定失败，请稍候再试");
                    return;
                }
                var id = $"{TimeUtil.TimeStamp}_{Random.Next(1000, 10000)}";

                // 新天策符
                var fu = new TianceFu()
                {
                    Id = id,
                    Type = config.type,
                    Name = config.name,
                    Grade = grade,
                    Addition = addition,
                    SkillId = skillid,
                    Tier = config.tier,
                    State = 0,
                };
                Tiance.list.Add(fu);
                resp.List.Add(fu);
                await SyncTiance();
                await AddItem(itemId, -1, tag: "百变天策符鉴定--消耗");
                await SendPacket(GameCmd.S2CTianceFuJianDing, resp);
                SendNotice($"成功鉴定获得一张10级{config.name}（{sconfig.type}）");
            }
            else
            {
                SendNotice("请选择一张百变天策符鉴定！");
                return;
            }
        }

        //切割--进入
        private async Task ReqQieGeEnter()
        {
            var resp = new S2C_QieGeEnter();
            resp.Level = QieGeLevel;
            resp.Exp = QieGeExp;
            await SendPacket(GameCmd.S2CQieGeEnter, resp);
        }
        //切割--升级
        private async Task ReqQieGeUpgrade(bool quick)
        {
            uint multi = 10;
            if (quick) {
                multi = 1000;
            }
            // 满级判断
            if (QieGeLevel >= (ConfigService.QieGeLevelList.Count - 1))
            {
                SendNotice("已满级，无需升级");
                return;
            }
            // 积分判断
            if (GetMoney(MoneyType.BindJade) < 1*multi)
            {
                SendNotice("积分不足");
                return;
            }
            var levelConfig = ConfigService.QieGeLevelList.GetValueOrDefault(QieGeLevel, null);
            if (levelConfig == null)
            {
                SendNotice("内部错误，请稍候再试");
                return;
            }
            var level = QieGeLevel;
            var exp = QieGeExp + 100*multi;
            while (exp >= levelConfig.exp)
            {
                exp -= levelConfig.exp;
                level += 1;
                levelConfig = ConfigService.QieGeLevelList.GetValueOrDefault(level, null);
                if (levelConfig == null)
                {
                    SendNotice("内部错误，请稍候再试");
                    return;
                }
            }
            await CostMoney(MoneyType.BindJade, 1*multi, false, "切割升级--消耗");
            var needMapSync = false;
            if (level != QieGeLevel)
            {
                SendNotice($"恭喜神器升{level - QieGeLevel}级，到{level}级");
                needMapSync = true;
            }
            QieGeExp = exp;
            QieGeLevel = level;
            await SyncQieGe(needMapSync);
            if (needMapSync) {
                // 属性方案刷新潜能和属性
                await FreshAllSchemeAttrs();
            }   
            var resp = new S2C_QieGeEnter() { Exp = exp, Level = level };
            await SendPacket(GameCmd.S2CQieGeEnter, resp);
        }

        //神之力--查询信息
        private async Task ReqShenZhiLiInfo()
        {
            var resp = new S2C_ShenZhiLiInfo();
            resp.HurtLv = ShenZhiLiHurtLv;
            resp.HpLv = ShenZhiLiHpLv;
            resp.SpeedLv = ShenZhiLiSpeedLv;
            await SendPacket(GameCmd.S2CShenZhiLiInfo, resp);
        }
        //神之力--升级
        private async Task ReqShenZhiLiUpgrade(uint lvType)
        {
            //每次升级，消耗100神格 
            uint costItemId = 500077;
            if (GetBagItemNum(costItemId) < 100)
            {
                SendNotice("神格数量不足");
                return;
            }

            var newLevel = ShenZhiLiHurtLv;
            if (lvType == 1) {
                newLevel = ShenZhiLiHurtLv + 1;
            } else if (lvType == 2) {
                newLevel = ShenZhiLiHpLv + 1;
            } else if (lvType == 3) {
                newLevel = ShenZhiLiSpeedLv + 1;
            } else {
                SendNotice("神之力-类型错误");
                return;
            }

            // 消耗神格
            var ret = await AddBagItem(costItemId, -100, tag: "神之力-升级消耗神格");
            if (!ret)
            {
                SendNotice("神格数量不足");
                return;
            }
            //增加等级
            if (lvType == 1) {
                ShenZhiLiHurtLv = newLevel;
                SendNotice($"神之力真实伤害等级升级到{newLevel}级");
            } else if (lvType == 2) {
                ShenZhiLiHpLv = newLevel;
                SendNotice($"神之力气血等级升级到{newLevel}级");
            } else if (lvType == 3) {
                ShenZhiLiSpeedLv = newLevel;
                SendNotice($"神之力速度等级升级到{newLevel}级");
            } else {
                SendNotice("神之力-类型错误");
                return;
            }

            //同步神之力
            await SyncShenZhiLi();
            // 属性方案刷新潜能和属性
            await FreshAllSchemeAttrs();
  
            var resp = new S2C_ShenZhiLiInfo() { HurtLv = ShenZhiLiHurtLv, HpLv = ShenZhiLiHpLv, SpeedLv = ShenZhiLiSpeedLv };
            await SendPacket(GameCmd.S2CShenZhiLiInfo, resp);
        }

        private uint GetCanUseCszlTimes() {
            if (VipLevel >= 10) {
                return 8;
            } else if (VipLevel >= 10) {
                return 8;
            } else if (VipLevel >= 9) {
                return 5;
            } else if (VipLevel >= 8) {
                return 4;
            } else if (VipLevel >= 7) {
                return 3;
            } else if (VipLevel >= 6) {
                return 2;
            } else if (VipLevel >= 5) {
                return 1;
            } 
            return 0;
        }

        //成神之路--查询信息
        private async Task ReqCszlInfo()
        {
            var resp = new S2C_CszlInfo();
            resp.Layer = CszlLayer;
            resp.TodayTimes =  await RedisService.GetCszlTimesDaily(RoleId);
            await SendPacket(GameCmd.S2CCszlInfo, resp);
        }

        //成神之路--挑战副本
        private async Task ReqCszlChallenge()
        {
            if (CszlLayer >= 99) {
                SendNotice("已经在99层");
                return;
            }
            if (InTeam) {
                SendNotice("不能组队挑战");
                return;
            }

            var useTimes = await RedisService.GetCszlTimesDaily(RoleId);
            LogInformation(string.Format("成神之路--挑战副本 玩家ID{0} 当前层{1}, 今天挑战次数{2}, 能挑战的次数{3}", Entity.Id, CszlLayer, useTimes, GetCanUseCszlTimes()));
            if ( useTimes >= GetCanUseCszlTimes()) { 
                SendNotice("挑战次数不足");
                return;
            } else {
                await RedisService.AddCszlTimesDaily(RoleId);
            }
            ConfigService.Cszl.TryGetValue(CszlLayer, out var cszlCfg);
            if (cszlCfg == null) {
                SendNotice("不能挑战");
                LogError($"当前层[{CszlLayer}]获取配置失败了");
                return;
            }
            var ret = await StartPve(0, cszlCfg.MonsterGroup, BattleType.Cszl);
            // var ret = await StartPve(0, 20160, BattleType.Cszl);
            if (!ret) {
                // await RedisService.AddCszlTimesDaily(RoleId);
                SendNotice("挑战失败");
                return;
            }
        }

        private async Task OnCszlChallengeResult(bool win) {
            if (win) {
                //添加道具奖励
                ConfigService.Cszl.TryGetValue(CszlLayer, out var cszlCfg);
                if (cszlCfg != null && cszlCfg.Items != null) {
                    foreach (var item in cszlCfg.Items)
                    {
                        if (item.Num == 0) continue;
                        if (item.Id > 0)
                        {
                            await AddItem(item.Id, (int)item.Num, true, "成神之路副本奖励");
                        }
                    }
                }

                if (CszlLayer < 98) {
                    await RedisService.ReduceCszlTimesDaily(RoleId);
                }

                CszlLayer += 1;
                //同步成神之路
                await SyncCszl();
                var resp = new S2C_CszlInfo();
                resp.Layer = CszlLayer;
                resp.TodayTimes =  await RedisService.GetCszlTimesDaily(RoleId);
                await SendPacket(GameCmd.S2CCszlInfo, resp);

                //发送跑马灯公告
                if (CszlLayer < 99) {
                    if (CszlLayer % 10 == 0) {
                        var text =
                            $"<color=#00ff00 > {Entity.NickName}</c > <color=#ffffff> 玩家在成神之路走到了第</c ><color=#0fffff > {CszlLayer}</color ><color=#ffffff > 层！</c >";
                        BroadcastScreenNotice(text, 0);
                    }
                } else {
                    for (int i=0; i<3;i++) {
                        var text =
                            $"<color=#00ff00 > {Entity.NickName}</c > <color=#ffffff> 玩家在成神之路，神勇无敌，披荆斩棘，成功到达了</c ><color=#0fffff > {99}</color ><color=#ffffff > 层。</c >";
                        BroadcastScreenNotice(text, 0);
                    }
                }
            } else {
                //挑战失败
                if (CszlLayer < 99) {
                    var text =
                        $"<color=#00ff00 > {Entity.NickName}</c > <color=#ffffff> 玩家在成神之路走到了第</c ><color=#0fffff > {CszlLayer}</color ><color=#ffffff > 层，还需努力啊！</c >";
                    BroadcastScreenNotice(text, 0);
                }
            }
        }

        //成神之路--积分跳过本层
        private async Task ReqCszlScoreSkip()
        {
            if (CszlLayer >= 99) {
                SendNotice("已经在99层");
                return;
            }
            if (!CheckMoney(MoneyType.BindJade, CszlLayer * 100)) {
                SendNotice("积分不足");
                return;
            }
            await CostMoney(MoneyType.BindJade, CszlLayer * 100);

            //添加道具奖励
            ConfigService.Cszl.TryGetValue(CszlLayer, out var cszlCfg);
            if (cszlCfg != null && cszlCfg.Items != null) {
                foreach (var item in cszlCfg.Items)
                {
                    if (item.Num == 0) continue;
                    if (item.Id > 0)
                    {
                        await AddItem(item.Id, (int)item.Num, true, "成神之路副本奖励");
                    }
                }
            }

            CszlLayer += 1;
            //同步成神之路
            await SyncCszl();
            var resp = new S2C_CszlInfo();
            resp.Layer = CszlLayer;
            resp.TodayTimes =  await RedisService.GetCszlTimesDaily(RoleId);
            await SendPacket(GameCmd.S2CCszlInfo, resp);
        }

        //成神之路--积分重置
        private async Task ReqCszlScoreReset()
        {
            if (!CheckMoney(MoneyType.BindJade, 500)) {
                SendNotice("积分不足");
                return;
            }
            await CostMoney(MoneyType.BindJade, 500);

            CszlLayer = 1;
            //同步成神之路
            await SyncCszl();
            var resp = new S2C_CszlInfo();
            resp.Layer = CszlLayer;
            resp.TodayTimes =  await RedisService.GetCszlTimesDaily(RoleId);
            await SendPacket(GameCmd.S2CCszlInfo, resp);
            SendNotice("重置完成");
        }

        //成神之路--爬塔层数排行榜
        private async Task ReqRankCszlLayer(uint pageIndex)
        {
            // 防止恶意请求, 间隔5s才重新刷新数据
            if (_lastRankCszlLayerResp == null || TimeUtil.TimeStamp - _lastFetchCszlLayerRankTime > 5)
            {
                var list = await RedisService.GetRoleCszlLayerRank(Entity.ServerId, (int)pageIndex);
                var myRank = await RedisService.GetRoleCszlLayerRankIndex(Entity.ServerId, Entity.Id);
                _lastRankCszlLayerResp = new S2C_RankCszlLayer
                {
                    PageIndex = pageIndex,
                    List = { list },
                    MyRank = (uint)(myRank + 1)
                };
                _lastFetchCszlLayerRankTime = TimeUtil.TimeStamp;
            }

            await SendPacket(GameCmd.S2CRankCszlLayer, _lastRankCszlLayerResp);
            
        }

        // 红包--进入主界面
        private Task ReqRedEnterMain()
        {
            var redGrain = GrainFactory.GetGrain<IRedGrain>(Entity.ServerId);
            return redGrain.Enter(RoleId, Entity.SectId);
        }
        // 红包--详情
        private Task ReqRedDetail(uint id)
        {
            var redGrain = GrainFactory.GetGrain<IRedGrain>(Entity.ServerId);
            return redGrain.Detail(RoleId, id);
        }
        // 红包--历史记录
        private Task ReqRedHistory(RedType type, bool recived)
        {
            var redGrain = GrainFactory.GetGrain<IRedGrain>(Entity.ServerId);
            return redGrain.History(RoleId, (byte)type, recived);
        }
        // 红包--发送
        private async Task ReqRedSend(C2S_RedSend req)
        {
            if (Entity.Relive < 1 || Entity.Level < 80)
            {
                SendNotice("至少1转80级才能发红包");
                return;
            }
            if (Entity.TotalPayBS < 1000)
            {
                SendNotice("至少充值1000元才能发红包");
                return;
            }
            if (req.Jade < 600000)
            {
                SendNotice("红包至少发60W仙玉");
                return;
            }
            if (GetMoney(MoneyType.Jade) < req.Jade)
            {
                SendNotice("仙玉不足");
                return;
            }
            if (req.Type == RedType.Sect && !InSect)
            {
                SendNotice("加入帮派，才能发帮派红包");
                return;
            }
            if (req.Type != RedType.World && req.Type != RedType.Sect)
            {
                SendNotice("发红包失败，请稍候再试（未知类型）");
                return;
            }
            // 检查非法字符, GM不过滤
            if (!IsGm && !TextFilter.CheckLimitWord(req.Wish))
            {
                SendNotice("发送失败，祝福中包含非法字符");
                return;
            }
            var ret = await CostMoney(MoneyType.Jade, req.Jade, false, "发红包");
            if (!ret)
            {
                SendNotice("仙玉不足");
                return;
            }
            var resp = new S2C_RedSend() { Type = req.Type, Wish = req.Wish, Jade = req.Jade, Total = req.Total };
            var redGrain = GrainFactory.GetGrain<IRedGrain>(Entity.ServerId);
            resp.Id = await redGrain.Send(RoleId, Entity.SectId, (byte)req.Type, req.Wish, req.Jade, req.Total);
            if (resp.Id > 0)
            {
                resp.Role = BuildRoleInfo();
                if (req.Type == RedType.World)
                {
                    _ = ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CRedSend, resp)));
                    // TODO: 广播世界消息，提示红包
                }
                else
                {
                    _ = SectGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CRedSend, resp)));
                    // TODO: 广播帮派消息，提示红包
                }
                SendNotice("发红包成功，发送者也可以抢包哦~");
            }
            else
            {
                if (await AddMoney(MoneyType.Jade, (int)req.Jade) != req.Jade)
                {
                    LogError($"红包发送失败，返还仙玉失败[{RoleId}][{req.Jade}]");
                }
                SendNotice("发红包失败，请稍候再试");
            }
        }
        // 红包--抢包
        private Task ReqRedGet(uint id)
        {
            var redGrain = GrainFactory.GetGrain<IRedGrain>(Entity.ServerId);
            return redGrain.Get(RoleId, id);
        }

        private async Task ReqShopBuy(uint cfgId, uint num)
        {
            if (CheckSafeLocked()) return;
            if (num == 0) return;
            ConfigService.ShopItems.TryGetValue(cfgId, out var cfg);
            if (cfg == null)
            {
                SendNotice("商品不存在，无法购买");
                return;
            }

            // 检查背包
            if (IsBagFull && cfg.Type != 7)
            {
                SendNotice("背包已满，无法购买");
                return;
            }
            // 多宝阁兑换
            if (cfg.Type == 6)
            {
                // 消耗积分
                var total = num * cfg.Price;
                var ret = await CostMoney(MoneyType.BindJade, total, tag: "多宝阁积分兑换消耗");
                if (!ret) return;
                // 翅膀
                if (cfg.Wing)
                {
                    ConfigService.Wings.TryGetValue(cfgId, out var wcfg);
                    if (wcfg != null)
                    {
                        for (var i = 0; i < num; i++)
                        {
                            await EquipMgr.AddEquip(wcfg, false);
                        }
                    }
                    else
                    {
                        LogError($"多宝阁积分兑换失败：消耗积分{total}-->{cfgId} {num}，没有找到翅膀{cfgId}");
                        return;
                    }
                }
                // 普通物品
                else
                {
                    await AddBagItem(cfgId, (int)num, tag: "多宝阁积分兑换");
                }
                LogInformation($"多宝阁积分兑换成功：消耗积分{total}-->{cfgId} {num}");
            }
            // 外观皮肤购买
            else if (cfg.Type == 7)
            {
                if (SkinHas.Contains((int)cfgId))
                {
                    SendNotice("你已经拥有该外观");
                    return;
                }
                // 消耗积分
                var total = num * cfg.Price;
                var ret = await CostMoney(MoneyType.BindJade, total, tag: "外观皮肤积分兑换消耗");
                if (!ret) return;
                SkinHas.Add((int)cfgId);
                // 同步到Entity
                SyncSkins();
                await ReqSkinInfo(new List<int>());
                LogInformation($"外观皮肤积分兑换成功：消耗积分{total}-->{cfgId} {num}");
            }
            // 王者积分兑换
            else if (cfg.Type == 8)
            {
                // 消耗积分
                var total = num * cfg.Price;
                var ret = await CostMoney(MoneyType.WzzzJiFen, total, tag: "王者之战积分兑换消耗");
                if (!ret) return;
                // 翅膀
                if (cfg.Wing)
                {
                    ConfigService.Wings.TryGetValue(cfgId, out var wcfg);
                    if (wcfg != null)
                    {
                        for (var i = 0; i < num; i++)
                        {
                            await EquipMgr.AddEquip(wcfg, false);
                        }
                    }
                    else
                    {
                        LogError($"王者之战积分兑换失败：消耗积分{total}-->{cfgId} {num}，没有找到翅膀{cfgId}");
                        return;
                    }
                }
                // 普通物品
                else
                {
                    await AddBagItem(cfgId, (int)num, tag: "王者之战积分兑换");
                }
                LogInformation($"王者之战积分兑换成功：消耗积分{total}-->{cfgId} {num}");
            }
            else
            {
                // 消耗仙玉
                var total = num * cfg.Price;
                var ret = await CostMoney(MoneyType.Jade, total, tag: "多宝阁消耗");
                if (!ret) return;
                await AddBagItem(cfgId, (int)num, tag: "多宝阁购买");
            }
        }

        private async Task ReqMallItems(byte[] reqBytes)
        {
            var respBytes = await _mallGrain.QueryItems(RoleId, new Immutable<byte[]>(reqBytes));
            if (respBytes.Value != null)
            {
                var resp = S2C_MallItems.Parser.ParseFrom(respBytes.Value);
                await SendPacket(GameCmd.S2CMallItems, resp);
            }
            else
            {
                await SendPacket(GameCmd.S2CMallItems, new S2C_MallItems() { PageIndex = 0, List = { } });
            }
        }

        private async Task ReqMallAddItem(C2S_MallAddItem req)
        {
            if (CheckSafeLocked()) return;
            if (req.Num == 0) return;
            var detail = ByteString.Empty;
            if (req.Type == MallItemType.Item)
            {
                // 检查id和数量
                Items.TryGetValue(req.CfgId, out var num);
                if (num < req.Num)
                {
                    SendNotice("数量不够");
                    return;
                }
            }
            else if (req.Type == MallItemType.Equip)
            {
                var equip = EquipMgr.FindEquip(req.DbId);
                if (equip == null)
                {
                    SendNotice("数量不够");
                    return;
                }

                if (equip.Place != EquipPlace.Bag)
                {
                    SendNotice("装备不在背包内, 无法上架");
                    return;
                }

                // 只能上架1件
                req.Num = 1;
                req.CfgId = equip.CfgId;
                // 构造商品详情
                detail = ByteString.CopyFrom(Packet.Serialize(equip.BuildPbData()));
            }
            else if (req.Type == MallItemType.Pet)
            {
                var pet = PetMgr.FindPet(req.DbId);
                if (pet == null)
                {
                    SendNotice("数量不够");
                    return;
                }

                if (pet.Active)
                {
                    SendNotice("宠物参战中, 无法上架");
                    return;
                }

                // 只能上架1件
                req.Num = 1;
                req.CfgId = pet.CfgId;
                // 构造商品详情
                detail = ByteString.CopyFrom(Packet.Serialize(pet.BuildPbData()));
            }
            else
            {
                return;
            }

            // 上架收取1%的摊位费, 最低1000， 最高10w
            var twf = (uint)MathF.Floor(req.Price * req.Num * 0.01f);
            twf = Math.Clamp(twf, 1000, 100000);
            if (!CheckMoney(MoneyType.Contrib, twf)) return;

            var addItemRequest = new MallAddItemRequest
            {
                DbId = req.DbId,
                CfgId = req.CfgId,
                Price = req.Price,
                Num = req.Num,
                Type = req.Type,
                Details = detail
            };
            var respBytes = await _mallGrain.AddItem(RoleId, new Immutable<byte[]>(Packet.Serialize(addItemRequest)));
            if (respBytes.Value == null)
            {
                SendNotice("上架失败");
                return;
            }

            var resp = MallItem.Parser.ParseFrom(respBytes.Value);
            await SendPacket(GameCmd.S2CMallAddItem, new S2C_MallAddItem { Data = resp });

            // 扣摊位费
            await AddMoney(MoneyType.Contrib, -(int)twf, "摆摊摊位费");

            if (req.Type == MallItemType.Item)
            {
                // 从自己的物品中删除
                await AddBagItem(req.CfgId, -(int)req.Num, tag: "上架摆摊");
            }
            else if (req.Type == MallItemType.Equip)
            {
                var equip = EquipMgr.FindEquip(req.DbId);
                await equip.SetOnMall();
            }
            else if (req.Type == MallItemType.Pet)
            {
                var pet = PetMgr.FindPet(req.DbId);
                await pet.SetOnMall();
            }
        }

        private async Task ReqMallDelItem(uint id)
        {
            if (CheckSafeLocked()) return;
            var respBytes = await _mallGrain.DelItem(RoleId, id);
            if (respBytes.Value == null)
            {
                SendNotice("商品已下架");
                return;
            }

            await OnMallItemUnShelf(respBytes);
        }

        private async Task ReqMallBuy(uint id, uint num)
        {
            if (CheckSafeLocked()) return;
            if (id == 0 || num == 0) return;
            if (IsBagFull)
            {
                SendNotice("背包已满");
                return;
            }

            // 获取商品信息
            var respBytes = await _mallGrain.GetItem(id);
            if (respBytes.Value == null)
            {
                SendNotice("商品已下架");
                return;
            }

            var resp = MallItem.Parser.ParseFrom(respBytes.Value);
            if (resp.Type == MallItemType.Equip && CheckIsBagOverflow(num))
            {
                SendNotice("购买数量太多，背包装不下");
                return;
            }

            // 检查消耗
            var cost = resp.Price * num;
            if (!CheckMoney(MoneyType.Silver, cost)) return;

            // 尝试去购买
            var buyRespBytes = await _mallGrain.BuyItem(RoleId, id, num);
            if (buyRespBytes.Value == null)
            {
                SendNotice("商品已下架");
                return;
            }

            var buyResp = S2C_MallBuy.Parser.ParseFrom(buyRespBytes.Value);
            if (buyResp.Num == 0)
            {
                SendNotice("商品已下架");
                return;
            }

            // 扣钱, 按实际购买的数量来算
            cost = buyResp.Num * buyResp.Price;
            await AddMoney(MoneyType.Silver, (int)cost, "摆摊购买");

            // 获得商品
            if (buyResp.Type == MallItemType.Item)
            {
                await AddBagItem(buyResp.CfgId, (int)buyResp.Num, tag: "摆摊购买");
            }
            else if (buyResp.Type == MallItemType.Equip)
            {
                var entity = await DbService.Sql.Queryable<EquipEntity>().Where(it => it.Id == buyResp.DbId)
                    .FirstAsync();
                if (entity != null)
                {
                    // 加载到内存中来
                    entity.RoleId = RoleId;
                    await DbService.Sql.Update<EquipEntity>()
                        .Where(it => it.Id == buyResp.DbId)
                        .Set(it => it.RoleId, RoleId)
                        .ExecuteAffrowsAsync();
                    // 添加到装备管理器中
                    await EquipMgr.AddEquip(entity);
                }
            }
            else if (buyResp.Type == MallItemType.Pet)
            {
                var entity = await DbService.Sql.Queryable<PetEntity>().Where(it => it.Id == buyResp.DbId)
                    .FirstAsync();
                if (entity != null)
                {
                    // 加载到内存中来
                    entity.RoleId = RoleId;
                    await DbService.Sql.Update<PetEntity>()
                        .Where(it => it.Id == buyResp.DbId)
                        .Set(it => it.RoleId, RoleId)
                        .ExecuteAffrowsAsync();
                    // 添加到装备管理器中
                    await PetMgr.AddPet(entity);
                }
            }

            await SendPacket(GameCmd.S2CMallBuy, buyResp);
        }

        private async Task ReqMallItemDetail(uint id)
        {
            // 获取商品详情
            var respBytes = await _mallGrain.GetItemDetail(id);
            if (respBytes.Value == null)
            {
                SendNotice("商品已下架");
                return;
            }

            var resp = S2C_MallItemDetail.Parser.ParseFrom(respBytes.Value);
            await SendPacket(GameCmd.S2CMallItemDetail, resp);
        }

        private async Task ReqMallMyItems()
        {
            var respBytes = await _mallGrain.GetMyItems(RoleId);
            var resp = S2C_MallMyItems.Parser.ParseFrom(respBytes.Value);
            await SendPacket(GameCmd.S2CMallMyItems, resp);
        }

        private async Task ReqMallUpdateMyItem(uint id, uint price)
        {
            if (CheckSafeLocked()) return;
            var respBytes = await _mallGrain.GetItem(id);
            if (respBytes.Value == null)
            {
                SendNotice("商品不存在");
                return;
            }

            var item = MallItem.Parser.ParseFrom(respBytes.Value);
            if (item.Num == 0)
            {
                SendNotice("商品已售罄");
                return;
            }

            if (item.Price == price)
            {
                SendNotice("两次单价一致");
                return;
            }

            // 改单价消耗1%的费用, 最低1000， 最高10w
            var cost = (uint)MathF.Floor(item.Num * item.Price * 0.1f);
            cost = Math.Clamp(cost, 1000, 100000);
            if (!CheckMoney(MoneyType.Contrib, cost)) return;

            var ret = await _mallGrain.UpdateItem(RoleId, id, price);
            if (!ret)
            {
                SendNotice("修改失败");
                return;
            }

            // 扣贡币
            await CostMoney(MoneyType.Contrib, cost, tag: "摆摊修改货物单价");

            await SendPacket(GameCmd.S2CMallUpdateMyItem, new S2C_MallUpdateMyItem { Id = id, Price = price });
        }

        private async Task ReqTianJiangLingHouFight(uint npcOnlyId)
        {
            await Task.CompletedTask;
            SendNotice("活动已关闭");
            if (npcOnlyId > 0)
            {
            }

            // var ret = await _tianJiangLingHouGrain.IsOpen();
            // if (!ret)
            // {
            //     SendNotice("活动已关闭");
            //     return;
            // }
            //
            // if (TaskMgr.MonkeyNum >= 5)
            // {
            //     await SendNpcNotice(GameDefine.LingHouNpcCfgId, "今日次数已达上限");
            //     return;
            // }
            //
            // if (InTeam)
            // {
            //     await SendNpcNotice(GameDefine.LingHouNpcCfgId, "不能组队 灵猴大吼一句，欺人太甚，还想以多欺少！");
            //     return;
            // }
            //
            // if (!CheckMoney(MoneyType.Jade, GameDefine.LingHouMinMoney, false))
            // {
            //     await SendNpcNotice(GameDefine.LingHouNpcCfgId, "仙玉不足2000 灵猴轻蔑的看了你一眼，便不再搭理你了！");
            //     return;
            // }
            //
            // var errCode = await _tianJiangLingHouGrain.Fight(RoleId, npcOnlyId);
            // if (errCode == 1)
            // {
            //     // 灵猴被打跑了
            //     await SendNpcNotice(GameDefine.LingHouNpcCfgId, "灵猴攻击次数太多 小猴子大喊一声，少侠饶命，小的再也不敢了！");
            //     return;
            // }
            //
            // if (errCode == 2)
            // {
            //     await SendNpcNotice(GameDefine.LingHouNpcCfgId, "今天攻击猴子次数太多 小猴子大喊一声，少侠饶命，小的再也不敢了！");
            //     return;
            // }
            //
            // // 发起PVE战斗
            // await StartPve(0, 10153, BattleType.TianJiangLingHou);
            //
            // TaskMgr.MonkeyNum++;
        }

        private async Task ReqPk(uint targetRoleId)
        {
            if (RoleId == targetRoleId)
            {
                SendNotice("不能挑战自己");
                return;
            }

            // 帮战中不能挑战
            if (_sectWarCamp > 0)
            {
                SendNotice("帮战期间不能挑战");
                return;
            }

            // 检测targetRoleId是否在线
            var ret = await ServerGrain.CheckOnline(targetRoleId);
            if (!ret)
            {
                SendNotice("对方不在线");
                return;
            }

            LogInformation($"发起对玩家[{targetRoleId}]的PK, 在地图[{Entity.MapId}]位置[{Entity.MapX}, {Entity.MapY}]");

            await StartPvp(targetRoleId, (byte)BattleType.Force);
        }

        private async Task ReqHcPk(uint targetRoleId, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (!IsGm && !TextFilter.CheckLimitWord(text))
                {
                    SendNotice("战书中包含非法字符");
                    return;
                }

                text = TextFilter.Filte(text);
            }

            if (InTeam && !IsTeamLeader)
            {
                SendNotice("队员无法发起皇城挑战");
                return;
            }

            if (InBattle)
            {
                SendNotice("战斗中无法发起皇城挑战");
                return;
            }

            if (RoleId == targetRoleId)
            {
                SendNotice("狠起来连自己都要打吗？");
                return;
            }

            if (Entity.Relive < 3)
            {
                SendNotice("3转后开放此功能！");
                return;
            }

            if (InTeam)
            {
                var signed = await TeamGrain.CheckSldhSigned(RoleId);
                if (signed)
                {
                    SendNotice("当前已报名水陆大会活动，无法发起皇城PK！");
                    return;
                }
                signed = await TeamGrain.CheckWzzzSigned(RoleId);
                if (signed)
                {
                    SendNotice("当前已报名王者之战活动，无法发起皇城PK！");
                    return;
                }
            }

            if (await IsSignedSinglePk())
            {
                SendNotice("当前已报名比武大会，无法发起皇城PK！");
                return;
            }
            if (await IsSignedDaLuanDou())
            {
                SendNotice("当前已参加大乱斗，无法发起皇城PK！");
                return;
            }

            const uint subJade = 2000U;
            const uint subSilver = 1500000U;
            text = text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                // 无战书, 消耗银两
                if (!CheckMoney(MoneyType.Silver, subSilver)) return;
            }
            else
            {
                // 写战书, 消耗仙玉
                if (!CheckMoney(MoneyType.Jade, subJade)) return;
            }

            var hcPkgrain = GrainFactory.GetGrain<IHcPkGrain>(Entity.ServerId);
            // 你或者此玩家正在被其他人邀请皇城pk中！
            var hasPk = await hcPkgrain.CheckPk(RoleId);
            if (hasPk)
            {
                SendNotice("您有Pk待处理");
                return;
            }

            var online = await ServerGrain.CheckOnline(targetRoleId);
            if (!online)
            {
                SendNotice("对方不在线");
                return;
            }

            var targetGrain = GrainFactory.GetGrain<IPlayerGrain>(targetRoleId);

            hasPk = await hcPkgrain.CheckPk(targetRoleId);
            if (hasPk)
            {
                SendNotice("对方有Pk待处理");
                return;
            }

            var bytes = await targetGrain.PreCheckHcPk();
            var resp = PreCheckHcPkResponse.Parser.ParseFrom(bytes.Value);
            if (!string.IsNullOrWhiteSpace(resp.Error))
            {
                SendNotice($"对方{resp.Error}");
                return;
            }

            var sender = new HcRoleInfo
            {
                Id = Entity.Id,
                Name = Entity.NickName,
                Relive = Entity.Relive,
                Level = Entity.Level,
                CfgId = Entity.CfgId,
                State = 1 //发起者就是准备好了
            };
            var recver = new HcRoleInfo
            {
                Id = resp.Info.Id,
                Name = resp.Info.Name,
                Relive = resp.Info.Relive,
                Level = resp.Info.Level,
                CfgId = resp.Info.CfgId,
                State = 0
            };

            var pkData = new S2C_HcPk
            {
                Sender = sender,
                Recver = recver,
                Text = TextFilter.Filte(text),
                Seconds = 120, // 2分钟后自动取消
                Win = 0
            };

            var addPkRet = await hcPkgrain.AddPk(new Immutable<byte[]>(Packet.Serialize(pkData)));
            if (!addPkRet)
            {
                SendNotice("您或者对方有Pk待处理");
                return;
            }

            // 扣钱
            if (string.IsNullOrWhiteSpace(text))
            {
                // 无战书, 消耗银两
                var ret = await CostMoney(MoneyType.Silver, subSilver, tag: "发起皇城决斗消耗");
                if (!ret) return;
            }
            else
            {
                // 写战书, 消耗仙玉
                var ret = await CostMoney(MoneyType.Jade, subJade, tag: "发起皇城决斗消耗");
                if (!ret) return;
            }

            var bits = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CHcPk, pkData));
            if (string.IsNullOrWhiteSpace(text))
            {
                // 无战书
                await SendMessage(bits);
                _ = targetGrain.SendMessage(bits);
            }
            else
            {
                // 写战书, 全服广播
                _ = ServerGrain.Broadcast(bits);
            }
        }

        private async Task ReqHcHandle(bool agree)
        {
            if (InBattle)
            {
                SendNotice("战斗中无法接受皇城挑战");
                return;
            }

            if (InTeam && !IsTeamLeader)
            {
                SendNotice("队员无法接受皇城挑战");
                return;
            }

            var hcPkgrain = GrainFactory.GetGrain<IHcPkGrain>(Entity.ServerId);
            var respBytes = await hcPkgrain.FindPk(RoleId);
            if (respBytes.Value == null)
            {
                SendNotice("决斗已被取消！");
                return;
            }

            var pkInfo = S2C_HcPk.Parser.ParseFrom(respBytes.Value);
            if (pkInfo.Sender == null || pkInfo.Recver == null) return;

            if (agree)
            {
                // 同意决斗
                if (pkInfo.Sender.Id == RoleId) pkInfo.Sender.State = 1;
                if (pkInfo.Recver.Id == RoleId) pkInfo.Recver.State = 1;

                // 如果两方都同意决斗
                if (pkInfo.Sender.State == 1 && pkInfo.Recver.State == 1)
                {
                    bool ret;
                    if (pkInfo.Sender.Id == RoleId)
                    {
                        // 我是sender
                        ret = await CheckCanHcPk();
                        if (!ret)
                        {
                            pkInfo.Sender.State = 2;
                            _ = hcPkgrain.DelPk(RoleId);
                            return;
                        }

                        ret = await GrainFactory.GetGrain<IPlayerGrain>(pkInfo.Recver.Id).CheckCanHcPk();
                        if (!ret)
                        {
                            pkInfo.Recver.State = 2;
                            _ = hcPkgrain.DelPk(pkInfo.Recver.Id);
                            return;
                        }
                    }
                    else
                    {
                        // 我是Recver
                        ret = await CheckCanHcPk();
                        if (!ret)
                        {
                            pkInfo.Recver.State = 2;
                            _ = hcPkgrain.DelPk(RoleId);
                            return;
                        }

                        ret = await GrainFactory.GetGrain<IPlayerGrain>(pkInfo.Sender.Id).CheckCanHcPk();
                        if (!ret)
                        {
                            pkInfo.Sender.State = 2;
                            _ = hcPkgrain.DelPk(pkInfo.Sender.Id);
                            return;
                        }
                    }

                    // 可以发起Pk
                    _ = hcPkgrain.ReadyPk(RoleId);
                }
            }
            else
            {
                // 拒绝决斗
                _ = hcPkgrain.DelPk(RoleId);
            }
        }

        private async Task ReqHcRoleList()
        {
            var hcPkgrain = GrainFactory.GetGrain<IHcPkGrain>(Entity.ServerId);
            _ = hcPkgrain.SendRoleList(RoleId);
            await Task.CompletedTask;
        }

        private async Task ReqChallengeNpc(uint onlyId, uint cfgId)
        {
            if (InTeam && !IsTeamLeader && !_teamLeave) return;
            if (InBattle) return;

            var respBytes = await ServerGrain.FindNpc(onlyId);
            if (respBytes.Value == null) return;
            var mod = MapObjectData.Parser.ParseFrom(respBytes.Value);
            if (mod == null) return;
            // 判断是否为地煞星
            var isStar = mod.Owner is { Type: NpcOwnerType.Activity, Value: (uint)ActivityId.DiShaXing };
            var isKuLouWang = mod.Owner is { Type: NpcOwnerType.Activity, Value: (uint)ActivityId.KuLouWang };
            var isJinChanSongBao = mod.Owner is { Type: NpcOwnerType.Activity, Value: (uint)ActivityId.JinChanSongBao };
            var isEagle = mod.Owner is { Type: NpcOwnerType.Activity, Value: (uint)ActivityId.Eagle };
            var isEagleRace = mod.Owner is { Type: NpcOwnerType.Activity, Value: (uint)ActivityId.EagleRace };
            if (isStar)
            {
                // 每天只能打999次
                if (TaskMgr.StarNum >= GameDefine.DiShaXingNumDaily)
                {
                    await SendNpcNotice(mod.CfgId, "今日次数已满，明日再来吧");
                    return;
                }
                var dsxGrain = GrainFactory.GetGrain<IDiShaXingGrain>(Entity.ServerId);
                var errCode = await dsxGrain.ApplyChallenge(onlyId, RoleId, Entity.Star);
                if (0 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "报名成功，请等待");
                    await SendPacket(GameCmd.S2CStarWaiting);
                }
                else if (3 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, $"请先击杀{Entity.Star}星！");
                }
                else if (4 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, $"请先完成上次杀星");
                }
                else
                {
                    await SendNpcNotice(mod.CfgId, "你来晚了，下次早点来哦");
                }
            }
            else if (isKuLouWang)
            {
                var swzGrain = GrainFactory.GetGrain<IKuLouWangGrain>(Entity.ServerId);
                var errCode = await swzGrain.ApplyChallenge(onlyId, RoleId);
                if (0 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "报名成功，请等待");
                    await SendPacket(GameCmd.S2CStarWaiting);
                }
                else if (3 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "请先完成上次骷髅王");
                }
                else
                {
                    await SendNpcNotice(mod.CfgId, "你来晚了，下次早点来哦");
                }
            }
            else if (isJinChanSongBao)
            {
                // 每天只能打20次
                if (TaskMgr.JinChanSongBaoNum >= GameDefine.JinChanSongBaoNumDaily)
                {
                    await SendNpcNotice(mod.CfgId, "今日次数已满，明日再来吧");
                    return;
                }
                var jcsbGrain = GrainFactory.GetGrain<IJinChanSongBaoGrain>(Entity.ServerId);
                var errCode = await jcsbGrain.ApplyChallenge(onlyId, RoleId);
                if (0 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "报名成功，请等待");
                    await SendPacket(GameCmd.S2CStarWaiting);
                }
                else if (3 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "请先完成上次金蟾送宝");
                }
                else
                {
                    await SendNpcNotice(mod.CfgId, "你来晚了，下次早点来哦");
                }
            }
            else if (isEagle)
            {
                // 每天只能打1次
                if (TaskMgr.EagleNum >= GameDefine.EagleNumDaily)
                {
                    await SendNpcNotice(mod.CfgId, "今日次数已满，明日再来吧");
                    return;
                }
                var eagleGrain = GrainFactory.GetGrain<IEagleGrain>(Entity.ServerId);
                var errCode = await eagleGrain.ApplyChallenge(onlyId, RoleId);
                if (0 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "报名成功，请等待");
                    await SendPacket(GameCmd.S2CStarWaiting);
                }
                else if (1 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "活动未开启");
                }
                else if (2 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "金翅大鹏疗伤中，请晚上10点再来...");
                }
                else
                {
                    await SendNpcNotice(mod.CfgId, "金翅大鹏正在接受挑战，请稍候...");
                }
            }
            else if (isEagleRace)
            {
                // 每天只能打1次
                if (TaskMgr.EagleNum >= GameDefine.EagleNumDaily)
                {
                    await SendNpcNotice(mod.CfgId, "今日次数已满，明日再来吧");
                    return;
                }
                var eagleGrain = GrainFactory.GetGrain<IEagleGrain>(Entity.ServerId);
                var errCode = await eagleGrain.ApplyChallenge(onlyId, RoleId);
                if (0 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "报名成功，请等待");
                    await SendPacket(GameCmd.S2CStarWaiting);
                }
                else if (1 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "活动未开启");
                }
                else if (2 == errCode)
                {
                    await SendNpcNotice(mod.CfgId, "金翅大鹏疗伤中，请晚上10点再来...");
                }
                else
                {
                    await SendNpcNotice(mod.CfgId, "金翅大鹏正在接受挑战，请稍候...");
                }
            }
            else
            {
                await TaskMgr.TriggerNpcBoomb(onlyId, cfgId);
            }
        }

        private async Task ReqSldhSign()
        {
            if (!IsTeamLeader)
            {
                SendNotice("只有队长才可以报名");
                return;
            }

            _ = TeamGrain.SignShuiLuDaHui(RoleId, true);
            await Task.CompletedTask;
        }

        private async Task ReqWzzzSign()
        {
            if (!IsTeamLeader)
            {
                SendNotice("只有队长才可以报名");
                return;
            }

            _ = TeamGrain.SignWangZheZhiZhan(RoleId, true);
            await Task.CompletedTask;
        }

        private async Task ReqSldhUnSign()
        {
            if (!IsTeamLeader)
            {
                if (InTeam)
                {
                    SendNotice("只有队长才可以离场");
                }
                else
                {
                    await OnExitShuiLuDaHui(true);
                }
                return;
            }

            _ = TeamGrain.SignShuiLuDaHui(RoleId, false);
            await Task.CompletedTask;
        }

        private async Task ReqWzzzUnSign()
        {
            if (!IsTeamLeader)
            {
                if (InTeam)
                {
                    SendNotice("只有队长才可以离场");
                }
                else
                {
                    await OnExitWangZheZhiZhan(true);
                }
                return;
            }

            _ = TeamGrain.SignWangZheZhiZhan(RoleId, false);
            await Task.CompletedTask;
        }

        private async Task ReqSldhInfo()
        {
            if (!InTeam) return;
            var sldhGrain = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(Entity.ServerId);

            var respBytes = await sldhGrain.GetInfo(TeamId);
            if (respBytes.Value == null)
            {
                await SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "水陆大会未开启" });
                return;
            }

            var resp = S2C_SldhInfo.Parser.ParseFrom(respBytes.Value);
            resp.Score = _sldh.Score;
            resp.GongJi = Entity.SldhGongJi;
            resp.Win = _sldh.Win;
            resp.Lost = _sldh.Lost;
            await SendPacket(GameCmd.S2CSldhInfo, resp);
        }

        private async Task ReqWzzzInfo()
        {
            // if (!InTeam) return;
            var wzzzGrain = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(Entity.ServerId);

            var respBytes = await wzzzGrain.GetInfo(TeamId);
            var resp0 = new S2C_WzzzInfo
            {
                State = WzzzState.Close,
                Turn = 0,
                IsSign = false,
            }; 
            if (respBytes.Value == null)
            {
                await SendPacket(GameCmd.S2CWzzzInfo, resp0);
                // await SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "王者之战未开启" });
                return;
            }

            var resp = S2C_WzzzInfo.Parser.ParseFrom(respBytes.Value);
            resp.Score = _wzzz.Score;
            resp.GongJi = Entity.WzzzJiFen;
            resp.Win = _wzzz.Win;
            resp.Lost = _wzzz.Lost;
            await SendPacket(GameCmd.S2CWzzzInfo, resp);
        }

        // 神兽降临--报名
        private async Task ReqSsjlSign()
        {
            if (!IsTeamLeader)
            {
                SendNotice("只有队长才可以报名");
                return;
            }
            _ = TeamGrain.SignShenShouJiangLin(RoleId, true);
            await Task.CompletedTask;
        }

        // 神兽降临--退赛
        private async Task ReqSsjlUnSign()
        {
            if (!IsTeamLeader)
            {
                SendNotice("只有队长才可以离场");
                return;
            }
            _ = TeamGrain.SignShenShouJiangLin(RoleId, false);
            await Task.CompletedTask;
        }

        private async Task ReqSinglePkSign()
        {
            if (Entity.Relive == 0)
            {
                SendNotice("1转后开放此功能");
                return;
            }

            if (InTeam && !_teamLeave)
            {
                SendNotice("请先离队或暂离");
                return;
            }

            var reqData = new SinglePkRoleInfo
            {
                Role = BuildRoleInfo(),
                Win = _singlePkVo.Win,
                Lost = _singlePkVo.Lost,
                Score = _singlePkVo.Score
            };
            if (_singlePkGrain == null)
            {
                SendNotice("活动暂不可用");
                return;
            }
            var error = await _singlePkGrain.Sign(new Immutable<byte[]>(Packet.Serialize(reqData)));
            if (!string.IsNullOrWhiteSpace(error))
            {
                SendNotice(error);
                return;
            }

            _singlePkVo.Sign = true;
            await SendPacket(GameCmd.S2CSinglePkSign, new S2C_SinglePkSign { State = SinglePkState.Sign });
            SendNotice("报名成功");
        }

        private async Task ReqSinglePkUnSign()
        {
            if (_singlePkGrain != null)
            {
                _ = _singlePkGrain.UnSign(RoleId);
            }
            await Task.CompletedTask;
        }

        private async Task ReqSinglePkInfo()
        {
            if (InTeam && !_teamLeave) return;
            if (_singlePkGrain == null) return;

            var respBytes = await _singlePkGrain.GetInfo(RoleId);
            if (respBytes.Value == null)
            {
                await SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "比武大会未开启" });
                return;
            }

            await SendPacket(GameCmd.S2CSinglePkInfo, respBytes.Value);
        }

        private async Task ReqDaLuanDouSign()
        {
            if (!IsTeamLeader)
            {
                SendNotice("只有队长才可以报名");
                return;
            }

            if (Entity.Relive < 2)
            {
                SendNotice("等级未到2转！");
                return;
            }

            _ = TeamGrain.SignDaLuanDou(RoleId, true);
            await Task.CompletedTask;            
        }

        private async Task ReqDaLuanDouUnSign()
        {
            if (!IsTeamLeader)
            {
                SendNotice("只有队长才可以退出报名");
                return;
            }

            _ = TeamGrain.SignDaLuanDou(RoleId, false);
            await Task.CompletedTask;
        }

        private async Task ReqDaLuanDouInfo()
        {
            // if (!InTeam) return;
            var dldGrain = GrainFactory.GetGrain<IDaLuanDouGrain>(Entity.ServerId);

            // LogInformation($"ReqDaLuanDouInfo--> ReqDaLuanDouInfo");
            var respBytes = await dldGrain.GetInfo(TeamId);
            var resp0 = new S2C_DaLuanDouInfo
            {
                State = DaLuanDouState.Close,
                Turn = 0,
                IsSign = false,
            }; 
            if (respBytes.Value == null)
            {
                await SendPacket(GameCmd.S2CDaLuanDouInfo, resp0);
                // await SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "大乱斗未开启" });
                // LogInformation($"发送空包--> S2CDaLuanDouInfo ");
                return;
            }
            await SendPacket(GameCmd.S2CDaLuanDouInfo, respBytes.Value);
            // LogInformation($"发送GetInfo--> S2CDaLuanDouInfo ");
        }

        private async Task ReqDaLuanDouPk(uint roleId)
        {
            if (roleId == 0) return;
            if (!InTeam) {
                SendNotice("请先加入一个队伍");
                return;
            }
            if (InTeam && !IsTeamLeader) {
                SendNotice("不是队长");
                return;
            }
            
            _ = _daLuanDouGrain.DaLuanDouPk(RoleId, TeamId, roleId);
            await Task.CompletedTask;
        }

        // 帮战进场
        private async Task ReqSectWarEnter()
        {
            if (!InSect)
            {
                SendNotice("请先加入一个帮派");
                return;
            }

            // 入帮时间>=48小时
            // var span = TimeUtil.TimeStamp - Entity.SectJoinTime;
            // var ts = TimeSpan.FromSeconds(span);
            // if (ts.Hours < 48)
            // {
            //     SendNotice("入帮时间不足48小时, 不能参与帮战");
            //     return;
            // }

            if (InTeam)
            {
                SendNotice("请先离队");
                return;
            }

            // 入场
            var bytes = await _sectWarGrain.Enter(new Immutable<byte[]>(Packet.Serialize(new SectWarApproachRequest
            {
                Id = Entity.Id,
                Name = Entity.NickName,
                SectId = Entity.SectId
            })));
            var resp = SectWarApproachResponse.Parser.ParseFrom(bytes.Value);
            if (!string.IsNullOrWhiteSpace(resp.Error))
            {
                SendNotice(resp.Error);
                return;
            }

            if (resp.Camp != 1 && resp.Camp != 2)
            {
                SendNotice("入场失败");
                return;
            }

            _sectWarId = resp.SectWarId;
            _sectWarCamp = resp.Camp;
            _sectWarPlace = resp.Place;
            _sectWarState = resp.State;

            await SendPacket(GameCmd.S2CSectWarEnter, new S2C_SectWarEnter
            {
                Camp = resp.Camp,
                Place = resp.Place,
                State = resp.State
            });

            if (_sectWarCamp == 1)
                await SyncMapPos(5001, 13, 7, true);
            else if (_sectWarCamp == 2)
                await SyncMapPos(5001, 180, 100, true);
        }

        private async Task ReqSectWarExit()
        {
            await Task.CompletedTask;
            if (InTeam && !IsTeamLeader)
            {
                // FIXME: 老旧BUG补丁，当前帮战活动是关闭的，但是玩家带帮战信息，则强制退出帮战
                await CheckAndExitSectWar();
                return;
            }
#if false
            if (_sectWarCamp == 0 || _sectWarPlace != SectWarPlace.JiDi)
            {
                // FIXME: 老旧BUG补丁，当前帮战活动是关闭的，但是玩家带帮战信息，则强制退出帮战
                await CheckAndExitSectWar();
                return;
            }
#endif
            _ = _sectWarGrain.Exit(RoleId);
        }

        // FIXME: 老旧BUG补丁，当前帮战活动是关闭的，但是玩家带帮战信息，则强制退出帮战
        // 检查帮战退出--如果帮战活动是关闭状态，则可以直接退出
        private async Task CheckAndExitSectWar()
        {
            SectWarState state = (SectWarState)await _sectWarGrain.State();
            if (state == SectWarState.Close)
            {
                await OnExitSectWar();
            }
        }

        private async Task ReqSectWarChangePlace(SectWarPlace place)
        {
            if (_sectWarCamp == 0) return;
            if (InTeam && !IsTeamLeader) return;
            var ret = await _sectWarGrain.ChangePlace(RoleId, (byte)place);
            if (ret)
            {
                await SendPacket(GameCmd.S2CSectWarChangePlace, new S2C_SectWarChangePlace
                {
                    Place = place
                });
            }
        }

        private async Task ReqSectWarReadyPk()
        {
            if (_sectWarCamp == 0 || _sectWarPlace != SectWarPlace.BiWuChang) return;
            if (InTeam && !IsTeamLeader) return;
            _ = _sectWarGrain.ReadyPk(RoleId);
            await Task.CompletedTask;
        }

        private async Task ReqSectWarCancelPk()
        {
            if (_sectWarCamp == 0 || _sectWarPlace != SectWarPlace.BiWuChang) return;
            if (InTeam && !IsTeamLeader) return;
            _ = _sectWarGrain.CancelPk(RoleId);
            await Task.CompletedTask;
        }

        private async Task ReqSectWarGrabCannon()
        {
            if (_sectWarCamp == 0 || _sectWarPlace != SectWarPlace.ZhanChang) return;
            if (InTeam && !IsTeamLeader) return;
            _ = _sectWarGrain.GrabCannon(RoleId);
            await Task.CompletedTask;
        }

        private async Task ReqSectWarLockDoor()
        {
            if (_sectWarCamp == 0 || _sectWarPlace != SectWarPlace.ZhanChang) return;
            if (InTeam && !IsTeamLeader) return;
            _ = _sectWarGrain.LockDoor(RoleId);
            await Task.CompletedTask;
        }

        private async Task ReqSectWarCancelDoor()
        {
            if (_sectWarCamp == 0 || _sectWarPlace != SectWarPlace.ZhanChang) return;
            if (InTeam && !IsTeamLeader) return;
            _ = _sectWarGrain.CancelDoor(RoleId);
            await Task.CompletedTask;
        }

        private async Task ReqSectWarBreakDoor(uint roleId)
        {
            if (roleId == 0 || _sectWarCamp == 0 || _sectWarPlace != SectWarPlace.ZhanChang) return;
            if (InTeam && !IsTeamLeader) return;
            _ = _sectWarGrain.BreakDoor(RoleId, roleId);
            await Task.CompletedTask;
        }

        private async Task ReqSectWarFreePk(uint roleId)
        {
            if (roleId == 0 || _sectWarCamp == 0 || _sectWarPlace != SectWarPlace.ZhanChang) return;
            if (InTeam && !IsTeamLeader) return;
            _ = _sectWarGrain.FreePk(RoleId, roleId);
            await Task.CompletedTask;
        }

        private async Task ReqSectWarInfo()
        {
            if (_sectWarCamp == 0 || !InSect) return;
            var bytes = await _sectWarGrain.QuerySectInfo(RoleId, Entity.SectId);
            if (bytes.Value != null)
            {
                var resp = S2C_SectWarInfo.Parser.ParseFrom(bytes.Value);
                await SendPacket(GameCmd.S2CSectWarInfo, resp);
            }

            await Task.CompletedTask;
        }

        private async Task ReqGetEquipShareInfo(uint id)
        {
            var resp = new S2C_ShareEquipInfo();
            var bytes = await RedisService.GetEquipInfo(id);
            if (bytes is { Length: > 0 })
            {
                resp.Data = EquipData.Parser.ParseFrom(bytes);
            }

            await SendPacket(GameCmd.S2CShareEquipInfo, resp);
        }

        private async Task ReqGetPetShareInfo(uint id)
        {
            var resp = new S2C_SharePetInfo();
            var bytes = await RedisService.GetPetInfo(id);
            if (bytes is { Length: > 0 })
            {
                resp.Data = PetData.Parser.ParseFrom(bytes);
            }

            await SendPacket(GameCmd.S2CSharePetInfo, resp);
        }

        private async Task ReqGetPetOrnamentShareInfo(uint id)
        {
            var resp = new S2C_SharePetOrnamentInfo();
            var bytes = await RedisService.GetPetOrnamentInfo(id);
            if (bytes is { Length: > 0 })
            {
                resp.Data = PetOrnamentData.Parser.ParseFrom(bytes);
            }

            await SendPacket(GameCmd.S2CSharePetOrnamentInfo, resp);
        }

        public Task SendMessage(Immutable<byte[]> bytes)
        {
            if (!IsActive) return Task.CompletedTask;
            if (IsOnline && IsEnterServer)
            {
                _ = _packet.SendPacket(RoleId, bytes.Value);
            }

            return Task.CompletedTask;
        }

        public Task BroadcastMessage(Immutable<byte[]> bytes)
        {
            if (!IsActive) return Task.CompletedTask;
            if (IsOnline && IsEnterServer)
            {
                _ = ServerGrain.Broadcast(bytes);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 队长管队员要参战数据, 队员是不能携带Partner的
        /// </summary>
        public async Task<Immutable<byte[]>> GetBattleMembers(Immutable<byte[]> reqBytes, uint campId)
        {
            await Task.CompletedTask;
            // 说明当前我在战场中
            if (InBattle) return new Immutable<byte[]>(null);

            var req = GetBattleMembersRequest.Parser.ParseFrom(reqBytes.Value);
            // 绑定好Battle
            _battleGrain = GrainFactory.GetGrain<IBattleGrain>(req.BattleId);
            _battleId = req.BattleId;
            _campId = campId;
            // 停止AOI同步
            _ = _mapGrain.PlayerEnterBattle(OnlyId, _battleId, _campId);

            var resp = new GetBattleMembersResponse();
            var self = BuildBattleMemberData();
            // 构建战斗单元失败
            if (self == null)
            {
                return new(null);
            }
            // 构建角色参战数据
            resp.List.Add(self);
            // 构建宠物参战数据
            var pets = PetMgr.BuildBattleTeamData(6);
            resp.List.AddRange(pets);

            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        // 战场中捕获了宝宝, 在monster表中查找
        public Task CreatePet(uint cfgId, string from = null)
        {
            if (!IsActive) return Task.CompletedTask;
            return PetMgr.CreatePet(cfgId);
        }

        public async ValueTask<bool> AddBagItem(uint cfgId, int num, bool send = true, string tag = "")
        {
            if (!IsActive) return false;
            ConfigService.Items.TryGetValue(cfgId, out var itemCfg);
            if (itemCfg == null) return false;
            if (num > 0)
            {
                if (Items.ContainsKey(cfgId))
                {
                    Items[cfgId] += (uint)num;
                }
                else
                {
                    if (IsBagFull)
                    {
                        SendNotice("背包已满");
                        return false;
                    }

                    Items[cfgId] = (uint)num;
                }

                // 同步给Entity
                SyncItems();

                if (send)
                {
                    var resp = new S2C_ItemAdd();
                    resp.List.Add(new ItemData { Id = cfgId, Num = (uint)num });
                    await SendPacket(GameCmd.S2CItemAdd, resp);
                }

                // 广播给本服在线玩家
                if (itemCfg.Notice > 0)
                {
                    var text =
                        $"<color=#00ff00 > {Entity.NickName}</c > <color=#ffffff> 获得了</c ><color=#0fffff > {itemCfg.Name}</color >，<color=#ffffff > 真是太幸运了</c >";
                    BroadcastScreenNotice(text, 0);
                }
            }
            else if (num < 0)
            {
                Items.TryGetValue(cfgId, out var oldNum);

                var val = (uint)Math.Abs(num);
                if (oldNum < val)
                {
                    SendNotice($"{itemCfg.Name}数量不足");
                    return false;
                }

                Items[cfgId] -= val;
                if (Items[cfgId] == 0) Items.Remove(cfgId);

                // 同步给Entity
                SyncItems();

                if (send)
                {
                    var resp = new S2C_ItemDel();
                    resp.List.Add(new ItemData { Id = cfgId, Num = val });
                    await SendPacket(GameCmd.S2CItemDel, resp);
                }
            }

            Items.TryGetValue(cfgId, out var total);
            LogInformation($"包裹({itemCfg.Name}) {num} -> {total} --- {tag}");
            return true;
        }

        public async ValueTask<long> AddMoney(byte type, int value, string tag = "", bool noNotice = false)
        {
            if (!IsActive) return 0;
            if (value == 0) return 0;
            var res = await AddMoney((MoneyType)type, value, tag, noNotice);
            return res;
        }

        public async ValueTask<long> AddMoney(MoneyType type, int value, string tag = "", bool noNotice = false)
        {
            if (!IsActive) return 0;
            long res = value;
            switch (type)
            {
                case MoneyType.Silver:
                {
                    if (value > 0)
                    {
                        // 防止溢出uint32， 所以先转成ulong
                        var tmp = Entity.Silver + (ulong)value;
                        if (tmp >= uint.MaxValue)
                        {
                            tmp = uint.MaxValue;
                            res = (long)(tmp - Entity.Silver);
                        }

                        Entity.Silver = (uint)tmp;
                    }
                    else
                    {
                        var delta = (uint)Math.Abs(value);
                        if (Entity.Silver >= delta)
                        {
                            Entity.Silver -= delta;
                        }
                        else
                        {
                            res = 0 - (long)Entity.Silver;
                            Entity.Silver = 0;
                        }
                    }
                    await SendPacket(GameCmd.S2CRoleMoney, new S2C_RoleMoney { Type = MoneyType.Silver, Value = Entity.Silver, Delta = value, NoNotice = noNotice });
                    LogInformation($"银两:{Entity.Silver} {tag} {value}");
                }
                    break;
                case MoneyType.Jade:
                {
                    if (value > 0)
                    {
                        var tmp = Entity.Jade + (ulong)value;
                        if (tmp >= uint.MaxValue)
                        {
                            tmp = uint.MaxValue;
                            res = (long)(tmp - Entity.Jade);
                        }

                        Entity.Jade = (uint)tmp;
                    }
                    else
                    {
                        var delta = (uint)Math.Abs(value);
                        if (Entity.Jade >= delta)
                        {
                            Entity.Jade -= delta;
                        }
                        else
                        {
                            res = 0 - (long)Entity.Jade;
                            Entity.Jade = 0;
                        }
                    }

                    await RedisService.SetRoleJade(Entity);
                    await SendPacket(GameCmd.S2CRoleMoney, new S2C_RoleMoney { Type = MoneyType.Jade, Value = Entity.Jade, Delta = value, NoNotice = noNotice });
                    LogInformation($"仙玉:{Entity.Jade} {tag} {value}");
                }
                    break;
                case MoneyType.BindJade:
                {
                    if (value > 0)
                    {
                        var tmp = Entity.BindJade + (ulong)value;
                        if (tmp >= uint.MaxValue)
                        {
                            tmp = uint.MaxValue;
                            res = (long)(tmp - Entity.BindJade);
                        }

                        Entity.BindJade = (uint)tmp;
                    }
                    else
                    {
                        var delta = (uint)Math.Abs(value);
                        if (Entity.BindJade >= delta)
                        {
                            Entity.BindJade -= delta;
                        }
                        else
                        {
                            res = 0 - (long)Entity.BindJade;
                            Entity.BindJade = 0;
                        }
                    }

                    await SendPacket(GameCmd.S2CRoleMoney, new S2C_RoleMoney { Type = MoneyType.BindJade, Value = Entity.BindJade, Delta = value, NoNotice = noNotice });
                    LogInformation($"绑定仙玉:{Entity.BindJade} {tag} {value}");
                }
                    break;
                case MoneyType.Contrib:
                {
                    if (value > 0)
                    {
                        var tmp = Entity.Contrib + (ulong)value;
                        if (tmp >= uint.MaxValue)
                        {
                            tmp = uint.MaxValue;
                            res = (long)(tmp - Entity.Contrib);
                        }

                        Entity.Contrib = (uint)tmp;
                    }
                    else
                    {
                        var delta = (uint)Math.Abs(value);
                        if (Entity.Contrib >= delta)
                        {
                            Entity.Contrib -= delta;
                        }
                        else
                        {
                            res = 0 - (long)Entity.Contrib;
                            Entity.Contrib = 0;
                        }
                    }

                    await SendPacket(GameCmd.S2CRoleMoney, new S2C_RoleMoney { Type = MoneyType.Contrib, Value = Entity.Contrib, Delta = value, NoNotice = noNotice });
                    LogInformation($"帮贡:{Entity.Contrib} {tag} {value}");
                }
                    break;
                case MoneyType.SldhGongJi:
                {
                    if (value > 0)
                    {
                        var tmp = Entity.SldhGongJi + (ulong)value;
                        if (tmp >= uint.MaxValue)
                        {
                            tmp = uint.MaxValue;
                            res = (long)(tmp - Entity.SldhGongJi);
                        }

                        Entity.SldhGongJi = (uint)tmp;
                    }
                    else
                    {
                        var delta = (uint)Math.Abs(value);
                        if (Entity.SldhGongJi >= delta)
                        {
                            Entity.SldhGongJi -= delta;
                        }
                        else
                        {
                            res = 0 - (long)Entity.SldhGongJi;
                            Entity.SldhGongJi = 0;
                        }
                    }

                    await SendPacket(GameCmd.S2CRoleMoney, new S2C_RoleMoney { Type = MoneyType.SldhGongJi, Value = Entity.SldhGongJi, Delta = value, NoNotice = noNotice });
                    LogInformation($"水路功绩:{Entity.SldhGongJi} {tag} {value}");
                }
                    break;
                case MoneyType.WzzzJiFen:
                {
                    if (value > 0)
                    {
                        var tmp = Entity.WzzzJiFen + (ulong)value;
                        if (tmp >= uint.MaxValue)
                        {
                            tmp = uint.MaxValue;
                            res = (long)(tmp - Entity.WzzzJiFen);
                        }

                        Entity.WzzzJiFen = (uint)tmp;
                    }
                    else
                    {
                        var delta = (uint)Math.Abs(value);
                        if (Entity.WzzzJiFen >= delta)
                        {
                            Entity.WzzzJiFen -= delta;
                        }
                        else
                        {
                            res = 0 - (long)Entity.WzzzJiFen;
                            Entity.WzzzJiFen = 0;
                        }
                    }

                    await SendPacket(GameCmd.S2CRoleMoney, new S2C_RoleMoney { Type = MoneyType.WzzzJiFen, Value = Entity.WzzzJiFen, Delta = value, NoNotice = noNotice });
                    LogInformation($"王者之战积分:{Entity.WzzzJiFen} {tag} {value}");
                }
                    break;
                case MoneyType.GuoShi:
                {
                    if (value > 0)
                    {
                        var tmp = Entity.GuoShi + (ulong)value;
                        if (tmp >= uint.MaxValue)
                        {
                            tmp = uint.MaxValue;
                            res = (long)(tmp - Entity.GuoShi);
                        }

                        Entity.GuoShi = (uint)tmp;
                    }
                    else
                    {
                        var delta = (uint)Math.Abs(value);
                        if (Entity.GuoShi >= delta)
                        {
                            Entity.GuoShi -= delta;
                        }
                        else
                        {
                            res = 0 - (long)Entity.GuoShi;
                            Entity.GuoShi = 0;
                        }
                    }

                    await SendPacket(GameCmd.S2CRoleMoney, new S2C_RoleMoney { Type = MoneyType.GuoShi, Value = Entity.GuoShi, Delta = value, NoNotice = noNotice });
                    LogInformation($"郭氏积分:{Entity.GuoShi} {tag} {value}");
                }
                    break;
            }

            return res;
        }

        /// <summary>
        /// 消耗货币
        /// </summary>
        /// <param name="type">货币类型</param>
        /// <param name="value">消耗值</param>
        /// <param name="useBind">是否使用绑定类型</param>
        /// <param name="tag">标记,便于日志跟踪</param>
        public async ValueTask<bool> CostMoney(MoneyType type, uint value, bool useBind = true, string tag = "")
        {
            if (!IsActive) return false;
            if (type == MoneyType.Unkown) return false;
            var money = GetMoney(type);
            /*
            var bindJade = GetMoney(MoneyType.BindJade);
            if (type == MoneyType.Jade && useBind)
            {
                money += bindJade;
            }
            */

            if (money < value)
            {
                GameDefine.MoneyName.TryGetValue(type, out var name);
                SendNotice($"{name}不足");
                return false;
            }
            /*
            if (type == MoneyType.Jade && useBind)
            {
                //if (bindJade > 0)
                //{
                    // 优先使用绑定仙玉
                   // if (bindJade >= value)
                    //{
                        //await AddMoney(MoneyType.BindJade, -(int)value, tag);
                   // }
                   // else
                   // {
                       // var left = value - bindJade;
                       // await AddMoney(MoneyType.BindJade, -(int)bindJade, tag);
                       // await AddMoney(MoneyType.Jade, -(int)left, tag);
                    //}
               // }
               // else
               // {
                    await AddMoney(MoneyType.Jade, -(int)value, tag);
               // }
            }
            else
            {
            */
                await AddMoney(type, -(int)value, tag);
            /*
            }
            */

            return true;
        }

        public uint GetMoney(MoneyType type)
        {
            if (!IsActive) return (uint)0;
            uint sum = type switch
            {
                MoneyType.Silver => Entity.Silver,
                MoneyType.Jade => Entity.Jade,
                MoneyType.BindJade => Entity.BindJade,
                MoneyType.Contrib => Entity.Contrib,
                MoneyType.SldhGongJi => Entity.SldhGongJi,
                MoneyType.WzzzJiFen => Entity.WzzzJiFen,
                MoneyType.GuoShi => Entity.GuoShi,
                _ => 0
            };
            return sum;
        }

        public bool CheckMoney(MoneyType type, uint value, bool sendNotice = true)
        {
            if (!IsActive) return false;
            if (GetMoney(type) >= value) return true;
            if (sendNotice)
            {
                GameDefine.MoneyName.TryGetValue(type, out var name);
                SendNotice($"{name}不足");
            }

            return false;
        }

        public async Task AddShanE(uint value)
        {
            if (!IsActive) return;
            var startTime = DateTimeOffset.Now;
            // if (Entity.Shane > 0) startTime = DateTimeOffset.FromUnixTimeSeconds(Entity.Shane).AddHours(8);
            // 监禁结束时间
            Entity.Shane = (uint)startTime.AddSeconds(value).ToUnixTimeSeconds();
            // 监禁倒计时
            _shane = value;
            await CheckShanEChange();
        }

        public ValueTask<bool> CheckInPrison()
        {
            if (!IsActive) return new(false);
            return new(_inPrison);
        }

        public ValueTask<bool> CheckInBattle()
        {
            if (!IsActive) return new(false);
            return new(InBattle);
        }

        public ValueTask<bool> CheckCanHcPk()
        {
            if (!IsActive) return new(false);
            // 金銮殿
            return new(!InBattle && Entity.MapId == 1206);
        }

        public async Task AddShuiLuDaHuiScore(uint season, uint score, bool win)
        {
            if (!IsActive) return;
            if (_sldh.Season != season)
                _sldh = new RoleSldhVo { Season = season };
            _sldh.Score += score;
            if (win) _sldh.Win++;
            else _sldh.Lost++;

            await SyncSldh();
        }

        public async Task AddDaLuanDouScore(uint season, uint score, bool win)
        {
            if (!IsActive) return;
            if (_daLuanDouVo.Season != season)
                _daLuanDouVo = new RoleDaLuanDouVo { Season = season };
            _daLuanDouVo.Score += score;
            if (win) _daLuanDouVo.Win++;
            else _daLuanDouVo.Lost++;

            await SyncDaLuanDou();
        }

        public async Task AddWangZheZhiZhanScore(uint season, uint score, bool win)
        {
            if (!IsActive) return;
            if (_wzzz.Season != season)
                _wzzz = new RoleWzzzVo { Season = season };
            _wzzz.Score += score;
            if (_wzzz.Score >= 500) {
                if (_wzzz.DuanWei < 4) {
                    _wzzz.DuanWei = 4;
                    //todo 这里需要添加额外积分
                    await AddMoney(MoneyType.WzzzJiFen, (int)300);
                }
            }
            else if (_wzzz.Score >= 200) 
            {
                if (_wzzz.DuanWei < 3) {
                    _wzzz.DuanWei = 3;
                    await AddMoney(MoneyType.WzzzJiFen, (int)100);
                }
            }
            else if (_wzzz.Score >= 100) 
            {
                if (_wzzz.DuanWei < 2) {
                    _wzzz.DuanWei = 2;
                    await AddMoney(MoneyType.WzzzJiFen, (int)50);
                }
            }
            else if (_wzzz.Score >= 50) 
            {
                if (_wzzz.DuanWei < 1) {
                    _wzzz.DuanWei = 1;
                    await AddMoney(MoneyType.WzzzJiFen, (int)20);
                }
            }
            if (win) _wzzz.Win++;
            else _wzzz.Lost++;

            await SyncWzzz();
        }

        public async Task ExitBattle(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return;
            var req = ExitBattleRequest.Parser.ParseFrom(reqBytes.Value);
            var isWin = req.Win == 1;
            var isLost = req.Win == 2;
            var isDraw = req.Win == 0;
            _battleGrain = null;
            _battleId = 0;
            _campId = 0;
            if (IsEnterServer)
                await SendPacket(GameCmd.S2CBattleStop, new S2C_BattleStop { BattleId = req.Id, Win = isWin, Type = req.Type });
            // 观战 只退出观战
            if (_battleGrainWatched != null)
            {
                await ExitBattleWatch();
                return;
            }
            LogDebug($"退出战斗[{req.Id}]胜败[{req.Win}]类型[{req.Type}]源[{req.Source}]");
            // 临时代码
            // if (Entity.Level > 0)
            // {
            //     _battleGrain = null;
            //     _ = _mapGrain.PlayerExitBattle(OnlyId);
            //     return;
            // }
            // 金翅大鹏 停掉 广播
            _eagleBroadCastTimer?.Dispose();
            _eagleBroadCastTimer = null;

            switch (req.Type)
            {
                case BattleType.SectWarArena:
                {
                    // 帮战-比武场
                    if (!InTeam || IsTeamLeader)
                    {
                        // 竞技场只可能输和赢, 避免多次提交, 让赢的人去提交
                        if (isWin)
                        {
                            _ = _sectWarGrain.OnPkWin(RoleId);
                        }
                    }
                }
                    break;
                case BattleType.SectWarCannon:
                {
                    // 帮战-抢夺炮台
                    if (!InTeam || IsTeamLeader)
                    {
                        if (isWin)
                        {
                            _ = _sectWarGrain.OnCannonWin(RoleId);
                        }
                    }
                }
                    break;
                case BattleType.SectWarDoor:
                {
                    // 帮战-抢夺城门
                    if (!InTeam || IsTeamLeader)
                    {
                        if (isWin)
                        {
                            _ = _sectWarGrain.OnDoorWin(RoleId);
                        }
                    }
                }
                    break;
                case BattleType.SectWarFreePk:
                {
                    // 帮战-自由PK
                    if (!InTeam || IsTeamLeader)
                    {
                        if (isWin)
                        {
                            _ = _sectWarGrain.OnFreePkWin(RoleId);
                        }
                    }
                }
                    break;
                case BattleType.ShuiLuDaHui:
                {
                    // 水陆大会
                    if (IsTeamLeader)
                    {
                        var grain = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(Entity.ServerId);
                        _ = grain.OnBattleEnd(TeamId, !isLost);
                    }
                }
                    break;
                case BattleType.WangZheZhiZhan:
                {
                    // 王者之战
                    if (IsTeamLeader)
                    {
                        var grain = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(Entity.ServerId);
                        _ = grain.OnBattleEnd(TeamId, !isLost);
                    }
                }
                    break;
                case BattleType.SinglePk:
                {
                    // 单人PK
                    if (_singlePkGrain != null)
                    {
                        _ = _singlePkGrain.OnBattleEnd(RoleId, !isLost);
                    }
                }
                    break;
                case BattleType.DaLuanDou:
                {
                    // 大乱斗PK
                    if (IsTeamLeader)
                    {
                        var grain = GrainFactory.GetGrain<IDaLuanDouGrain>(Entity.ServerId);
                        _ = grain.OnBattleEnd(TeamId, !isLost);
                    }
                }
                    break;
                case BattleType.HuangChengPk:
                {
                    // 皇城PK
                    if ((isWin || isDraw) && (!InTeam || IsTeamLeader))
                    {
                        var hcPkGrain = GrainFactory.GetGrain<IHcPkGrain>(Entity.ServerId);
                        _ = hcPkGrain.PkWin(RoleId, req.Win);
                    }
                }
                    break;
                case BattleType.Force:
                {
                    // 强制PK
                    if (req.Source == RoleId && isWin)
                    {
                        await AddShanE(GameDefine.PrisionTime); // 1个小时
                        if (IsTeamLeader)
                        {
                            // 所有队员都需要增加善恶值
                            var rids = await TeamGrain.QueryTeamPlayers(false);
                            foreach (var rid in rids)
                            {
                                var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                                _ = grain.AddShanE(GameDefine.PrisionTime);
                            }
                        }
                    }
                    if (isLost)
                    {
                        ulong subExp = (ulong)Math.Ceiling(Entity.Exp * 0.1);
                        if (subExp > 0)
                        {
                            Entity.Exp = Entity.Exp >= subExp ? Entity.Exp - subExp : 0;
                            await SendRoleExp();
                            SendNotice($"人在河边走，那有不湿鞋，经验掉了{subExp}");
                        }
                    }
                }
                    break;
                default:
                {
                    // 神兽降临
                    if (IsTeamLeader && req.Type == BattleType.ShenShouJiangLin )
                    {
                        var grain = GrainFactory.GetGrain<IShenShouJiangLinGrain>(Entity.ServerId);
                        _ = grain.OnBattleEnd(TeamId);
                    }
                    uint oldX2Exp = 0;
                    uint newX2Exp = 0;
                    ulong multiExp = 1;
                    // PVE
                    var reward = isWin;
                    // 任务相关
                    if (isWin)
                    {
                        if (req.Type == BattleType.DiShaXing)
                        {
                            // 每天最多杀5次
                            if (TaskMgr.StarNum < GameDefine.DiShaXingNumDaily)
                            {
                                TaskMgr.StarNum++;

                                // 只有队长才能升星
                                if (!InTeam || IsTeamLeader)
                                {
                                    if (req.StarLevel >= Entity.Star) Entity.Star++;
                                }
                            }
                            else
                            {
                                // 没有奖励
                                reward = false;
                            }

                            var dsxGrain = GrainFactory.GetGrain<IDiShaXingGrain>(Entity.ServerId);
                            _ = dsxGrain.ChallengeResult(req.Source, true);
                        }
                        else if (req.Type == BattleType.JinChanSongBao)
                        {
                            // 每天最多杀20次
                            if (TaskMgr.JinChanSongBaoNum < GameDefine.JinChanSongBaoNumDaily)
                            {
                                TaskMgr.JinChanSongBaoNum++;
                            }
                            else
                            {
                                // 没有奖励
                                reward = false;
                            }
                            var jcsbGrain = GrainFactory.GetGrain<IJinChanSongBaoGrain>(Entity.ServerId);
                            _ = jcsbGrain.ChallengeResult(req.Source, true);
                        }
                        else if (req.Type == BattleType.Eagle)
                        {
                            // 每天最多杀1次
                            if (TaskMgr.EagleNum < GameDefine.EagleNumDaily)
                            {
                                TaskMgr.EagleNum++;
                            }
                            else
                            {
                                // 没有奖励
                                reward = false;
                            }
                            var eagleGrain = GrainFactory.GetGrain<IEagleGrain>(Entity.ServerId);
                            _ = eagleGrain.ChallengeResult(req.Source, true);
                        }
                        else if (req.Type == BattleType.KuLouWang)
                        {
                            var swzGrain = GrainFactory.GetGrain<IKuLouWangGrain>(Entity.ServerId);
                            _ = swzGrain.ChallengeResult(req.Source, true);
                        }
                        else
                        {
                            // 任务完成 Normal Source是Npc OnlyId
                            var respBytes = await ServerGrain.FindNpc(req.Source);
                            if (respBytes.Value != null)
                            {
                                var mod = MapObjectData.Parser.ParseFrom(respBytes.Value);
                                ConfigService.Npcs.TryGetValue(mod.CfgId, out var cfg);
                                if (cfg != null)
                                {
                                    oldX2Exp = await RedisService.GetRoleX2ExpLeft(RoleId);
                                    await TaskMgr.SubmitKillNpcTask(req.Source, cfg);
                                    // 其实下面这里是多余了，但是为了确保能及时删除Npc，重复调用也行
                                    if (mod.Owner.Type == NpcOwnerType.Player)
                                    {
                                        DeleteNpc(mod.OnlyId);
                                    }
                                    else if (mod.Owner.Type == NpcOwnerType.Team)
                                    {
                                        DeleteTeamNpc(mod.OnlyId);
                                    }
                                    newX2Exp = await RedisService.GetRoleX2ExpLeft(RoleId);
                                    if(oldX2Exp > newX2Exp)
                                    {
                                        multiExp = 2;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (req.Type == BattleType.DiShaXing)
                        {
                            var dsxGrain = GrainFactory.GetGrain<IDiShaXingGrain>(Entity.ServerId);
                            _ = dsxGrain.ChallengeResult(req.Source, false);
                        }
                        if (req.Type == BattleType.JinChanSongBao)
                        {
                            var jcsbGrain = GrainFactory.GetGrain<IJinChanSongBaoGrain>(Entity.ServerId);
                            _ = jcsbGrain.ChallengeResult(req.Source, false);
                        }
                        if (req.Type == BattleType.Eagle)
                        {
#if false
                            // 每天最多杀1次
                            if (TaskMgr.EagleNum < GameDefine.EagleNumDaily)
                            {
                                TaskMgr.EagleNum++;
                            }
#endif
                            var eagleGrain = GrainFactory.GetGrain<IEagleGrain>(Entity.ServerId);
                            _ = eagleGrain.ChallengeResult(req.Source, false);
                        }
                        else if (req.Type == BattleType.KuLouWang)
                        {
                            var swzGrain = GrainFactory.GetGrain<IKuLouWangGrain>(Entity.ServerId);
                            _ = swzGrain.ChallengeResult(req.Source, false);
                        }
                        else
                        {
                            // 任务失败
                            await TaskMgr.FailEvent(TaskEventType.FailEventDead, new SubmitTaskEventData { Time = 0 });
                        }
                    }

                    // FIXME: 金翅大鹏 必得物品
                    if (reward && req.Type == BattleType.Eagle)
                    {
                        await AddItem(91001, 100, true, "战斗奖励");
                    }
                    //成神之路，挑战副本奖励
                    if (req.Type == BattleType.Cszl)
                    {
                        await OnCszlChallengeResult(reward);
                    }
                    // 获得MonsterGroup奖励
                    if (reward && req.MonsterGroup > 0)
                    {
                        ConfigService.MonsterGroups.TryGetValue(req.MonsterGroup, out var groupCfg);
                        if (groupCfg != null)
                        {
                            if (groupCfg.Exp > 0)
                            {
                                var exp = groupCfg.Exp;
                                // 北俱暗雷、杀星、骷髅王 经验翻倍
                                // if (groupCfg.Id == 10154 || req.Type == BattleType.DiShaXing ||
                                //     req.Type == BattleType.KuLouWang)
                                // {
                                //     exp *= 2;
                                // }

                                var expAddition = 0u;
                                // 队长经验加成10%
                                if (IsTeamLeader && TeamMemberCount >= 5)
                                {
                                    expAddition = (uint)MathF.Ceiling(exp * 0.1f);
                                }

                                await AddExp((exp + expAddition) * multiExp);
                                //这里暂时不要发送Pet, 等后面增加亲密度再发送
                                if (PetMgr.Pet != null)
                                    await PetMgr.Pet.AddExp(exp * multiExp);

                                if (expAddition > 0)
                                {
                                    SendNotice($"队长额外获得: {expAddition * multiExp}角色经验");
                                }
                            }

                            // 宠物增加亲密度, 这里发送宠物数据
                            PetMgr.Pet?.AddIntimacy(1);

                            if (groupCfg.Gold > 0) await AddMoney(MoneyType.Silver, (int)groupCfg.Gold, "战斗奖励");
                            if (groupCfg.Items != null)
                            {
                                foreach (var item in groupCfg.Items)
                                {
                                    if (item.Num == 0) continue;

                                    if (item.Id > 0)
                                    {
                                        var r = Random.Next(0, 100);
                                        if (r < item.Rate)
                                        {
                                            await AddItem(item.Id, (int)item.Num, true, "战斗奖励");
                                        }
                                    }
                                    else if (item.Ids is { Length: > 0 })
                                    {
                                        // 从ids中随机产生num个
                                        var dic = new Dictionary<uint, int>();
                                        for (var i = 0; i < item.Num; i++)
                                        {
                                            var idx = Random.Next(0, item.Ids.Length);
                                            var cfgId = item.Ids[idx];
                                            if (dic.ContainsKey(cfgId))
                                                dic[cfgId] += 1;
                                            else
                                                dic[cfgId] = 1;
                                        }

                                        foreach (var (k, v) in dic)
                                        {
                                            await AddItem(k, v, true, "战斗奖励");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (oldX2Exp > newX2Exp)
                    {
                        SendNotice($"已获双倍经验，还剩双倍经验点<color=#00ff00>{newX2Exp}</c>");
                    }
                }
                    break;
            }

            // 开启AOI同步
            if (_mapGrain != null)
            {
                _ = _mapGrain.PlayerExitBattle(OnlyId);
            }
        }

        public async Task OnTeamLeave()
        {
            if (!IsActive) return;
            if (!InTeam) return;
            _teamLeave = true;
            _ = _mapGrain.SetPlayerTeamLeave(OnlyId, _teamLeave);
            await Task.CompletedTask;
        }

        public async Task OnTeamBack(uint mapId, int mapX, int mapY)
        {
            if (!IsActive) return;
            if (!InTeam) return;
            _teamLeave = false;
            _ = _mapGrain.SetPlayerTeamLeave(OnlyId, _teamLeave);
            // 立即同步队伍位置
            await OnTeamMapPosChanged(mapId, mapX, mapY, true, false, false);
        }

        public async Task OnExitTeam()
        {
            if (!IsActive) return;
            var inTeam = InTeam;
            TeamId = 0;
            TeamLeader = 0;
            TeamGrain = null;
            if (inTeam)
            {
                await SendPacket(GameCmd.S2CTeamData, new S2C_TeamData { Data = null });
                // 通知地图服务
                TeamMemberCount = 0;
                _ = _mapGrain.SetPlayerTeam(OnlyId, 0, 0, 0);
            }

            await TaskMgr.AbortAllTeamTask();
        }

        public Task OnTeamChanged(uint teamId, uint teamLeader, uint teamMemberCount)
        {
            if (!IsActive) return Task.CompletedTask;
            if (InTeam && TeamId == teamId)
            {
                TeamLeader = teamLeader;
                // 通知地图服务, teamMemberCount只对队长有效
                if (teamLeader == RoleId) TeamMemberCount = teamMemberCount;
                else TeamMemberCount = 0;
                _ = _mapGrain.SetPlayerTeam(OnlyId, teamId, teamLeader, TeamMemberCount);
            }

            return Task.CompletedTask;
        }

        public async Task OnTeamJoinApplyAgree(uint teamId, uint teamLeader, byte teamTarget, uint teamSect)
        {
            if (!IsActive) return;
            // 申请被同意, 主动加入
            if (InTeam || InBattle || await IsSignedSinglePk() || await IsSignedDaLuanDou())
            {
                return;
            }

            // 如果是帮战队伍, 需要主动加入帮战
            var target = (TeamTarget)teamTarget;
            if (target == TeamTarget.SectWar)
            {
                if (Entity.SectId != teamSect || _sectWarCamp == 0) return;
            }
            else
            {
                // 帮战中不能加入非帮战队伍
                if (_sectWarCamp > 0) return;
            }

            // 构建队伍成员数据
            var req = new TeamObjectData
            {
                Type = TeamObjectType.Player,
                OnlyId = OnlyId,
                DbId = RoleId,
                Name = Entity.NickName,
                CfgId = Entity.CfgId,
                Relive = Entity.Relive,
                Level = Entity.Level,
                Online = IsEnterServer,
                SldhScore = 0,
                WzzzScore = 0,
                SectId = Entity.SectId,
                Skins = { SkinUse },
                Weapon = MapWeapon,
                Wing = MapWing,
            };

            // 加入的过程要二次确认
            var ret = false;
            uint mapId = 0;
            var mapX = 0;
            var mapY = 0;
            try
            {
                TeamId = teamId;
                TeamLeader = teamLeader;
                TeamGrain = GrainFactory.GetGrain<ITeamGrain>($"{Entity.ServerId}_{teamId}");
                var bytes = await TeamGrain.Join(new Immutable<byte[]>(Packet.Serialize(req)));
                if (bytes.Value != null && bytes.Value is { Length: > 0 })
                {
                    var resp = JoinTeamResponse.Parser.ParseFrom(bytes.Value);
                    mapId = resp.MapId;
                    mapX = resp.MapX;
                    mapY = resp.MapY;

                    ret = true;
                }
            }
            finally
            {
                if (!ret)
                {
                    TeamId = 0;
                    TeamLeader = 0;
                    TeamGrain = null;
                }
            }

            // 加入成功
            if (ret)
            {
                if (Entity.MapId != mapId)
                {
                    await ChangeMap(mapId, mapX, mapY);
                }
                else
                {
                    Entity.MapX = mapX;
                    Entity.MapY = mapY;
                    await _mapGrain.PlayerMove(OnlyId, mapX, mapY, true);
                }

                // 由于我不是队长，所以memberCount可以传递0
                _ = _mapGrain.SetPlayerTeam(OnlyId, TeamId, TeamLeader, 0);

                if (IsEnterServer)
                {
                    await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                    {
                        Map = Entity.MapId,
                        X = Entity.MapX,
                        Y = Entity.MapY,
                        Immediate = true
                    });
                }
            }
        }

        /// <summary>
        /// 队长移动后提交了位置信息给TeamGrain, TeamGrain统一打包一次性提交给了MapGrain, 所以这里只需要修改自身的数据即可
        /// </summary>
        public async Task OnTeamMapPosChanged(uint mapId, int mapX, int mapY, bool immediate = false,
            bool includeLeader = false, bool synced = true)
        {
            if (!IsActive) return;
            if (!InTeam) return;
            if (!includeLeader && IsTeamLeader) return;

            if (mapId != Entity.MapId)
            {
                Entity.MapId = mapId;
                Entity.MapX = mapX;
                Entity.MapY = mapY;
                await EnterMap();

                // 通知前端刷新地图
                if (IsEnterServer)
                {
                    await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                    {
                        Map = Entity.MapId,
                        X = Entity.MapX,
                        Y = Entity.MapY,
                        Immediate = true
                    });
                }
            }
            else
            {
                Entity.MapId = mapId;
                Entity.MapX = mapX;
                Entity.MapY = mapY;

                if (!synced)
                {
                    // 通知地图
                    _ = _mapGrain.PlayerMove(OnlyId, mapX, mapY, immediate);

                    if (IsEnterServer)
                    {
                        await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                        {
                            Map = Entity.MapId,
                            X = Entity.MapX,
                            Y = Entity.MapY,
                            Immediate = immediate
                        });
                    }
                }
            }

            await Task.CompletedTask;
        }

        public async Task OnTeamTasksChanged(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return;
            if (!InTeam || IsTeamLeader) return;
            var req = UpdateTeamTasksRequest.Parser.ParseFrom(reqBytes.Value);
            await TaskMgr.OnTeamTasksChanged(req.List);
        }

        public async Task OnTeamTaskEventFinish(uint taskId, uint step)
        {
            if (!IsActive) return;
            if (!InTeam || IsTeamLeader) return;
            await TaskMgr.OnTeamTaskEventDone(taskId, step);
        }

        public async Task OnTeamTaskFinish(uint cfgId, bool success)
        {
            if (!IsActive) return;
            if (!InTeam || IsTeamLeader) return;
            await TaskMgr.OnTeamTaskFinish(cfgId, success);
        }

        public async Task OnTeamLeaderApplyPassed(uint teamId)
        {
            await Task.CompletedTask;
            if (!IsActive) return;
            if (!InTeam || IsTeamLeader || TeamId != teamId) return;
            if (InBattle)
            {
                SendNotice("战斗中不允许交换队长");
                return;
            }

            var req = new HandOverTeamRequest { RoleId = RoleId };
            req.List.AddRange(PartnerMgr.Actives.Select(p => p.BuildTeamObjectData()));
            _ = TeamGrain.HandOver(new Immutable<byte[]>(Packet.Serialize(req)));
        }

        public async Task OnEnterSect(uint sectId, string sectName, uint ownerId, byte job)
        {
            if (!IsActive) return;
            // 帮主创建帮派的时候, 就已经赋过值了
            if (ownerId != RoleId)
            {
                Entity.SectId = sectId;
                Entity.SectContrib = 0;
                Entity.SectJob = job;
                Entity.SectJoinTime = TimeUtil.TimeStamp;
                SetFlag(FlagType.SectSilent, false);
            }

            SectGrain = GrainFactory.GetGrain<ISectGrain>(sectId);
            _ = _mapGrain.SetPlayerSect(OnlyId, Entity.SectId);

            // 增加帮派称号
            await TitleMgr.AddSectTitle((SectMemberType)job, sectName);
        }

        public async Task OnExitSect(uint sectId, string sectName, uint ownerId)
        {
            if (!IsActive) return;
            Entity.SectId = 0;
            Entity.SectContrib = 0;
            Entity.SectJob = 0;
            Entity.SectJoinTime = 0;
            SectGrain = null;
            SetFlag(FlagType.SectSilent, false);

            _ = _mapGrain.SetPlayerSect(OnlyId, Entity.SectId);
            await SendPacket(GameCmd.S2CSectData, new S2C_SectData());

            // 清空所有帮派称号
            await TitleMgr.DelSectTitles();
        }

        public async Task OnSectJob(uint sectId, string sectName, uint targetRoleId, byte job)
        {
            if (!IsActive) return;
            if (Entity.SectId != sectId) return;

            // 发送通知
            if (IsEnterServer)
            {
                await SendPacket(GameCmd.S2CSectAppoint, new S2C_SectAppoint
                {
                    RoleId = targetRoleId,
                    Type = (SectMemberType)job
                });
            }

            if (targetRoleId == RoleId)
            {
                Entity.SectJob = job;
                await TitleMgr.AddSectTitle((SectMemberType)job, sectName);
            }
        }

        public async Task OnSectSilent(uint sectId, string sectName, uint opRoleId, string opName, byte opJob)
        {
            if (!IsActive) return;
            if (Entity.SectId != sectId) return;
            if (GetFlag(FlagType.SectSilent)) return;

            SetFlag(FlagType.SectSilent, true);
            if (IsEnterServer)
            {
                await SendPacket(GameCmd.S2CSectSilent, new S2C_SectSilent
                {
                    RoleId = RoleId,
                    Silent = true,
                    OpRoleId = opRoleId,
                    OpName = opName,
                    OpJob = (SectMemberType)opJob,
                });
            }
        }

        public async Task OnSectSilentGm()
        {
            if (!IsActive) return;
            if (GetFlag(FlagType.SectSilent)) return;

            SetFlag(FlagType.SectSilent, true);
            if (IsEnterServer)
            {
                await SendPacket(GameCmd.S2CSectSilent, new S2C_SectSilent
                {
                    RoleId = RoleId,
                    Silent = true,
                    OpRoleId = 0,
                    OpName = "管理员",
                    OpJob = SectMemberType.Unkown,
                });
            }
        }

        public async Task OnSectJoinApplyAgree(uint sectId, uint ownerId)
        {
            if (!IsActive) return;
            // 申请被同意, 主动加入
            if (InSect) return;
            // 主动加入
            var grain = GrainFactory.GetGrain<ISectGrain>(sectId);
            _ = grain.Join(new Immutable<byte[]>(Packet.Serialize(BuildSectMemberData())));
            await Task.CompletedTask;
        }

        /// <summary>
        /// 当我在线的时候，有人申请加我为好友
        /// </summary>
        public async Task OnFriendApply(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return;
            if (reqBytes.Value == null || reqBytes.Value.Length == 0) return;
            var req = RoleInfo.Parser.ParseFrom(reqBytes.Value);
            if (req == null || req.Id == RoleId) return;

            // 检查对方是否是我的好友
            var ret = _friendList.Exists(p => p.Id == req.Id);
            if (ret) return;
            // 检查是否已经申请过了
            ret = _friendApplyList.Exists(p => p.Id == req.Id);
            if (ret) return;
            // 加入到申请列表
            _friendApplyList.Add(req);
            // 加入数据库
            await RedisService.AddFriendApply(RoleId, req.Id);

            // 通知客户端
            await SendPacket(GameCmd.S2CFriendApplyAdd, new S2C_FriendApplyAdd { Data = req });
        }

        // 正式建交, 主动建交的人通知我，我不用再通知他, 我也不用入库
        public async Task OnFriendAdd(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return;
            var friend = RoleInfo.Parser.ParseFrom(reqBytes.Value);
            if (friend == null || friend.Id == RoleId) return;
            // 检查是否已经是我的好友
            if (_friendList.Exists(p => p.Id == friend.Id)) return;
            _friendList.Add(friend);
            // 推送新建立的好友
            await SendPacket(GameCmd.S2CFriendAdd, new S2C_FriendAdd
            {
                Data = friend
            });

            // 如果我的好友也申请过加我，那么就删除
            var idx = _friendApplyList.FindIndex(p => p.Id == friend.Id);
            if (idx >= 0)
            {
                _friendApplyList.RemoveAt(idx);
                await RedisService.DelFriendApply(RoleId, friend.Id);
                // 通知前端删除申请
                await SendPacket(GameCmd.S2CFriendApplyDel, new S2C_FriendApplyDel { RoleId = friend.Id });
            }
        }

        public async Task OnFriendDel(uint roleId)
        {
            if (!IsActive) return;
            if (roleId == RoleId) return;
            var idx = _friendList.FindIndex(p => p.Id == roleId);
            if (idx < 0) return;
            // 删除好友, 数据不用处理，主动删除方已经处理过了
            _friendList.RemoveAt(idx);
            // 通知前端
            await SendPacket(GameCmd.S2CFriendDel, new S2C_FriendDel { RoleId = roleId });
        }

        public async Task OnRecvChat(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return;
            var cm = ChatMessage.Parser.ParseFrom(reqBytes.Value);
            if (cm.Type != ChatMessageType.Friend && cm.Type != ChatMessageType.Stranger) return;
            if (cm.From == null || cm.From.Id == RoleId) return;

            if (cm.Type == ChatMessageType.Friend)
            {
                if (!_friendList.Exists(p => p.Id == cm.From.Id)) return;
            }
            else if (cm.Type == ChatMessageType.Stranger)
            {
                if (_friendList.Exists(p => p.Id == cm.From.Id))
                {
                    cm.Type = ChatMessageType.Friend;
                }
            }

            await SendPacket(GameCmd.S2CChat, new S2C_Chat { Msg = cm });
        }

        public async ValueTask<bool> OnChatSilent()
        {
            if (!IsActive) return false;
            if (IsGm) return false;
            if (!GetFlag(FlagType.WorldSilent))
            {
                SetFlag(FlagType.WorldSilent, true);
                await SendPacket(GameCmd.S2CChatSilent, new S2C_ChatSilent { RoleId = RoleId, Silent = true });
            }

            return true;
        }

        public async Task OnMallItemSelled(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return;
            var req = OnMallItemSelledRequest.Parser.ParseFrom(reqBytes.Value);
            // 获得银币奖励
            await AddMoney(MoneyType.Silver, (int)req.Reward);
            // 提示
            SendNotice($"您摆摊的商品被人购买了，获得{req.Reward}银币");
        }

        // 商品下架
        public async ValueTask<bool> OnMallItemUnShelf(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return false;
            var resp = MallItemUnShelfRequest.Parser.ParseFrom(reqBytes.Value);
            if (resp.Type == MallItemType.Item)
            {
                await AddBagItem(resp.CfgId, (int)resp.Num, tag: "下架商品");
            }

            if (resp.Type == MallItemType.Equip)
            {
                var entity = await DbService.Sql.Queryable<EquipEntity>().Where(it => it.Id == resp.DbId)
                    .FirstAsync();
                if (entity == null) return true;
                // 加载到内存中来
                entity.RoleId = RoleId;
                await DbService.Sql.Update<EquipEntity>()
                    .Where(it => it.Id == resp.DbId)
                    .Set(it => it.RoleId, RoleId)
                    .ExecuteAffrowsAsync();
                // 添加到装备管理器中
                await EquipMgr.AddEquip(entity);
            }

            if (resp.Type == MallItemType.Pet)
            {
                var entity = await DbService.Sql.Queryable<PetEntity>().Where(it => it.Id == resp.DbId)
                    .FirstAsync();
                if (entity == null) return true;
                // 加载到内存中来
                entity.RoleId = RoleId;
                await DbService.Sql.Update<PetEntity>()
                    .Where(it => it.Id == resp.DbId)
                    .Set(it => it.RoleId, RoleId)
                    .ExecuteAffrowsAsync();
                // 添加到装备管理器中
                await PetMgr.AddPet(entity);
            }

            SendNotice("您摆摊的商品已下架");
            await SendPacket(GameCmd.S2CMallDelItem, new S2C_MallDelItem { Id = resp.Id });
            return true;
        }

        public async ValueTask<bool> PreCheckPvp(byte type = 0)
        {
            if (!IsActive) return false;
            await Task.CompletedTask;
            if (InBattle) return false;
            if (InTeam && !IsTeamLeader) return false;
            return true;
        }

        /// <summary>
        /// 主动发起一场PVP
        /// </summary>
        public async ValueTask<int> StartPvp(uint targetRoleId, byte type = 0)
        {
            if (!IsActive) return 1;
            if (InBattle || targetRoleId == RoleId) return 1;
            if (InTeam && !IsTeamLeader && !_teamLeave)
            {
                SendNotice("队员不能发起PK");
                return 1;
            }

            var battleType = (BattleType)type;
            if (battleType == BattleType.Force)
            {
                if (Entity.Level < GameDefine.PkLevelLimit)
                {
                    SendNotice("等级不足");
                    return 1;
                }

                if (!GameDefine.PkMaps.ContainsKey(Entity.MapId))
                {
                    SendNotice("当前地图不支持PK");
                    return 1;
                }
            }

            // 从Battles中创建一个id
            var battleId = await GlobalGrain.CreateBattle();

            // 检查对方是否符合PK的条件
            var targetGrain = GrainFactory.GetGrain<IPlayerGrain>(targetRoleId);
            var respBytes = await targetGrain.OnPvp(new Immutable<byte[]>(Packet.Serialize(
                new GetBattleMembersRequest
                {
                    BattleId = battleId,
                    BattleType = battleType
                })));
            if (respBytes.Value == null)
            {
                SendNotice("对方当前不支持PK");
                // 说明对方当前不符合PVP条件, 记得要释放battleId
                _ = GlobalGrain.RemoveBattle(battleId);
                return -1;
            }

            var resp = GetBattleMembersResponse.Parser.ParseFrom(respBytes.Value);
            if (resp.List.Count == 0)
            {
                SendNotice("对方当前不支持PK");
                _ = GlobalGrain.RemoveBattle(battleId);
                return -1;
            }

            // 构建我方阵营
            var members = await BuildBattleTeamData(battleId, battleType, 1);
            if (members.Count <= 0)
            {
                SendNotice("构建阵营失败，请稍候再试！");
            }

            // 构建战斗发起请求
            var req = new StartBattleRequest
            {
                Type = battleType,
                Source = RoleId,
                RoleId = RoleId,
                Team1 = { members },
                Team2 = { resp.List },
                ServerId = Entity.ServerId
            };

            _battleGrain = GrainFactory.GetGrain<IBattleGrain>(battleId);
            _battleId = battleId;
            _campId = 1;
            // 发起战斗
            _ = _battleGrain.StartUp(new Immutable<byte[]>(Packet.Serialize(req)));
            // 停止AOI同步
            _ = _mapGrain.PlayerEnterBattle(OnlyId, _battleId, _campId);
            // 广播观战 皇城pk
            if (battleType == BattleType.HuangChengPk)
            {
                var bytes = await targetGrain.GetRoleInfo();
                var sender = BuildRoleInfo();
                var senderSkin = new List<int>();
                senderSkin.AddRange(sender.Skins);
                var recver = RoleInfo.Parser.ParseFrom(bytes.Value);
                var recverSkin = new List<int>();
                recverSkin.AddRange(recver.Skins);
                // 构造消息 发起方
                var msgSender = new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Msg = Json.SafeSerialize(new
                    {
                        type = "HuangChengPk",
                        recver = Json.SafeSerialize(new
                        {
                            id = recver.Id,
                            name = recver.Name,
                            relive = recver.Relive,
                            level = recver.Level,
                            cfgId = recver.CfgId,
                            type = recver.Type,
                            skins = recverSkin,
                            vipLevel = recver.VipLevel,
                            qiegeLevel = recver.QiegeLevel
                        })
                    }),
                    From = sender,
                    To = 0,
                    BattleInfo = new InBattleInfo() { BattleId = _battleId, CampId = 1 }
                };
                _ = ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msgSender })));
                // 构造消息 接收方
                var msgRecver = new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Msg = Json.SafeSerialize(new
                    {
                        type = "HuangChengPk",
                        sender = Json.SafeSerialize(new
                        {
                            id = sender.Id,
                            name = sender.Name,
                            relive = sender.Relive,
                            level = sender.Level,
                            cfgId = sender.CfgId,
                            type = sender.Type,
                            skins = senderSkin,
                            vipLevel = sender.VipLevel,
                            qiegeLevel = sender.QiegeLevel
                        })
                    }),
                    From = recver,
                    To = 0,
                    BattleInfo = new InBattleInfo() { BattleId = _battleId, CampId = 2 }
                };
                _ = ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msgRecver })));
            }
            return 0;
        }

        public async Task<Immutable<byte[]>> OnPvp(Immutable<byte[]> reqBytes)
        {
            if (!IsActive) return new(null);
            if (InBattle) return new Immutable<byte[]>(null);
            if (InTeam && !IsTeamLeader && !_teamLeave) return new Immutable<byte[]>(null);
            var req = GetBattleMembersRequest.Parser.ParseFrom(reqBytes.Value);
            // 如果是玩家强制PK，需要等级限制
            if (req.BattleType == BattleType.Force)
            {
                // 检查等级
                if (Entity.Level < GameDefine.PkLevelLimit) return new Immutable<byte[]>(null);
                // 检查地图
                if (!GameDefine.PkMaps.ContainsKey(Entity.MapId)) return new Immutable<byte[]>(null);
            }

            // 构建参战队伍
            var list = await BuildBattleTeamData(req.BattleId, req.BattleType, 2);
            if (list.Count > 0)
            {
                // 标记为进入战斗
                _battleGrain = GrainFactory.GetGrain<IBattleGrain>(req.BattleId);
                _battleId = req.BattleId;
                _campId = 2;
                // 停止AOI同步
                _ = _mapGrain.PlayerEnterBattle(OnlyId, _battleId, _campId);
            }
            else
            {
                SendNotice("构建阵营失败，请稍候再试！");
                return new(null);
            }

            return new Immutable<byte[]>(Packet.Serialize(new GetBattleMembersResponse
            {
                List = { list }
            }));
        }

        public Task OnEnterShuiLuDaHui(uint group)
        {
            if (!IsActive) return Task.CompletedTask;
            _sldh.Group = group;
            return _mapGrain.SetPlayerSldhGroup(OnlyId, _sldh.Group);
        }

        public Task OnEnterWangZheZhiZhan(uint group)
        {
            if (!IsActive) return Task.CompletedTask;
            _wzzz.Group = group;
            return _mapGrain.SetPlayerWzzzGroup(OnlyId, _wzzz.Group);
        }

        public async Task OnExitShuiLuDaHui(bool changeMap = false)
        {
            if (!IsActive) return;
            _sldh.Group = 0;
            await _mapGrain.SetPlayerSldhGroup(OnlyId, _sldh.Group);

            if (Entity.MapId == 3001)
            {
                await ChangeMap(1206, 25, 18);
                // 通知前端刷新地图
                if (IsEnterServer)
                {
                    await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                    {
                        Map = Entity.MapId,
                        X = Entity.MapX,
                        Y = Entity.MapY,
                        Immediate = true
                    });
                }
            }
        }
        public async Task OnExitWangZheZhiZhan(bool changeMap = false)
        {
            if (!IsActive) return;
            _wzzz.Group = 0;
            await _mapGrain.SetPlayerWzzzGroup(OnlyId, _wzzz.Group);

            // if (Entity.MapId == 3001)
            // {
            //     await ChangeMap(1206, 25, 18);
            //     // 通知前端刷新地图
            //     if (IsEnterServer)
            //     {
            //         await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
            //         {
            //             Map = Entity.MapId,
            //             X = Entity.MapX,
            //             Y = Entity.MapY,
            //             Immediate = true
            //         });
            //     }
            // }
        }

        public async Task OnShuiLuDaHuiBattleResult(uint season, bool win)
        {
            if (!IsActive) return;
            var exp = win ? Entity.Level * 200000UL : Entity.Level * 100000UL;
            var petExp = (ulong)Math.Floor(exp * 1.5f);
            var score = (uint)(win ? 100 : 50);
            var gongji = (uint)(win ? 100 : 50);

            await AddExp(exp);
            if (PetMgr.Pet != null) await PetMgr.Pet.AddExp(petExp);
            await AddMoney(MoneyType.SldhGongJi, (int)gongji);

            await AddShuiLuDaHuiScore(season, score, win);

            await SendPacket(GameCmd.S2CSldhBattleResult, new S2C_SldhBattleResult
            {
                Win = win,
                Exp = exp,
                PetExp = petExp,
                Score = score,
                GongJi = gongji
            });
        }

        public async Task OnDaLuanDouBattleResult(uint season, bool win)
        {
            await Task.CompletedTask;
            if (!IsActive) return;
            var exp = win ?  50000000UL : 10000000UL;
            // var petExp = (ulong)Math.Floor(exp * 1.5f);
            var score = (int)(win ? 50 : 0);
            // var gongji = (uint)(win ? 100 : 50);

            await AddExp(exp);
            // if (PetMgr.Pet != null) await PetMgr.Pet.AddExp(petExp);
            // await AddMoney(MoneyType.BindJade, (int)score);

            await RedisService.AddDaLuanDouScore(Entity.ServerId, RoleId, (uint)score);

            await SendPacket(GameCmd.S2CDaLuanDouBattleResult, new S2C_DaLuanDouBattleResult
            {
                Win = win,
                Exp = exp,
                // PetExp = petExp,
                Score = score//,
                // GongJi = gongji
            });
        }

        //王者之战 战斗结束处理逻辑
        public async Task OnWangZheZhiZhanBattleResult(uint season, bool win)
        {
            if (!IsActive) return;
            // var exp = win ? Entity.Level * 200000UL : Entity.Level * 100000UL;
            // var petExp = (ulong)Math.Floor(exp * 1.5f);
            var score = (uint)(win ? 5 : 2);
            var gongji = (uint)(win ? 5 : 2);

            //加1000w经验
            await AddExp(10000000UL);
            // if (PetMgr.Pet != null) await PetMgr.Pet.AddExp(petExp);
            await AddMoney(MoneyType.WzzzJiFen, (int)gongji);

            await AddWangZheZhiZhanScore(season, score, win);

            await SendPacket(GameCmd.S2CWzzzBattleResult, new S2C_WzzzBattleResult
            {
                Win = win,
                // Exp = exp,
                // PetExp = petExp,
                Score = score,
                GongJi = gongji
            });
            LogDebug($"王者之战战斗结束，添加积分score={score} gongji={gongji}");
        }

        public async Task OnShuiLuDaHuiNewSeason(uint season)
        {
            if (!IsActive) return;
            // 重置水路大会所有数据
            _sldh = new RoleSldhVo { Season = season };
            await SyncSldh();
        }
        public async Task OnDaLuanDouNewSeason(uint season)
        {
            if (!IsActive) return;
            // 重置大乱斗所有数据
            _daLuanDouVo = new RoleDaLuanDouVo { Season = season };
            await SyncDaLuanDou();
        }
        public async Task OnWangZheZhiZhanNewSeason(uint season)
        {
            if (!IsActive) return;
            // 重置王者之战所有数据
            _wzzz = new RoleWzzzVo { Season = season };
            await SyncWzzz();
        }

        /// <summary>
        /// 进入神兽降临
        /// </summary>
        public async Task OnEnterShenShouJiangLin()
        {
            if (!IsActive) return;
            _ssjl.Signed = true;
            await Task.CompletedTask;
        }

        /// <summary>
        /// 退出神兽降临
        /// </summary>
        public async Task OnExitShenShouJiangLin(bool changeMap)
        {
            if (!IsActive) return;
            _ssjl.Signed = false;
            if (!changeMap) return;
            ConfigService.Npcs.TryGetValue(81004, out var npcCfg);
            if (npcCfg == null) return;
            ConfigService.Maps.TryGetValue(npcCfg.AutoCreate.Map, out var mapCfg);
            if (mapCfg == null) return;
            // 回到神兽大使附近
            var x = npcCfg.AutoCreate.X + new Random().Next(-50, -50);
            var y = npcCfg.AutoCreate.Y + new Random().Next(-50, -50);
            await ChangeMap(mapCfg.Id, x, y);
            // 通知前端刷新地图
            if (IsEnterServer)
            {
                await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                {
                    Map = Entity.MapId,
                    X = Entity.MapX,
                    Y = Entity.MapY,
                    Immediate = true
                });
            }
        }
        /// <summary>
        /// 神兽降临开始抓捕
        /// </summary>
        public async Task OnStartShenShouJiangLin(uint endTime, uint shenShouId, uint serverId)
        {
            if (!IsActive) return;
            _ssjl.ServerId = serverId;
            _ssjl.Started = true;
            _ssjl.EndTime = endTime;
            _ssjl.ShenShouId = shenShouId;
            _ssjl.NextTime =  (uint)Random.Next(15, 31);
            await Task.CompletedTask;
        }
        /// <summary>
        /// 神兽降临停止抓捕
        /// </summary>
        public async Task OnStopShenShouJiangLin()
        {
            if (!IsActive) return;
            _ssjl.Started = false;
            _ssjl.EndTime = 0;
            _ssjl.NextTime = 0;
            await Task.CompletedTask;
        }
        public async Task OnShenShouJiangLinNewSeason(uint season)
        {
            if (!IsActive) return;
            _ssjl = new RoleSsjlVo { Season = season, Started = false };
            await Task.CompletedTask;
        }

        public async Task OnEnterSinglePk()
        {
            if (!IsActive) return;
            _singlePkVo.Sign = true;

            // 进入PK地图
            Entity.MapId = 3003;
            Entity.MapX = 999999;
            Entity.MapY = 999999;
            await EnterMap();

            // 通知前端刷新地图
            if (IsEnterServer)
            {
                await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                {
                    Map = Entity.MapId,
                    X = Entity.MapX,
                    Y = Entity.MapY,
                    Immediate = true
                });
            }
        }

        public async Task OnExitSinglePk(uint win, uint lost, uint score)
        {
            if (!IsActive) return;
            _singlePkVo.Sign = false;
            _singlePkVo.Win += win;
            _singlePkVo.Lost += lost;
            _singlePkVo.Score += score;
            await SyncSinglePk();

            // 退出战斗
            if (InBattle && _battleGrain != null)
            {
                var battleId = (uint)_battleGrain.GetPrimaryKeyLong();

                _ = _battleGrain.Exit(RoleId);
                _ = _mapGrain.PlayerExitBattle(OnlyId);
                _battleGrain = null;
                _battleId = 0;
                _campId = 0;

                await SendPacket(GameCmd.S2CBattleStop, new S2C_BattleStop { BattleId = battleId, Win = false });
            }

            // 如果是在单人PK地图，则退到金銮殿
            if (Entity.MapId == 3003)
            {
                Entity.MapId = 1206;
                Entity.MapX = 999999;
                Entity.MapY = 999999;
                await EnterMap();

                // 通知前端刷新地图
                await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                {
                    Map = Entity.MapId,
                    X = Entity.MapX,
                    Y = Entity.MapY,
                    Immediate = true
                });
            }
        }

        public async Task OnEnterDaLuanDou()
        {
            if (!IsActive) return;
            _daLuanDouVo.Sign = true;

            // 进入大乱斗地图
            Entity.MapId = 3004;
            Entity.MapX = 999999;
            Entity.MapY = 999999;
            await EnterMap();

            // 通知前端刷新地图
            if (IsEnterServer)
            {
                await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                {
                    Map = Entity.MapId,
                    X = Entity.MapX,
                    Y = Entity.MapY,
                    Immediate = true
                });
            }
        }

        public async Task OnExitDaLuanDou(uint win, uint lost, uint score)
        {
            if (!IsActive) return;
            // _daLuanDouVo.Sign = false;
            // _daLuanDouVo.Win += win;
            // _daLuanDouVo.Lost += lost;
            // _daLuanDouVo.Score += score;
            // await SyncDaLuanDou();

            // 退出战斗
            if (InBattle && _battleGrain != null)
            {
                var battleId = (uint)_battleGrain.GetPrimaryKeyLong();

                _ = _battleGrain.Exit(RoleId);
                _ = _mapGrain.PlayerExitBattle(OnlyId);
                _battleGrain = null;
                _battleId = 0;
                _campId = 0;

                await SendPacket(GameCmd.S2CBattleStop, new S2C_BattleStop { BattleId = battleId, Win = false });
            }

            // 如果是在单人PK地图，则退到长安城
            if (Entity.MapId == 3004)
            {
                Entity.MapId = 1011;
                Entity.MapX = 999999;
                Entity.MapY = 999999;
                await EnterMap();

                // 通知前端刷新地图
                await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                {
                    Map = Entity.MapId,
                    X = Entity.MapX,
                    Y = Entity.MapY,
                    Immediate = true
                });
            }
        }

        public async ValueTask<bool> OnStarBattle(uint npcOnlyId, byte level)   //todo 挑战地煞星
        {
            if (!IsActive) return false;
            var respBytes = await ServerGrain.FindNpc(npcOnlyId);
            if (respBytes.Value == null) return false;
            var mod = MapObjectData.Parser.ParseFrom(respBytes.Value);
            if (mod == null) return false;
            ConfigService.Npcs.TryGetValue(mod.CfgId, out var cfg);
            if (cfg == null) return false;
            var ret = await StartPve(npcOnlyId, cfg.MonsterGroup, BattleType.DiShaXing, starLevel: level);
            return ret;
        }

        public async ValueTask<bool> OnKuLouWangBattle(uint npcOnlyId)
        {
            if (!IsActive) return false;
            var respBytes = await ServerGrain.FindNpc(npcOnlyId);
            if (respBytes.Value == null) return false;
            var mod = MapObjectData.Parser.ParseFrom(respBytes.Value);
            if (mod == null) return false;
            ConfigService.Npcs.TryGetValue(mod.CfgId, out var cfg);
            if (cfg == null) return false;
            var ret = await StartPve(npcOnlyId, cfg.MonsterGroup, BattleType.KuLouWang);
            return ret;
        }

        public async ValueTask<bool> OnJinChanSongBaoBattle(uint npcOnlyId)
        {
            if (!IsActive) return false;
            var respBytes = await ServerGrain.FindNpc(npcOnlyId);
            if (respBytes.Value == null) return false;
            var mod = MapObjectData.Parser.ParseFrom(respBytes.Value);
            if (mod == null) return false;
            ConfigService.Npcs.TryGetValue(mod.CfgId, out var cfg);
            if (cfg == null) return false;
            var ret = await StartPve(npcOnlyId, cfg.MonsterGroup, BattleType.JinChanSongBao);
            return ret;
        }

        public async ValueTask<bool> OnEagleBattle(uint npcOnlyId)
        {
            if (!IsActive) return false;
            var respBytes = await ServerGrain.FindNpc(npcOnlyId);
            if (respBytes.Value == null) return false;
            var mod = MapObjectData.Parser.ParseFrom(respBytes.Value);
            if (mod == null) return false;
            ConfigService.Npcs.TryGetValue(mod.CfgId, out var cfg);
            if (cfg == null) return false;
            var ret = await StartPve(npcOnlyId, cfg.MonsterGroup, BattleType.Eagle);
            return ret;
        }

        public async ValueTask<Immutable<byte[]>> PreCheckHcPk()
        {
            await Task.CompletedTask;
            if (!IsActive) return new(null);
            var resp = new PreCheckHcPkResponse();

            while (true)
            {
                if (Entity.Relive < 3)
                {
                    SendNotice("等级未到3转！");
                    break;
                }

                if (InBattle)
                {
                    resp.Error = "在战斗中";
                    break;
                }

                if (InTeam)
                {
                    var signed = await TeamGrain.CheckSldhSigned(RoleId);
                    if (signed)
                    {
                        resp.Error = "当前已报名水陆大会，无法接受皇城PK！";
                        break;
                    }
                    signed = await TeamGrain.CheckWzzzSigned(RoleId);
                    if (signed)
                    {
                        resp.Error = "当前已报名王者之战，无法接受皇城PK！";
                        break;
                    }
                }

                if (await IsSignedSinglePk())
                {
                    resp.Error = "当前已报名比武大会，无法接受皇城PK！";
                }
                if (await IsSignedDaLuanDou())
                {
                    resp.Error = "当前已参加大乱斗，无法接受皇城PK！";
                }

                break;
            }

            if (string.IsNullOrWhiteSpace(resp.Error))
            {
                resp.Info = BuildRoleInfo();
            }

            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        public async Task OnHcPkResult(bool win)
        {
            if (!IsActive) return;
            if (win) return;
            // 输掉了，扣当前等级段位上的0.2倍经验
            // var max = ConfigService.GetRoleUpgradeExp(Entity.Relive, Entity.Level);

            // 还是扣仙玉吧
            var subJade = Math.Min(2000, Entity.Jade);
            await CostMoney(MoneyType.Jade, subJade, tag: "皇城PK输扣");

            SendNotice($"皇城PK您输掉了，扣{subJade}仙玉！");
        }

        public Task<Immutable<byte[]>> QueryRoleInfo()
        {
            if (!IsActive) return new(null);
            var data = BuildRoleInfo();
            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(data)));
        }

        public async Task<Immutable<byte[]>> QueryRoleList()
        {
            if (!IsActive) return new(null);
            var list = new RoleInfoList();
            list.List.Add(BuildRoleInfo());

            if (IsTeamLeader)
            {
                // 这里只包含了队员的数据
                var bits = await TeamGrain.QueryRoleInfos();
                if (bits.Value != null && bits.Value.Length > 0)
                {
                    var res = RoleInfoList.Parser.ParseFrom(bits.Value);
                    if (res.List.Count > 0)
                    {
                        list.List.AddRange(res.List);
                    }
                }
            }

            return new Immutable<byte[]>(Packet.Serialize(list));
        }

        public async Task OnExitSectWar()
        {
            if (!IsActive) return;
            _sectWarId = 0;
            _sectWarCamp = 0;
            _sectWarPlace = SectWarPlace.JiDi;
            _sectWarState = SectWarRoleState.Idle;

            await SendPacket(GameCmd.S2CSectWarExit, new S2C_SectWarExit());

            // 退到长安城帮派接引人面前
            await SyncMapPos(1011, 230, 20, true);

            // 帮战中的队伍结束后自动解散
            if (IsTeamLeader)
            {
                _ = TeamGrain.Exit(RoleId);
            }

            // 退出战斗
            if (InBattle && _battleGrain != null)
            {
                var battleId = (uint)_battleGrain.GetPrimaryKeyLong();

                _ = _battleGrain.Exit(RoleId);
                _ = _mapGrain.PlayerExitBattle(OnlyId);
                _battleGrain = null;
                _battleId = 0;
                _campId = 0;

                await SendPacket(GameCmd.S2CBattleStop, new S2C_BattleStop { BattleId = battleId, Win = false });
            }
        }

        public Task OnSectWarState(byte state)
        {
            if (!IsActive) return Task.CompletedTask;
            if (_sectWarCamp != 0)
                _sectWarState = (SectWarRoleState)state;
            return Task.CompletedTask;
        }

        public async Task OnSectWarPlace(byte place)
        {
            if (!IsActive) return;
            if (_sectWarCamp == 0 || _sectWarPlace == (SectWarPlace)place) return;
            _sectWarPlace = (SectWarPlace)place;

            if (!InTeam || IsTeamLeader)
            {
                switch (_sectWarPlace)
                {
                    case SectWarPlace.JiDi:
                        if (_sectWarCamp == 1)
                            await SyncMapPos(5001, 13, 7, true);
                        else if (_sectWarCamp == 2)
                            await SyncMapPos(5001, 180, 100, true);
                        break;
                    case SectWarPlace.BiWuChang:
                        await SyncMapPos(5001, 168, 24, true);
                        break;
                    case SectWarPlace.ZhanChang:
                        if (_sectWarCamp == 1)
                            await SyncMapPos(5001, 80, 50, true);
                        else if (_sectWarCamp == 2)
                            await SyncMapPos(5001, 135, 60, true);
                        break;
                }
            }
        }

        public async Task OnSectWarResult(bool win)
        {
            if (!IsActive) return;
            if (_sectWarCamp == 0) return;
            _sectWarId = 0;
            _sectWarCamp = 0;
            _sectWarPlace = SectWarPlace.JiDi;
            _sectWarState = SectWarRoleState.Idle;

            var resp = new S2C_SectWarResult { Win = win };
            resp.Exp = win ? Entity.Level * 200000UL : Entity.Level * 100000UL;
            resp.PetExp = (ulong)Math.Floor(resp.Exp * 1.5f);
            resp.Money = win ? 30_000_000U : 15_000_000U;
            resp.Contrib = win ? 2_000_000U : 1_000_000U;
            if (win)
            {
                // 山河图1 + 净瓶玉露50
                resp.Items.Add(new ItemData { Id = 50005, Num = 1 });
                resp.Items.Add(new ItemData { Id = 100001, Num = 50 });
            }
            else
            {
                // 净瓶玉露20
                resp.Items.Add(new ItemData { Id = 100001, Num = 20 });
            }

            await SendPacket(GameCmd.S2CSectWarResult, resp);

            await AddExp(resp.Exp);
            if (PetMgr.Pet != null) await PetMgr.Pet.AddExp(resp.PetExp);
            await AddMoney(MoneyType.Silver, (int)resp.Money, "帮战奖励");
            await AddMoney(MoneyType.Contrib, (int)resp.Contrib, "帮战奖励");
            foreach (var item in resp.Items)
            {
                await AddBagItem(item.Id, (int)item.Num, tag: "帮战奖励");
            }
        }

        private async Task SyncMapPos(uint mapId, int x, int y, bool immediate)
        {
            if (!IsActive) return;
            if (Entity.MapId != mapId)
            {
                await ChangeMap(mapId, x, y);
            }
            else
            {
                await ReqMove(x, y, false, immediate);
            }

            // 将队伍的地图和位置信息下发给队员
            if (IsEnterServer)
            {
                await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
                {
                    Map = Entity.MapId,
                    X = Entity.MapX,
                    Y = Entity.MapY,
                    Immediate = immediate
                });
            }
        }

        public ValueTask<bool> CheckCanInSectWarTeam(uint teamSectId)
        {
            if (!IsActive) return new(false);
            var ret = !InTeam && !InBattle && Entity.SectId == teamSectId && _sectWarCamp > 0;
            return new ValueTask<bool>(ret);
        }

        public ValueTask<bool> InSectWar()
        {
            if (!IsActive) return new(false);
            return new(_sectWarCamp > 0);
        }

        public ValueTask<bool> IsTeamMember()
        {
            if (!IsActive) return new(false);
            return new(InTeam && !IsTeamLeader);
        }

        public async ValueTask<bool> IsSignedSinglePk()
        {
            if (!IsActive) return false;
            if (_singlePkVo.Sign && _singlePkGrain != null)
            {
                _singlePkVo.Sign = await _singlePkGrain.CheckRoleActive(RoleId);
            }
            else
            {
                _singlePkVo.Sign = false;
            }
            return _singlePkVo.Sign;
        }

        public async Task OnSinglePkResult(int rank, uint title)
        {
            if (!IsActive) return;
            if (rank == 0)
            {
                // 第一名：二郎神、称号、100高级藏宝图
                await AddBagItem(90082, 1, tag: "比武大会奖励");
                await AddBagItem(50004, 100, tag: "比武大会奖励");
            }
            else if (rank == 1)
            {
                // 第二名：珍稀神兽x1 称号x1 藏宝图x100
                await AddBagItem(90101, 1, tag: "比武大会奖励");
                await AddBagItem(50004, 100, tag: "比武大会奖励");
            }
            else if (rank == 2)
            {
                // 第三名：五常神兽x1 称号x1 藏宝图x100
                await AddBagItem(90100, 1, tag: "比武大会奖励");
                await AddBagItem(50004, 100, tag: "比武大会奖励");
            }
            else if (rank is >= 3 and < 10)
            {
                // 第四名~第十名, 三级五行材料随机一个  藏宝图x50
                await AddBagItem((uint)Random.Next(20011, 20016), 1, tag: "比武大会奖励");
                await AddBagItem(50004, 50, tag: "比武大会奖励");
            }
            else if (rank is >= 10 and < 50)
            {
                // 第十一名~第五十名, 30个藏宝图
                await AddBagItem(50004, 30, tag: "比武大会奖励");
            }
            else
            {
                // 10个藏宝图
                await AddBagItem(50004, 10, tag: "比武大会奖励");
            }

            // 处理称号
            if (title > 0) await GmAddTitle(title, true, true);

            // 处理角色经验和宠物经验
            // var exp = Entity.Level * 100000UL;
            // var petExp = (ulong) Math.Floor(exp * 1.5f);
            // await AddExp(exp);
            // if (PetMgr.Pet != null) await PetMgr.Pet.AddExp(petExp);
        }

        public async ValueTask<bool> IsSignedDaLuanDou()
        {
            await Task.CompletedTask;
            if (!IsActive) return false;
            return false;
            //if (_daLuanDouVo.Sign && _daLuanDouGrain != null)
            //{
            //    _daLuanDouVo.Sign = await _daLuanDouGrain.CheckRoleActive(RoleId);
            //}
            //else
            //{
            //    _daLuanDouVo.Sign = false;
            //}
            //return _daLuanDouVo.Sign;
        }

        public async Task OnDaLuanDouResult(int rank, uint title)
        {
            await Task.CompletedTask;
            if (!IsActive) return;
            // if (rank == 0)
            // {
            //     // 第一名：二郎神、称号、100高级藏宝图
            //     await AddBagItem(90082, 1, tag: "比武大会奖励");
            //     await AddBagItem(50004, 100, tag: "比武大会奖励");
            // }
            // else if (rank == 1)
            // {
            //     // 第二名：珍稀神兽x1 称号x1 藏宝图x100
            //     await AddBagItem(90101, 1, tag: "比武大会奖励");
            //     await AddBagItem(50004, 100, tag: "比武大会奖励");
            // }
            // else if (rank == 2)
            // {
            //     // 第三名：五常神兽x1 称号x1 藏宝图x100
            //     await AddBagItem(90100, 1, tag: "比武大会奖励");
            //     await AddBagItem(50004, 100, tag: "比武大会奖励");
            // }
            // else if (rank is >= 3 and < 10)
            // {
            //     // 第四名~第十名, 三级五行材料随机一个  藏宝图x50
            //     await AddBagItem((uint)Random.Next(20011, 20016), 1, tag: "比武大会奖励");
            //     await AddBagItem(50004, 50, tag: "比武大会奖励");
            // }
            // else if (rank is >= 10 and < 50)
            // {
            //     // 第十一名~第五十名, 30个藏宝图
            //     await AddBagItem(50004, 30, tag: "比武大会奖励");
            // }
            // else
            // {
            //     // 10个藏宝图
            //     await AddBagItem(50004, 10, tag: "比武大会奖励");
            // }

            // // 处理称号
            // if (title > 0) await GmAddTitle(title, true, true);

            // 处理角色经验和宠物经验
            // var exp = Entity.Level * 100000UL;
            // var petExp = (ulong) Math.Floor(exp * 1.5f);
            // await AddExp(exp);
            // if (PetMgr.Pet != null) await PetMgr.Pet.AddExp(petExp);
        }

        public async Task OnRecvMail(Immutable<byte[]> bytes)
        {
            if (!IsActive) return;
            if (bytes.Value == null) return;
            var resp = MailData.Parser.ParseFrom(bytes.Value);

            if (MailMgr != null)
                await MailMgr.RecvMail(resp);
        }

        public async Task OnDelMail(Immutable<byte[]> bytes)
        {
            if (!IsActive) return;
            if (bytes.Value == null) return;
            var resp = MailData.Parser.ParseFrom(bytes.Value);
            if (MailMgr != null)
                await MailMgr.DelMail(resp);
        }

        public async Task OnRecvMail(uint id)
        {
            if (!IsActive) return;
            if (MailMgr != null)
                await MailMgr.RecvMail(id);
        }

        public async Task OnDelMail(uint id)
        {
            if (!IsActive) return;
            if (MailMgr != null)
                await MailMgr.DelMail(id);
        }

        public async ValueTask<int> OnPayed(int money, int jade)
        {
            if (!IsActive)
            {
                // 这里比较特殊，要确保是激活状态的
                await StartUp();
            }
            // 多倍充值
            money *= GameDefine.BindJadePerYuan;
            jade *= GameDefine.MultiChargeFactor;
            if (money == 0) return 0;
            var now = DateTimeOffset.Now;
            var lastDailyPayTs = DateTimeOffset.FromUnixTimeSeconds(Entity.DailyPayTime).AddHours(8);
            var needResetDailyPay = now.Year != lastDailyPayTs.Year || now.DayOfYear != lastDailyPayTs.DayOfYear;

            // var newTotalPay = Entity.TotalPay;
            // var newEwaiPay = Entity.EwaiPay;
            // var newTotalPayBS = Entity.TotalPayBS;
            // var newDailyPay = Entity.DailyPay;
            // var newDailyPayTime = Entity.DailyPayTime;
            // var newDailyPayRewards = Entity.DailyPayRewards;

            if (money > 0)
            {
                var isCommited = false;
                using var uow = DbService.Sql.CreateUnitOfWork();

                // newTotalPay = Entity.TotalPay + (uint)money;
                // newEwaiPay = Entity.EwaiPay + (uint)money;
                // newTotalPayBS = Entity.TotalPayBS + (uint)money;
                // newDailyPay = Entity.DailyPay + (uint)money;
                // if (needResetDailyPay)
                // {
                //     newDailyPay = (uint)money;
                //     newDailyPayTime = TimeUtil.TimeStamp;
                //     newDailyPayRewards = string.Empty;
                //     _dailyPayRewards.Clear();
                // }

                // 更新TotalPay
                {
                    // var er = await uow.Orm.Update<RoleEntity>()
                    //     .Where(it => it.Id == RoleId)
                    //     .Set(it => it.TotalPay, newTotalPay)
                    //     .Set(it => it.EwaiPay, newEwaiPay)
                    //     .Set(it => it.TotalPayBS, newTotalPayBS)
                    //     .Set(it => it.DailyPay, newDailyPay)
                    //     .Set(it => it.DailyPayTime, newDailyPayTime)
                    //     .Set(it => it.DailyPayRewards, newDailyPayRewards)
                    //     .ExecuteAffrowsAsync();
                    // if (er == 0)
                    // {
                    //     uow.Rollback();
                    //     return 0;
                    // }

                    // 2021-03-20 00:00:00 之前创角，并且已经领取过首充的本次金额计入次充
                    // if (Entity.CreateTime <= 1616169600 && Entity.SecondPay == 0 &&
                    //     _flags.GetFlag(FlagType.FirstPayReward) && !_flags.GetFlag(FlagType.SecondPayReward))
                    // {
                    //     Entity.SecondPay = (uint) money;
                    //     LastEntity.SecondPay = Entity.SecondPay;
                    //     await uow.Orm.Update<RoleEntity>()
                    //         .Where(it => it.Id == RoleId)
                    //         .Set(it => it.SecondPay, Entity.SecondPay)
                    //         .ExecuteAffrowsAsync();
                    // }
                }
                // if (Entity.Spread > 0)
                // {
                //     // 30天过期
                //     var createTime = (uint)now.ToUnixTimeSeconds();
                //     var expireTime = (uint)now.AddDays(30).ToUnixTimeSeconds();

                //     var items = Json.SafeSerialize(new Dictionary<uint, uint>
                //     {
                //         // 仙玉
                //         [90004] = (uint)MathF.Ceiling(jade * GameDefine.RechargeRewardJadePercent)
                //     });

                //     // 给推广人发送奖励邮件   //TODO 寻找一下礼包码
                //     var entity = new MailEntity
                //     {
                //         ServerId = Entity.ServerId,
                //         Sender = 0,
                //         Recver = Entity.Spread,
                //         Admin = 0,
                //         Type = (byte)MailType.System,
                //         Text = $"恭喜您，玩家{Entity.NickName}绑定了您的推广码并充值了，系统给您赠送些许奖励以示感谢!",
                //         Items = items,
                //         Remark = "",
                //         CreateTime = createTime,
                //         PickedTime = 0,
                //         DeleteTime = 0,
                //         ExpireTime = expireTime
                //     };

                //     var repo = uow.GetRepository<MailEntity>();
                //     await repo.InsertAsync(entity);
                //     if (entity.Id == 0)
                //     {
                //         uow.Rollback();
                //         return 0;
                //     }

                //     // 先提交事务, 否则playerGrain.OnRecvMail中查询不到数据
                //     uow.Commit();
                //     isCommited = true;

                //     // 如果在线, 就推送
                //     var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                //     var active = await globalGrain.CheckPlayer(Entity.Spread);
                //     if (active)
                //     {
                //         var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(Entity.Spread);
                //         _ = playerGrain.OnRecvMail(entity.Id);
                //     }
                // }

                await AddMoney(MoneyType.Jade, jade, "充值");
                await AddMoney(MoneyType.BindJade, money, "充值");
                if (!isCommited) uow.Commit();
            }
            else
            {
                // if ((uint)Math.Abs(money) > newTotalPay)
                // {
                //     newTotalPay = 0;
                // }
                // else
                // {
                //     newTotalPay -= (uint)Math.Abs(money);
                // }
                // if ((uint)Math.Abs(money) > newTotalPayBS)
                // {
                //     newTotalPayBS = 0;
                // }
                // else
                // {
                //     newTotalPayBS -= (uint)Math.Abs(money);
                // }

                // if (needResetDailyPay)
                // {
                //     newDailyPay = 0;
                //     newDailyPayTime = TimeUtil.TimeStamp;
                //     newDailyPayRewards = string.Empty;
                //     _dailyPayRewards.Clear();
                // }
                // else
                // {
                //     if ((uint)Math.Abs(money) > newDailyPay)
                //     {
                //         newDailyPay = 0;
                //     }
                //     else
                //     {
                //         newDailyPay -= (uint)Math.Abs(money);
                //     }
                // }

                // // 更新TotalPay
                // var er = await DbService.Sql.Update<RoleEntity>()
                //     .Where(it => it.Id == RoleId)
                //     .Set(it => it.TotalPay, newTotalPay)
                //     .Set(it => it.EwaiPay, newEwaiPay)
                //     .Set(it => it.TotalPayBS, newTotalPayBS)
                //     .Set(it => it.DailyPay, newDailyPay)
                //     .Set(it => it.DailyPayTime, newDailyPayTime)
                //     .Set(it => it.DailyPayRewards, newDailyPayRewards)
                //     .ExecuteAffrowsAsync();
                // if (er == 0) return 0;

                await AddMoney(MoneyType.Jade, jade, "充值");
                await AddMoney(MoneyType.BindJade, money, "充值");
            }

            // Entity.TotalPay = newTotalPay;
            // Entity.EwaiPay = newEwaiPay;
            // Entity.TotalPayBS = newTotalPayBS;
            // Entity.DailyPay = newDailyPay;
            // Entity.DailyPayTime = newDailyPayTime;
            // Entity.DailyPayRewards = newDailyPayRewards;
            LastEntity.TotalPay = Entity.TotalPay;
            LastEntity.EwaiPay = Entity.EwaiPay;
            LastEntity.TotalPayBS = Entity.TotalPayBS;
            LastEntity.DailyPay = Entity.DailyPay;
            LastEntity.DailyPayTime = Entity.DailyPayTime;
            LastEntity.DailyPayRewards = Entity.DailyPayRewards;
            // 更新缓存信息和充值排行榜
            await RedisService.SetRolePay(Entity);
            // // 更新限时充值排行榜
            // await RedisService.AddLimitPayRoleScore(Entity.ServerId, Entity.Id, (uint)Math.Abs(money));
            // 刷新数据
            await ReqRolePays();
            // 计算VIP等级
            await CalcVipLevel();
            return jade;
        }


        //积分充值的业务逻辑
        public async ValueTask<int> OnPayedBindJade(int money, int jade, int bindJade, bool multi)
        {
            if (!IsActive)
            {
                // 这里比较特殊，要确保是激活状态的
                await StartUp();
            }

            //仙玉和积分加倍
            if (multi)
            {
                jade = jade * GameDefine.CustomerJadeMulti;
                bindJade = bindJade * GameDefine.CustomerBindJadeMulti;
            }

            // 多倍充值
            if (multi) {
                money *= GameDefine.BindJadeMulti;
            }
            // bindJade *= GameDefine.MultiChargeFactor;
            if (money == 0) return 0;
            var now = DateTimeOffset.Now;
            var lastDailyPayTs = DateTimeOffset.FromUnixTimeSeconds(Entity.DailyPayTime).AddHours(8);
            var needResetDailyPay = now.Year != lastDailyPayTs.Year || now.DayOfYear != lastDailyPayTs.DayOfYear;

            var newTotalPay = Entity.TotalPay;
            var newEwaiPay = Entity.EwaiPay;
            var newTotalPayBS = Entity.TotalPayBS;
            var newDailyPay = Entity.DailyPay;
            var newDailyPayTime = Entity.DailyPayTime;
            var newDailyPayRewards = Entity.DailyPayRewards;

            if (money > 0)
            {
                var isCommited = false;
                using var uow = DbService.Sql.CreateUnitOfWork();

                newTotalPay = Entity.TotalPay + (uint)money;
                newEwaiPay = Entity.EwaiPay + (uint)money;
                newTotalPayBS = Entity.TotalPayBS + (uint)money;
                newDailyPay = Entity.DailyPay + (uint)money;
                if (needResetDailyPay)
                {
                    newDailyPay = (uint)money;
                    newDailyPayTime = TimeUtil.TimeStamp;
                    newDailyPayRewards = string.Empty;
                    _dailyPayRewards.Clear();
                }

                // 更新TotalPay
                {
                    var er = await uow.Orm.Update<RoleEntity>()
                        .Where(it => it.Id == RoleId)
                        .Set(it => it.TotalPay, newTotalPay)
                        .Set(it => it.EwaiPay, newEwaiPay)
                        .Set(it => it.TotalPayBS, newTotalPayBS)
                        .Set(it => it.DailyPay, newDailyPay)
                        .Set(it => it.DailyPayTime, newDailyPayTime)
                        .Set(it => it.DailyPayRewards, newDailyPayRewards)
                        .ExecuteAffrowsAsync();
                    if (er == 0)
                    {
                        uow.Rollback();
                        return 0;
                    }

                    // 2021-03-20 00:00:00 之前创角，并且已经领取过首充的本次金额计入次充
                    // if (Entity.CreateTime <= 1616169600 && Entity.SecondPay == 0 &&
                    //     _flags.GetFlag(FlagType.FirstPayReward) && !_flags.GetFlag(FlagType.SecondPayReward))
                    // {
                    //     Entity.SecondPay = (uint) money;
                    //     LastEntity.SecondPay = Entity.SecondPay;
                    //     await uow.Orm.Update<RoleEntity>()
                    //         .Where(it => it.Id == RoleId)
                    //         .Set(it => it.SecondPay, Entity.SecondPay)
                    //         .ExecuteAffrowsAsync();
                    // }
                }
#if false
                if (Entity.Spread > 0)
                {
                    // 30天过期
                    var createTime = (uint)now.ToUnixTimeSeconds();
                    var expireTime = (uint)now.AddDays(30).ToUnixTimeSeconds();

                    var items = Json.SafeSerialize(new Dictionary<uint, uint>
                    {
                        // 充值积分
                        [91001] = (uint)MathF.Ceiling(bindJade * GameDefine.RechargeRewardBindJadePercent)
                    });

                    // 给推广人发送奖励邮件
                    var entity = new MailEntity
                    {
                        ServerId = Entity.ServerId,
                        Sender = 0,
                        Recver = Entity.Spread,
                        Admin = 0,
                        Type = (byte)MailType.System,
                        Text = $"恭喜您，玩家{Entity.NickName}绑定了您的推广码并充值了，系统给您赠送些许奖励以示感谢!",
                        Items = items,
                        Remark = "",
                        CreateTime = createTime,
                        PickedTime = 0,
                        DeleteTime = 0,
                        ExpireTime = expireTime
                    };

                    var repo = uow.GetRepository<MailEntity>();
                    await repo.InsertAsync(entity);
                    if (entity.Id == 0)
                    {
                        uow.Rollback();
                        return 0;
                    }

                    // 先提交事务, 否则playerGrain.OnRecvMail中查询不到数据
                    uow.Commit();
                    isCommited = true;

                    // 如果在线, 就推送
                    var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                    var active = await globalGrain.CheckPlayer(Entity.Spread);
                    if (active)
                    {
                        var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(Entity.Spread);
                        _ = playerGrain.OnRecvMail(entity.Id);
                    }
                }
#endif

                await AddMoney(MoneyType.Jade, jade, "充值");
                await AddMoney(MoneyType.BindJade, bindJade, "充值");
                if (!isCommited) uow.Commit();
            }
            else
            {
                if ((uint)Math.Abs(money) > newTotalPay)
                {
                    newTotalPay = 0;
                }
                else
                {
                    newTotalPay -= (uint)Math.Abs(money);
                }
                if ((uint)Math.Abs(money) > newTotalPayBS)
                {
                    newTotalPayBS = 0;
                }
                else
                {
                    newTotalPayBS -= (uint)Math.Abs(money);
                }

                if (needResetDailyPay)
                {
                    newDailyPay = 0;
                    newDailyPayTime = TimeUtil.TimeStamp;
                    newDailyPayRewards = string.Empty;
                    _dailyPayRewards.Clear();
                }
                else
                {
                    if ((uint)Math.Abs(money) > newDailyPay)
                    {
                        newDailyPay = 0;
                    }
                    else
                    {
                        newDailyPay -= (uint)Math.Abs(money);
                    }
                }

                // 更新TotalPay
                var er = await DbService.Sql.Update<RoleEntity>()
                    .Where(it => it.Id == RoleId)
                    .Set(it => it.TotalPay, newTotalPay)
                    .Set(it => it.EwaiPay, newEwaiPay)
                    .Set(it => it.TotalPayBS, newTotalPayBS)
                    .Set(it => it.DailyPay, newDailyPay)
                    .Set(it => it.DailyPayTime, newDailyPayTime)
                    .Set(it => it.DailyPayRewards, newDailyPayRewards)
                    .ExecuteAffrowsAsync();
                if (er == 0) return 0;

                await AddMoney(MoneyType.Jade, jade, "充值");
                await AddMoney(MoneyType.BindJade, bindJade, "充值");
            }

            Entity.TotalPay = newTotalPay;
            Entity.EwaiPay = newEwaiPay;
            Entity.TotalPayBS = newTotalPayBS;
            Entity.DailyPay = newDailyPay;
            Entity.DailyPayTime = newDailyPayTime;
            Entity.DailyPayRewards = newDailyPayRewards;
            LastEntity.TotalPay = Entity.TotalPay;
            LastEntity.EwaiPay = Entity.EwaiPay;
            LastEntity.TotalPayBS = Entity.TotalPayBS;
            LastEntity.DailyPay = Entity.DailyPay;
            LastEntity.DailyPayTime = Entity.DailyPayTime;
            LastEntity.DailyPayRewards = Entity.DailyPayRewards;
            // 更新缓存信息和充值排行榜
            await RedisService.SetRolePay(Entity);
            // 更新限时充值排行榜
            await RedisService.AddLimitPayRoleScore(Entity.ServerId, Entity.Id, (uint)Math.Abs(money));
            // 刷新数据
            await ReqRolePays();
            // 计算VIP等级
            await CalcVipLevel();
            return bindJade;
        }


        //直购道具的业务逻辑
        public async ValueTask<int> OnPayedItem(uint item, uint num, int money)
        {
            // 多倍充值
            // money *= GameDefine.MultiChargeFactor;
            // if (money <= 0) return 0;
            if (!IsActive)
            {
                // 这里比较特殊，要确保是激活状态的
                await StartUp();
            }
            if (GameDefine.BindJadeMulti > 0) {
                // num = num * GameDefine.BindJadeMulti;
                money = money * GameDefine.BindJadeMulti;
            }
            
            var now = DateTimeOffset.Now;
            var newTotalPayBS = Entity.TotalPayBS;
            var lastDailyPayTs = DateTimeOffset.FromUnixTimeSeconds(Entity.DailyPayTime).AddHours(8);
            var needResetDailyPay = now.Year != lastDailyPayTs.Year || now.DayOfYear != lastDailyPayTs.DayOfYear;

            var newTotalPay = Entity.TotalPay;
            var newEwaiPay = Entity.EwaiPay;
            var newDailyPay = Entity.DailyPay;
            var newDailyPayTime = Entity.DailyPayTime;
            var newDailyPayRewards = Entity.DailyPayRewards;

            newTotalPayBS = Entity.TotalPayBS + (uint)money;
            newTotalPay = Entity.TotalPay + (uint)money;
            newEwaiPay = Entity.EwaiPay + (uint)money;
            newDailyPay = Entity.DailyPay + (uint)money;
            if (needResetDailyPay)
            {
                newDailyPay = (uint)money;
                newDailyPayTime = TimeUtil.TimeStamp;
                newDailyPayRewards = string.Empty;
                _dailyPayRewards.Clear();
            }


            // 更新TotalPay
            var er = await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.TotalPayBS, newTotalPayBS)
                .Set(it => it.TotalPay, newTotalPay)
                .Set(it => it.EwaiPay, newEwaiPay)
                .Set(it => it.DailyPay, newDailyPay)
                .Set(it => it.DailyPayTime, newDailyPayTime)
                .Set(it => it.DailyPayRewards, newDailyPayRewards)
                .ExecuteAffrowsAsync();
            if (er == 0) return 0;
            await AddBagItem(item, (int)num, tag: "直接购买");
            Entity.TotalPayBS = newTotalPayBS;
            Entity.TotalPay = newTotalPay;
            Entity.EwaiPay = newEwaiPay;
            Entity.DailyPay = newDailyPay;
            Entity.DailyPayTime = newDailyPayTime;
            Entity.DailyPayRewards = newDailyPayRewards;

            LastEntity.TotalPayBS = Entity.TotalPayBS;
            LastEntity.TotalPay = Entity.TotalPay;
            LastEntity.EwaiPay = Entity.EwaiPay;
            LastEntity.DailyPay = Entity.DailyPay;
            LastEntity.DailyPayTime = Entity.DailyPayTime;
            LastEntity.DailyPayRewards = Entity.DailyPayRewards;
            // 更新缓存信息和充值排行榜
            await RedisService.SetRolePay(Entity);
            // 更新限时充值排行榜
            await RedisService.AddLimitPayRoleScore(Entity.ServerId, Entity.Id, (uint)Math.Abs(money));
            // 刷新数据
            await ReqRolePays();
            // 计算VIP等级
            await CalcVipLevel();
            SendNotice("购买成功");
            return money;
        }

        //直购礼包的业务逻辑
        public async ValueTask<int> OnPayedGift(uint id, int money)
        {
            if (!IsActive)
            {
                // 这里比较特殊，要确保是激活状态的
                await StartUp();
            }
            // if (GameDefine.BindJadeMulti > 0) {
            //     money = money * GameDefine.BindJadeMulti;
            // }
            var giftGood = ConfigService.GiftShopGoods.GetValueOrDefault(id, null);
            if (giftGood != null) {
                LogInformation($"直购礼包Id:{id}获取不到配置");
                return 0;
            }
            
            var now = DateTimeOffset.Now;
            var newTotalPayBS = Entity.TotalPayBS;
            var lastDailyPayTs = DateTimeOffset.FromUnixTimeSeconds(Entity.DailyPayTime).AddHours(8);
            var needResetDailyPay = now.Year != lastDailyPayTs.Year || now.DayOfYear != lastDailyPayTs.DayOfYear;

            var newTotalPay = Entity.TotalPay;
            var newEwaiPay = Entity.EwaiPay;
            var newDailyPay = Entity.DailyPay;
            var newDailyPayTime = Entity.DailyPayTime;
            var newDailyPayRewards = Entity.DailyPayRewards;

            newTotalPayBS = Entity.TotalPayBS + (uint)money;
            newTotalPay = Entity.TotalPay + (uint)money;
            newEwaiPay = Entity.EwaiPay + (uint)money;
            newDailyPay = Entity.DailyPay + (uint)money;
            if (needResetDailyPay)
            {
                newDailyPay = (uint)money;
                newDailyPayTime = TimeUtil.TimeStamp;
                newDailyPayRewards = string.Empty;
                _dailyPayRewards.Clear();
            }


            // 更新TotalPay
            var er = await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.TotalPayBS, newTotalPayBS)
                .Set(it => it.TotalPay, newTotalPay)
                .Set(it => it.EwaiPay, newEwaiPay)
                .Set(it => it.DailyPay, newDailyPay)
                .Set(it => it.DailyPayTime, newDailyPayTime)
                .Set(it => it.DailyPayRewards, newDailyPayRewards)
                .ExecuteAffrowsAsync();
            if (er == 0) return 0;

            foreach (var item in giftGood.items)
            {
                await AddBagItem(item.id, (int)item.num, tag: "直购礼包购买");
            }            

            Entity.TotalPayBS = newTotalPayBS;
            Entity.TotalPay = newTotalPay;
            Entity.EwaiPay = newEwaiPay;
            Entity.DailyPay = newDailyPay;
            Entity.DailyPayTime = newDailyPayTime;
            Entity.DailyPayRewards = newDailyPayRewards;

            LastEntity.TotalPayBS = Entity.TotalPayBS;
            LastEntity.TotalPay = Entity.TotalPay;
            LastEntity.EwaiPay = Entity.EwaiPay;
            LastEntity.DailyPay = Entity.DailyPay;
            LastEntity.DailyPayTime = Entity.DailyPayTime;
            LastEntity.DailyPayRewards = Entity.DailyPayRewards;
            // 更新缓存信息和充值排行榜
            await RedisService.SetRolePay(Entity);
            // 更新限时充值排行榜
            await RedisService.AddLimitPayRoleScore(Entity.ServerId, Entity.Id, (uint)Math.Abs(money));
            // 刷新数据
            await ReqRolePays();
            // 计算VIP等级
            await CalcVipLevel();
            SendNotice("购买成功");
            return money;
        }

        public async ValueTask<string> GmSendRoleGift()
        {
            if (!IsActive) return "赠送失败";
            var sent = GetFlag(FlagType.GmSentGift);
            if (sent)
            {
                return "角色已经获得了礼物";
            }
            // 送1000充值
            // 发货
            var payRate = await RedisService.GetPayRateJade();
            var ret = await OnPayed(1000, 1000 * (int)payRate);
            if (ret != 0)
            {
                SetFlag(FlagType.GmSentGift, true);
                // 防止自动入库失败，这里先把flag强行入库
                await DbService.Sql.Update<RoleEntity>()
                    .Where(it => it.Id == RoleId)
                    .Set(it => it.Flags, Entity.Flags)
                    .ExecuteAffrowsAsync();
                return "";
            }
            else
            {
                return "赠送失败";
            }
        }

        // 检查多人日常任务是否有队员已完成
        public async ValueTask<string> CheckTeamDailyTaskCompleted(uint group)
        {
            if (!IsActive) return "";
            if (InTeam && IsTeamLeader)
            {
                return await TeamGrain.CheckDailyTaskCompleted(group);
            }
            return "";
        }
        public async ValueTask<bool> IsDailyTaskCompleted(uint group)
        {
            await Task.CompletedTask;
            if (!IsActive) return true;
            return TaskMgr.IsDailyTaskCompleted(group);
        }
        // 检查多人副本任务是否有队员已完成
        public async ValueTask<string> CheckTeamInstanceTaskCompleted(uint taskId)
        {
            if (!IsActive) return "";
            if (InTeam && IsTeamLeader)
            {
                return await TeamGrain.CheckInstanceTaskCompleted(taskId);
            }
            return "";
        }
        public async ValueTask<bool> IsInstanceTaskCompleted(uint taskId)
        {
            await Task.CompletedTask;
            if (!IsActive) return true;
            return TaskMgr.IsInstanceTaskCompleted(taskId);
        }

        // 重置单人PK排行榜
        public async ValueTask<bool> ResetSinglePkInfo()
        {
            if (!IsActive) return false;
            _lastRankSinglePkResp = null;
            _lastFetchSinglePkRankTime = 0;
            _singlePkVo = new RoleSinglePkVo();
            Entity.SinglePk = Json.SafeSerialize(_singlePkVo);
            Entity.SinglePkScore = _singlePkVo.Score;
            Entity.SinglePkWin = _singlePkVo.Win;
            Entity.SinglePkLost = _singlePkVo.Lost;
            await RedisService.SetRoleSinglePk(Entity);
            return true;
        }

        // 重置大乱斗PK排行榜
        public async ValueTask<bool> ResetDaLuanDouInfo()
        {
            if (!IsActive) return false;
            _lastRankDaLuanDouResp = null;
            _lastFetchDaLuanDouRankTime = 0;
            _daLuanDouVo = new RoleDaLuanDouVo();
            Entity.DaLuanDou = Json.SafeSerialize(_daLuanDouVo);
            Entity.DaLuanDouScore = _daLuanDouVo.Score;
            Entity.DaLuanDouWin = _daLuanDouVo.Win;
            Entity.DaLuanDouLost = _daLuanDouVo.Lost;
            await RedisService.SetRoleDaLuanDou(Entity);
            return true;
        }

        // 队长拉队员进入观战
        public async Task EnterBattleWatch(uint battleId, uint campId)
        {
            if (InBattle)
            {
                SendNotice("已经参战/观战，不能再观战");
            }
            else
            {
                var bgrain = GrainFactory.GetGrain<IBattleGrain>(battleId);
                if (bgrain != null && await bgrain.EnterWatchBattle(campId, RoleId))
                {
                    // 重置上次弹幕时间戳
                    _lastDanMuTimestamp = 0;
                    _battleGrainWatched = bgrain;
                    _battleIdWatched = battleId;
                    _campIdWatched = campId;
                    // 停止AOI同步
                    if (_mapGrain != null)
                    {
                        _ = _mapGrain.PlayerEnterBattle(OnlyId, _battleIdWatched, _campIdWatched);
                    }
                    // 是队长，则把所有队员拉入观战
                    if (IsTeamLeader)
                    {
                        var tbm = QueryTeamBattleMembersResponse.Parser.ParseFrom((await TeamGrain.QueryTeamBattleMemebers()).Value);
                        foreach (var rid in tbm.Players)
                        {
                            var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                            if (grain != null)
                            {
                                _ = grain.EnterBattleWatch(battleId, campId);
                            }
                        }
                    }
                }
                else
                {
                    SendNotice("战斗不存在或已结束");
                }
            }
        }

        // 队长拉队员退出观战
        public async Task ExitBattleWatch()
        {
            if (_battleGrainWatched == null) return;
            await _battleGrainWatched.ExitWatchBattle(RoleId);
            await SendPacket(GameCmd.S2CBattleStop, new S2C_BattleStop { BattleId = _battleIdWatched, Win = false });
            _battleGrainWatched = null;
            _battleIdWatched = 0;
            _campIdWatched = 0;
            // 开启AOI同步
            if (_mapGrain != null)
            {
                _ = _mapGrain.PlayerExitBattle(OnlyId);
            }
            // 是队长，则把所有队员拉出观战
            if (IsTeamLeader)
            {
                var tbm = QueryTeamBattleMembersResponse.Parser.ParseFrom((await TeamGrain.QueryTeamBattleMemebers()).Value);
                foreach (var rid in tbm.Players)
                {
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                    if (grain != null)
                    {
                        _ = grain.ExitBattleWatch();
                    }
                }
            }
        }

        // 观战--进入
        private Task ReqEnterWatchBattle(C2S_EnterWatchBattle req)
        {
            return EnterBattleWatch(req.BattleId, req.CampId);
        }

        // 观战--退出
        protected Task ReqExitWatchBattle()
        {
            return ExitBattleWatch();
        }

        // 判断玩家是否在战场上
        public ValueTask<bool> IsInBattle()
        {
            return ValueTask.FromResult(InBattle);
        }

        // 获取角色信息
        public async Task<Immutable<byte[]>> GetRoleInfo()
        {
            var role = BuildRoleInfo();
            // 没有构建成功？
            if (role.Id <= 0 || string.IsNullOrEmpty(role.Name))
            {
                role = null;
                // 从Redis取
                var rer = await RedisService.GetRoleInfo(RoleId);
                if (rer != null && !string.IsNullOrEmpty(rer.NickName))
                {
                    role = new RoleInfo()
                    {
                        Id = rer.Id,
                        CfgId = rer.CfgId,
                        Name = rer.NickName,
                        Level = rer.Level,
                        Relive = rer.Relive
                    };
                }
                // 从数据库取
                else
                {
                    var er = await DbService.QueryRole(RoleId);
                    if (er != null && !string.IsNullOrEmpty(er.NickName))
                    {
                        role = new RoleInfo
                        {
                            Id = er.Id,
                            Name = er.NickName,
                            Relive = er.Relive,
                            Level = er.Level,
                            CfgId = er.CfgId,
                            Type = (uint)er.Type,
                        };
                    }
                }
            }
            if (role != null)
            {
                return new Immutable<byte[]>(Packet.Serialize(role));
            }
            else
            {
                return new Immutable<byte[]>(null);
            }
        }
    
        public async Task<uint> GetMapId()
        {
            await Task.CompletedTask;
            return Entity.MapId;
        }
    }
}