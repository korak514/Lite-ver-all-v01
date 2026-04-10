// Models/CategoryRule.cs
using System;

namespace WPF_LoginForm.Models
{
    public class CategoryRule
    {
        /// <summary>
        /// The raw string found in the database (even with typos).
        /// </summary>
        public string StartsWith { get; set; }

        /// <summary>
        /// The corrected/standardized category name.
        /// </summary>
        public string MapTo { get; set; }

        /// <summary>
        /// Higher value = higher priority.
        /// Use higher priority for specific spelling corrections.
        /// </summary>
        public int Priority { get; set; } = 0;

        public CategoryRule()
        { }

        public CategoryRule(string startsWith, string mapTo, int priority = 0)
        {
            StartsWith = startsWith;
            MapTo = mapTo;
            Priority = priority;
        }
    }
}