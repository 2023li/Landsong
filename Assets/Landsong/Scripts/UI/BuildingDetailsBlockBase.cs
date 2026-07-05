using Landsong.BuildingSystem;
using UnityEngine;

public abstract class BuildingDetailsBlockBase : MonoBehaviour
{
    public virtual void Initialize(Popup_BuildingDetails detailOwner)
    {
    }

    public abstract bool CanShow(BuildingBase targetBuilding);
    public abstract void Bind(BuildingBase targetBuilding);
    public abstract void Refresh();
    public abstract void Unbind();
}
