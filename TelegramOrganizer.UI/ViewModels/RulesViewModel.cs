using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.UI.ViewModels
{
    public partial class RulesViewModel : ObservableObject
    {
        private readonly IRulesService _rulesService;

        private ObservableCollection<OrganizationRule> _rules = new();
        public ObservableCollection<OrganizationRule> Rules
        {
            get => _rules;
            set => SetProperty(ref _rules, value);
        }

        private OrganizationRule? _selectedRule;
        public OrganizationRule? SelectedRule
        {
            get => _selectedRule;
            set => SetProperty(ref _selectedRule, value);
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        // Edit fields
        private string _editName = string.Empty;
        public string EditName
        {
            get => _editName;
            set => SetProperty(ref _editName, value);
        }

        private string _editDescription = string.Empty;
        public string EditDescription
        {
            get => _editDescription;
            set => SetProperty(ref _editDescription, value);
        }

        private RuleType _editRuleType;
        public RuleType EditRuleType
        {
            get => _editRuleType;
            set => SetProperty(ref _editRuleType, value);
        }

        private string _editPattern = string.Empty;
        public string EditPattern
        {
            get => _editPattern;
            set => SetProperty(ref _editPattern, value);
        }

        private PatternMatchType _editMatchType;
        public PatternMatchType EditMatchType
        {
            get => _editMatchType;
            set => SetProperty(ref _editMatchType, value);
        }

        private string _editTargetFolder = string.Empty;
        public string EditTargetFolder
        {
            get => _editTargetFolder;
            set => SetProperty(ref _editTargetFolder, value);
        }

        private int _editPriority;
        public int EditPriority
        {
            get => _editPriority;
            set => SetProperty(ref _editPriority, value);
        }

        private bool _editIsEnabled = true;
        public bool EditIsEnabled
        {
            get => _editIsEnabled;
            set => SetProperty(ref _editIsEnabled, value);
        }

        // Combo box sources
        public Array RuleTypes => Enum.GetValues(typeof(RuleType));
        public Array MatchTypes => Enum.GetValues(typeof(PatternMatchType));

        public RulesViewModel(IRulesService rulesService)
        {
            _rulesService = rulesService;
            LoadRules();
        }

        private void LoadRules()
        {
            var rules = _rulesService.LoadRules();
            Rules = new ObservableCollection<OrganizationRule>(rules.OrderByDescending(r => r.Priority));
        }

        [RelayCommand]
        private void NewRule()
        {
            SelectedRule = null;
            IsEditing = true;

            // Reset edit fields
            EditName = "New Rule";
            EditDescription = string.Empty;
            EditRuleType = RuleType.FileExtension;
            EditPattern = string.Empty;
            EditMatchType = PatternMatchType.Contains;
            EditTargetFolder = string.Empty;
            EditPriority = 0;
            EditIsEnabled = true;
        }

        [RelayCommand]
        private void EditRule()
        {
            if (SelectedRule == null) return;

            IsEditing = true;
            EditName = SelectedRule.Name;
            EditDescription = SelectedRule.Description;
            EditRuleType = SelectedRule.RuleType;
            EditPattern = SelectedRule.Pattern;
            EditMatchType = SelectedRule.MatchType;
            EditTargetFolder = SelectedRule.TargetFolder;
            EditPriority = SelectedRule.Priority;
            EditIsEnabled = SelectedRule.IsEnabled;
        }

        [RelayCommand]
        private void SaveRule()
        {
            if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditPattern))
            {
                return;
            }

            if (SelectedRule == null)
            {
                // New rule
                var newRule = new OrganizationRule
                {
                    Name = EditName,
                    Description = EditDescription,
                    RuleType = EditRuleType,
                    Pattern = EditPattern,
                    MatchType = EditMatchType,
                    TargetFolder = EditTargetFolder,
                    Priority = EditPriority,
                    IsEnabled = EditIsEnabled
                };
                _rulesService.AddRule(newRule);
            }
            else
            {
                // Update existing
                SelectedRule.Name = EditName;
                SelectedRule.Description = EditDescription;
                SelectedRule.RuleType = EditRuleType;
                SelectedRule.Pattern = EditPattern;
                SelectedRule.MatchType = EditMatchType;
                SelectedRule.TargetFolder = EditTargetFolder;
                SelectedRule.Priority = EditPriority;
                SelectedRule.IsEnabled = EditIsEnabled;
                SelectedRule.ModifiedAt = DateTime.Now;

                _rulesService.UpdateRule(SelectedRule);
            }

            IsEditing = false;
            LoadRules();
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
        }

        [RelayCommand]
        private void DeleteRule()
        {
            if (SelectedRule == null) return;

            _rulesService.DeleteRule(SelectedRule.Id);
            LoadRules();
            SelectedRule = null;
        }

        [RelayCommand]
        private void ToggleRule()
        {
            if (SelectedRule == null) return;

            SelectedRule.IsEnabled = !SelectedRule.IsEnabled;
            _rulesService.UpdateRule(SelectedRule);
            LoadRules();
        }

        [RelayCommand]
        private void MoveUp()
        {
            if (SelectedRule == null) return;

            SelectedRule.Priority += 1;
            _rulesService.UpdateRule(SelectedRule);
            LoadRules();
        }

        [RelayCommand]
        private void MoveDown()
        {
            if (SelectedRule == null) return;

            SelectedRule.Priority = Math.Max(0, SelectedRule.Priority - 1);
            _rulesService.UpdateRule(SelectedRule);
            LoadRules();
        }
    }
}
