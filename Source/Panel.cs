using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IsoRimHolstersPack
{
    // Find a weapon by typing its name, or browse a group; it gets its own sliders. One panel for the settings page (Whole) and the
    // floating window (Compact), which also lists what the selected pawn has out.
    internal sealed class WeaponPanel
    {
        private const float Range = 0.6f;
        private const float TurnRange = 90f;
        private const float RowHeight = 30f;
        private const float SliderHeight = 28f;
        private const float ButtonHeight = 30f;
        private const float PanelHeight = 46f + SliderHeight * 5f + (ButtonHeight + 4f) * 2f;
        private const int RowsShown = 6;
        private const int WornShown = 3;
        private const int Refresh = 30;

        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private List<Slot.Weapon> weapons;
        private readonly List<Slot.Weapon> shown = new List<Slot.Weapon>();
        private List<string> groups;
        private string browsing;
        private Slot.Weapon picked;
        private Vector2 scroll;
        private int drawn = -10;
        private Pawn pawn;
        private readonly List<Slot.Weapon> worn = new List<Slot.Weapon>();
        private int wornRead;

        internal bool Typing => search.CurrentlyFocused();

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

        private void Prepare()
        {
            if (Time.frameCount - drawn > 2)
            {
                weapons = Slot.Weapons();
                groups = Slot.GroupNames();
                Filter();
            }
            drawn = Time.frameCount;
        }

        // For a floating window just opened: everything found again, the selected pawn taken up as new.
        internal void Open()
        {
            browsing = null;
            drawn = -10;
            pawn = null;
            worn.Clear();
        }

        internal void Whole(Rect inRect)
        {
            Prepare();
            if (weapons.Count == 0)
            {
                return;
            }
            Rect left = inRect.LeftPart(0.56f), right = inRect.RightPart(0.40f);
            Search(left);
            float y = left.y + QuickSearchWidget.WidgetHeight + 6f;
            Results(new Rect(left.x, y, left.width, RowHeight * RowsShown));
            y += RowHeight * RowsShown + 8f;
            Groups(new Rect(left.x, y, left.width, left.yMax - y));
            if (picked != null)
            {
                Sliders(right);
            }
            if (Widgets.ButtonText(new Rect(right.x, right.yMax - ButtonHeight * 2f - 4f, right.width, ButtonHeight), "IsoRimHolsters_ResetAll".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("IsoRimHolsters_ResetAllSure".Translate(), HolstersPackMod.Settings.ResetAll, destructive: true));
            }
            if (Current.ProgramState == ProgramState.Playing
                && Widgets.ButtonText(new Rect(right.x, right.yMax - ButtonHeight, right.width, ButtonHeight), "IsoRimHolsters_Preview".Translate()))
            {
                HolstersPreview.Open();
            }
        }

        internal void Compact(Rect inRect)
        {
            Prepare();
            if (weapons.Count == 0)
            {
                return;
            }
            Follow();
            float y = inRect.y;
            if (worn.Count > 0)
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f), pawn.LabelShortCap);
                y += 24f;
                for (int i = 0; i < worn.Count && i < WornShown; i++)
                {
                    Row(new Rect(inRect.x, y, inRect.width, RowHeight), worn[i]);
                    y += RowHeight;
                }
                y += 6f;
            }
            Search(new Rect(inRect.x, y, inRect.width, 0f));
            y += QuickSearchWidget.WidgetHeight + 6f;
            float bottom = picked != null ? inRect.yMax - PanelHeight : inRect.yMax;
            Results(new Rect(inRect.x, y, inRect.width, bottom - 6f - y));
            if (picked != null)
            {
                Sliders(new Rect(inRect.x, bottom, inRect.width, PanelHeight));
            }
        }

        // The selected pawn's weapons, the first picked when another pawn comes up.
        private void Follow()
        {
            Pawn now = Find.Selector.FirstSelectedObject as Pawn;
            if (now == pawn && Time.frameCount - wornRead < Refresh)
            {
                return;
            }
            bool another = now != pawn;
            pawn = now;
            wornRead = Time.frameCount;
            worn.Clear();
            if (pawn != null)
            {
                foreach (ThingWithComps thing in Holstered.WeaponsOn(pawn))
                {
                    Slot.Weapon w = weapons.Find(x => x.def == thing.def);
                    if (w != null && !worn.Contains(w))
                    {
                        worn.Add(w);
                    }
                }
            }
            if (another && worn.Count > 0)
            {
                picked = worn[0];
            }
        }

        private void Search(Rect rect)
        {
            search.OnGUI(new Rect(rect.x, rect.y, rect.width, QuickSearchWidget.WidgetHeight), () =>
            {
                browsing = null;
                Filter();
            });
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
                Row(new Rect(0f, top, view.width, RowHeight), shown[i]);
            }
            Widgets.EndScrollView();
        }

        private void Row(Rect row, Slot.Weapon w)
        {
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

        private void Sliders(Rect rect)
        {
            HolstersPackSettings settings = HolstersPackMod.Settings;
            string name = picked.def.defName;
            Widgets.DefIcon(new Rect(rect.x, rect.y, 40f, 40f), picked.def);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 46f, rect.y, rect.width - 46f, 40f), picked.label.CapitalizeFirst());
            Text.Anchor = TextAnchor.UpperLeft;
            float y = rect.y + 46f;
            Vector3 was = settings.OffsetOf(name), now = was;
            Vector2 turnWas = settings.TurnOf(name), turn = turnWas;
            now.x = Slider(rect, ref y, "IsoRimHolsters_Sideways", was.x, Range, 100f, "0.00");
            now.y = Slider(rect, ref y, "IsoRimHolsters_Height", was.y, Range, 100f, "0.00");
            now.z = Slider(rect, ref y, "IsoRimHolsters_Forward", was.z, Range, 100f, "0.00");
            turn.x = Slider(rect, ref y, "IsoRimHolsters_Tilt", turnWas.x, TurnRange, 1f, "0°");
            turn.y = Slider(rect, ref y, "IsoRimHolsters_Lean", turnWas.y, TurnRange, 1f, "0°");
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, ButtonHeight), "IsoRimHolsters_Reset".Translate()))
            {
                now = Vector3.zero;
                turn = Slot.DefaultTurn(name);
            }
            y += ButtonHeight + 4f;
            if (now != was)
            {
                settings.Set(name, now);
            }
            if (turn != turnWas)
            {
                settings.SetTurn(name, turn);
            }
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, ButtonHeight), "IsoRimHolsters_CopyToGroup".Translate(picked.group)))
            {
                settings.CopyToGroup(name);
            }
        }

        private static float Slider(Rect rect, ref float y, string key, float value, float range, float steps, string format)
        {
            Rect row = new Rect(rect.x, y, rect.width, SliderHeight);
            y += SliderHeight;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(row.LeftPart(0.44f), key.Translate() + ": " + value.ToString(format));
            Text.Anchor = TextAnchor.UpperLeft;
            return Mathf.Round(Widgets.HorizontalSlider(row.RightPart(0.54f), value, -range, range, true) * steps) / steps;
        }
    }

    // The panel in a small window over the map: the changes show on the real pawns while the game goes on, as it was, paused or not.
    internal sealed class HolstersPreview : Window
    {
        private readonly WeaponPanel panel = HolstersPackMod.Instance.panel;

        public override Vector2 InitialSize => new Vector2(380f, 600f);

        private HolstersPreview()
        {
            optionalTitle = "IsoRimHolsters_Preview".Translate();
            draggable = true;
            doCloseX = true;
            closeOnAccept = false;
            preventCameraMotion = false;
            panel.Open();
        }

        // Out of the settings and the menus they were opened through (each stops the game while open), then onto the map.
        internal static void Open()
        {
            for (int i = Find.WindowStack.Count - 1; i >= 0; i--)
            {
                if (Find.WindowStack[i].forcePause)
                {
                    Find.WindowStack[i].Close(false);
                }
            }
            Find.WindowStack.Add(new HolstersPreview());
        }

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(UI.screenWidth - InitialSize.x - 10f, Mathf.Max(10f, (UI.screenHeight - InitialSize.y) / 2f - 40f), InitialSize.x, InitialSize.y);
        }

        public override void DoWindowContents(Rect inRect)
        {
            preventCameraMotion = panel.Typing;
            panel.Compact(inRect);
        }

        public override void PostClose()
        {
            base.PostClose();
            HolstersPackMod.Instance.WriteSettings();
        }
    }
}
