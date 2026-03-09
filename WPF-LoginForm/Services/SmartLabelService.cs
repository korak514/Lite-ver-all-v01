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
        // 🎛️ FINE-TUNING CONSTANTS
        // =========================================================================
        public static class Config
        {
            public static double PeakGapY = 8;
            public static double ValleyGapY = 16;

            public static double DefaultTopGapY = 6;
            public static double DefaultBottomGapY = 8;

            public static double PeakOffsetX = 0;
            public static double PeakOffsetY = 0;
            public static double ValleyOffsetX = 0;
            public static double ValleyOffsetY = 0;

            public static double GapX = 12;

            public static double FarGapY_Add = 12;
            public static double FarGapX_Add = 10;

            public static double DiagonalSnugY = 6;
            public static double DiagonalSnugX = 8;

            public static double FirstLabelOffsetX = 12;
            public static double FirstLabelOffsetY = 0;

            public static double LastLabelOffsetX = 12;
            public static double LastLabelOffsetY = 0;

            public static double CharWidth = 7.5;
            public static double BoxPaddingX = 16;
            public static double BoxHeight = 38;

            public static double CollisionPadX = 4;
            public static double CollisionPadY = 2;

            public static double SteepSlope = 0.07;
            public static double FlatSlope = 0.05;

            public static double VirtualWidthPerPoint = 75.0;
            public static double VirtualHeight = 450.0;

            // Constants for Bar Chart Standard Labels
            public static double BarLabelGapY = 10.0;
        }

        // =========================================================================
        // 🛠️ ALGORITHM IMPLEMENTATION
        // =========================================================================

        private enum Dir
        {
            N, S, E, W, NE, NW, SE, SW,
            Far_N, Far_S, Far_NE, Far_NW, Far_SE, Far_SW, Far_E, Far_W
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
            // SAFETY CHECK: Ensure points list is not null or empty
            if (points == null || points.Count == 0) return;

            // =========================================================================
            // 🚫 BYPASS SMART LABELS FOR BAR/COLUMN CHARTS
            // =========================================================================
            if (string.Equals(seriesType, "Bar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(seriesType, "Column", StringComparison.OrdinalIgnoreCase))
            {
                int count = points.Count;

                // Rule 1: Only use labels if there are more than 6 bars
                if (count <= 6)
                {
                    foreach (var p in points)
                    {
                        if (p != null) p.ShowLabel = false; // Safety check
                    }
                    return;
                }

                // Find indices for Max and Min
                int maxIdx = 0;
                int minIdx = 0;
                double maxVal = double.MinValue;
                double minVal = double.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    var p = points[i];
                    // SAFETY CHECK: Ensure point is not null
                    if (p == null) continue;

                    double val = p.Y;
                    if (val > maxVal) { maxVal = val; maxIdx = i; }
                    if (val < minVal) { minVal = val; minIdx = i; }
                }

                // Create list of unique indices to label
                HashSet<int> indicesToLabel = new HashSet<int>();

                // Add First and Last
                indicesToLabel.Add(0);
                indicesToLabel.Add(count - 1);

                // Add Max and Min
                indicesToLabel.Add(maxIdx);
                indicesToLabel.Add(minIdx);

                // Apply Labels
                for (int i = 0; i < count; i++)
                {
                    var p = points[i];

                    // SAFETY CHECK: Ensure point is not null
                    if (p == null) continue;

                    // Check if this index is in our list AND value is not 0
                    if (indicesToLabel.Contains(i) && p.Y != 0)
                    {
                        p.Label = FormatKiloMega(p.Y);
                        double w = (p.Label.Length * Config.CharWidth) + Config.BoxPaddingX;

                        p.LabelDx = -w / 2.0; // Center horizontally
                        p.LabelDy = -Config.BoxHeight - Config.BarLabelGapY; // Position above bar

                        p.ShowLabel = true;
                        p.HasLeaderLine = false;
                    }
                    else
                    {
                        p.ShowLabel = false;
                    }
                }

                return; // Exit immediately
            }

            // =========================================================================
            // 📈 LINE CHART SMART LABEL LOGIC (CONTINUES BELOW)
            // =========================================================================

            int countLine = points.Count;

            // SAFETY CHECK: Filter out null points for calculations
            var validPoints = points.Where(p => p != null).ToList();
            if (validPoints.Count == 0) return;

            double minY = validPoints.Min(p => p.Y);
            double maxY = validPoints.Max(p => p.Y);
            double rangeY = maxY - minY == 0 ? 1 : maxY - minY;

            double minX = validPoints.Min(p => p.X);
            double maxX = validPoints.Max(p => p.X);
            double rangeX = maxX - minX == 0 ? 1 : maxX - minX;

            bool[] isPeakArray = new bool[countLine];
            bool[] isValleyArray = new bool[countLine];
            int[] importance = new int[countLine];

            for (int i = 0; i < countLine; i++)
            {
                var p = points[i];
                if (p == null) continue; // Safety check

                string vStr = FormatKiloMega(p.Y);
                string dStr = isDateAxis
                    ? new DateTime((long)p.X).ToString("MMM yyyy")
                    : (xAxisLabels != null && p.X >= 0 && p.X < xAxisLabels.Count ? xAxisLabels[(int)p.X] : "");

                p.Label = string.IsNullOrEmpty(dStr?.Trim()) ? vStr : $"{vStr}\n{dStr.Trim()}";
                p.ShowLabel = false;
                p.IsImportant = true;

                double curr = p.Y;
                double prev = i > 0 && points[i - 1] != null ? points[i - 1].Y : curr;
                double next = i < countLine - 1 && points[i + 1] != null ? points[i + 1].Y : curr;

                isPeakArray[i] = (curr >= prev && curr >= next) && (curr > prev || curr > next);
                isValleyArray[i] = (curr <= prev && curr <= next) && (curr < prev || curr < next);
            }

            for (int i = 0; i < countLine; i++)
            {
                if (points[i] == null) continue;

                importance[i] = 50;
                if (isPeakArray[i] || isValleyArray[i]) importance[i] = 80;
                if (points[i].Y == maxY || points[i].Y == minY) importance[i] = 90;
                if (i == 0 || i == countLine - 1) importance[i] = 100;
            }

            var sortedIndices = Enumerable.Range(0, countLine).OrderByDescending(i => importance[i]).ToList();
            List<LabelPlacement> placements = new List<LabelPlacement>();

            double VIRTUAL_WIDTH = Math.Max(800.0, countLine * Config.VirtualWidthPerPoint);
            double VIRTUAL_HEIGHT = Config.VirtualHeight;
            double steepThresholdValue = rangeY * Config.SteepSlope;
            double flatThresholdValue = rangeY * Config.FlatSlope;

            foreach (int i in sortedIndices)
            {
                var p = points[i];
                if (p == null) continue; // Safety check

                double curr = p.Y;
                double prev = i > 0 && points[i - 1] != null ? points[i - 1].Y : curr;
                double next = i < countLine - 1 && points[i + 1] != null ? points[i + 1].Y : curr;

                bool isPeak = isPeakArray[i];
                bool isValley = isValleyArray[i];

                double slopeIn = curr - prev;
                double slopeOut = next - curr;

                bool isSharpRise = (slopeIn > steepThresholdValue || slopeOut > steepThresholdValue);
                bool isSharpFall = (slopeIn < -steepThresholdValue || slopeOut < -steepThresholdValue);

                bool isDiveToFlat = slopeIn < -steepThresholdValue && Math.Abs(slopeOut) < flatThresholdValue;
                bool isRiseToFlat = slopeIn > steepThresholdValue && Math.Abs(slopeOut) < flatThresholdValue;
                bool isFlatToDive = Math.Abs(slopeIn) < flatThresholdValue && slopeOut < -steepThresholdValue;

                int maxCharCount = 5;
                if (!string.IsNullOrEmpty(p.Label))
                {
                    var lines = p.Label.Split('\n');
                    maxCharCount = lines.Max(l => l.Length);
                }

                double w = (maxCharCount * Config.CharWidth) + Config.BoxPaddingX;
                double h = Config.BoxHeight;

                double px = countLine > 1 ? ((points[i].X - minX) / rangeX) * VIRTUAL_WIDTH : VIRTUAL_WIDTH / 2;
                double py = rangeY > 0 ? VIRTUAL_HEIGHT - (((curr - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2;

                double prevPx = i > 0 && points[i - 1] != null ? ((points[i - 1].X - minX) / rangeX) * VIRTUAL_WIDTH : px;
                double prevPy = i > 0 && points[i - 1] != null ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[i - 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : py;

                double nextPx = i < countLine - 1 && points[i + 1] != null ? ((points[i + 1].X - minX) / rangeX) * VIRTUAL_WIDTH : px;
                double nextPy = i < countLine - 1 && points[i + 1] != null ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[i + 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : py;

                List<Dir> candidates = GetCandidates(i, countLine, curr, prev, next, isPeak, isValley, seriesType, rangeY);
                var placement = new LabelPlacement { Index = i, Show = false };

                foreach (Dir dir in candidates)
                {
                    GetOffsets(dir, w, h, isPeak, isValley, isDiveToFlat, isFlatToDive, isSharpRise, out double dx, out double dy);
                    Rect proposedBox = new Rect(px + dx, py + dy, w, h);

                    bool collidesLabels = placements.Any(placed => placed.Show && placed.Bounds.Intersects(proposedBox));
                    Rect coreBox = new Rect(proposedBox.X - 1, proposedBox.Y - 1, proposedBox.W + 2, proposedBox.H + 2);

                    bool collidesLine = false;
                    if (i > 0) collidesLine |= LineIntersectsRect(prevPx, prevPy, px, py, coreBox);
                    if (i < countLine - 1) collidesLine |= LineIntersectsRect(px, py, nextPx, nextPy, coreBox);

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

                if (!placement.Show)
                {
                    Dir[] emergencyDirs = { Dir.S, Dir.N, Dir.SE, Dir.NE, Dir.E };
                    foreach (Dir edir in emergencyDirs)
                    {
                        GetOffsets(edir, w, h, isPeak, isValley, isDiveToFlat, isFlatToDive, isSharpRise, out double dx, out double dy, forceFar: true);
                        Rect proposedBox = new Rect(px + dx, py + dy, w, h);

                        bool collidesLabels = placements.Any(placed => placed.Show && placed.Bounds.Intersects(proposedBox));
                        if (!collidesLabels)
                        {
                            placement.Dx = dx;
                            placement.Dy = dy;
                            placement.Bounds = proposedBox;
                            placement.Show = true;
                            placement.IsFar = true;
                            break;
                        }
                    }

                    if (!placement.Show)
                    {
                        GetOffsets(Dir.S, w, h, isPeak, isValley, isDiveToFlat, isFlatToDive, isSharpRise, out double dx, out double dy, forceFar: true);
                        placement.Dx = dx;
                        placement.Dy = dy;
                        placement.Bounds = new Rect(px + dx, py + dy, w, h);
                        placement.Show = true;
                    }
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

                    double leftEdge = px + placement.Dx;
                    double rightEdge = px + placement.Dx + w;

                    if (leftEdge < 5)
                    {
                        placement.Dx += (5 - leftEdge);
                    }
                    else if (rightEdge > VIRTUAL_WIDTH - 5)
                    {
                        placement.Dx -= (rightEdge - (VIRTUAL_WIDTH - 5));
                    }

                    placement.Bounds.X = px + placement.Dx;
                    placement.Bounds.Y = py + placement.Dy;
                }

                placements.Add(placement);
            }

            // 4. POST-PLACEMENT AGGRESSIVE ZIPPER
            double[] shiftsX = new double[] { 0, -10, 10, -20, 20 };
            double[] shiftsY = new double[] { 0, -5, 5 };
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

                        // Safety check for index bounds
                        if (idx < 0 || idx >= countLine || points[idx] == null) continue;

                        double currY = points[idx].Y;
                        double cx = countLine > 1 ? ((points[idx].X - minX) / rangeX) * VIRTUAL_WIDTH : VIRTUAL_WIDTH / 2;
                        double cy = rangeY > 0 ? VIRTUAL_HEIGHT - (((currY - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2;

                        double prevPx = idx > 0 && points[idx - 1] != null ? ((points[idx - 1].X - minX) / rangeX) * VIRTUAL_WIDTH : cx;
                        double prevPy = idx > 0 && points[idx - 1] != null ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[idx - 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : cy;
                        double nextPx = idx < countLine - 1 && points[idx + 1] != null ? ((points[idx + 1].X - minX) / rangeX) * VIRTUAL_WIDTH : cx;
                        double nextPy = idx < countLine - 1 && points[idx + 1] != null ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[idx + 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : cy;

                        foreach (double sx in shiftsX)
                        {
                            foreach (double sy in shiftsY)
                            {
                                if (sx == 0 && sy == 0) continue;

                                Rect testBounds = new Rect(current.Bounds.X + sx, current.Bounds.Y + sy, current.Bounds.W, current.Bounds.H);
                                if (testBounds.X < 5 || testBounds.X + testBounds.W > VIRTUAL_WIDTH - 5) continue;

                                bool stillOverlaps = placements.Any(o => o != current && o.Show && testBounds.Intersects(o.Bounds));
                                Rect coreBox = new Rect(testBounds.X - 1, testBounds.Y - 1, testBounds.W + 2, testBounds.H + 2);

                                bool hitLine = false;
                                if (idx > 0) hitLine |= LineIntersectsRect(prevPx, prevPy, cx, cy, coreBox);
                                if (idx < countLine - 1) hitLine |= LineIntersectsRect(cx, cy, nextPx, nextPy, coreBox);

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

            foreach (var pm in placements)
            {
                if (pm.Index >= 0 && pm.Index < countLine && points[pm.Index] != null)
                {
                    var p = points[pm.Index];
                    p.LabelDx = pm.Dx;
                    p.LabelDy = pm.Dy;
                    p.ShowLabel = pm.Show;
                    p.HasLeaderLine = false;
                }
            }
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
                if (next > curr) list.AddRange(new[] { Dir.N, Dir.S, Dir.SE, Dir.E, Dir.NE });
                else list.AddRange(new[] { Dir.N, Dir.S, Dir.NE, Dir.E, Dir.SE });
                return list;
            }

            if (i == count - 1)
            {
                if (prev > curr) list.AddRange(new[] { Dir.N, Dir.S, Dir.Far_S, Dir.SW, Dir.Far_SW });
                else if (prev < curr) list.AddRange(new[] { Dir.N, Dir.S, Dir.NW, Dir.W, Dir.SW });
                else list.AddRange(new[] { Dir.N, Dir.S, Dir.Far_N, Dir.Far_S });
                return list;
            }

            double slopeIn = curr - prev;
            double slopeOut = next - curr;
            double steepThresh = rangeY * Config.SteepSlope;
            double flatThresh = rangeY * Config.FlatSlope;

            bool isDiveToFlat = slopeIn < -steepThresh && Math.Abs(slopeOut) < flatThresh;
            bool isRiseToFlat = slopeIn > steepThresh && Math.Abs(slopeOut) < flatThresh;
            bool isFlatToDive = Math.Abs(slopeIn) < flatThresh && slopeOut < -steepThresh;
            bool isPlateau = Math.Abs(slopeIn) < flatThresh && Math.Abs(slopeOut) < flatThresh;

            bool isSteepFall = slopeIn < -steepThresh && slopeOut < -steepThresh;
            bool isGentleFall = slopeIn < 0 && slopeOut < 0 && !isSteepFall;

            bool isSteepRise = slopeIn > steepThresh && slopeOut > steepThresh;
            bool isGentleRise = slopeIn > 0 && slopeOut > 0 && !isSteepRise;

            if (isPlateau)
            {
                list.AddRange(new[] { Dir.N, Dir.S, Dir.NE, Dir.SE, Dir.NW, Dir.SW });
                return list;
            }
            if (isDiveToFlat)
            {
                list.AddRange(new[] { Dir.N, Dir.S, Dir.SE, Dir.Far_N });
                return list;
            }
            if (isRiseToFlat)
            {
                list.AddRange(new[] { Dir.N, Dir.NW, Dir.SE, Dir.S, Dir.Far_N });
                return list;
            }
            if (isFlatToDive)
            {
                list.AddRange(new[] { Dir.N, Dir.NW, Dir.NE, Dir.E });
                return list;
            }

            if (isPeak)
            {
                list.AddRange(new[] { Dir.N, Dir.NW, Dir.NE, Dir.Far_N });
                return list;
            }
            if (isValley)
            {
                list.AddRange(new[] { Dir.S, Dir.SE, Dir.SW, Dir.N, Dir.Far_S });
                return list;
            }

            if (isSteepRise)
            {
                list.AddRange(new[] { Dir.E, Dir.SE, Dir.S, Dir.N });
            }
            else if (isGentleRise)
            {
                list.AddRange(new[] { Dir.N, Dir.S, Dir.SE, Dir.NE, Dir.E });
            }
            else if (isSteepFall)
            {
                list.AddRange(new[] { Dir.S, Dir.NE, Dir.E, Dir.N });
            }
            else if (isGentleFall)
            {
                list.AddRange(new[] { Dir.N, Dir.NE, Dir.E, Dir.S, Dir.Far_N });
            }
            else
            {
                list.AddRange(new[] { Dir.N, Dir.S, Dir.NE, Dir.SE });
            }

            return list;
        }

        private static void GetOffsets(Dir dir, double w, double h, bool isPeak, bool isValley, bool isDiveToFlat, bool isFlatToDive, bool isSharpRise, out double dx, out double dy, bool forceFar = false)
        {
            bool isFar = forceFar || dir.ToString().StartsWith("Far_");
            if (isFar)
            {
                string cleanDir = dir.ToString().Replace("Far_", "");
                dir = (Dir)Enum.Parse(typeof(Dir), cleanDir);
            }

            bool isTop = dir == Dir.N || dir == Dir.NE || dir == Dir.NW;
            bool isBottom = dir == Dir.S || dir == Dir.SE || dir == Dir.SW;

            double gY;
            if (isTop) gY = isPeak ? Config.PeakGapY : Config.DefaultTopGapY;
            else if (isBottom) gY = isValley ? Config.ValleyGapY : Config.DefaultBottomGapY;
            else gY = Config.DefaultTopGapY;

            double gX = Config.GapX;

            if (isFar)
            {
                gX += Config.FarGapX_Add;
                gY += Config.FarGapY_Add;
            }

            switch (dir)
            {
                case Dir.N:
                    dx = (-w / 2) + (isPeak ? Config.PeakOffsetX : 0) + (isFlatToDive ? -8 : 0);
                    dy = -h - gY + (isPeak ? Config.PeakOffsetY : 0);
                    break;

                case Dir.S:
                    dx = (-w / 2) + (isValley ? Config.ValleyOffsetX : 0) + (isDiveToFlat ? 4 : 0);
                    dy = gY + (isValley ? Config.ValleyOffsetY : 0);
                    break;

                case Dir.E:
                    dx = gX + 6;
                    dy = -h / 2;
                    break;

                case Dir.W:
                    dx = -w - gX;
                    dy = -h / 2;
                    break;

                case Dir.NE: dx = gX - Config.DiagonalSnugX; dy = -h - gY + Config.DiagonalSnugY; break;
                case Dir.NW: dx = -w - gX + Config.DiagonalSnugX; dy = -h - gY + Config.DiagonalSnugY; break;
                case Dir.SE:
                    dx = (gX - Config.DiagonalSnugX) + (isSharpRise ? 6 : 0);
                    dy = gY - Config.DiagonalSnugY;
                    break;

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