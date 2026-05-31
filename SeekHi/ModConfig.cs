using StardewModdingAPI;

namespace SeekHi
{
    public class ModConfig
    {
        public SButton GreetingKey { get; set; } = SButton.Q;
        public int SearchRadius { get; set; } = 7;
        public bool AllowGreetingIfAlreadyTalked { get; set; } = true;

        public float TurnDelaySeconds { get; set; } = 0.75f;    
        public float BubbleDelaySeconds { get; set; } = 1.00f;  
    }
}