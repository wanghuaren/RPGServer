using System;
using System.ComponentModel.DataAnnotations;

namespace Ddxy.Common.Model.Admin
{
    [Serializable]
    public class SignInReq
    {
        [Required]
        [StringLength(32, MinimumLength = 5)]
        public string UserName { get; set; }

        [Required]
        [StringLength(32, MinimumLength = 6)]
        public string Password { get; set; }
    }

    [Serializable]
    public class SaveProfileReq
    {
        [StringLength(8, MinimumLength = 2)] public string NickName { get; set; }

        [StringLength(32, MinimumLength = 6)] public string Password { get; set; }
    }

    [Serializable]
    public class AddAdminReq
    {
        [Required]
        [StringLength(32, MinimumLength = 5)]
        public string UserName { get; set; }

        [Required]
        [StringLength(32, MinimumLength = 6)]
        public string Password { get; set; }

        [Required]
        [StringLength(8, MinimumLength = 2)]
        public string NickName { get; set; }
    }

    [Serializable]
    public class DelAdminReq
    {
        [Required] public uint? Id { get; set; }
    }

    [Serializable]
    public class EditAdminReq
    {
        [Required] public uint Id { get; set; }

        [StringLength(32, MinimumLength = 6)] public string Password { get; set; }

        [StringLength(8, MinimumLength = 2)] public string NickName { get; set; }
    }

    [Serializable]
    public class FrozeAdminReq
    {
        [Required] public uint Id { get; set; }

        [Required] public byte Status { get; set; }
    }

    [Serializable]
    public class ListAgencyReq : ListPageReq
    {
        public uint? Agency { get; set; }

        public int Order { get; set; }

        [StringLength(16)] public string SearchParent { get; set; }
    }

    [Serializable]
    public class AddServerReq
    {
        [Required]
        [StringLength(8, MinimumLength = 2)]
        public string Name { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 6)]
        public string Addr { get; set; }
    }

    [Serializable]
    public class EditServerReq
    {
        [Required] public uint Id { get; set; }
        [StringLength(8, MinimumLength = 2)] public string Name { get; set; }
        [StringLength(200, MinimumLength = 6)] public string Addr { get; set; }
        public bool? Recom { get; set; }
    }

    [Serializable]
    public class ChangeServerStatusReq
    {
        [Required] public uint Id { get; set; }

        [Required] public byte Status { get; set; }
    }

    [Serializable]
    public class StartServerReq
    {
        [Required] public uint Id { get; set; }
    }

    [Serializable]
    public class OpenActivityReq
    {
        [Required] public uint? Sid { get; set; }

        [Required] public uint? Aid { get; set; }

        public bool Open { get; set; }
    }

    [Serializable]
    public class CombineServerReq
    {
        [Required] public uint? Target { get; set; }
        [Required] public uint[] From { get; set; }
    }

    [Serializable]
    public class SetNoticeReq
    {
        [StringLength(1000)] public string Text { get; set; }
    }

    [Serializable]
    public class SetPayEnableReq
    {
        public bool Enable { get; set; }
    }

    [Serializable]
    public class SetPayRateReq
    {
        [Required] public uint? Rate { get; set; }
        [Required] public uint? BindRate { get; set; }
    }

    [Serializable]
    public class AddMailReq
    {
        public uint Sid { get; set; }

        public uint Recv { get; set; }

        [Required] [StringLength(500)] public string Text { get; set; }

        [StringLength(500)] public string Remark { get; set; }

        public ItemPair[] Items { get; set; }

        [Required] public byte? MinRelive { get; set; }
        [Required] public uint? MinLevel { get; set; }
        [Required] public byte? MaxRelive { get; set; }
        [Required] public uint? MaxLevel { get; set; }
        public uint Expire { get; set; }
    }

    [Serializable]
    public class DelMailReq
    {
        [Required] public uint? Id { get; set; }
    }

    [Serializable]
    public class SetResVersionReq
    {
        [MaxLength(32)] public string Version { get; set; }

        public bool Force { get; set; }
    }

    [Serializable]
    public class ListMailReq : ListPageReq
    {
        public uint? Server { get; set; }

        public byte? Type { get; set; }

        public bool? Picked { get; set; }

        public bool? Delete { get; set; }
    }

    public class ItemPair
    {
        public uint Id { get; set; }

        public int Num { get; set; }
    }

    [Serializable]
    public class ListUserReq : ListPageReq
    {
        public byte Type { get; set; }

        public byte? Status { get; set; }

        public string SearchParent { get; set; }
    }

    [Serializable]
    public class EditUserReq
    {
        [Required] public uint? Id { get; set; }

        [Required]
        [StringLength(18, MinimumLength = 6)]
        public string Password { get; set; }
    }

    [Serializable]
    public class FrozeUserReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public byte? Status { get; set; }
    }

    [Serializable]
    public class ListRoleReq : ListPageReq
    {
        public uint? Server { get; set; }
        public byte Type { get; set; }
        public byte? Status { get; set; }
        public byte? Sex { get; set; }
        public byte? Race { get; set; }
        public bool? Online { get; set; }
        public byte SearchTextType { get; set; }
    }

    [Serializable]
    public class FrozeRoleReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public byte? Status { get; set; }
    }

    [Serializable]
    public class ChangeRoleOnlineReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public bool? Online { get; set; }
    }

    [Serializable]
    public class GetRoleDetailReq
    {
        [Required] public uint? Id { get; set; }
    }

    [Serializable]
    public class GetRoleEquipsReq
    {
        [Required] public uint? Id { get; set; }
    }

    [Serializable]
    public class SetMountSkillReq
    {
        [Required] public uint? Rid { get; set; }
        [Required] public uint? Mid { get; set; }
        [Required] [Range(1, 3)] public int? SkIdx { get; set; }
        [Required] public uint? SkCfgId { get; set; }
        [Required] [Range(0, 1)] public byte? SkLevel { get; set; }
        [Required] [Range(0, 20000)] public uint? SkExp { get; set; }
    }

    [Serializable]
    public class SetEquipRefineReq
    {
        [Required] public uint? Rid { get; set; }
        [Required] public uint? Id { get; set; }
        [Required] public KeyValuePair[] Pairs { get; set; }
    }

    public class KeyValuePair
    {
        public int Key { get; set; }

        public int Value { get; set; }
    }

    [Serializable]
    public class ChangeRoleLevelReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public byte? Level { get; set; }
    }

    [Serializable]
    public class ChangeRoleMoneyReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public int? Silver { get; set; }
        [Required] public int? Jade { get; set; }
        [Required] public int? BindJade { get; set; }
        [Required] public int? Contrib { get; set; }
        [Required] public int? SldhGongJi { get; set; }
    }

    [Serializable]
    public class ChangeRoleItemReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public uint? ItemId { get; set; }

        [Required] public int? Value { get; set; }
    }

    [Serializable]
    public class ChangeRoleStarReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public int? Value { get; set; }
    }

    [Serializable]
    public class ChangeRoleTotalPayReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public int? Value { get; set; }
    }

    [Serializable]

    public class AddRoleEquipReq
    {
        [Required] public uint? Id { get; set; }

        public uint CfgId { get; set; }

        public byte Category { get; set; }

        public byte Index { get; set; }

        public byte Grade { get; set; }
    }

    [Serializable]
    public class AddRoleOrnamentReq
    {
        [Required] public uint? Id { get; set; }

        public uint CfgId { get; set; }

        public uint Suit { get; set; }

        public byte Index { get; set; }

        public byte Grade { get; set; }
    }

    [Serializable]
    public class AddRoleWingReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public uint? CfgId { get; set; }
    }

    [Serializable]
    public class AddRoleTitleReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public uint? CfgId { get; set; }
    }

    [Serializable]
    public class DelRoleTitleReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public uint? CfgId { get; set; }
    }

    [Serializable]
    public class SetRoleTypeReq
    {
        [Required] public uint? Id { get; set; }

        public byte Type { get; set; }
    }

    [Serializable]
    public class SetRoleFlagReq
    {
        [Required] public uint? Id { get; set; }

        public byte Type { get; set; }

        public bool Value { get; set; }
    }

    [Serializable]
    public class DelRoleShaneReq
    {
        [Required] public uint? Id { get; set; }
    }

    [Serializable]
    public class RechargeReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public int? Value { get; set; }

        public string Remark { get; set; }
    }

    [Serializable]
    public class RechargeRoleReq
    {
        [Required] public uint? Id { get; set; }

        [Required] public int? Value { get; set; }

        public string Remark { get; set; }
    }

    [Serializable]
    public class ListRechargeReq : ListPageReq
    {
        public bool? Remark { get; set; }

        public int Order { get; set; }
    }

    [Serializable]
    public class DelRecordsReq
    {
        /// <summary>
        /// 起始时间
        /// </summary>
        [Required]
        public uint? StartTime { get; set; }

        /// <summary>
        /// 终止时间
        /// </summary>
        [Required]
        public uint? EndTime { get; set; }
    }

    [Serializable]
    public class RefreshOrderReq
    {
        [Required] public uint? CpOrderId { get; set; }
    }

    [Serializable]
    public class ListRechargeRoleReq : ListPageReq
    {
        public bool? Remark { get; set; }

        public int Order { get; set; }

        public string SearchParent { get; set; }

        public string SearchOp { get; set; }
    }

    [Serializable]
    public class ListPayReq : ListPageReq
    {
        public byte? Status { get; set; }

        public int Order { get; set; }

        public int Server { get; set; }

        public int SearchTextType { get; set; }

        // public string SearchParent { get; set; }
    }

    [Serializable]
    public class ListPayRecordsReq : ListPageReq
    {
        public int Order { get; set; }
    }

    [Serializable]
    public class ListRankReq : ListPageReq
    {
        [Required] public uint? Server { get; set; }
    }

    [Serializable]
    public class DissmissSectReq
    {
        [Required] public uint? Id { get; set; }
    }

    [Serializable]
    public class ReloadSectsReq
    {
        [Required] public uint? Sid { get; set; }
    }

    [Serializable]
    public class ListOpLogReq : ListPageReq
    {
        public uint? Uid { get; set; }
    }

    [Serializable]
    public class ListBugLogReq : ListPageReq
    {
        public uint? Uid { get; set; }

        public uint? Rid { get; set; }

        public uint? Status { get; set; }
    }

    [Serializable]
    public class SendPalaceNoticeReq
    {
        [Required] public uint Id { get; set; }
        [Required] public string Msg { get; set; }
        [Required] public uint Times { get; set; }
    }

    [Serializable]
    public class SendRoleGiftReq
    {
        [Required] public uint Id { get; set; }
    }

    [Serializable]
    public class SendSetLimitChargeRankReq
    {
        [Required] public uint Server { get; set; }
        [Required] public uint Start { get; set; }
        [Required] public uint End { get; set; }
        [Required] public bool Cleanup { get; set; }
    }

    [Serializable]
    public class SendGetLimitChargeRankReq
    {
        [Required] public uint Server { get; set; }
    }

    [Serializable]
    public class ListLimitRankReq : ListPageReq
    {
        [Required] public uint? Server { get; set; }
    }

    [Serializable]
    public class ListChatLogReq : ListPageReq
    {
        public uint? FromRid { get; set; }

        public uint? ToRid { get; set; }

        public uint? MsgType { get; set; }
    }
}