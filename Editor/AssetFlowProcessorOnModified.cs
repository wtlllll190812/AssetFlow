using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEngine;

namespace AssetFlow
{
    /// <summary>
    /// 自动应用 AssetFlow 的资源修改处理器
    /// 当资源发生变化时，自动查找并应用对应的 AssetFlow 设置
    /// </summary>
    public class AssetFlowProcessorOnModified : AssetsModifiedProcessor
    {
        /// <summary>
        /// 当资源被修改时调用
        /// </summary>
        protected override void OnAssetsModified(string[] changedAssets, string[] addedAssets, string[] deletedAssets, AssetMoveInfo[] movedAssets)
        {
            var flowsToProcess = new HashSet<FolderSetting>();

            // 收集所有变化的资源路径
            var allAssetPaths = CollectModifiedAssetPaths(changedAssets, addedAssets, deletedAssets, movedAssets, flowsToProcess);

            // 为所有变化的资源路径找到相关的 AssetFlow
            foreach (var assetPath in allAssetPaths)
            {
                var flow = AssetFlowUtility.GetTemplateForAsset(assetPath);
                if (flow != null)
                    flowsToProcess.Add(flow);
            }

            // 处理所有相关的 AssetFlow
            ProcessFlows(flowsToProcess);
        }

        /// <summary>
        /// 收集所有修改的资源路径
        /// </summary>
        private static HashSet<string> CollectModifiedAssetPaths(
            string[] changedAssets, string[] addedAssets, string[] deletedAssets,
            AssetMoveInfo[] movedAssets, HashSet<FolderSetting> flowsToProcess)
        {
            var allAssetPaths = new HashSet<string>();

            // 处理新增、修改、删除的资源
            var allModified = (addedAssets ?? Enumerable.Empty<string>())
                .Concat(changedAssets ?? Enumerable.Empty<string>())
                .Concat(deletedAssets ?? Enumerable.Empty<string>());

            foreach (var assetPath in allModified)
            {
                if (!IsValidAssetPath(assetPath))
                    continue;

                if (!TryAddAssetFlowIfExists(assetPath, flowsToProcess))
                    allAssetPaths.Add(assetPath);
            }

            // 处理移动的资源
            if (movedAssets != null)
            {
                foreach (var moveInfo in movedAssets)
                {
                    if (IsValidAssetPath(moveInfo.sourceAssetPath))
                        allAssetPaths.Add(moveInfo.sourceAssetPath);

                    if (IsValidAssetPath(moveInfo.destinationAssetPath)
                        && !TryAddAssetFlowIfExists(moveInfo.destinationAssetPath, flowsToProcess))
                    {
                        allAssetPaths.Add(moveInfo.destinationAssetPath);
                    }
                }
            }

            return allAssetPaths;
        }

        /// <summary>
        /// 处理所有 AssetFlow
        /// </summary>
        private static void ProcessFlows(HashSet<FolderSetting> flowsToProcess)
        {
            foreach (var flow in flowsToProcess.Where(f => f != null))
            {
                try
                {
                    AssetFlowUtility.Process(flow);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AssetFlow] 处理 AssetFlow 失败 [{flow.name}]: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 检查路径是否是有效的资源路径（非空且不是 .meta 文件）
        /// </summary>
        private static bool IsValidAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && !assetPath.EndsWith(".meta");
        }

        /// <summary>
        /// 如果路径是 AssetFlow 文件，则加载并添加到集合中
        /// </summary>
        /// <returns>如果是 AssetFlow 文件并成功添加则返回 true，否则返回 false</returns>
        private static bool TryAddAssetFlowIfExists(string assetPath, HashSet<FolderSetting> flowsToProcess)
        {
            if (!AssetFlowUtility.IsAssetFlow(assetPath))
                return false;

            var flow = AssetDatabase.LoadAssetAtPath<FolderSetting>(assetPath);
            if (flow != null)
                flowsToProcess.Add(flow);
            return true;
        }
    }
}

