using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;

namespace TelegramOrganizer.Tests.Services
{
    public class JsonRulesServiceTests
    {
        private JsonRulesService CreateService()
        {
            return new JsonRulesService();
        }

        [Fact]
        public void LoadRules_ReturnsRules()
        {
            // Arrange
            var service = CreateService();

            // Act
            var rules = service.LoadRules();

            // Assert
            Assert.NotNull(rules);
            Assert.NotEmpty(rules);
        }

        [Fact]
        public void LoadRules_ContainsImagesRule()
        {
            // Arrange
            var service = CreateService();

            // Act
            var rules = service.LoadRules();

            // Assert
            Assert.Contains(rules, r => r.Name == "Images" && r.RuleType == RuleType.FileExtension);
        }

        [Fact]
        public void LoadRules_ContainsDocumentsRule()
        {
            // Arrange
            var service = CreateService();

            // Act
            var rules = service.LoadRules();

            // Assert
            Assert.Contains(rules, r => r.Name == "Documents" && r.RuleType == RuleType.FileExtension);
        }

        [Fact]
        public void GetEnabledRules_RulesAreOrderedByPriority()
        {
            // Arrange
            var service = CreateService();

            // Act
            var rules = service.GetEnabledRules();

            // Assert - If there are enabled rules, they should be ordered
            if (rules.Count > 1)
            {
                for (int i = 1; i < rules.Count; i++)
                {
                    Assert.True(rules[i - 1].Priority >= rules[i].Priority,
                        $"Rules should be ordered by priority descending");
                }
            }
        }

        [Fact]
        public void AddRule_IncreasesRuleCount()
        {
            // Arrange
            var service = CreateService();
            var initialCount = service.LoadRules().Count;
            
            var newRule = new OrganizationRule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Rule " + Guid.NewGuid().ToString(),
                RuleType = RuleType.FileExtension,
                Pattern = ".xyz" + Guid.NewGuid().ToString().Substring(0, 4),
                MatchType = PatternMatchType.Exact,
                TargetFolder = "CustomFolder",
                Priority = 100,
                IsEnabled = true
            };

            // Act
            service.AddRule(newRule);
            var rules = service.LoadRules();

            // Assert
            Assert.Equal(initialCount + 1, rules.Count);
            Assert.Contains(rules, r => r.Id == newRule.Id);
            
            // Cleanup
            service.DeleteRule(newRule.Id);
        }

        [Fact]
        public void DeleteRule_RemovesRule()
        {
            // Arrange
            var service = CreateService();
            var newRule = new OrganizationRule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Rule To Delete " + Guid.NewGuid().ToString(),
                RuleType = RuleType.FileExtension,
                Pattern = ".del",
                TargetFolder = "DeleteMe",
                Priority = 50,
                IsEnabled = true
            };
            service.AddRule(newRule);
            var countAfterAdd = service.LoadRules().Count;

            // Act
            service.DeleteRule(newRule.Id);
            var rules = service.LoadRules();

            // Assert
            Assert.Equal(countAfterAdd - 1, rules.Count);
            Assert.DoesNotContain(rules, r => r.Id == newRule.Id);
        }

        [Fact]
        public void UpdateRule_ModifiesExistingRule()
        {
            // Arrange
            var service = CreateService();
            var newRule = new OrganizationRule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Original Name " + Guid.NewGuid().ToString(),
                RuleType = RuleType.FileExtension,
                Pattern = ".orig",
                TargetFolder = "Original",
                Priority = 50,
                IsEnabled = true
            };
            service.AddRule(newRule);

            // Act
            newRule.Name = "Updated Name";
            newRule.TargetFolder = "Updated";
            service.UpdateRule(newRule);
            
            var rules = service.LoadRules();
            var updatedRule = rules.FirstOrDefault(r => r.Id == newRule.Id);

            // Assert
            Assert.NotNull(updatedRule);
            Assert.Equal("Updated Name", updatedRule.Name);
            Assert.Equal("Updated", updatedRule.TargetFolder);
            
            // Cleanup
            service.DeleteRule(newRule.Id);
        }

        [Fact]
        public void FindMatchingRule_ReturnsNull_WhenNoRulesEnabled()
        {
            // Arrange
            var service = CreateService();
            
            // Disable all rules temporarily
            var rules = service.LoadRules();
            var enabledRules = rules.Where(r => r.IsEnabled).ToList();
            foreach (var rule in enabledRules)
            {
                rule.IsEnabled = false;
                service.UpdateRule(rule);
            }

            // Act
            var matchedRule = service.FindMatchingRule("document.pdf", "AnyGroup", 1000);

            // Assert
            Assert.Null(matchedRule);
            
            // Cleanup - re-enable
            foreach (var rule in enabledRules)
            {
                rule.IsEnabled = true;
                service.UpdateRule(rule);
            }
        }

        [Fact]
        public void LoadRules_ImagesRule_HasCorrectExtensions()
        {
            // Arrange
            var service = CreateService();

            // Act
            var rules = service.LoadRules();
            var imagesRule = rules.FirstOrDefault(r => r.Name == "Images");

            // Assert
            Assert.NotNull(imagesRule);
            Assert.Contains(".jpg", imagesRule.Pattern);
            Assert.Contains(".png", imagesRule.Pattern);
        }

        [Fact]
        public void AddRule_ThenFindMatchingRule_ReturnsAddedRule()
        {
            // Arrange
            var service = CreateService();
            var uniqueExt = ".test" + Guid.NewGuid().ToString().Substring(0, 4);
            var newRule = new OrganizationRule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Match Rule",
                RuleType = RuleType.FileExtension,
                Pattern = uniqueExt,
                MatchType = PatternMatchType.Contains,
                TargetFolder = "TestMatch",
                Priority = 1000, // High priority
                IsEnabled = true
            };
            service.AddRule(newRule);

            // Act
            var matchedRule = service.FindMatchingRule("file" + uniqueExt, "AnyGroup", 100);

            // Assert
            Assert.NotNull(matchedRule);
            Assert.Equal(newRule.Id, matchedRule?.Id);
            
            // Cleanup
            service.DeleteRule(newRule.Id);
        }
    }
}
