using System;
using System.Collections.Generic;

namespace GreyWardenPolicePurity
{
    // Bannerlord design policy, not GWENT gameplay rules. An equal power ratio says
    // nothing about combat tempo: actual hostile HP loss and casualties drive intensity.
    internal sealed class GwpMusicBattlePolicy
    {
        // Pacing (2026-09-24, user: tiers climbed too fast). Saturation at 16% power damaged or
        // 10% lost per 20 s; a first arrow is not a medium; high needs an established medium phase
        // and, once reached, holds long enough not to flip with medium.
        internal const float DamageScale = .16f, LossScale = .10f;
        internal const float MediumEnter = .12f, HighEnter = .55f, HighKeep = .35f;
        internal const float FirstClimb = 40, Reclimb = 20, HighHold = 30;
        private bool _climbed;
        private readonly Queue<(float time, float damage, float losses)> _window = new();
        private readonly Queue<(float time, float damage, float losses)> _recent = new();
        private float _recentDamage, _recentLosses;
        private float _time, _lastContact = float.NegativeInfinity;
        private float _pendingDamage, _pendingLosses, _deployed, _lost;
        private string _candidate = "low";
        private float _stable, _held;
        public string Tier { get; private set; } = "low";
        public float Damage20 { get; private set; }
        public float Casualties20 { get; private set; }
        public float Exchange { get; private set; }
        public float RecentExchange { get; private set; }
        public float Attrition { get; private set; }
        public float Pressure { get; private set; }
        public float Tension { get; private set; }
        public float QuietSeconds => float.IsNegativeInfinity(_lastContact) ? -1 : _time - _lastContact;
        public string Reason { get; private set; } = "no-contact";

        private static float Unit(float n) => Math.Max(0, Math.Min(1, n));
        public void Initialize(float ours, float enemy)
        {
            Tier = _candidate = "low"; _stable = _held = _time = 0; _climbed = false;
            _lastContact = float.NegativeInfinity;
            _window.Clear(); _pendingDamage = _pendingLosses = _lost = 0;
            _recent.Clear(); _recentDamage = _recentLosses = RecentExchange = 0;
            Damage20 = Casualties20 = Exchange = Attrition = Pressure = Tension = 0;
            _deployed = Math.Max(1, ours + enemy); Reason = "no-contact";
        }
        public void AddReinforcementPower(float power) => _deployed += Math.Max(0, power);
        public void RecordDamage(float powerEquivalent)
        {
            if (powerEquivalent <= 0) return;
            _pendingDamage += powerEquivalent; _lastContact = _time;
        }
        public void RecordCasualty(float power)
        {
            if (power <= 0) return;
            _pendingLosses += power; _lost += power; _lastContact = _time;
        }
        public bool Update(float ours, float enemy, float dt)
        {
            if (dt <= 0) return false;
            _time += dt; _held += dt;
            if (_pendingDamage > 0 || _pendingLosses > 0)
            {
                _window.Enqueue((_time, _pendingDamage, _pendingLosses));
                _recent.Enqueue((_time, _pendingDamage, _pendingLosses));
                _recentDamage += _pendingDamage; _recentLosses += _pendingLosses;
                Damage20 += _pendingDamage; Casualties20 += _pendingLosses;
                _pendingDamage = _pendingLosses = 0;
            }
            while (_window.Count > 0 && _window.Peek().time <= _time - 20)
            {
                var old = _window.Dequeue(); Damage20 -= old.damage; Casualties20 -= old.losses;
            }
            Damage20 = Math.Max(0, Damage20); Casualties20 = Math.Max(0, Casualties20);
            while (_recent.Count > 0 && _recent.Peek().time <= _time - 5)
            {
                var old = _recent.Dequeue(); _recentDamage -= old.damage; _recentLosses -= old.losses;
            }
            _recentDamage = Math.Max(0, _recentDamage); _recentLosses = Math.Max(0, _recentLosses);
            // Include recent casualties in the denominator, avoiding a final survivor
            // turning one small hit into a false whole-army damage spike.
            float force = Math.Max(1, ours + enemy + Casualties20);
            // Max, not sum: a lethal hit must not be counted twice as damage + death.
            Exchange = Unit(Math.Max(Damage20 / (force * DamageScale), Casualties20 / (force * LossScale)));
            // The same per-second rate as the 20-second window, over five seconds.
            RecentExchange = Unit(Math.Max(_recentDamage / (force * DamageScale / 4), _recentLosses / (force * LossScale / 4)));
            Attrition = Unit(_lost / Math.Max(1, _deployed) / .5f);
            Pressure = Unit((enemy / Math.Max(.01f, ours) - 1) / .75f);
            bool fighting = QuietSeconds >= 0 && QuietSeconds <= 12;
            // Losses and disadvantage strengthen real fighting; neither supplies
            // a permanent intensity floor after the fighting has subsided.
            Tension = fighting ? Exchange * (.7f + .2f * Attrition + .1f * Pressure) : 0;
            bool freshClimax = QuietSeconds >= 0 && QuietSeconds <= 2.5f && RecentExchange >= HighEnter;
            // High is entered only from a medium phase that has lasted; the first climb waits longer.
            bool established = Tier == "medium" && _held >= (_climbed ? Reclimb : FirstClimb);
            bool high = Tier == "high" ? Tension >= HighKeep : Tension >= HighEnter && freshClimax && established;
            bool contact = Tier != "low" || Tension >= MediumEnter;
            string wanted = !fighting ? "low" : high ? "high" : contact ? "medium" : "low";
            Reason = !fighting ? "no-recent-contact" : high ? "intense-exchange"
                : !contact ? "light-contact"
                : Tension >= HighEnter && !established ? "medium-not-established"
                : Tension >= HighEnter && !freshClimax ? "waiting-for-fresh-exchange" : "contact";
            if (wanted != _candidate) { _candidate = wanted; _stable = dt; }
            else _stable += dt;
            int Rank(string state) => state == "high" ? 2 : state == "medium" ? 1 : 0;
            bool rising = Rank(wanted) > Rank(Tier);
            float settle = rising ? (wanted == "high" ? 6 : 4) : 6;
            float hold = Tier == "low" ? 2 : Tier == "high" ? HighHold : 12;
            if (wanted == Tier || _stable < settle || _held < hold) return false;
            if (wanted == "high") _climbed = true; else if (wanted == "low") _climbed = false;
            Tier = wanted; _stable = _held = 0; return true;
        }
    }
}
