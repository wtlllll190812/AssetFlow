using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetFlow
{
    public class POWValidator : AssetValidatorBase
    {
        public override bool IsValid(Object asset, out string errorMessage)
        {
            errorMessage = "";
            if (asset == null) return true;
            if (asset is Texture2D texture)
            {
                var isWidthPOW = Mathf.IsPowerOfTwo(texture.width);
                var isHeightPOW = Mathf.IsPowerOfTwo(texture.height);

                if (isWidthPOW && isHeightPOW)
                    return true;

                if (isWidthPOW)
                    errorMessage = "素材宽不是2的幂次,";
                if (isHeightPOW)
                    errorMessage += "素材高不是2的幂次";
            }

            return false;
        }
    }
}