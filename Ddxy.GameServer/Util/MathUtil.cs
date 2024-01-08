namespace Ddxy.GameServer.Util
{
    public static class MathUtil
    {
        public static float Smooth(float start, float end, float amount)
        {
            // Clamp to 0-1;
            amount = amount > 1f ? 1f : amount;
            amount = amount < 0f ? 0f : amount;

            // Cubicly adjust the amount value.
            amount = (amount * amount) * (3f - (2f * amount));

            return (start + ((end - start) * amount));
        }
    }
}