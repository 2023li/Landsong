using System.Collections.Generic;
using Landsong.BuildingSystem;
using UnityEngine;

public abstract class BuildingDetailsBlockBase : MonoBehaviour
{
    public virtual void Initialize(Popup_BuildingDetails detailOwner)
    {
    }

    public abstract bool ValidateConfiguration(out string error);
    public abstract bool CanShow(BuildingBase targetBuilding);
    public abstract void Bind(BuildingBase targetBuilding);
    public abstract void Refresh();
    public abstract void Unbind();

    protected static void AddMissingReference(
        List<string> missing,
        Object target,
        string fieldName)
    {
        if (target == null)
        {
            missing.Add(fieldName);
        }
    }

    protected static bool BuildValidationResult(List<string> missing, out string error)
    {
        error = missing == null || missing.Count == 0
            ? string.Empty
            : $"缺少必需引用：{string.Join("、", missing)}";
        return string.IsNullOrEmpty(error);
    }
}

public readonly struct BuildingDetailsSidebarRow
{
    public BuildingDetailsSidebarRow(string label, string value)
        : this(label, value, 0f, false)
    {
    }

    public BuildingDetailsSidebarRow(string label, string value, float signedValue, bool hasSignedValue)
    {
        Label = label;
        Value = value;
        SignedValue = signedValue;
        HasSignedValue = hasSignedValue;
    }

    public string Label { get; }

    public string Value { get; }

    public float SignedValue { get; }

    public bool HasSignedValue { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Label) || !string.IsNullOrWhiteSpace(Value);
}
