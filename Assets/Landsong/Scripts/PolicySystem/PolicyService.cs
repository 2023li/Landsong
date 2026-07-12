using System;
using System.Collections.Generic;

namespace Landsong.PolicySystem
{
    public enum PolicySelectionError
    {
        None = 0,
        PolicyNotFound = 1,
        InsufficientPublicOpinion = 2
    }

    public readonly struct PolicySelectionResult
    {
        private PolicySelectionResult(
            bool succeeded,
            PolicySelectionError error,
            PolicyDefinition selectedPolicy,
            PolicyDefinition replacedPolicy)
        {
            Succeeded = succeeded;
            Error = error;
            SelectedPolicy = selectedPolicy;
            ReplacedPolicy = replacedPolicy;
        }

        public bool Succeeded { get; }
        public PolicySelectionError Error { get; }
        public PolicyDefinition SelectedPolicy { get; }
        public PolicyDefinition ReplacedPolicy { get; }

        internal static PolicySelectionResult Success(PolicyDefinition selected, PolicyDefinition replaced)
        {
            return new PolicySelectionResult(true, PolicySelectionError.None, selected, replaced);
        }

        internal static PolicySelectionResult Failure(PolicySelectionError error)
        {
            return new PolicySelectionResult(false, error, null, null);
        }
    }

    [Serializable]
    public sealed class PolicySaveData
    {
        public int PublicOpinion;
        public List<string> SelectedPolicyIds = new List<string>();

        public void Validate()
        {
            PublicOpinion = Math.Max(0, PublicOpinion);
            SelectedPolicyIds ??= new List<string>();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = SelectedPolicyIds.Count - 1; i >= 0; i--)
            {
                var policyId = NormalizeId(SelectedPolicyIds[i]);
                if (string.IsNullOrEmpty(policyId) || !seen.Add(policyId))
                {
                    SelectedPolicyIds.RemoveAt(i);
                }
                else
                {
                    SelectedPolicyIds[i] = policyId;
                }
            }
        }

        internal static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public sealed class PolicyService
    {
        private readonly PolicyCatalog catalog;
        private readonly Dictionary<PolicySlotKey, string> selectedPolicyIdsBySlot =
            new Dictionary<PolicySlotKey, string>();
        private readonly HashSet<string> activePolicyIds = new HashSet<string>(StringComparer.Ordinal);

        public PolicyService(
            PolicyCatalog catalog,
            int startingPublicOpinion = 0,
            IEnumerable<PolicyDefinition> startingSelectedPolicies = null)
        {
            this.catalog = catalog;
            PublicOpinion = Math.Max(0, startingPublicOpinion);
            RestoreSelections(startingSelectedPolicies);
            RebuildActivePolicyIds();
        }

        public int PublicOpinion { get; private set; }
        public PolicyCatalog Catalog => catalog;
        public IReadOnlyCollection<string> SelectedPolicyIds => selectedPolicyIdsBySlot.Values;
        public IReadOnlyCollection<string> ActivePolicyIds => activePolicyIds;

        public event Action<PolicyService> PublicOpinionChanged;
        public event Action<PolicyService, PolicyDefinition, PolicyDefinition> SelectionChanged;
        public event Action<PolicyService, PolicyDefinition, bool> PolicyActivationChanged;
        public event Action<PolicyService> StateChanged;

        public bool SetPublicOpinion(int value)
        {
            var normalizedValue = Math.Max(0, value);
            if (normalizedValue == PublicOpinion)
            {
                return false;
            }

            var previouslyActive = new HashSet<string>(activePolicyIds, StringComparer.Ordinal);
            PublicOpinion = normalizedValue;
            PublicOpinionChanged?.Invoke(this);
            RebuildActivePolicyIds();
            PublishActivationChanges(previouslyActive);
            StateChanged?.Invoke(this);
            return true;
        }

        public bool AddPublicOpinion(int delta)
        {
            var target = (long)PublicOpinion + delta;
            return SetPublicOpinion((int)Math.Max(0L, Math.Min(int.MaxValue, target)));
        }

        public bool CanSelectPolicy(string policyId)
        {
            return TryGetDefinition(policyId, out var definition)
                   && PublicOpinion >= definition.RequiredPublicOpinion;
        }

        public PolicySelectionResult TrySelectPolicy(string policyId)
        {
            if (!TryGetDefinition(policyId, out var definition))
            {
                return PolicySelectionResult.Failure(PolicySelectionError.PolicyNotFound);
            }

            if (PublicOpinion < definition.RequiredPublicOpinion)
            {
                return PolicySelectionResult.Failure(PolicySelectionError.InsufficientPublicOpinion);
            }

            var slot = new PolicySlotKey(definition.TreeId, definition.Tier);
            var replaced = GetSelectedPolicy(slot);
            if (ReferenceEquals(replaced, definition)
                || replaced != null && string.Equals(replaced.PolicyId, definition.PolicyId, StringComparison.Ordinal))
            {
                return PolicySelectionResult.Success(definition, replaced);
            }

            var previouslyActive = new HashSet<string>(activePolicyIds, StringComparer.Ordinal);
            selectedPolicyIdsBySlot[slot] = definition.PolicyId;
            RebuildActivePolicyIds();
            SelectionChanged?.Invoke(this, definition, replaced);
            PublishActivationChanges(previouslyActive);
            StateChanged?.Invoke(this);
            return PolicySelectionResult.Success(definition, replaced);
        }

        public bool TryClearSelection(string treeId, int tier)
        {
            if (string.IsNullOrWhiteSpace(treeId) || tier < 1)
            {
                return false;
            }

            var slot = new PolicySlotKey(treeId, tier);
            var selected = GetSelectedPolicy(slot);
            if (selected == null || !selectedPolicyIdsBySlot.Remove(slot))
            {
                return false;
            }

            var previouslyActive = new HashSet<string>(activePolicyIds, StringComparer.Ordinal);
            RebuildActivePolicyIds();
            SelectionChanged?.Invoke(this, null, selected);
            PublishActivationChanges(previouslyActive);
            StateChanged?.Invoke(this);
            return true;
        }

        public PolicyDefinition GetSelectedPolicy(string treeId, int tier)
        {
            if (string.IsNullOrWhiteSpace(treeId) || tier < 1)
            {
                return null;
            }

            return GetSelectedPolicy(new PolicySlotKey(treeId, tier));
        }

        public bool IsPolicySelected(string policyId)
        {
            if (!TryGetDefinition(policyId, out var definition))
            {
                return false;
            }

            var selected = GetSelectedPolicy(definition.TreeId, definition.Tier);
            return selected != null && string.Equals(selected.PolicyId, definition.PolicyId, StringComparison.Ordinal);
        }

        public bool IsPolicyActive(string policyId)
        {
            var normalizedId = PolicySaveData.NormalizeId(policyId);
            return !string.IsNullOrEmpty(normalizedId) && activePolicyIds.Contains(normalizedId);
        }

        public PolicySaveData CaptureSaveData()
        {
            var saveData = new PolicySaveData
            {
                PublicOpinion = PublicOpinion,
                SelectedPolicyIds = new List<string>(selectedPolicyIdsBySlot.Values)
            };
            saveData.SelectedPolicyIds.Sort(StringComparer.Ordinal);
            saveData.Validate();
            return saveData;
        }

        public void RestoreSaveData(PolicySaveData saveData)
        {
            var previouslyActive = new HashSet<string>(activePolicyIds, StringComparer.Ordinal);
            var previousPublicOpinion = PublicOpinion;
            selectedPolicyIdsBySlot.Clear();

            if (saveData == null)
            {
                PublicOpinion = 0;
            }
            else
            {
                saveData.Validate();
                PublicOpinion = saveData.PublicOpinion;
                for (var i = 0; i < saveData.SelectedPolicyIds.Count; i++)
                {
                    if (!TryGetDefinition(saveData.SelectedPolicyIds[i], out var definition))
                    {
                        continue;
                    }

                    var slot = new PolicySlotKey(definition.TreeId, definition.Tier);
                    if (!selectedPolicyIdsBySlot.ContainsKey(slot))
                    {
                        selectedPolicyIdsBySlot.Add(slot, definition.PolicyId);
                    }
                }
            }

            RebuildActivePolicyIds();
            if (previousPublicOpinion != PublicOpinion)
            {
                PublicOpinionChanged?.Invoke(this);
            }

            PublishActivationChanges(previouslyActive);
            StateChanged?.Invoke(this);
        }

        private void RestoreSelections(IEnumerable<PolicyDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null || !TryGetDefinition(definition.PolicyId, out var catalogDefinition))
                {
                    continue;
                }

                selectedPolicyIdsBySlot[new PolicySlotKey(catalogDefinition.TreeId, catalogDefinition.Tier)] =
                    catalogDefinition.PolicyId;
            }
        }

        private bool TryGetDefinition(string policyId, out PolicyDefinition definition)
        {
            if (catalog != null)
            {
                return catalog.TryGetDefinition(policyId, out definition);
            }

            definition = null;
            return false;
        }

        private PolicyDefinition GetSelectedPolicy(PolicySlotKey slot)
        {
            return selectedPolicyIdsBySlot.TryGetValue(slot, out var policyId)
                   && TryGetDefinition(policyId, out var definition)
                ? definition
                : null;
        }

        private void RebuildActivePolicyIds()
        {
            activePolicyIds.Clear();
            foreach (var policyId in selectedPolicyIdsBySlot.Values)
            {
                if (TryGetDefinition(policyId, out var definition)
                    && PublicOpinion >= definition.RequiredPublicOpinion)
                {
                    activePolicyIds.Add(definition.PolicyId);
                }
            }
        }

        private void PublishActivationChanges(HashSet<string> previouslyActive)
        {
            foreach (var policyId in previouslyActive)
            {
                if (!activePolicyIds.Contains(policyId) && TryGetDefinition(policyId, out var definition))
                {
                    PolicyActivationChanged?.Invoke(this, definition, false);
                }
            }

            foreach (var policyId in activePolicyIds)
            {
                if (!previouslyActive.Contains(policyId) && TryGetDefinition(policyId, out var definition))
                {
                    PolicyActivationChanged?.Invoke(this, definition, true);
                }
            }
        }

        private readonly struct PolicySlotKey : IEquatable<PolicySlotKey>
        {
            public PolicySlotKey(string treeId, int tier)
            {
                TreeId = PolicySaveData.NormalizeId(treeId);
                Tier = Math.Max(1, tier);
            }

            private string TreeId { get; }
            private int Tier { get; }

            public bool Equals(PolicySlotKey other)
            {
                return Tier == other.Tier && string.Equals(TreeId, other.TreeId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PolicySlotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((TreeId != null ? StringComparer.Ordinal.GetHashCode(TreeId) : 0) * 397) ^ Tier;
                }
            }
        }
    }
}
