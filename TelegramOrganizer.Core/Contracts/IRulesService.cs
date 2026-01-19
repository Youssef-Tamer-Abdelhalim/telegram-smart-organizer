using System.Collections.Generic;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Service for managing custom organization rules.
    /// </summary>
    public interface IRulesService
    {
        /// <summary>
        /// Loads all rules from storage.
        /// </summary>
        List<OrganizationRule> LoadRules();
        
        /// <summary>
        /// Saves all rules to storage.
        /// </summary>
        void SaveRules(List<OrganizationRule> rules);
        
        /// <summary>
        /// Adds a new rule.
        /// </summary>
        void AddRule(OrganizationRule rule);
        
        /// <summary>
        /// Updates an existing rule.
        /// </summary>
        void UpdateRule(OrganizationRule rule);
        
        /// <summary>
        /// Deletes a rule by ID.
        /// </summary>
        void DeleteRule(string ruleId);
        
        /// <summary>
        /// Gets all enabled rules sorted by priority.
        /// </summary>
        List<OrganizationRule> GetEnabledRules();
        
        /// <summary>
        /// Finds the best matching rule for a file.
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="groupName">Group/channel name</param>
        /// <param name="fileSize">File size in bytes</param>
        /// <returns>Matching rule or null</returns>
        OrganizationRule? FindMatchingRule(string fileName, string groupName, long fileSize = 0);
    }
}
