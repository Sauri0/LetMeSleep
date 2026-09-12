namespace LetMeSleep.Presentation
{
    // Pure temporal/spatial decision policy. No transforms, animation writes, game state or network effects.
    internal static class MenuReactionPolicy
    {
        internal enum Beat { Idle, Look, Swat, Return, Settle }
        internal static Beat Next(Beat beat, float seconds, float cooldown, float lookSeconds,
            float swatSeconds, float returnSeconds, float maxLookSeconds,
            float distance, float predictedDistance, float noticeRadius, float swatRadius, bool reducedMotion)
        {
            if (reducedMotion) return Beat.Idle;
            switch (beat)
            {
                case Beat.Idle:
                    return seconds >= cooldown && distance <= noticeRadius ? Beat.Look : beat;
                case Beat.Look:
                    if (seconds >= lookSeconds && distance <= noticeRadius && predictedDistance < distance && predictedDistance <= swatRadius)
                        return Beat.Swat;
                    return seconds > maxLookSeconds || distance > noticeRadius * 1.4f ? Beat.Settle : beat;
                case Beat.Swat: return seconds >= swatSeconds ? Beat.Return : beat;
                case Beat.Return: return seconds >= returnSeconds ? Beat.Idle : beat;
                case Beat.Settle: return seconds >= .4f ? Beat.Idle : beat;
                default: return Beat.Idle;
            }
        }
    }
}

namespace LetMeSleep.Presentation
{
    internal static class BlinkWeightPolicy
    {
        internal static void Fill(float closure,float[] weights)
        {
            for(int i=0;i<4;i++) weights[i]=0;
            float scaled=System.Math.Max(0,System.Math.Min(1,closure))*4;
            int lower=(int)System.Math.Floor(scaled);
            float fraction=scaled-lower;
            if(lower>0) weights[lower-1]=(1-fraction)*100;
            if(lower<4) weights[lower]=fraction*100;
        }
    }
}
