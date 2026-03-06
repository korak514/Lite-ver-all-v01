// Services/SmartLabelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public static class SmartLabelService
    {
        // =========================================================================
        // 🎛️ FINE-TUNING CONSTANTS (Preserved exactly as you set them)
        // =========================================================================
        public static class Config
        {
            public static double PeakGapY = 1;
            public static double ValleyGapY = 16;
            public static double DefaultGapY = 6;

            public static double SteepRiseLift = 0;
            public static double SteepFallLift = 12;

            public static double PeakOffsetX = 0;
            public static double PeakOffsetY = 0;
            public static double ValleyOffsetX = 0;
            public static double ValleyOffsetY = 0;

            public static double GapX = 4;

            public static double FarGapY_Add = 12;
            public static double FarGapX_Add = 10;

            public static double DiagonalSnugY = -2;
            public static double DiagonalSnugX = 2;

            public static double FirstLabelOffsetX = 0;
            public static double FirstLabelOffsetY = 0;

            public static double LastLabelOffsetX = 6;
            public static double LastLabelOffsetY = 0;

            public static double CharWidth = 7.5;
            public static double BoxPaddingX = 16;
            public static double BoxHeight = 38;

            public static double CollisionPadX = 2;
            public static double CollisionPadY = 1;

            public static double SteepSlope = 0.10;
            public static double FlatSlope = 0.08;

            public static double VirtualWidthPerPoint = 85.0;
            public static double VirtualHeight = 450.0;
        }

        // =========================================================================
        // 🛠️ ALGORITHM IMPLEMENTATION
        // =========================================================================

        private enum Dir
        {
            N, S, E, W, NE, NW, SE, SW,
            Far_N, Far_S, Far_NE, Far_NW, Far_SE, Far_SW
        }

        private class Rect
        {
            public double X, Y, W, H;

            public Rect(double x, double y, double w, double h)
            { X = x; Y = y; W = w; H = h; }

            public bool Intersects(Rect o)
            {
                return X - Config.CollisionPadX < o.X + o.W && X + W + Config.CollisionPadX > o.X &&
                       Y - Config.CollisionPadY < o.Y + o.H && Y + H + Config.CollisionPadY > o.Y;
            }
        }

        private class LabelPlacement
        {
            public int Index { get; set; }
            public Rect Bounds { get; set; }
            public double Dx { get; set; }
            public double Dy { get; set; }
            public bool Show { get; set; }
            public bool IsFar { get; set; }
        }

        public static void ApplyLabels(List<DashboardDataPoint> points, bool isDateAxis, List<string> xAxisLabels, string seriesType)
        {
            if (points == null || points.Count == 0) return;

            int count = points.Count;
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);
            double rangeY = maxY - minY == 0 ? 1 : maxY - minY;

            bool[] isPeakArray = new bool[count];
            bool[] isValleyArray = new bool[count];
            int[] importance = new int[count];

            // 1. Format Labels & Establish Extremes
            for (int i = 0; i < count; i++)
            {
                var p = points[i];
                string vStr = FormatKiloMega(p.Y);
                string dStr = isDateAxis
                    ? new DateTime((long)p.X).ToString("MMM yyyy")
                    : (xAxisLabels != null && p.X >= 0 && p.X < xAxisLabels.Count ? xAxisLabels[(int)p.X] : "");

                p.Label = string.IsNullOrEmpty(dStr?.Trim()) ? vStr : $"{vStr}\n{dStr.Trim()}";
                p.ShowLabel = false;
                p.IsImportant = true;

                double curr = p.Y;
                double prev = i > 0 ? points[i - 1].Y : curr;
                double next = i < count - 1 ? points[i + 1].Y : curr;

                isPeakArray[i] = (curr >= prev && curr >= next) && (curr > prev || curr > next);
                isValleyArray[i] = (curr <= prev && curr <= next) && (curr < prev || curr < next);
            }

            // 2. Assign Importance Scores
            for (int i = 0; i < count; i++)
            {
                importance[i] = 50;
                if (isPeakArray[i] || isValleyArray[i]) importance[i] = 80;
                if (points[i].Y == maxY || points[i].Y == minY) importance[i] = 90;
                if (i == 0 || i == count - 1) importance[i] = 100;
            }

            var sortedIndices = Enumerable.Range(0, count).OrderByDescending(i => importance[i]).ToList();
            List<LabelPlacement> placements = new List<LabelPlacement>();

            double VIRTUAL_WIDTH = Math.Max(800.0, count * Config.VirtualWidthPerPoint);
            double VIRTUAL_HEIGHT = Config.VirtualHeight;
            double steepThresholdValue = rangeY * Config.SteepSlope;

            // 3. Global Overlap Engine
            foreach (int i in sortedIndices)
            {
                var p = points[i];
                double curr = p.Y;
                double prev = i > 0 ? points[i - 1].Y : curr;
                double next = i < count - 1 ? points[i + 1].Y : curr;

                bool isPeak = isPeakArray[i];
                bool isValley = isValleyArray[i];

                double slopeIn = curr - prev;
                double slopeOut = next - curr;

                bool isSharpRise = (slopeIn > steepThresholdValue || slopeOut > steepThresholdValue);
                bool isSharpFall = (slopeIn < -steepThresholdValue || slopeOut < -steepThresholdValue);

                int maxCharCount = 5;
                if (!string.IsNullOrEmpty(p.Label))
                {
                    var lines = p.Label.Split('\n');
                    maxCharCount = lines.Max(l => l.Length);
                }

                double w = (maxCharCount * Config.CharWidth) + Config.BoxPaddingX;
                double h = Config.BoxHeight;

                double px = count > 1 ? (i / (double)(count - 1)) * VIRTUAL_WIDTH : VIRTUAL_WIDTH / 2;
                double py = rangeY > 0 ? VIRTUAL_HEIGHT - (((curr - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2;

                double prevPx = i > 0 ? ((i - 1) / (double)(count - 1)) * VIRTUAL_WIDTH : px;
                double prevPy = i > 0 ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[i - 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : py;

                double nextPx = i < count - 1 ? ((i + 1) / (double)(count - 1)) * VIRTUAL_WIDTH : px;
                double nextPy = i < count - 1 ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[i + 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : py;

                List<Dir> candidates = GetCandidates(i, count, curr, prev, next, isPeak, isValley, seriesType, rangeY);
                var placement = new LabelPlacement { Index = i, Show = false };

                foreach (Dir dir in candidates)
                {
                    GetOffsets(dir, w, h, isPeak, isValley, isSharpRise, isSharpFall, out double dx, out double dy);
                    Rect proposedBox = new Rect(px + dx, py + dy, w, h);

                    bool collidesLabels = placements.Any(placed => placed.Show && placed.Bounds.Intersects(proposedBox));

                    // [IMPROVEMENT 3] Only calculate lines if they actually exist
                    bool collidesLine = false;
                    if (i > 0) collidesLine |= LineIntersectsRect(prevPx, prevPy, px, py, proposedBox);
                    if (i < count - 1) collidesLine |= LineIntersectsRect(px, py, nextPx, nextPy, proposedBox);

                    if (!collidesLabels && !collidesLine)
                    {
                        placement.Dx = dx;
                        placement.Dy = dy;
                        placement.Bounds = proposedBox;
                        placement.Show = true;
                        placement.IsFar = dir.ToString().StartsWith("Far_");
                        break;
                    }
                }

                if (!placement.Show && importance[i] >= 80)
                {
                    GetOffsets(candidates[0], w, h, isPeak, isValley, isSharpRise, isSharpFall, out double dx, out double dy, forceFar: true);
                    placement.Dx = dx;
                    placement.Dy = dy;
                    placement.Bounds = new Rect(px + dx, py + dy, w, h);
                    placement.Show = true;
                    placement.IsFar = true;
                }

                if (placement.Show)
                {
                    if (i == 0)
                    {
                        placement.Dx += Config.FirstLabelOffsetX;
                        placement.Dy += Config.FirstLabelOffsetY;
                        placement.Bounds.X = px + placement.Dx;
                        placement.Bounds.Y = py + placement.Dy;
                    }
                    else if (i == count - 1)
                    {
                        double rightEdge = px + placement.Dx + w;
                        double screenLimit = VIRTUAL_WIDTH - 5;
                        if (rightEdge > screenLimit) placement.Dx -= (rightEdge - screenLimit);
                        placement.Dx += Config.LastLabelOffsetX;
                        placement.Dy += Config.LastLabelOffsetY;
                        placement.Bounds.X = px + placement.Dx;
                        placement.Bounds.Y = py + placement.Dy;
                    }
                }

                placements.Add(placement);
            }

            // 4. Post-Placement Micro-Adjustments
            // [IMPROVEMENT 2] Added 0. This allows labels to slide on only one axis if needed.
            double[] shifts = new double[] { 0, -2, 2, -4, 4, -8, 8 };
            int maxPasses = 3;

            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool anyOverlap = false;
                foreach (var current in placements)
                {
                    if (!current.Show) continue;
                    bool isOverlapping = placements.Any(o => o != current && o.Show && current.Bounds.Intersects(o.Bounds));
                    if (isOverlapping)
                    {
                        anyOverlap = true;
                        bool resolved = false;

                        int idx = current.Index;
                        double currY = points[idx].Y;
                        double cx = count > 1 ? (idx / (double)(count - 1)) * VIRTUAL_WIDTH : VIRTUAL_WIDTH / 2;
                        double cy = rangeY > 0 ? VIRTUAL_HEIGHT - (((currY - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2;

                        double prevPx = idx > 0 ? ((idx - 1) / (double)(count - 1)) * VIRTUAL_WIDTH : cx;
                        double prevPy = idx > 0 ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[idx - 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : cy;

                        double nextPx = idx < count - 1 ? ((idx + 1) / (double)(count - 1)) * VIRTUAL_WIDTH : cx;
                        double nextPy = idx < count - 1 ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[idx + 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : cy;

                        foreach (double sx in shifts)
                        {
                            foreach (double sy in shifts)
                            {
                                if (sx == 0 && sy == 0) continue;

                                Rect testBounds = new Rect(current.Bounds.X + sx, current.Bounds.Y + sy, current.Bounds.W, current.Bounds.H);

                                // [IMPROVEMENT 1] Boundary Safety. Prevent shifting off screen.
                                if (testBounds.X < 0 || testBounds.X + testBounds.W > VIRTUAL_WIDTH + 20) continue;

                                bool stillOverlaps = placements.Any(o => o != current && o.Show && testBounds.Intersects(o.Bounds));

                                bool hitLine = false;
                                if (idx > 0) hitLine |= LineIntersectsRect(prevPx, prevPy, cx, cy, testBounds);
                                if (idx < count - 1) hitLine |= LineIntersectsRect(cx, cy, nextPx, nextPy, testBounds);

                                if (!stillOverlaps && !hitLine)
                                {
                                    current.Bounds = testBounds;
                                    current.Dx += sx;
                                    current.Dy += sy;
                                    resolved = true;
                                    break;
                                }
                            }
                            if (resolved) break;
                        }
                    }
                }
                if (!anyOverlap) break;
            }

            // 5. Apply Values
            foreach (var pm in placements)
            {
                var p = points[pm.Index];
                p.LabelDx = pm.Dx;
                p.LabelDy = pm.Dy;
                p.ShowLabel = pm.Show;
                p.HasLeaderLine = false;
            }

            points.OrderBy(p => p.X);
        }

        private static bool LineIntersectsRect(double x1, double y1, double x2, double y2, Rect r)
        {
            return LineIntersectsLine(x1, y1, x2, y2, r.X, r.Y, r.X + r.W, r.Y) ||
                   LineIntersectsLine(x1, y1, x2, y2, r.X + r.W, r.Y, r.X + r.W, r.Y + r.H) ||
                   LineIntersectsLine(x1, y1, x2, y2, r.X + r.W, r.Y + r.H, r.X, r.Y + r.H) ||
                   LineIntersectsLine(x1, y1, x2, y2, r.X, r.Y + r.H, r.X, r.Y) ||
                   (r.X < x1 && x1 < r.X + r.W && r.Y < y1 && y1 < r.Y + r.H) ||
                   (r.X < x2 && x2 < r.X + r.W && r.Y < y2 && y2 < r.Y + r.H);
        }

        private static bool LineIntersectsLine(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
        {
            double den = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            if (den == 0) return false;
            double ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / den;
            double ub = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / den;
            return (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1);
        }

        private static List<Dir> GetCandidates(int i, int count, double curr, double prev, double next, bool isPeak, bool isValley, string seriesType, double rangeY)
        {
            var list = new List<Dir>();

            if (seriesType != "Line")
            {
                list.Add(Dir.N); list.Add(Dir.Far_N);
                return list;
            }

            if (i == 0)
            {
                if (next > curr) list.AddRange(new[] { Dir.SE, Dir.E, Dir.S, Dir.NE, Dir.N });
                else list.AddRange(new[] { Dir.NE, Dir.E, Dir.N, Dir.SE, Dir.S });
                return list;
            }

            if (i == count - 1)
            {
                if (prev > curr) list.AddRange(new[] { Dir.S, Dir.Far_S, Dir.SW, Dir.Far_SW, Dir.N });
                else if (prev < curr) list.AddRange(new[] { Dir.NW, Dir.W, Dir.N, Dir.SW, Dir.S });
                else list.AddRange(new[] { Dir.N, Dir.S, Dir.Far_N, Dir.Far_S });
                return list;
            }

            double slopeIn = curr - prev;
            double slopeOut = next - curr;
            double steepThresh = rangeY * Config.SteepSlope;
            double flatThresh = rangeY * Config.FlatSlope;

            bool isDiveToFlat = slopeIn < -steepThresh && Math.Abs(slopeOut) < flatThresh;
            bool isRiseToFlat = slopeIn > steepThresh && Math.Abs(slopeOut) < flatThresh;

            bool isFlatToRise = Math.Abs(slopeIn) < flatThresh && slopeOut > steepThresh;
            bool isValleyToSteepRise = isValley && slopeOut > steepThresh;

            bool isFlatToDive = Math.Abs(slopeIn) < flatThresh && slopeOut < -steepThresh;

            if (isDiveToFlat)
            {
                list.AddRange(new[] { Dir.S, Dir.SE, Dir.Far_S });
                return list;
            }
            if (isRiseToFlat)
            {
                list.AddRange(new[] { Dir.N, Dir.NW, Dir.NE, Dir.Far_N });
                return list;
            }

            if (isFlatToRise || isValleyToSteepRise)
            {
                list.AddRange(new[] { Dir.SE, Dir.E, Dir.S, Dir.Far_SE });
                return list;
            }

            if (isFlatToDive)
            {
                list.AddRange(new[] { Dir.NE, Dir.N, Dir.E, Dir.Far_NE });
                return list;
            }

            if (isPeak)
            {
                list.AddRange(new[] { Dir.N, Dir.NW, Dir.NE, Dir.Far_N });
                return list;
            }
            if (isValley)
            {
                list.AddRange(new[] { Dir.S, Dir.SE, Dir.SW, Dir.Far_S });
                return list;
            }

            if (slopeIn > 0 && slopeOut > 0)
            {
                list.AddRange(new[] { Dir.N, Dir.NW, Dir.SE, Dir.S, Dir.E, Dir.Far_SE });
            }
            else if (slopeIn < 0 && slopeOut < 0)
            {
                list.AddRange(new[] { Dir.N, Dir.NE, Dir.SW, Dir.S, Dir.Far_NE });
            }
            else
            {
                list.AddRange(new[] { Dir.N, Dir.S, Dir.NE, Dir.SE, Dir.NW, Dir.SW });
            }

            return list;
        }

        private static void GetOffsets(Dir dir, double w, double h, bool isPeak, bool isValley, bool isSharpRise, bool isSharpFall, out double dx, out double dy, bool forceFar = false)
        {
            double gY = isPeak ? Config.PeakGapY : (isValley ? Config.ValleyGapY : Config.DefaultGapY);
            double gX = Config.GapX;

            if (forceFar || dir.ToString().StartsWith("Far_"))
            {
                gX += Config.FarGapX_Add;
                gY += Config.FarGapY_Add;
                string cleanDir = dir.ToString().Replace("Far_", "");
                dir = (Dir)Enum.Parse(typeof(Dir), cleanDir);
            }

            if (isSharpRise) gY += Config.SteepRiseLift;
            else if (isSharpFall) gY += Config.SteepFallLift;

            switch (dir)
            {
                case Dir.N: dx = (-w / 2) + (isPeak ? Config.PeakOffsetX : 0); dy = -h - gY + (isPeak ? Config.PeakOffsetY : 0); break;
                case Dir.S: dx = (-w / 2) + (isValley ? Config.ValleyOffsetX : 0); dy = gY + (isValley ? Config.ValleyOffsetY : 0); break;
                case Dir.E: dx = gX; dy = -h / 2; break;
                case Dir.W: dx = -w - gX; dy = -h / 2; break;
                case Dir.NE: dx = gX - Config.DiagonalSnugX; dy = -h - gY + Config.DiagonalSnugY; break;
                case Dir.NW: dx = -w - gX + Config.DiagonalSnugX; dy = -h - gY + Config.DiagonalSnugY; break;
                case Dir.SE: dx = gX - Config.DiagonalSnugX; dy = gY - Config.DiagonalSnugY; break;
                case Dir.SW: dx = -w - gX + Config.DiagonalSnugX; dy = gY - Config.DiagonalSnugY; break;
                default: dx = -w / 2; dy = -h - gY; break;
            }
        }

        public static string FormatKiloMega(double value)
        {
            double abs = Math.Abs(value);
            if (abs >= 1_000_000) return (value / 1_000_000D).ToString("0.##") + "M";
            if (abs >= 10_000) return (value / 1_000D).ToString("0.##") + "K";
            if (value % 1 == 0) return value.ToString("N0");
            return value.ToString("N2");
        }
    }
}