using System;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Rule types for file organization.
    /// </summary>
    public enum RuleType
    {
        /// <summary>File extension based rule (e.g., .pdf, .jpg)</summary>
        FileExtension,
        
        /// <summary>File name pattern (contains, starts with, ends with)</summary>
        FileNamePattern,
        
        /// <summary>Group/Channel name based rule</summary>
        GroupName,
        
        /// <summary>File size based rule</summary>
        FileSize,
        
        /// <summary>Combined multiple conditions</summary>
        Combined
    }

    /// <summary>
    /// Pattern matching types for rules.
    /// </summary>
    public enum PatternMatchType
    {
        /// <summary>Exact match</summary>
        Exact,
        
        /// <summary>Contains substring</summary>
        Contains,
        
        /// <summary>Starts with</summary>
        StartsWith,
        
        /// <summary>Ends with</summary>
        EndsWith,
        
        /// <summary>Regular expression</summary>
        Regex
    }

    /// <summary>
    /// Represents a custom organization rule.
    /// </summary>
    public class OrganizationRule
    {
        /// <summary>Unique identifier for the rule</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>Rule name for display</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Rule description</summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>Type of rule</summary>
        public RuleType RuleType { get; set; }
        
        /// <summary>Pattern to match</summary>
        public string Pattern { get; set; } = string.Empty;
        
        /// <summary>How to match the pattern</summary>
        public PatternMatchType MatchType { get; set; }
        
        /// <summary>Target folder (relative to base destination)</summary>
        public string TargetFolder { get; set; } = string.Empty;
        
        /// <summary>Priority (higher = executed first)</summary>
        public int Priority { get; set; } = 0;
        
        /// <summary>Whether the rule is enabled</summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>When the rule was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        /// <summary>When the rule was last modified</summary>
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        
        /// <summary>How many times this rule has been applied</summary>
        public int TimesApplied { get; set; } = 0;
    }
}
