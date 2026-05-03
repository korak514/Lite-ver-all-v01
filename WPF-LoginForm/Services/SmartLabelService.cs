// Services/SmartLabelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public static class SmartLabelService
    {
        public static class Config
        {
            public static double PeakGapY = 6;
            public static double ValleyGapY = 10;
            public static double DefaultTopGapY = 6;
            public static double DefaultBottomGapY = 10;
            public static double PeakOffsetX = 0;
            public static double PeakOffsetY = 0;
            public static double ValleyOffsetX = 0;
            public static double ValleyOffsetY = 0;
            public static double GapX = 8;
            public static double FarGapY_Add = 20;
            public static double FarGapX_Add = 15;
            public static double DiagonalSnugY = 4;
            public static double DiagonalSnugX = 4;
            public static double FirstLabelOffsetX = 0;
            public static double FirstLabelOffsetY = 0;
            public static double CharWidth = 7.0;
            public static double BoxPaddingX = 16;
            public static double BoxHeight = 36;
            public static double CollisionPadX = 2;
            public static double CollisionPadY = 2;
            public static double SteepSlope = 0.08;
            public static double FlatSlope = 0.06;
            public static double VirtualWidthPerPoint = 100.0;
            public static double VirtualHeight = 450.0;
            public static double BarLabelGapY = 10.0;
        }

        private enum Dir
        {
            N, S, E, W, NE, NW, SE, SW,
            N_Up5, N_Up7, N_Up10, S_Down5, S_Down7, S_Down10,
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
            public Dir DirUsed { get; set; }
        }

        public static void ApplyLabels(List<DashboardDataPoint> points, bool isDateAxis, List<string> xAxisLabels, string seriesType)
        {
            if (points == null || points.Count == 0) return;

            if (string.Equals(seriesType, "Bar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(seriesType, "Column", StringComparison.OrdinalIgnoreCase))
            {
                int count = points.Count;
                if (count <= 6)
                {
                    foreach (var p in points) { if (p != null) p.ShowLabel = false; }
                    return;
                }

                int maxIdx = 0; int minIdx = 0;
                double maxVal = double.MinValue; double minVal = double.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    var p = points[i];
                    if (p == null) continue;

                    double val = p.Y;
                    if (val > maxVal) { maxVal = val; maxIdx = i; }
                    if (val < minVal) { minVal = val; minIdx = i; }
                }

                HashSet<int> indicesToLabel = new HashSet<int> { 0, count - 1, maxIdx, minIdx };

                for (int i = 0; i < count; i++)
                {
                    var p = points[i];
                    if (p == null) continue;

                    if (indicesToLabel.Contains(i) && p.Y != 0)
                    {
                        // ONLY generate default label if a custom blueprint label wasn't passed in
                        if (string.IsNullOrEmpty(p.Label))
                        {
                            p.Label = FormatKiloMega(p.Y);
                        }

                        double w = (p.Label.Length * Config.CharWidth) + Config.BoxPaddingX;
                        p.LabelDx = -w / 2.0;
                        p.LabelDy = -Config.BoxHeight - Config.BarLabelGapY;
                        p.ShowLabel = true;
                        p.HasLeaderLine = false;
                    }
                    else
                    {
                        p.ShowLabel = false;
                    }
                }
                return;
            }

            int countLine = points.Count;
            var validPoints = points.Where(p => p != null && !double.IsNaN(p.Y)).ToList();
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
                if (p == null || double.IsNaN(p.Y)) continue;

                // BUGFIX: Respect the custom label. If null, it means it's a standard chart, so generate default.
                if (string.IsNullOrEmpty(p.Label))
                {
                    string vStr = FormatKiloMega(p.Y);
                    string dStr = isDateAxis
                        ? new DateTime((long)p.X).ToString("MMM yyyy")
                        : (xAxisLabels != null && p.X >= 0 && p.X < xAxisLabels.Count ? xAxisLabels[(int)p.X] : "");

                    p.Label = string.IsNullOrEmpty(dStr?.Trim()) ? vStr : $"{vStr}\n{dStr.Trim()}";
                }

                p.ShowLabel = false;
                p.IsImportant = true;

                double curr = p.Y;
                double prev = i > 0 && points[i - 1] != null && !double.IsNaN(points[i - 1].Y) ? points[i - 1].Y : curr;
                double next = i < countLine - 1 && points[i + 1] != null && !double.IsNaN(points[i + 1].Y) ? points[i + 1].Y : curr;

                isPeakArray[i] = (curr >= prev && curr >= next) && (curr > prev || curr > next);
                isValleyArray[i] = (curr <= prev && curr <= next) && (curr < prev || curr < next);
            }

            for (int i = 0; i < countLine; i++)
            {
                if (points[i] == null || double.IsNaN(points[i].Y)) continue;
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
                if (p == null || double.IsNaN(p.Y)) continue;

                bool isFirstPoint = (i == 0);
                bool isLastPoint = (i == countLine - 1);

                double curr = p.Y;
                double prev = i > 0 && points[i - 1] != null && !double.IsNaN(points[i - 1].Y) ? points[i - 1].Y : curr;
                double next = i < countLine - 1 && points[i + 1] != null && !double.IsNaN(points[i + 1].Y) ? points[i + 1].Y : curr;

                bool isPeak = isPeakArray[i];
                bool isValley = isValleyArray[i];
                double slopeIn = curr - prev;
                double slopeOut = next - curr;

                bool isSharpRise = (slopeIn > steepThresholdValue || slopeOut > steepThresholdValue);
                bool isDiveToFlat = slopeIn < -steepThresholdValue && Math.Abs(slopeOut) < flatThresholdValue;
                bool isFlatToDive = Math.Abs(slopeIn) < flatThresholdValue && slopeOut < -steepThresholdValue;
                bool hasFlatSide = Math.Abs(slopeIn) < flatThresholdValue || Math.Abs(slopeOut) < flatThresholdValue;

                int maxCharCount = 5;
                if (!string.IsNullOrEmpty(p.Label))
                {
                    var lines = p.Label.Split('\n');
                    maxCharCount = lines.Max(l => l.Length);
                }

                double w = (maxCharCount * Config.CharWidth) + Config.BoxPaddingX;
                // Dynamically scale height of hitboxes so multi-line combination labels don't overlap lines
                double linesCount = !string.IsNullOrEmpty(p.Label) ? p.Label.Split('\n').Length : 2;
                double h = (linesCount * 14) + 10;

                double px = countLine > 1 ? ((points[i].X - minX) / rangeX) * VIRTUAL_WIDTH : VIRTUAL_WIDTH / 2;
                double py = rangeY > 0 ? VIRTUAL_HEIGHT - (((curr - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2;
                double prevPx = i > 0 && points[i - 1] != null ? ((points[i - 1].X - minX) / rangeX) * VIRTUAL_WIDTH : px;
                double prevPy = i > 0 && points[i - 1] != null && !double.IsNaN(points[i - 1].Y) ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[i - 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : py;
                double nextPx = i < countLine - 1 && points[i + 1] != null ? ((points[i + 1].X - minX) / rangeX) * VIRTUAL_WIDTH : px;
                double nextPy = i < countLine - 1 && points[i + 1] != null && !double.IsNaN(points[i + 1].Y) ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[i + 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : py;

                List<Dir> candidates = GetCandidates(i, countLine, curr, prev, next, isPeak, isValley, hasFlatSide, seriesType, rangeY);
                var placement = new LabelPlacement { Index = i, Show = false };

                foreach (Dir dir in candidates)
                {
                    GetOffsets(dir, w, h, isPeak, isValley, isDiveToFlat, isFlatToDive, isSharpRise, isFirstPoint, isLastPoint, hasFlatSide, out double rawDx, out double rawDy);

                    double dx = rawDx;
                    double dy = rawDy;

                    double leftEdge = px + dx;
                    double rightEdge = px + dx + w;
                    double maxRightAllowed = VIRTUAL_WIDTH + (VIRTUAL_WIDTH / countLine);
                    if (isLastPoint) maxRightAllowed += 100;

                    if (leftEdge < 5) dx += (5 - leftEdge);
                    else if (rightEdge > maxRightAllowed - 5) dx -= (rightEdge - (maxRightAllowed - 5));

                    if (Math.Abs(dx - rawDx) > 15.0) continue;

                    Rect proposedBox = new Rect(px + dx, py + dy, w, h);

                    bool collidesLabels = placements.Any(placed => placed.Show && placed.Bounds.Intersects(proposedBox));

                    bool isTopDir = dir == Dir.N || dir == Dir.NW || dir == Dir.NE || dir.ToString().StartsWith("Far_N") || dir.ToString().StartsWith("N_");
                    bool isBottomDir = dir == Dir.S || dir == Dir.SW || dir == Dir.SE || dir.ToString().StartsWith("Far_S") || dir.ToString().StartsWith("S_");

                    Rect lineSafeBox = new Rect(proposedBox.X - 2, proposedBox.Y, proposedBox.W + 4, proposedBox.H);

                    if (isTopDir)
                    {
                        double spaceBelow = Math.Max(0, py - (proposedBox.Y + proposedBox.H) - 1);
                        lineSafeBox.H += Math.Min(6, spaceBelow);
                    }
                    else if (isBottomDir)
                    {
                        double spaceAbove = Math.Max(0, proposedBox.Y - py - 1);
                        double inflation = Math.Min(6, spaceAbove);
                        lineSafeBox.Y -= inflation;
                        lineSafeBox.H += inflation;
                    }
                    else
                    {
                        double spaceAbove = Math.Max(0, proposedBox.Y - py - 1);
                        double spaceBelow = Math.Max(0, py - (proposedBox.Y + proposedBox.H) - 1);
                        double infUp = Math.Min(4, spaceAbove);
                        double infDown = Math.Min(4, spaceBelow);
                        lineSafeBox.Y -= infUp;
                        lineSafeBox.H += (infUp + infDown);
                    }

                    bool collidesLine = false;
                    if (i > 0) collidesLine |= LineIntersectsRect(prevPx, prevPy, px, py, lineSafeBox);
                    if (i < countLine - 1) collidesLine |= LineIntersectsRect(px, py, nextPx, nextPy, lineSafeBox);

                    bool collidesDot = false;
                    double dotHitbox = 2.0;

                    Rect currDot = new Rect(px - dotHitbox, py - dotHitbox, dotHitbox * 2, dotHitbox * 2);
                    if (proposedBox.Intersects(currDot)) collidesDot = true;

                    if (i > 0)
                    {
                        Rect prevDot = new Rect(prevPx - dotHitbox, prevPy - dotHitbox, dotHitbox * 2, dotHitbox * 2);
                        if (proposedBox.Intersects(prevDot)) collidesDot = true;
                    }
                    if (i < countLine - 1)
                    {
                        Rect nextDot = new Rect(nextPx - dotHitbox, nextPy - dotHitbox, dotHitbox * 2, dotHitbox * 2);
                        if (proposedBox.Intersects(nextDot)) collidesDot = true;
                    }

                    if (!collidesLabels && !collidesLine && !collidesDot)
                    {
                        placement.Dx = dx;
                        placement.Dy = dy;
                        placement.Bounds = proposedBox;
                        placement.Show = true;
                        placement.DirUsed = dir;
                        break;
                    }
                }

                if (!placement.Show)
                {
                    Dir[] emergencyDirs = { Dir.N, Dir.Far_N, Dir.Far_NW, Dir.Far_NE, Dir.S, Dir.Far_S };
                    foreach (Dir edir in emergencyDirs)
                    {
                        GetOffsets(edir, w, h, isPeak, isValley, isDiveToFlat, isFlatToDive, isSharpRise, isFirstPoint, isLastPoint, hasFlatSide, out double rawDx, out double rawDy, forceFar: true);

                        double dx = rawDx;
                        double dy = rawDy;

                        double leftEdge = px + dx;
                        double rightEdge = px + dx + w;
                        double maxRightAllowed = VIRTUAL_WIDTH + (VIRTUAL_WIDTH / countLine);
                        if (isLastPoint) maxRightAllowed += 100;

                        if (leftEdge < 5) dx += (5 - leftEdge);
                        else if (rightEdge > maxRightAllowed - 5) dx -= (rightEdge - (maxRightAllowed - 5));

                        Rect proposedBox = new Rect(px + dx, py + dy, w, h);

                        bool collidesLabels = placements.Any(placed => placed.Show && placed.Bounds.Intersects(proposedBox));
                        if (!collidesLabels)
                        {
                            placement.Dx = dx;
                            placement.Dy = dy;
                            placement.Bounds = proposedBox;
                            placement.Show = true;
                            placement.DirUsed = edir;
                            break;
                        }
                    }

                    if (!placement.Show)
                    {
                        Dir fallbackDir = candidates[0];
                        if (isFirstPoint) fallbackDir = (curr <= next) ? Dir.SE : Dir.NE;

                        GetOffsets(fallbackDir, w, h, isPeak, isValley, isDiveToFlat, isFlatToDive, isSharpRise, isFirstPoint, isLastPoint, hasFlatSide, out double rawDx, out double rawDy);
                        placement.Dx = rawDx;
                        placement.Dy = rawDy;
                        placement.Bounds = new Rect(px + rawDx, py + rawDy, w, h);
                        placement.Show = true;
                        placement.DirUsed = fallbackDir;
                    }
                }

                placements.Add(placement);
            }

            double[] shiftsX = new double[] { 0, -5, 5, -15, 15, -25, 25 };
            double[] shiftsY = new double[] { 0, -12, 12, -26, 26, -40, 40, -60, 60, -85, 85 };
            int maxPasses = 6;

            var zipperOrder = placements.OrderBy(p => importance[p.Index]).ToList();

            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool anyOverlap = false;
                bool isDesperationPass = (pass >= maxPasses - 2);

                foreach (var current in zipperOrder)
                {
                    if (!current.Show) continue;
                    bool isOverlapping = placements.Any(o => o != current && o.Show && current.Bounds.Intersects(o.Bounds));
                    if (isOverlapping)
                    {
                        anyOverlap = true;
                        bool resolved = false;

                        int idx = current.Index;
                        if (idx < 0 || idx >= countLine || points[idx] == null) continue;

                        double cx = countLine > 1 ? ((points[idx].X - minX) / rangeX) * VIRTUAL_WIDTH : VIRTUAL_WIDTH / 2;
                        double cy = rangeY > 0 ? VIRTUAL_HEIGHT - (((points[idx].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2;

                        double prevPx = idx > 0 && points[idx - 1] != null ? ((points[idx - 1].X - minX) / rangeX) * VIRTUAL_WIDTH : cx;
                        double prevPy = idx > 0 && points[idx - 1] != null && !double.IsNaN(points[idx - 1].Y) ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[idx - 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : cy;
                        double nextPx = idx < countLine - 1 && points[idx + 1] != null ? ((points[idx + 1].X - minX) / rangeX) * VIRTUAL_WIDTH : cx;
                        double nextPy = idx < countLine - 1 && points[idx + 1] != null && !double.IsNaN(points[idx + 1].Y) ? (rangeY > 0 ? VIRTUAL_HEIGHT - (((points[idx + 1].Y - minY) / rangeY) * VIRTUAL_HEIGHT) : VIRTUAL_HEIGHT / 2) : cy;

                        bool foundPerfect = false;

                        foreach (double sx in shiftsX)
                        {
                            foreach (double sy in shiftsY)
                            {
                                if (sx == 0 && sy == 0) continue;

                                Rect testBounds = new Rect(current.Bounds.X + sx, current.Bounds.Y + sy, current.Bounds.W, current.Bounds.H);

                                double maxRightAllowed = VIRTUAL_WIDTH + (VIRTUAL_WIDTH / countLine);
                                if (idx == countLine - 1) maxRightAllowed += 100;

                                if (testBounds.X < 5 || testBounds.X + testBounds.W > maxRightAllowed - 5) continue;

                                bool stillOverlaps = placements.Any(o => o != current && o.Show && testBounds.Intersects(o.Bounds));

                                bool isTopDir = (testBounds.Y + (testBounds.H / 2)) < cy;
                                bool isBottomDir = !isTopDir;

                                Rect lineSafeBox = new Rect(testBounds.X - 2, testBounds.Y, testBounds.W + 4, testBounds.H);
                                if (isTopDir)
                                {
                                    double spaceBelow = Math.Max(0, cy - (testBounds.Y + testBounds.H) - 1);
                                    lineSafeBox.H += Math.Min(6, spaceBelow);
                                }
                                else if (isBottomDir)
                                {
                                    double spaceAbove = Math.Max(0, testBounds.Y - cy - 1);
                                    double inflation = Math.Min(6, spaceAbove);
                                    lineSafeBox.Y -= inflation;
                                    lineSafeBox.H += inflation;
                                }

                                bool hitLine = false;
                                bool hitDot = false;

                                if (!isDesperationPass)
                                {
                                    if (idx > 0) hitLine |= LineIntersectsRect(prevPx, prevPy, cx, cy, lineSafeBox);
                                    if (idx < countLine - 1) hitLine |= LineIntersectsRect(cx, cy, nextPx, nextPy, lineSafeBox);

                                    double dotHitbox = 2.0;
                                    if (testBounds.Intersects(new Rect(cx - dotHitbox, cy - dotHitbox, dotHitbox * 2, dotHitbox * 2))) hitDot = true;
                                    if (idx > 0 && testBounds.Intersects(new Rect(prevPx - dotHitbox, prevPy - dotHitbox, dotHitbox * 2, dotHitbox * 2))) hitDot = true;
                                    if (idx < countLine - 1 && testBounds.Intersects(new Rect(nextPx - dotHitbox, nextPy - dotHitbox, dotHitbox * 2, dotHitbox * 2))) hitDot = true;
                                }

                                if (!stillOverlaps && !hitLine && !hitDot)
                                {
                                    current.Bounds = testBounds;
                                    current.Dx += sx;
                                    current.Dy += sy;
                                    resolved = true;
                                    foundPerfect = true;
                                    break;
                                }
                            }
                            if (foundPerfect) break;
                        }

                        if (!foundPerfect && isDesperationPass)
                        {
                            foreach (double sx in shiftsX)
                            {
                                foreach (double sy in shiftsY)
                                {
                                    if (sx == 0 && sy == 0) continue;

                                    Rect testBounds = new Rect(current.Bounds.X + sx, current.Bounds.Y + sy, current.Bounds.W, current.Bounds.H);
                                    double maxRightAllowed = VIRTUAL_WIDTH + (VIRTUAL_WIDTH / countLine);
                                    if (idx == countLine - 1) maxRightAllowed += 100;
                                    if (testBounds.X < 5 || testBounds.X + testBounds.W > maxRightAllowed - 5) continue;

                                    bool stillOverlaps = placements.Any(o => o != current && o.Show && testBounds.Intersects(o.Bounds));

                                    if (!stillOverlaps)
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

        private static List<Dir> GetCandidates(int i, int count, double curr, double prev, double next, bool isPeak, bool isValley, bool hasFlatSide, string seriesType, double rangeY)
        {
            var list = new List<Dir>();

            if (seriesType != "Line")
            {
                list.Add(Dir.N); list.Add(Dir.Far_N);
                return list;
            }

            if (i == count - 1)
            {
                if (prev > curr) list.AddRange(new[] { Dir.N, Dir.N_Up5, Dir.N_Up10, Dir.NE, Dir.NW, Dir.SE, Dir.SW });
                else list.AddRange(new[] { Dir.N, Dir.N_Up5, Dir.N_Up10, Dir.SE, Dir.NE, Dir.S, Dir.NW });
                return list;
            }

            if (i == 1)
            {
                if (prev > curr) list.AddRange(new[] { Dir.S, Dir.SW, Dir.SE, Dir.N, Dir.NE });
                else list.AddRange(new[] { Dir.N, Dir.NW, Dir.NE, Dir.S, Dir.SE });
                return list;
            }

            if (i == 0)
            {
                if (curr <= next) list.AddRange(new[] { Dir.SE, Dir.S, Dir.NE, Dir.N });
                else list.AddRange(new[] { Dir.NE, Dir.N, Dir.SE, Dir.S });
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

            if (isPeak) { list.AddRange(new[] { Dir.N, Dir.NW, Dir.NE, Dir.Far_N }); return list; }
            if (isValley) { list.AddRange(new[] { Dir.S, Dir.SE, Dir.SW, Dir.Far_S }); return list; }

            if (hasFlatSide || isPlateau || isGentleRise || isGentleFall)
            {
                list.AddRange(new[] {
                    Dir.N, Dir.N_Up5, Dir.N_Up7, Dir.N_Up10,
                    Dir.S, Dir.S_Down5, Dir.S_Down7, Dir.S_Down10,
                    Dir.NE, Dir.SE, Dir.NW, Dir.SW
                });
                return list;
            }

            if (isDiveToFlat) { list.AddRange(new[] { Dir.N, Dir.NE, Dir.S, Dir.Far_N }); return list; }
            if (isRiseToFlat) { list.AddRange(new[] { Dir.S, Dir.SE, Dir.NW, Dir.N, Dir.Far_N }); return list; }
            if (isFlatToDive) { list.AddRange(new[] { Dir.NE, Dir.NW, Dir.N, Dir.E }); return list; }

            if (isSteepRise) list.AddRange(new[] { Dir.N, Dir.SE, Dir.NW, Dir.S, Dir.NE, Dir.SW });
            else if (isSteepFall) list.AddRange(new[] { Dir.NE, Dir.E, Dir.N, Dir.SW, Dir.S, Dir.NW });
            else list.AddRange(new[] { Dir.N, Dir.NE, Dir.NW, Dir.S });

            return list;
        }

        private static void GetOffsets(Dir dir, double w, double h, bool isPeak, bool isValley, bool isDiveToFlat, bool isFlatToDive, bool isSharpRise, bool isFirstPoint, bool isLastPoint, bool hasFlatSide, out double dx, out double dy, bool forceFar = false)
        {
            bool isFar = forceFar || dir.ToString().StartsWith("Far_");
            if (isFar && dir.ToString().StartsWith("Far_"))
            {
                string cleanDir = dir.ToString().Replace("Far_", "");
                dir = (Dir)Enum.Parse(typeof(Dir), cleanDir);
            }

            bool isTop = dir == Dir.N || dir == Dir.NE || dir == Dir.NW || dir.ToString().StartsWith("N_");
            bool isBottom = dir == Dir.S || dir == Dir.SE || dir == Dir.SW || dir.ToString().StartsWith("S_");

            double gY;
            if (isTop) gY = isPeak ? Config.PeakGapY : Config.DefaultTopGapY;
            else if (isBottom) gY = isValley ? Config.ValleyGapY : Config.DefaultBottomGapY;
            else gY = Config.DefaultTopGapY;

            if (hasFlatSide) gY += 6.0;

            double gX = Config.GapX;

            if (isFar)
            {
                gX += Config.FarGapX_Add;
                gY += Config.FarGapY_Add;
            }

            switch (dir)
            {
                case Dir.N: dx = (-w / 2) + (isPeak ? 0 : 4.0) + (isFlatToDive ? -6 : 0); dy = -h - gY + (isPeak ? Config.PeakOffsetY : 0); break;
                case Dir.N_Up5: dx = (-w / 2) + 4.0; dy = -h - gY - 5; break;
                case Dir.N_Up7: dx = (-w / 2) + 4.0; dy = -h - gY - 7; break;
                case Dir.N_Up10: dx = (-w / 2) + 4.0; dy = -h - gY - 10; break;
                case Dir.S: dx = (-w / 2) + (isValley ? 0 : 4.0) + (isDiveToFlat ? 4 : 0); dy = gY + (isValley ? Config.ValleyOffsetY : 0); break;
                case Dir.S_Down5: dx = (-w / 2) + 4.0; dy = gY + 5; break;
                case Dir.S_Down7: dx = (-w / 2) + 4.0; dy = gY + 7; break;
                case Dir.S_Down10: dx = (-w / 2) + 4.0; dy = gY + 10; break;
                case Dir.E: dx = gX + 6; dy = -h / 2; break;
                case Dir.W: dx = -w - gX; dy = -h / 2; break;
                case Dir.NE: dx = isFirstPoint ? 2.0 : (gX - Config.DiagonalSnugX); dy = isFirstPoint ? (-h - gY) : (-h - gY + Config.DiagonalSnugY); break;
                case Dir.NW: dx = -w - gX + Config.DiagonalSnugX; dy = -h - gY + Config.DiagonalSnugY; break;
                case Dir.SE: dx = isFirstPoint ? 2.0 : (gX - Config.DiagonalSnugX) + (isSharpRise ? 4 : 0); dy = isFirstPoint ? gY : (gY - Config.DiagonalSnugY); break;
                case Dir.SW: dx = -w - gX + Config.DiagonalSnugX; dy = gY - Config.DiagonalSnugY; break;
                default: dx = (-w / 2) + 4.0; dy = -h - gY; break;
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