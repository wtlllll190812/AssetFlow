using System.Collections.Generic;
using UnityEditor;

namespace AssetFlow
{
    /// <summary>
    /// 自动应用 AssetFlow 的资源导入后处理器
    /// 当资源导入时，自动查找并应用对应的 AssetFlow 设置
    /// </summary>
    public class AssetFlowPostProcessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingValidation = new();

        /// <summary>
        /// 在资源导入前调用，应用 AssetFlow 设置
        /// </summary>
        private void OnPreprocessAsset()
        {
            var path = assetImporter.assetPath;

            // 快速检查：跳过 AssetFlow 文件本身和不可导入的资源
            if (!ShouldProcessAsset(path, assetImporter))
                return;

            // 查找并应用 AssetFlow 设置
            var flow = AssetFlowUtility.GetTemplateForAsset(path);
            if (flow != null && !AssetFlowUtility.IsAssetExcluded(flow, path))
            {
                AssetFlowUtility.ApplyFlow(flow, assetImporter, path);
                PendingValidation.Add(path);
            }
        }

        /// <summary>
        /// 在所有资源导入完成后调用，进行校验
        /// </summary>
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (PendingValidation.Count == 0)
                return;

            var pathsToValidate = new HashSet<string>(PendingValidation);
            PendingValidation.Clear();

            foreach (var assetPath in pathsToValidate)
            {
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                ValidateAsset(assetPath);
            }
        }

        /// <summary>
        /// 检查是否应该处理该资源
        /// </summary>
        private static bool ShouldProcessAsset(string assetPath, AssetImporter importer)
        {
            return !AssetFlowUtility.IsAssetFlow(assetPath)
                && AssetFlowUtility.IsImportableAsset(assetPath, importer);
        }

        /// <summary>
        /// 校验资源
        /// </summary>
        private static void ValidateAsset(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || !ShouldProcessAsset(assetPath, importer))
                return;

            var flow = AssetFlowUtility.GetTemplateForAsset(assetPath);
            if (flow != null && !AssetFlowUtility.IsAssetExcluded(flow, assetPath))
            {
                AssetFlowUtility.LogValidationErrors(flow, assetPath);
            }
        }
    }
}

