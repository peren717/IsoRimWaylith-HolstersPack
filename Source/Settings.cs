using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IsoRimHolstersPack
{
    // The player's offsets, per weapon by defName: x sideways (+ the person's right), y height, z forward-back (+ forward), cells, on
    // the weapon's 3D place in main and side slots alike. Its turns, degrees: x tilt, y lean (Slot.Spin). Missing means zero, and zero
    // is not stored; a defName no longer loaded stays in the file, never asked for.
    public class HolstersPackSettings : ModSettings
    {
        internal Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();
        internal Dictionary<string, Vector2> turns = new Dictionary<string, Vector2>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref offsets, "offsets", LookMode.Value, LookMode.Value);
            if (offsets == null)
            {
                offsets = new Dictionary<string, Vector3>();
            }
            Scribe_Collections.Look(ref turns, "turns", LookMode.Value, LookMode.Value);
            if (turns == null)
            {
                turns = new Dictionary<string, Vector2>();
            }
        }

        internal Vector3 OffsetOf(string weapon)
        {
            Vector3 offset;
            return offsets.TryGetValue(weapon, out offset) ? offset : Vector3.zero;
        }

        internal void Set(string weapon, Vector3 offset)
        {
            if (offset == Vector3.zero)
            {
                offsets.Remove(weapon);
            }
            else
            {
                offsets[weapon] = offset;
            }
            Slot.Reread();
        }

        internal Vector2 TurnOf(string weapon)
        {
            Vector2 turn;
            return turns.TryGetValue(weapon, out turn) ? turn : Vector2.zero;
        }

        internal void SetTurn(string weapon, Vector2 turn)
        {
            if (turn == Vector2.zero)
            {
                turns.Remove(weapon);
            }
            else
            {
                turns[weapon] = turn;
            }
            Slot.Reread();
        }

        // This weapon's offsets and turns on every weapon of its Holsters group.
        internal void CopyToGroup(string weapon)
        {
            Vector3 offset = OffsetOf(weapon);
            Vector2 turn = TurnOf(weapon);
            foreach (string other in Slot.WeaponsWith(weapon))
            {
                Set(other, offset);
                SetTurn(other, turn);
            }
        }
    }

    // The mod settings page; the weapons and sliders are WeaponPanel, shared with the floating window.
    public class HolstersPackMod : Mod
    {
        internal static HolstersPackSettings Settings;
        internal static HolstersPackMod Instance;

        internal readonly WeaponPanel panel = new WeaponPanel();

        public HolstersPackMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HolstersPackSettings>();
            Instance = this;
        }

        public override string SettingsCategory()
        {
            return "IsoRimHolsters_Title".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            panel.Whole(inRect);
        }
    }
}
