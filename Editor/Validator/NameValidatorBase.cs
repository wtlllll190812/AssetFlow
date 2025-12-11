using UnityEngine;

namespace Editor.AssetFlow
{
    public class NameValidatorBase : AssetValidatorBase
    {
        [SerializeField]
        public string pattern;

        public override bool IsValid(Object asset, out string errorMessage)
        {
            errorMessage = null;
            if (asset == null)
            {
                errorMessage = "资源为空";
                return false;
            }

            if (string.IsNullOrEmpty(pattern))
            {
                errorMessage = "正则表达式未设置";
                return false;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(asset.name, pattern))
            {
                errorMessage = $"资源名称 \"{asset.name}\" 不符合正则表达式: {pattern}";
                return false;
            }

            return true;
        }
    }
}