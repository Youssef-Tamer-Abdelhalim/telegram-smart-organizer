using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// JSON-based implementation of IRulesService.
    /// </summary>
    public class JsonRulesService : IRulesService
    {
        private readonly string _rulesFilePath;
        private readonly object _lock = new();
        private List<OrganizationRule> _cachedRules;

        public JsonRulesService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _rulesFilePath = Path.Combine(appFolder, "rules.json");
            _cachedRules = LoadRules();
        }

        public List<OrganizationRule> LoadRules()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_rulesFilePath))
                    {
                        // Create default rules
                        var defaultRules = CreateDefaultRules();
                        SaveRules(defaultRules);
                        return defaultRules;
                    }

                    string json = File.ReadAllText(_rulesFilePath);
                    var rules = JsonSerializer.Deserialize<List<OrganizationRule>>(json);
                    _cachedRules = rules ?? new List<OrganizationRule>();
                    return _cachedRules;
                }
                catch
                {
                    return new List<OrganizationRule>();
                }
            }
        }

        public void SaveRules(List<OrganizationRule> rules)
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(rules, options);
                    File.WriteAllText(_rulesFilePath, json);
                    _cachedRules = rules;
                }
                catch
                {
                    // Log error
                }
            }
        }

        public void AddRule(OrganizationRule rule)
        {
            var rules = LoadRules();
            rules.Add(rule);
            SaveRules(rules);
        }

        public void UpdateRule(OrganizationRule rule)
        {
            var rules = LoadRules();
            var existing = rules.FirstOrDefault(r => r.Id == rule.Id);
            
            if (existing != null)
            {
                int index = rules.IndexOf(existing);
                rule.ModifiedAt = DateTime.Now;
                rules[index] = rule;
                SaveRules(rules);
            }
        }

        public void DeleteRule(string ruleId)
        {
            var rules = LoadRules();
            rules.RemoveAll(r => r.Id == ruleId);
            SaveRules(rules);
        }

        public List<OrganizationRule> GetEnabledRules()
        {
            return LoadRules()
                .Where(r => r.IsEnabled)
                .OrderByDescending(r => r.Priority)
                .ToList();
        }

        public OrganizationRule? FindMatchingRule(string fileName, string groupName, long fileSize = 0)
        {
            var rules = GetEnabledRules();

            foreach (var rule in rules)
            {
                if (IsMatch(rule, fileName, groupName, fileSize))
                {
                    return rule;
                }
            }

            return null;
        }

        private bool IsMatch(OrganizationRule rule, string fileName, string groupName, long fileSize)
        {
            switch (rule.RuleType)
            {
                case RuleType.FileExtension:
                    string ext = Path.GetExtension(fileName).ToLower();
                    return MatchPattern(ext, rule.Pattern, rule.MatchType);

                case RuleType.FileNamePattern:
                    return MatchPattern(fileName, rule.Pattern, rule.MatchType);

                case RuleType.GroupName:
                    return MatchPattern(groupName, rule.Pattern, rule.MatchType);

                case RuleType.FileSize:
                    // Pattern format: "min-max" in KB (e.g., "0-1024" for files < 1MB)
                    if (long.TryParse(rule.Pattern.Split('-')[0], out long min) &&
                        long.TryParse(rule.Pattern.Split('-')[1], out long max))
                    {
                        long sizeKB = fileSize / 1024;
                        return sizeKB >= min && sizeKB <= max;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private bool MatchPattern(string value, string pattern, PatternMatchType matchType)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
                return false;

            switch (matchType)
            {
                case PatternMatchType.Exact:
                    return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);

                case PatternMatchType.Contains:
                    return value.Contains(pattern, StringComparison.OrdinalIgnoreCase);

                case PatternMatchType.StartsWith:
                    return value.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);

                case PatternMatchType.EndsWith:
                    return value.EndsWith(pattern, StringComparison.OrdinalIgnoreCase);

                case PatternMatchType.Regex:
                    try
                    {
                        return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }

                default:
                    return false;
            }
        }

        private List<OrganizationRule> CreateDefaultRules()
        {
            return new List<OrganizationRule>
            {
                // Images
                new OrganizationRule
                {
                    Name = "Images",
                    Description = "Organize image files",
                    RuleType = RuleType.FileExtension,
                    Pattern = ".jpg|.jpeg|.png|.gif|.bmp|.svg|.webp",
                    MatchType = PatternMatchType.Contains,
                    TargetFolder = "Images",
                    Priority = 10,
                    IsEnabled = false // Disabled by default
                },
                
                // Documents
                new OrganizationRule
                {
                    Name = "Documents",
                    Description = "Organize document files",
                    RuleType = RuleType.FileExtension,
                    Pattern = ".pdf|.docx|.doc|.txt|.xlsx|.xls|.pptx|.ppt",
                    MatchType = PatternMatchType.Contains,
                    TargetFolder = "Documents",
                    Priority = 10,
                    IsEnabled = false
                },
                
                // Videos
                new OrganizationRule
                {
                    Name = "Videos",
                    Description = "Organize video files",
                    RuleType = RuleType.FileExtension,
                    Pattern = ".mp4|.mkv|.avi|.mov|.wmv|.flv|.webm",
                    MatchType = PatternMatchType.Contains,
                    TargetFolder = "Videos",
                    Priority = 10,
                    IsEnabled = false
                },
                
                // Audio
                new OrganizationRule
                {
                    Name = "Audio",
                    Description = "Organize audio files",
                    RuleType = RuleType.FileExtension,
                    Pattern = ".mp3|.wav|.flac|.aac|.ogg|.m4a",
                    MatchType = PatternMatchType.Contains,
                    TargetFolder = "Audio",
                    Priority = 10,
                    IsEnabled = false
                },
                
                // Archives
                new OrganizationRule
                {
                    Name = "Archives",
                    Description = "Organize compressed files",
                    RuleType = RuleType.FileExtension,
                    Pattern = ".zip|.rar|.7z|.tar|.gz",
                    MatchType = PatternMatchType.Contains,
                    TargetFolder = "Archives",
                    Priority = 10,
                    IsEnabled = false
                }
            };
        }
    }
}
