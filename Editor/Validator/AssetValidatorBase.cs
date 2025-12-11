using UnityEngine;

namespace Editor.AssetFlow
{
    public abstract class AssetValidatorBase
    {
        public abstract bool IsValid(Object asset, out string errorMessage);
    }
}