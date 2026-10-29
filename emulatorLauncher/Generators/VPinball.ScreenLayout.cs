using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    enum VPinballDmdLocation
    {
        None,       // hardware DMD only, no DMD window
        Playfield,
        Backglass,
        Screen      // dedicated screen
    }

    /// <summary>
    /// Screen roles for Visual Pinball (playfield, backglass, DMD), computed from the live Windows geometry.
    /// Must be built after any display mode / rotation change.
    /// All rectangles are physical pixels in virtual desktop coordinates.
    /// Screen numbering for the features is the same as MonitorIndex (SDL order).
    /// </summary>
    class VPinballScreenLayout
    {
        const double DmdAspectRatio = 4.0;              // 128x32 dot matrix
        const double BackglassDmdBandRatio = 0.25;      // part of the backglass screen given to the DMD when both share the screen
        const double PlayfieldDmdWidthLandscape = 0.25; // DMD width on a landscape playfield (desktop view)
        const double PlayfieldDmdWidthPortrait = 0.5;   // DMD width on a portrait playfield (full single screen view)

        /// <summary>SCREEN SETUP not set : nothing computed, legacy behavior (SCREENRES values and previous defaults).</summary>
        public bool IsManual { get; private set; }

        /// <summary>Effective number of screens used (1 to 3).</summary>
        public int ScreenCount { get; private set; }

        public Screen PlayfieldScreen { get; private set; }
        public Rectangle Playfield { get; private set; }

        /// <summary>Null when no backglass screen is used.</summary>
        public Screen BackglassScreen { get; private set; }

        /// <summary>Area of the B2S backglass image (top part of the screen when the DMD shares it). Empty when no backglass screen.</summary>
        public Rectangle Backglass { get; private set; }

        /// <summary>Whole backglass screen bounds, needed to place the B2S window. Empty when no backglass screen.</summary>
        public Rectangle BackglassScreenBounds { get; private set; }

        public VPinballDmdLocation DmdLocation { get; private set; }

        /// <summary>Only set when DmdLocation is Screen.</summary>
        public Screen DmdScreen { get; private set; }

        /// <summary>Whole area available for the DMD (used for the B2S full DMD). Empty when DmdLocation is None.</summary>
        public Rectangle DmdArea { get; private set; }

        /// <summary>4:1 area centered in DmdArea (used for the PinMAME and virtual DMD windows). Empty when DmdLocation is None.</summary>
        public Rectangle DmdWindow { get; private set; }

        public bool HasBackglass { get { return BackglassScreen != null; } }

        /// <summary>Table view used when TABLE VIEW is AUTO : 0 = desktop, 1 = cabinet, 2 = full single screen.</summary>
        public int DefaultViewMode
        {
            get
            {
                if (ScreenCount > 1)
                    return 1;

                return Playfield.Height > Playfield.Width ? 2 : 0;
            }
        }

        private VPinballScreenLayout() { }

        public static VPinballScreenLayout Build()
        {
            var config = Program.SystemConfig;
            var ret = new VPinballScreenLayout();

            string setup = config["vp_screensetup"];
            if (string.IsNullOrEmpty(setup))
            {
                ret.IsManual = true;
                SimpleLogger.Instance.Info("[VPinballScreenLayout] Screen setup not set, SCREENRES values are used.");
                return ret;
            }

            // Same numbering as MonitorIndex
            Screen[] screens = Displays.SdlOrder;

            ret.PlayfieldScreen = GetPlayfieldScreen();
            ret.Playfield = GetBounds(ret.PlayfieldScreen);

            if (ret.PlayfieldScreen == null || ret.Playfield.IsEmpty)
            {
                ret.IsManual = true;
                SimpleLogger.Instance.Warning("[VPinballScreenLayout] Playfield screen not found, SCREENRES values are used.");
                return ret;
            }

            // Screens available for the other roles (compared by device name, Screen instances are not reliable)
            var others = screens.Where(s => s.DeviceName != ret.PlayfieldScreen.DeviceName).ToList();

            // Number of screens ("auto" : all connected screens, up to 3)
            int wanted;
            if (!int.TryParse(setup, out wanted) || wanted < 1 || wanted > 3)
                wanted = Math.Min(3, screens.Length);

            ret.ScreenCount = Math.Max(1, Math.Min(wanted, others.Count + 1));
            if (ret.ScreenCount < wanted)
                SimpleLogger.Instance.Warning("[VPinballScreenLayout] " + wanted + " screens requested but only " + (others.Count + 1) + " available.");

            // Backglass : chosen screen, else the largest screen other than the playfield
            if (ret.ScreenCount >= 2)
            {
                ret.BackglassScreen = PickScreen(config["vp_backglass_screen"], screens, others, true, "backglass");
                if (ret.BackglassScreen != null)
                {
                    string backglassDevice = ret.BackglassScreen.DeviceName;
                    others.RemoveAll(s => s.DeviceName == backglassDevice);

                    ret.BackglassScreenBounds = GetBounds(ret.BackglassScreen);
                    ret.Backglass = ret.BackglassScreenBounds;
                }
            }

            // DMD location
            VPinballDmdLocation location;
            switch (config["vp_dmd_location"])
            {
                case "playfield": location = VPinballDmdLocation.Playfield; break;
                case "backglass": location = VPinballDmdLocation.Backglass; break;
                case "screen": location = VPinballDmdLocation.Screen; break;
                case "none": location = VPinballDmdLocation.None; break;
                default:
                    if (ret.ScreenCount >= 3)
                        location = VPinballDmdLocation.Screen;
                    else if (ret.ScreenCount == 2)
                        location = VPinballDmdLocation.Backglass;
                    else
                        location = VPinballDmdLocation.Playfield;
                    break;
            }

            // Dedicated screen : only with a 3 screens setup. Chosen screen, else the smallest remaining screen
            if (location == VPinballDmdLocation.Screen)
            {
                if (ret.ScreenCount >= 3)
                    ret.DmdScreen = PickScreen(config["vp_dmd_screen"], screens, others, false, "DMD");

                if (ret.DmdScreen == null)
                {
                    location = ret.HasBackglass ? VPinballDmdLocation.Backglass : VPinballDmdLocation.Playfield;
                    SimpleLogger.Instance.Warning("[VPinballScreenLayout] No screen available for the DMD, using " + location + ".");
                }
            }

            if (location == VPinballDmdLocation.Backglass && !ret.HasBackglass)
            {
                location = VPinballDmdLocation.Playfield;
                SimpleLogger.Instance.Warning("[VPinballScreenLayout] No backglass screen for the DMD, using Playfield.");
            }

            ret.DmdLocation = location;

            // DMD rectangles
            switch (location)
            {
                case VPinballDmdLocation.Playfield:
                    ret.DmdArea = GetPlayfieldDmdArea(ret.Playfield);
                    ret.DmdWindow = ret.DmdArea;
                    break;

                case VPinballDmdLocation.Backglass:
                    {
                        // Backglass image on the top part, DMD band at the bottom of the same screen
                        Rectangle bg = ret.BackglassScreenBounds;
                        int band = (int)Math.Round(bg.Height * BackglassDmdBandRatio);

                        ret.Backglass = new Rectangle(bg.X, bg.Y, bg.Width, bg.Height - band);
                        ret.DmdArea = new Rectangle(bg.X, bg.Bottom - band, bg.Width, band);
                        ret.DmdWindow = FitAspect(ret.DmdArea, DmdAspectRatio);
                    }
                    break;

                case VPinballDmdLocation.Screen:
                    ret.DmdArea = GetBounds(ret.DmdScreen);
                    ret.DmdWindow = FitAspect(ret.DmdArea, DmdAspectRatio);
                    break;

                default:
                    ret.DmdArea = Rectangle.Empty;
                    ret.DmdWindow = Rectangle.Empty;
                    break;
            }

            ret.Log();
            return ret;
        }

        /// <summary>Playfield screen : same rule as the VPX Display setting (MonitorIndex target, else primary).</summary>
        public static Screen GetPlayfieldScreen()
        {
            return Program.IsMonitorIndexSet && Program.TargetScreen != null ? Program.TargetScreen : Screen.PrimaryScreen;
        }

        /// <summary>Position of a rectangle relative to the top left corner of another one (ScreenRes.txt offsets).</summary>
        public static Point RelativeTo(Rectangle rect, Rectangle origin)
        {
            return new Point(rect.X - origin.X, rect.Y - origin.Y);
        }

        /// <summary>Live bounds (valid right after a mode change), Screen.Bounds as fallback.</summary>
        public static Rectangle GetBounds(Screen screen)
        {
            if (screen == null)
                return Rectangle.Empty;

            Rectangle? live = ScreenResolution.GetDisplayBounds(screen.DeviceName);
            if (live.HasValue)
                return live.Value;

            SimpleLogger.Instance.Warning("[VPinballScreenLayout] Live bounds not available for " + screen.DeviceName + ", using cached bounds.");
            return screen.Bounds;
        }

        private static Screen PickScreen(string index, Screen[] screens, List<Screen> candidates, bool largest, string role)
        {
            if (candidates.Count == 0)
                return null;

            int n;
            if (!string.IsNullOrEmpty(index) && int.TryParse(index, out n))
            {
                Screen chosen = (n >= 0 && n < screens.Length) ? candidates.FirstOrDefault(c => c.DeviceName == screens[n].DeviceName) : null;
                if (chosen != null)
                    return chosen;

                SimpleLogger.Instance.Warning("[VPinballScreenLayout] Screen " + n + " can't be used for the " + role + " (out of range or already used), using automatic choice.");
            }

            // OrderBy is stable : on equal sizes, the SDL order (same as MonitorIndex) is kept
            if (largest)
                return candidates.OrderByDescending(c => Area(GetBounds(c))).First();

            return candidates.OrderBy(c => Area(GetBounds(c))).First();
        }

        private static long Area(Rectangle r)
        {
            return (long)r.Width * r.Height;
        }

        /// <summary>DMD on the playfield screen : top right corner in landscape (desktop view), top center in portrait (full single screen view).</summary>
        private static Rectangle GetPlayfieldDmdArea(Rectangle pf)
        {
            bool portrait = pf.Height > pf.Width;

            int width = (int)Math.Round(pf.Width * (portrait ? PlayfieldDmdWidthPortrait : PlayfieldDmdWidthLandscape));
            int height = (int)Math.Round(width / DmdAspectRatio);
            int x = portrait ? pf.X + (pf.Width - width) / 2 : pf.Right - width;

            return new Rectangle(x, pf.Y, width, height);
        }

        /// <summary>Largest rectangle with the given aspect ratio, centered in area.</summary>
        private static Rectangle FitAspect(Rectangle area, double ratio)
        {
            int width = area.Width;
            int height = (int)Math.Round(width / ratio);

            if (height > area.Height)
            {
                height = area.Height;
                width = (int)Math.Round(height * ratio);
            }

            return new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
        }

        private void Log()
        {
            SimpleLogger.Instance.Info("[VPinballScreenLayout] " + ScreenCount + " screen(s), playfield " + PlayfieldScreen.DeviceName + " " + Playfield);

            if (HasBackglass)
                SimpleLogger.Instance.Info("[VPinballScreenLayout] Backglass " + BackglassScreen.DeviceName + " " + Backglass);

            SimpleLogger.Instance.Info("[VPinballScreenLayout] DMD " + DmdLocation +
                (DmdScreen != null ? " " + DmdScreen.DeviceName : "") +
                (DmdLocation != VPinballDmdLocation.None ? " area " + DmdArea + " window " + DmdWindow : ""));
        }
    }
}
