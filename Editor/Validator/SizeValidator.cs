using System;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace AssetFlow
{
    public class SizeValidator : AssetValidatorBase
    {
        public int width;
        public int height;

        public override bool IsValid(Object asset, out string errorMessage)
        {
            errorMessage = "";
            if (asset == null) return true;
            if (asset is Texture2D texture)
            {
                if (texture.width != width || texture.height != height)
                    errorMessage = $"Width and height must match width and height: {width}x{height}";
                else return true;
            }

            return false;
        }
    }
}