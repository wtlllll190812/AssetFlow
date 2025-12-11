using UnityEngine;

namespace AssetFlow
{
    public class RectValidator: AssetValidatorBase
    {
        public override bool IsValid(Object asset, out string errorMessage)
        {
            errorMessage = "";
            if (asset == null) return true;
            if (asset is Texture2D texture)
            {
                if (texture.width == texture.height)
                    errorMessage = $"Width must equal with height";
                else return true;
            }

            return false;
        }
    }
}