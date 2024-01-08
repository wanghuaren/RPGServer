namespace Ddxy.GameServer.Core
{
    public enum FlagType
    {
        /// <summary>
        /// 是否领取过系统赠送的宠物
        /// </summary>
        AdoptPet = 0,

        /// <summary>
        /// 是否领取过新手礼包
        /// </summary>
        XinShouGift = 1,

        /// <summary>
        /// 世界禁言
        /// </summary>
        WorldSilent = 2,

        /// <summary>
        /// 帮派禁言
        /// </summary>
        SectSilent = 3,

        /// <summary>
        /// 是否已经领取过首充
        /// </summary>
        FirstPayReward = 4,

        /// <summary>
        /// 是否开启闪现支援
        /// </summary>
        ShanXianOrder = 5,

        /// <summary>
        /// 是否已经后台赠送礼物
        /// </summary>
        GmSentGift = 6,
        
        /// <summary>
        /// 是否已经获得公益好服礼包
        /// </summary>
        GongYiHaoFuGift = 6,
    }

    public class Flags
    {
        public int Value { get; private set; }

        public Flags()
        {
        }

        public Flags(int value)
        {
            Value = value;
        }

        public bool GetFlag(FlagType ft)
        {
            return GetFlag((int) ft);
        }

        public bool GetFlag(int index)
        {
            if (index < 0 || index > 31) return false;
            var ret = Value & (1 << index);
            return ret != 0;
        }

        public Flags SetFlag(FlagType ft, bool value)
        {
            return SetFlag((int) ft, value);
        }

        public Flags SetFlag(int index, bool value)
        {
            // 4个字节
            if (index < 0 || index > 31) return this;
            if (value)
            {
                Value |= 1 << index;
            }
            else
            {
                Value &= ~(1 << index);
            }

            return this;
        }
    }
}