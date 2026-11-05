using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IsoRimHolstersPack
{
    // The player's offsets, per weapon by defName: x sideways (+ the person's right), y height, z forward-back (+ forward), cells, on
    // the weapon's 3D place in main and side slots alike. Missing means zero, and zero is not stored; a defName no longer loaded
    // stays in the file, never asked for.
    public class HolstersPackSettings : ModSettings
    {
        internal Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref offsets, "offsets", LookMode.Value, LookMode.Value);
            if (offsets == null)
            {
                offsets = new Dictionary<string, Vector3>();
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

        // This weapon's offsets on every weapon of its Holsters group.
        internal void CopyToGroup(string weapon)
        {
            Vector3 offset = OffsetOf(weapon);
            foreach (string other in Slot.WeaponsWith(weapon))
            {
                Set(other, offset);
            }
        }
    }

    // Find a weapon by typing its name, or browse a group; it gets its own three sliders.
    public class HolstersPackMod : Mod
    {
        private const float Range = 0.6f;
        private const float RowHeight = 30f;
        private const int RowsShown = 6;

        internal static HolstersPackSettings Settings;

        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private List<Slot.Weapon> weapons;
        private readonly List<Slot.Weapon> shown = new List<Slot.Weapon>();
        private List<string> groups;
        private string browsing;
        private Slot.Weapon picked;
        private Vector2 scroll;
        private int drawn = -10;

        public HolstersPackMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HolstersPackSettings>();
        }

        public override string SettingsCategory()
        {
            return "IsoRimHolsters_Title".Translate();
        }

        private void Filter()
        {
            shown.Clear();
            foreach (Slot.Weapon w in weapons)
            {
                if ((browsing == null || w.group == browsing)
                    && (!search.filter.Active || search.filter.Matches(w.label) || search.filter.Matches(w.english) || search.filter.Matches(w.def.defName)))
                {
                    shown.Add(w);
                }
            }
            scroll = Vector2.zero;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Time.frameCount - drawn > 2)
            {
                weapons = Slot.Weapons();
                groups = Slot.GroupNames();
                Filter();
            }
            drawn = Time.frameCount;
            if (weapons.Count == 0)
            {
                return;
            }
            Rect left = inRect.LeftPart(0.56f), right = inRect.RightPart(0.40f);
            search.OnGUI(new Rect(left.x, left.y, left.width, QuickSearchWidget.WidgetHeight), () =>
            {
                browsing = null;
                Filter();
            });
            float y = left.y + QuickSearchWidget.WidgetHeight + 6f;
            Results(new Rect(left.x, y, left.width, RowHeight * RowsShown));
            y += RowHeight * RowsShown + 8f;
            Groups(new Rect(left.x, y, left.width, left.yMax - y));
            if (picked != null)
            {
                Panel(right);
            }
        }

        private void Results(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect view = new Rect(0f, 0f, rect.width - 16f, shown.Count * RowHeight);
            Widgets.BeginScrollView(rect, ref scroll, view);
            for (int i = 0; i < shown.Count; i++)
            {
                float top = i * RowHeight;
                if (top + RowHeight < scroll.y || top > scroll.y + rect.height)
                {
                    continue;
                }
                Slot.Weapon w = shown[i];
                Rect row = new Rect(0f, top, view.width, RowHeight);
                if (w == picked)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.DefIcon(new Rect(row.x + 3f, row.y + 3f, RowHeight - 6f, RowHeight - 6f), w.def);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + RowHeight + 2f, row.y, row.width * 0.6f - RowHeight, row.height), w.label.CapitalizeFirst());
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(row.x + row.width * 0.6f, row.y, row.width * 0.4f - 4f, row.height), w.group);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(row))
                {
                    picked = w;
                }
            }
            Widgets.EndScrollView();
        }

        private void Groups(Rect rect)
        {
            float half = (rect.width - 6f) / 2f;
            for (int i = 0; i < groups.Count; i++)
            {
                Rect button = new Rect(rect.x + i % 2 * (half + 6f), rect.y + i / 2 * 32f, half, 28f);
                if (groups[i] == browsing)
                {
                    Widgets.DrawHighlightSelected(button);
                }
                if (Widgets.ButtonText(button, groups[i]))
                {
                    browsing = groups[i] == browsing ? null : groups[i];
                    search.Reset();
                    Filter();
                }
            }
        }

        private void Panel(Rect rect)
        {
            string name = picked.def.defName;
            Widgets.DefIcon(new Rect(rect.x, rect.y, 40f, 40f), picked.def);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 46f, rect.y, rect.width - 46f, 40f), picked.label.CapitalizeFirst());
            Text.Anchor = TextAnchor.UpperLeft;
            Listing_Standard list = new Listing_Standard();
            list.Begin(new Rect(rect.x, rect.y + 46f, rect.width, rect.height - 46f));
            Vector3 was = Settings.OffsetOf(name), now = was;
            now.x = Slider(list, "IsoRimHolsters_Sideways", was.x);
            now.y = Slider(list, "IsoRimHolsters_Height", was.y);
            now.z = Slider(list, "IsoRimHolsters_Forward", was.z);
            if (list.ButtonText("IsoRimHolsters_Reset".Translate()))
            {
                now = Vector3.zero;
            }
            if (now != was)
            {
                Settings.Set(name, now);
            }
            if (list.ButtonText("IsoRimHolsters_CopyToGroup".Translate(picked.group)))
            {
                Settings.CopyToGroup(name);
            }
            list.End();
        }

        private static float Slider(Listing_Standard list, string key, float value)
        {
            list.Label(key.Translate() + ": " + value.ToString("0.00"));
            return Mathf.Round(list.Slider(value, -Range, Range) * 100f) / 100f;
        }
    }
}
