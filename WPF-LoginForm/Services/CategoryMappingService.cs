using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public class CategoryMappingService
    {
        private const string FilePath = "category_rules.json";

        public List<CategoryRule> LoadRules()
        {
            if (!File.Exists(FilePath)) return new List<CategoryRule>();
            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonConvert.DeserializeObject<List<CategoryRule>>(json) ?? new List<CategoryRule>();
            }
            catch
            {
                return new List<CategoryRule>();
            }
        }

        public void SaveRules(List<CategoryRule> rules)
        {
            try
            {
                string json = JsonConvert.SerializeObject(rules, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving rules: {ex.Message}");
            }
        }

        public string GetMappedCategory(string rawDescription, List<CategoryRule> rules)
        {
            if (string.IsNullOrWhiteSpace(rawDescription)) return "Unknown";

            // 1. Check against user-defined rules (Priority 1)
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    if (!string.IsNullOrEmpty(rule.StartsWith) &&
                        rawDescription.StartsWith(rule.StartsWith, StringComparison.OrdinalIgnoreCase))
                    {
                        return rule.MapTo;
                    }
                }
            }

            // 2. Default Fallback (Priority 2)
            // Logic: Include the FIRST and SECOND part of the name if available.
            // Example: "TAMBUR-TEMIZLIGI-YAPILDI" -> "TAMBUR-TEMIZLIGI"
            // Example: "ACIL-STOP" -> "ACIL-STOP"

            var parts = rawDescription.Split('-');

            if (parts.Length >= 2)
            {
                // Return "Part1-Part2"
                return $"{parts[0].Trim()}-{parts[1].Trim()}".ToUpper();
            }

            // Fallback for single words
            return parts[0].Trim().ToUpper();
        }
    }
}