using System;

namespace RadiationShowcase
{
    /// <summary>Interactables report finished actions here ("suit", "pill", "fan"); GameFlow listens.</summary>
    public static class ShowcaseEvents
    {
        public static event Action<string> ActionDone;

        public static void Raise(string id) => ActionDone?.Invoke(id);
    }
}
