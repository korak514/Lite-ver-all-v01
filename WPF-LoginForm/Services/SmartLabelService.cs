// Services/SmartLabelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public static class SmartLabelService
    {
        // 1. Struct to handle virtual 2D coordinates for physics
        private struct VPoint
        {
            public double X, Y;

            public VPoint(double x, double y)
            { X = x; Y = y; }
        }

        public static void ApplyLabels(List<DashboardDataPoint> points, bool isDateAxis, List<string> xAxisLabels, string seriesType)
        {
            if (points == null || points.Count == 0) return;

            // 1. Text Formatting (Fixes empty line bug)
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                string vStr = FormatKiloMega(p.Y);
                string dStr = isDateAxis
                    ? new DateTime((long)p.X).ToString("MMM yyyy")
                    : (xAxisLabels != null && p.X >= 0 && p.X < xAxisLabels.Count ? xAxisLabels[(int)p.X] : "");

                dStr = dStr?.Trim();
                if (string.IsNullOrEmpty(dStr)) p.Label = vStr;
                else p.Label = $"{vStr}\n{dStr}";

                p.ShowLabel = false;
            }

            if (seriesType != "Line")
            {
                foreach (var p in points.Where(x => x.Y > 0)) { p.ShowLabel = true; p.LabelMargin = new Thickness(0, -35, 0, 0); }
                return;
            }

            // 2. Identify Important Points
            var importantIndices = GetImportantIndices(points, 8);
            var placedLabels = new List<VPoint>();

            // 3. Normalize data to Screen Space (800x400) for accurate line collision math
            double minX = 0;
            double maxX = points.Count - 1;
            if (maxX <= 0) maxX = 1;

            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);
            double rangeY = maxY - minY == 0 ? 1 : maxY - minY;

            double screenW = 800;
            double screenH = 400;

            var vPoints = new List<VPoint>();
            for (int i = 0; i < points.Count; i++)
            {
                vPoints.Add(new VPoint(
                    (i / maxX) * screenW,
                    screenH - ((points[i].Y - minY) / rangeY * screenH) // WPF Y goes down
                ));
            }

            // Queue: Process First, Last, then highest peaks
            var queue = new List<int>();
            if (points.Count > 0) queue.Add(0);
            if (points.Count > 1) queue.Add(points.Count - 1);
            foreach (var idx in importantIndices.OrderByDescending(i => points[i].Y))
            {
                if (!queue.Contains(idx)) queue.Add(idx);
            }

            // 4. RADIAL PHYSICS CONFIGURATION
            // Treat Label as a bounding circle. Offset strictly starts from edge.
            double r_label = 24;  // Label radius footprint
            double r_point = 5;   // Data point circle radius

            // Generate two tracks (inner orbit and outer orbit) starting immediately outside the point's edge
            double[] distances = { r_point + r_label + 4, r_point + r_label + 24 };

            // Check 8 directions (Angles in degrees. 270 = Straight Up in WPF, 90 = Straight Down)
            double[] angles = { 270, 90, 315, 225, 45, 135, 0, 180 };

            // 5. Evaluate the "Most Empty Location"
            foreach (int idx in queue)
            {
                var p = points[idx];
                p.ShowLabel = true;
                VPoint targetVP = vPoints[idx];

                double bestDx = 0;
                double bestDy = -35;
                double minPenalty = double.MaxValue;

                foreach (double dist in distances)
                {
                    foreach (double angle in angles)
                    {
                        double rad = angle * Math.PI / 180.0;
                        double dx = dist * Math.Cos(rad);
                        double dy = dist * Math.Sin(rad);

                        double cx = targetVP.X + dx;
                        double cy = targetVP.Y + dy;
                        double penalty = 0;

                        // Rule A: Screen Boundaries
                        if (cx - r_label < 0) penalty += 2000 * Math.Abs(cx - r_label);
                        if (cx + r_label > screenW) penalty += 2000 * Math.Abs((cx + r_label) - screenW);
                        if (cy - r_label < 0) penalty += 2000 * Math.Abs(cy - r_label);
                        if (cy + r_label > screenH) penalty += 2000 * Math.Abs((cy + r_label) - screenH);

                        // Rule B: Line Crossing (Check nearby segments)
                        int searchStart = Math.Max(0, idx - 2);
                        int searchEnd = Math.Min(points.Count - 1, idx + 2);
                        for (int j = searchStart; j < searchEnd; j++)
                        {
                            double distToLine = DistToSegment(new VPoint(cx, cy), vPoints[j], vPoints[j + 1]);
                            if (distToLine < r_label)
                            {
                                penalty += (r_label - distToLine) * 150; // High penalty for touching line
                            }
                        }

                        // Rule C: Overlapping other labels
                        foreach (var placed in placedLabels)
                        {
                            double distToOther = Math.Sqrt(Dist2(new VPoint(cx, cy), placed));
                            if (distToOther < r_label * 2.2) // Need to clear 2x radius + padding
                            {
                                penalty += (r_label * 2.2 - distToOther) * 200;
                            }
                        }

                        // Rule D: Aesthetic Preferences
                        if (dist > distances[0]) penalty += 15; // Prefer closer inner orbit
                        if (angle == 0 || angle == 180) penalty += 10; // Prefer vertical/diagonal over flat horizontal
                        if (angle % 90 != 0) penalty += 3; // Prefer straight Up/Down over diagonals

                        // Rule E: Strict Edge Constraints
                        if (idx == 0 && dx < 0) penalty += 5000; // First point CANNOT go left
                        if (idx == points.Count - 1 && dx > 0) penalty += 5000; // Last point CANNOT go right

                        if (penalty < minPenalty)
                        {
                            minPenalty = penalty;
                            bestDx = dx;
                            bestDy = dy;
                        }
                    }
                }

                p.LabelMargin = new Thickness(bestDx, bestDy, 0, 0);
                placedLabels.Add(new VPoint(targetVP.X + bestDx, targetVP.Y + bestDy));
            }
        }

        // --- MATH HELPERS ---
        private static double Dist2(VPoint v, VPoint w) => (v.X - w.X) * (v.X - w.X) + (v.Y - w.Y) * (v.Y - w.Y);

        private static double DistToSegment(VPoint p, VPoint v, VPoint w)
        {
            double l2 = Dist2(v, w);
            if (l2 == 0) return Math.Sqrt(Dist2(p, v));
            double t = ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2;
            t = Math.Max(0, Math.Min(1, t));
            VPoint proj = new VPoint(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
            return Math.Sqrt(Dist2(p, proj));
        }

        private static HashSet<int> GetImportantIndices(List<DashboardDataPoint> points, int maxLabels)
        {
            var indices = new HashSet<int>();
            if (points.Count <= maxLabels) { for (int i = 0; i < points.Count; i++) indices.Add(i); return indices; }

            indices.Add(0); indices.Add(points.Count - 1);
            double avgY = points.Average(p => p.Y);

            var scoredPoints = points.Select((p, i) =>
            {
                if (i == 0 || i == points.Count - 1) return new { Index = i, Score = double.MaxValue };
                double diffPrev = Math.Abs(points[i].Y - points[i - 1].Y);
                double diffNext = Math.Abs(points[i].Y - points[i + 1].Y);
                return new { Index = i, Score = (diffPrev + diffNext) * 2.0 + Math.Abs(points[i].Y - avgY) };
            }).OrderByDescending(x => x.Score).ToList();

            int minIndexDist = Math.Max(1, points.Count / maxLabels);
            foreach (var item in scoredPoints)
            {
                if (indices.Count >= maxLabels) break;
                if (!indices.Any(idx => Math.Abs(idx - item.Index) < minIndexDist)) indices.Add(item.Index);
            }

            foreach (var i in indices) points[i].IsImportant = true;
            return indices;
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