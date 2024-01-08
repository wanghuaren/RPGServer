namespace Ddxy.GameServer.Util
{
    public static class VersionUtil
    {
        public static bool CheckVersion(string oldVersion, string newVersion)
        {
            if (string.IsNullOrWhiteSpace(oldVersion) || string.IsNullOrWhiteSpace(newVersion)) return false;
            var oldArray = oldVersion.Split(".");
            var newArray = oldVersion.Split(".");
            for (var i = 0; i < oldArray.Length; i++)
            {
                int.TryParse(oldArray[i], out var o);
                int.TryParse(newArray[i], out var n);
                if (o == n) continue;
                return n > 0;
            }

            return newArray.Length > oldArray.Length;
        }
    }
}