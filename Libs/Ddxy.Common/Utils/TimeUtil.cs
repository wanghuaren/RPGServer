using System;

namespace Ddxy.Common.Utils
{
    public static class TimeUtil
    {
        /// <summary>
        /// 获取当前的unix timestamp(秒)
        /// </summary>
        public static uint TimeStamp => (uint) DateTimeOffset.Now.ToUnixTimeSeconds();

        public static uint MilliTimeStamp => (uint) DateTimeOffset.Now.ToUnixTimeMilliseconds();

        public static bool IsSameWeek(DateTimeOffset dto1, DateTimeOffset dto2)
        {
            var big = dto2;
            var small = dto1;
            if (dto2 < dto1)
            {
                big = dto1;
                small = dto2;
            }

            var days = big.DayOfYear - small.DayOfYear;
            var weeks = big.DayOfWeek - small.DayOfWeek;
            return days < 7 && weeks > 0;
        }

        public static bool IsSameDay(DateTimeOffset dto1, DateTimeOffset dto2)
        {
            return dto1.Year == dto2.Year && dto1.DayOfYear == dto2.DayOfYear;
        }
    }
}