using System;

namespace Ddxy.GameServer.Util
{
    public static class DateTimeUtil
    {
        // 毫秒
        public static Int64 GetTimestamp()
        {
            TimeSpan ts1 = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts1.TotalMilliseconds);
        }
        // 本周星期几，几时几分几秒？
        public static DateTime GetWeekDayStartTime(DayOfWeek dow, int hour = 0, int minute = 0, int second = 0)
        {
            DateTime now = DateTime.Now;
            DateTime today = new DateTime(now.Year, now.Month, now.Day, hour, minute, second);
            today = today.AddDays(today.DayOfWeek == DayOfWeek.Sunday ? -7 : 0);
            int wd = (int)today.DayOfWeek;
            for (int i = 1 - wd; i < 8 - wd; i++)
            {
                DateTime c = today.AddDays(i);
                if (c.DayOfWeek == dow)
                {
                    return c;
                }
            }
            return today;
        }
        // 是今年第几周？
        public static int GetWeekNumber(DateTime dt)
        {
            int firstWeekend = Convert.ToInt32(DateTime.Parse(dt.Year + "-1-1").DayOfWeek);
            int weekDay = firstWeekend == 0 ? 1 : (7 - firstWeekend + 1);
            int currentDay = dt.DayOfYear;
            return Convert.ToInt32(Math.Ceiling((currentDay - weekDay) / 7.0)) + 1;
        }
    }
}