using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace IsoRimHolstersPack
{
    // IsoRim Waylith - Holsters Pack (needs IsoRim Api 59; older: this does nothing, logged once, Holsters draws as ever).
    //
    // Holsters draws each sheathed weapon as a flat plane from HolstersComp.PostDraw: right for a sprite, lying on the ground under a
    // 3D figure. Here its own code still decides everything: from the figure hook its WeaponDrawingManager.DrawWeaponsFor runs as it
    // is (conditions, carried openly, side and smart side, group display), only the planes it would draw are caught instead
    // (Holstered). Each becomes one fixed 3D place on the body (Slot). Its PostDraw then stays out for that pawn. A pawn drawn as a
    // card keeps the plane, on its card (Api.DrawOnCard). Nothing caught, or "Held weapons in 3D" off: Holsters as ever.
    [StaticConstructorOnStartup]
    public static class Pack
    {
        internal const string Tag = "[IsoRim Holsters Pack] ";

        static Pack()
        {
            try
            {
                if (IsoRim.Api.Version < 59)
                {
                    Log.Message(Tag + "IsoRim is older than Api 59: Holsters draws as it does without the pack.");
                    return;
                }
                Holstered.Run(new Harmony("peren.isorimwaylith.holsterspack"));
            }
            catch (Exception e)
            {
                Log.Error(Tag + "Setup failed (Holsters draws as it does without the pack): " + e);
            }
        }
    }

    internal static class Holstered
    {
        private struct Caught
        {
            public ThingWithComps weapon;
            public object group;
            public bool side;
        }

        private static Action<Pawn> drawWeaponsFor;
        private static Type comp;
        private static bool catching;
        private static readonly List<Caught> caught = new List<Caught>();
        private static readonly Dictionary<int, int> shown = new Dictionary<int, int>();   // pawn: the last frame its weapons went on its figure
        private static Pawn drawing;   // the pawn whose HolstersComp is drawing
        private static PropertyInfo itemOf, locationOf, rotationOf, sizeOf, meshOf;

        internal static void Run(Harmony h)
        {
            comp = AccessTools.TypeByName("RimWorldHolsters.HolstersComp");
            Type props = AccessTools.TypeByName("RimWorldHolsters.Core.WeaponDrawingProperties");
            Type group = AccessTools.TypeByName("RimWorldHolsters.WeaponGroupCordInfo");
            Type display = AccessTools.TypeByName("RimWorldHolsters.Core.IR_DisplayWeapon");
            MethodInfo manager = AccessTools.Method("RimWorldHolsters.Core.WeaponDrawing.WeaponDrawingManager:DrawWeaponsFor", new[] { typeof(Pawn) });
            ConstructorInfo properties = props != null && group != null
                ? AccessTools.Constructor(props, new[] { typeof(ThingWithComps), group, typeof(Vector3), typeof(Rot4), typeof(bool) }) : null;
            MethodInfo draw = display != null ? AccessTools.Method(display, "DrawEquipmentHolstered") : null;
            MethodInfo postDraw = comp != null ? AccessTools.DeclaredMethod(comp, "PostDraw") : null;
            itemOf = props != null ? AccessTools.Property(props, "Item") : null;
            locationOf = props != null ? AccessTools.Property(props, "Location") : null;
            rotationOf = props != null ? AccessTools.Property(props, "Rotation") : null;
            sizeOf = props != null ? AccessTools.Property(props, "Size") : null;
            meshOf = props != null ? AccessTools.Property(props, "Mesh") : null;
            if (manager == null || properties == null || draw == null || postDraw == null || itemOf == null || locationOf == null || rotationOf == null || sizeOf == null
                || meshOf == null || !Slot.Find(group))
            {
                Log.Warning(Pack.Tag + "Holsters' drawing was not found where expected: it draws as it does without the pack.");
                return;
            }
            drawWeaponsFor = (Action<Pawn>)Delegate.CreateDelegate(typeof(Action<Pawn>), manager);
            h.Patch(properties, prefix: new HarmonyMethod(typeof(Holstered), nameof(Properties_Prefix)));
            h.Patch(draw, prefix: new HarmonyMethod(typeof(Holstered), nameof(Draw_Prefix)));
            h.Patch(postDraw, prefix: new HarmonyMethod(typeof(Holstered), nameof(PostDraw_Prefix)), finalizer: new HarmonyMethod(typeof(Holstered), nameof(PostDraw_Finalizer)));
            IsoRim.Api.AddFigureHook(Figure);
        }

        private static void Figure(IsoRim.FigureFrame frame)
        {
            Pawn pawn = frame.Pawn;
            if (pawn == null || pawn.Dead || !pawn.def.HasComp(comp))
            {
                return;
            }
            caught.Clear();
            catching = true;
            try
            {
                drawWeaponsFor(pawn);
            }
            finally
            {
                catching = false;
            }
            bool all = caught.Count > 0;
            for (int i = 0; i < caught.Count; i++)
            {
                Caught c = caught[i];
                Matrix4x4 local;
                float scale;
                Slot.Place(frame, c.weapon, c.group, c.side, out local, out scale);
                all &= frame.AttachWeaponToBody(c.weapon, local, scale);
            }
            if (all)
            {
                shown[pawn.thingIDNumber] = Time.frameCount;
            }
        }

        // The plane Holsters is about to draw: which weapon, by which group and slot.
        public static void Properties_Prefix(ThingWithComps item, object groupSettings, bool isSide)
        {
            if (catching)
            {
                caught.Add(new Caught { weapon = item, group = groupSettings, side = isSide });
            }
        }

        public static bool Draw_Prefix(object properties)
        {
            if (catching)
            {
                return false;
            }
            if (drawing == null || !IsoRim.Api.IsCard(drawing))
            {
                return true;
            }
            ThingWithComps item = (ThingWithComps)itemOf.GetValue(properties);
            Matrix4x4 matrix = Matrix4x4.TRS((Vector3)locationOf.GetValue(properties), Quaternion.AngleAxis((float)rotationOf.GetValue(properties), Vector3.up),
                Vector3.one / item.def.uiIconScale * (float)sizeOf.GetValue(properties));
            return !IsoRim.Api.DrawOnCard(drawing, (Mesh)meshOf.GetValue(properties), matrix, item.Graphic.MatSingleFor(item));
        }

        // Not for a pawn whose figure has its weapons (this frame, or the last: the figure may be drawn after this).
        public static bool PostDraw_Prefix(ThingComp __instance)
        {
            Pawn pawn = __instance.parent as Pawn;
            int frame;
            if (pawn != null && shown.TryGetValue(pawn.thingIDNumber, out frame) && Time.frameCount - frame <= 1)
            {
                return false;
            }
            drawing = pawn;
            return true;
        }

        public static void PostDraw_Finalizer()
        {
            drawing = null;
        }
    }

    // One fixed place on the body for a weapon in a Holsters slot. Players set each facing freely, so the rule is plain: the South
    // settings give sideways, height, tilt and size, the East settings forward-back and lean. The North and West settings are not
    // read. Read again every Refresh frames: a change in Holsters' settings shows at once.
    //   - South view: screen right is the person's left, up is up (FigureFrame.BodySprite, so body types fit). East view: screen
    //     right is forward, at the body's height scale, from the middle of its depth (as IsoRim lays the side sprite on the body).
    //   - The weapon's line: its sprite's long axis as Holsters turns it on the screen, South's tilt, East's lean.
    //   - Layer: set over the body in South it lies in front of it (under: behind); in East on its right (under: left). Used only
    //     where the two views leave the weapon in the body: it moves out the shorter way the layers allow.
    //   - It lies flat on the body: the picture's face toward the body's outside, its top the way South shows it.
    internal static class Slot
    {
        private const float MaxLean = 3.73f;     // tan 75: an East view lying flat tells no lean beyond this
        private const float Gap = 0.02f;         // between the body and the weapon
        private const float Opaque = 0.45f;      // the weapon's half length, of its picture's side
        private const float Picture = 1.11f;     // the game's pictures to figure units (a weapon's size in the hand)
        private const int Refresh = 30;

        // Holsters' settings for one weapon, slot and body type, as this pack uses them.
        private sealed class Views
        {
            public Vector3 south, east;
            public Vector2 front, top, topS, topE;
            public Vector3 line;
            public float sized, scale, forward, right;
        }

        private static MethodInfo getPos, getAngle, getFlip, getLayer, getSize;
        private static Type bodyType;
        private static readonly Dictionary<ValueTuple<ThingDef, bool, BodyTypeDef>, Views> views = new Dictionary<ValueTuple<ThingDef, bool, BodyTypeDef>, Views>();
        private static int read = -Refresh;

        internal static bool Find(Type group)
        {
            Type settings = AccessTools.TypeByName("RimWorldHolsters.IR_HolstersSettings");
            bodyType = AccessTools.TypeByName("RimWorldHolsters.BodyType");
            if (settings == null || group == null || bodyType == null)
            {
                return false;
            }
            getPos = AccessTools.Method(settings, "GetWeaponPos", new[] { group, typeof(Rot4), typeof(bool), bodyType });
            getAngle = AccessTools.Method(settings, "GetWeaponAngle", new[] { group, typeof(Rot4), typeof(bool) });
            getFlip = AccessTools.Method(settings, "GetWeaponFlip", new[] { group, typeof(Rot4), typeof(bool) });
            getLayer = AccessTools.Method(settings, "GetWeaponLayer", new[] { group, typeof(Rot4), typeof(bool) });
            getSize = AccessTools.Method(group, "GetSize", new[] { typeof(Rot4) });
            return getPos != null && getAngle != null && getFlip != null && getLayer != null && getSize != null;
        }

        private static Views Read(Thing weapon, object group, bool side, BodyTypeDef def)
        {
            if (Time.frameCount - read >= Refresh)
            {
                views.Clear();
                read = Time.frameCount;
            }
            ValueTuple<ThingDef, bool, BodyTypeDef> key = new ValueTuple<ThingDef, bool, BodyTypeDef>(weapon.def, side, def);
            Views v;
            if (views.TryGetValue(key, out v))
            {
                return v;
            }
            v = new Views();
            object body = Enum.Parse(bodyType, def.defName.ToLower());
            v.south = (Vector3)getPos.Invoke(null, new[] { group, Rot4.South, side, body });
            v.east = (Vector3)getPos.Invoke(null, new[] { group, Rot4.East, side, body });
            float angleS = (float)getAngle.Invoke(null, new[] { group, Rot4.South, side }), angleE = (float)getAngle.Invoke(null, new[] { group, Rot4.East, side });
            bool flipS = (bool)getFlip.Invoke(null, new[] { group, Rot4.South, side }), flipE = (bool)getFlip.Invoke(null, new[] { group, Rot4.East, side });

            Vector2 size = weapon.Graphic.drawSize;
            v.front = IsoRim.Api.WeaponAxis(weapon);
            v.top = new Vector2(-v.front.y, v.front.x);
            Vector2 frontS = OnScreen(v.front, angleS, flipS), frontE = OnScreen(v.front, angleE, flipE);
            v.topS = OnScreen(v.top, angleS, flipS);
            v.topE = OnScreen(v.top, angleE, flipE);
            float lean = Mathf.Abs(frontE.y) > 1e-3f ? Mathf.Clamp(frontE.x / frontE.y, -MaxLean, MaxLean) : MaxLean * Mathf.Sign(frontE.x);
            v.line = new Vector3(-frontS.x, frontS.y, frontS.y * lean).normalized;

            v.sized = (float)getSize.Invoke(group, new object[] { Rot4.South }) / weapon.def.uiIconScale;
            v.scale = v.sized / size.x;
            v.forward = (bool)getLayer.Invoke(null, new[] { group, Rot4.South, side }) ? 1f : -1f;
            v.right = (bool)getLayer.Invoke(null, new[] { group, Rot4.East, side }) ? 1f : -1f;
            views[key] = v;
            return v;
        }

        internal static void Place(IsoRim.FigureFrame frame, Thing weapon, object group, bool side, out Matrix4x4 local, out float scale)
        {
            Views v = Read(weapon, group, side, frame.Pawn.story.bodyType);
            scale = v.scale;
            Matrix4x4 sprite = frame.BodySprite;
            Vector3 centre = frame.BodyCentre, half = frame.BodySize * 0.5f, line = v.line;
            Vector3 at = sprite.MultiplyPoint3x4(new Vector3(v.south.x, v.south.z, 0f));
            at.z = centre.z + v.east.x * sprite.m11;
            at = OutOfBody(at, line, Opaque * v.sized * Picture, centre, half, v.forward, v.right);

            // Flat on the body: the face outward, as near as the line allows.
            Vector3 outward = new Vector3(at.x / half.x, 0f, (at.z - centre.z) / half.z);
            Vector3 face = outward - line * Vector3.Dot(outward, line);
            if (face.sqrMagnitude < 1e-4f)
            {
                face = Vector3.forward - line * line.z;
            }
            face.Normalize();
            Vector3 up = Vector3.Cross(face, line);
            float seen = Vector2.Dot(new Vector2(-up.x, up.y), v.topS);
            if (seen < 0f || (Mathf.Abs(seen) < 0.2f && Vector2.Dot(new Vector2(up.z, up.y), v.topE) < 0f))
            {
                up = -up;
            }
            Vector3 x = line * v.front.x + up * v.top.x, y = line * v.front.y + up * v.top.y;
            local = Matrix4x4.identity;
            local.SetColumn(0, x);
            local.SetColumn(1, y);
            local.SetColumn(2, Vector3.Cross(x, y));
            local.SetColumn(3, new Vector4(at.x, at.y, at.z, 1f));
        }

        // A picture's direction on the screen as Holsters draws it: mirrored in x on the flipped mesh and turned half round, then
        // turned clockwise by the angle (WeaponDrawingProperties).
        private static Vector2 OnScreen(Vector2 v, float angle, bool flip)
        {
            float r = (flip ? angle + 180f : angle) * Mathf.Deg2Rad;
            float x = flip ? -v.x : v.x, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(x * c + v.y * s, -x * s + v.y * c);
        }

        // Moved out of the body (a box) if the weapon's line runs through it: toward front (+1) or back, or right (+1) or left as
        // the layers say, whichever is the shorter way.
        private static Vector3 OutOfBody(Vector3 at, Vector3 line, float length, Vector3 centre, Vector3 half, float forward, float right)
        {
            float moveZ = 0f, moveX = 0f;
            bool inside = false;
            for (int i = -2; i <= 2; i++)
            {
                Vector3 p = at + line * (length * i * 0.5f);
                float z = p.z - centre.z;
                if (p.y < 0f || p.y > centre.y + half.y || Mathf.Abs(p.x) >= half.x || Mathf.Abs(z) >= half.z)
                {
                    continue;
                }
                inside = true;
                moveZ = Mathf.Max(moveZ, half.z + Gap - forward * z);
                moveX = Mathf.Max(moveX, half.x + Gap - right * p.x);
            }
            if (!inside)
            {
                return at;
            }
            if (moveZ <= moveX)
            {
                at.z += forward * moveZ;
            }
            else
            {
                at.x += right * moveX;
            }
            return at;
        }
    }
}
