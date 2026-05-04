// Services/CategoryMappingService.cs
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
            var rules = GeneralSettingsManager.Instance.Current.CategoryRules;
            return (rules ?? new List<CategoryRule>()).OrderByDescending(r => r.Priority).ToList();
        }

        public void SaveRules(List<CategoryRule> rules)
        {
            if (rules == null) return;
            try
            {
                var orderedRules = rules.OrderByDescending(r => r.Priority).ToList();
                GeneralSettingsManager.Instance.Current.CategoryRules = orderedRules;
                GeneralSettingsManager.Instance.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving rules: {ex.Message}");
            }
        }

        public string GetMappedCategory(string rawDescription, List<CategoryRule> rules)
        {
            if (string.IsNullOrWhiteSpace(rawDescription)) return "Unknown";

            // 1. Check against user-defined rules (Priority runs first because LoadRules sorted them)
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

            // 2. Default Fallback (Standard Data Format Handling)
            // Example: "TAMBUR-TEMIZLIGI-YAPILDI" -> "TAMBUR-TEMIZLIGI"
            var parts = rawDescription.Split('-');

            if (parts.Length >= 2)
            {
                return $"{parts[0].Trim()}-{parts[1].Trim()}".ToUpper();
            }

            return parts[0].Trim().ToUpper();
        }
    }
}