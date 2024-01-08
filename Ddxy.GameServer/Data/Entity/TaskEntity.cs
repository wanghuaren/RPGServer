using System;
using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "task")]
    public class TaskEntity : IEquatable<TaskEntity>
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 所属角色id
        /// </summary>
        [Column(Name = "rid")]
        [JsonIgnore]
        public uint RoleId { get; set; }

        /// <summary>
        /// 已完成的剧情任务id集合
        /// </summary>
        public string Complets { get; set; }

        /// <summary>
        /// 已接受的任务id及当前的step
        /// </summary>
        public string States { get; set; }

        /// <summary>
        /// 已经接受的日常任务group集合
        /// </summary>
        public string DailyStart { get; set; }

        /// <summary>
        /// 日常任务计数, key是task的group, value是完成的数量
        /// </summary>
        public string DailyCnt { get; set; }

        /// <summary>
        /// 副本任务计数, key是task的id, value是完成的次数
        /// </summary>
        public string InstanceCnt { get; set; }

        /// <summary>
        /// 活动任务积分
        /// </summary>
        public string ActiveScore { get; set; }

        /// <summary>
        /// 奖励领取情况
        /// </summary>
        public string BeenTake { get; set; }

        /// <summary>
        /// 今日杀星次数
        /// </summary>
        public uint StarNum { get; set; }

        /// <summary>
        /// 今日灵猴次数
        /// </summary>
        public uint MonkeyNum { get; set; }

        /// <summary>
        /// 今日金蟾送宝次数
        /// </summary>
        [Column(Name = "jinChanSongNum")]
        public uint JinChanSongBaoNum { get; set; }

        /// <summary>
        /// 今日金翅大鹏次数
        /// </summary>
        [Column(Name = "eagleNum")]
        public uint EagleNum { get; set; }

        /// <summary>
        /// 上次刷新时间
        /// </summary>
        public uint UpdateTime { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        public void CopyFrom(TaskEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            Complets = other.Complets;
            States = other.States;
            DailyStart = other.DailyStart;
            DailyCnt = other.DailyCnt;
            InstanceCnt = other.InstanceCnt;
            ActiveScore = other.ActiveScore;
            BeenTake = other.BeenTake;
            StarNum = other.StarNum;
            MonkeyNum = other.MonkeyNum;
            JinChanSongBaoNum = other.JinChanSongBaoNum;
            EagleNum = other.EagleNum;
            UpdateTime = other.UpdateTime;
            CreateTime = other.CreateTime;
        }

        public bool Equals(TaskEntity other)
        {
            if (null == other) return false;
            return Id == other.Id && RoleId == other.RoleId &&
                   Complets.Equals(other.Complets) && States.Equals(other.States) &&
                   DailyStart.Equals(other.DailyStart) && DailyCnt.Equals(other.DailyCnt) &&
                   InstanceCnt.Equals(other.InstanceCnt) && ActiveScore.Equals(other.ActiveScore) &&
                   BeenTake.Equals(other.BeenTake) && StarNum == other.StarNum && MonkeyNum == other.MonkeyNum &&
                   JinChanSongBaoNum == other.JinChanSongBaoNum &&
                   EagleNum == other.EagleNum &&
                   UpdateTime == other.UpdateTime && CreateTime == other.CreateTime;
        }
    }
}