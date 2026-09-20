namespace GreyWardenPolicePurity
{
    // Bannerlord mapping policy. This is not a GWENT gameplay rule.
    internal sealed class GwpMusicBattlePolicy
    {
        public string Tier { get; private set; } = "medium";
        private string _candidate = "medium";
        private float _stable, _held;
        public static string Classify(float ratio) => ratio < .8f ? "low" : ratio > 1.25f ? "high" : "medium";
        public void Initialize(float ratio) { Tier = _candidate = Classify(ratio); _stable = _held = 0; }
        public bool Update(float ratio, float dt)
        {
            _held += dt;
            string wanted = Classify(ratio);
            if (Tier == "low" && ratio < .88f) wanted = "low";
            if (Tier == "high" && ratio > 1.125f) wanted = "high";
            if (Tier == "medium" && ratio >= .72f && ratio <= 1.375f) wanted = "medium";
            if (wanted != _candidate) { _candidate = wanted; _stable = 0; }
            else _stable += dt;
            if (wanted == Tier || _stable < 4 || _held < 8) return false;
            Tier = wanted; _stable = _held = 0; return true;
        }
    }
}
