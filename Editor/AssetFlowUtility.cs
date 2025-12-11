using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Editor.AssetFlow
{
    /// <summary>
    /// 导入器模板工具类
    /// </summary>
    public static class AssetFlowUtility
    {
        // 跟踪正在处理的资源路径，避免重复导入
        private static readonly HashSet<string> ProcessingAssets = new();

        /// <summary>
        /// 处理 AssetFlow：应用设置、清理排除列表、校验资源
        /// </summary>
        public static void Process(FolderSetting flow)
        {
            if (flow == null || flow.Importer == null)
                return;

            var templatePath = AssetDatabase.GetAssetPath(flow);
            if (string.IsNullOrEmpty(templatePath))
                return;

            var templateFolder = NormalizePath(Path.GetDirectoryName(templatePath));
            var importerType = flow.Importer.GetType();

            // 1. 获取所有可以触达的资源（在作用范围内的资源）
            var reachableAssets = new List<Object>();
            CollectAffectedAssets(templateFolder, importerType, flow.includeSubfolders, reachableAssets);

            // 2. 清理排除列表（移除不在可触达资源中的项）
            CleanupExcludeListByReachable(flow, reachableAssets);

            // 3. 获取托管资源（触达资源中排除掉 excluded 资源）
            var managedAssets = GetManagedAssets(reachableAssets, flow.ExcludeAssetPaths);

            // 4. 应用设置并收集需要重新导入的资源
            var assetsToReimport = ApplyFlowToAssets(flow, managedAssets, importerType);

            // 5. 延迟重新导入，避免在导入过程中触发新的导入
            if (assetsToReimport.Count > 0)
            {
                ScheduleReimport(assetsToReimport);
            }

            // 6. 校验所有托管资源
            ValidateManagedAssets(flow, managedAssets);
        }

        /// <summary>
        /// 校验资源并输出错误日志
        /// </summary>
        public static void LogValidationErrors(FolderSetting flow, string assetPath)
        {
            var errors = ValidateAsset(flow, assetPath);
            if (errors == null || errors.Count == 0)
                return;

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            foreach (var error in errors)
            {
                Debug.LogError($"[AssetFlow 校验失败] {assetPath}: {error}", asset);
            }
        }

        /// <summary>
        /// 获取指定资源文件对应的最匹配的 AssetFlow（从资源文件所在文件夹逐级向上查找）
        /// </summary>
        public static FolderSetting GetTemplateForAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            var normalizedPath = NormalizePath(assetPath);
            var importer = AssetImporter.GetAtPath(normalizedPath);
            if (importer == null || !IsImportableAsset(normalizedPath, importer))
                return null;

            var importerType = importer.GetType();
            var assetFolder = NormalizePath(Path.GetDirectoryName(normalizedPath));

            // 从资源文件所在文件夹开始，逐级向上查找 FolderSetting.asset
            return TraverseUpFolders(assetFolder, folder =>
            {
                var flow = GetAssetFlowInFolder(folder);
                if (flow == null)
                    return TraverseResult<FolderSetting>.Continue();

                // 如果类型不匹配，停止查找（找到了但不可用）
                if (flow.Importer?.GetType() != importerType)
                    return TraverseResult<FolderSetting>.Stop();

                var flowFolder = NormalizePath(Path.GetDirectoryName(AssetDatabase.GetAssetPath(flow)));
                return IsAssetAffectedByFlow(assetFolder, flow, flowFolder)
                    ? TraverseResult<FolderSetting>.Continue(flow)
                    : TraverseResult<FolderSetting>.Continue();
            });
        }

        /// <summary>
        /// 检查资源是否在排除列表中
        /// </summary>
        public static bool IsAssetExcluded(FolderSetting flow, string assetPath)
        {
            if (flow?.ExcludeAssetPaths == null || flow.ExcludeAssetPaths.Count == 0 || string.IsNullOrEmpty(assetPath))
                return false;

            assetPath = NormalizePath(assetPath);
            // 使用 LINQ 简化查找
            return flow.ExcludeAssetPaths.Exists(path =>
                !string.IsNullOrEmpty(path) && NormalizePath(path) == assetPath);
        }

        /// <summary>
        /// 检查指定文件夹中是否存在 AssetFlow 文件（不考虑类型）
        /// </summary>
        public static bool HasAssetFlowInFolder(string folderPath)
        {
            return GetAssetFlowInFolder(folderPath) != null;
        }

        /// <summary>
        /// 检查资源是否是可导入的类型（只允许图片、模型、音频文件）
        /// </summary>
        public static bool IsImportableAsset(string assetPath, AssetImporter importer)
        {
            return importer != null && (
                importer is TextureImporter ||
                importer is ModelImporter ||
                importer is AudioImporter);
        }

        /// <summary>
        /// 判断是否是 FolderSetting 文件
        /// </summary>
        public static bool IsAssetFlow(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   Path.GetFileName(assetPath).Equals(FolderSetting.FolderSettingFileName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 应用 AssetFlow 设置到导入器
        /// </summary>
        /// <param name="flow">AssetFlow 模板</param>
        /// <param name="importer">目标导入器</param>
        /// <param name="assetPath">资源路径（用于日志）</param>
        /// <returns>是否成功应用</returns>
        public static bool ApplyFlow(FolderSetting flow, AssetImporter importer, string assetPath)
        {
            if (flow?.Importer == null || importer == null || importer.GetType() != flow.Importer.GetType())
                return false;

            try
            {
                EditorUtility.CopySerialized(flow.Importer, importer);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AssetFlow] 应用 AssetFlow 失败 [{assetPath}]: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 标准化路径（将反斜杠替换为正斜杠）
        /// </summary>
        public static string NormalizePath(string path)
        {
            return path?.Replace("\\", "/");
        }

        /// <summary>
        /// 获取托管资源列表（从触达资源中排除指定路径）
        /// </summary>
        private static List<Object> GetManagedAssets(List<Object> reachableAssets, List<string> excludePaths)
        {
            if (excludePaths == null || excludePaths.Count == 0)
                return new List<Object>(reachableAssets);

            var managedAssets = new List<Object>(reachableAssets);
            ExcludeAssetsByPath(managedAssets, excludePaths);
            return managedAssets;
        }

        /// <summary>
        /// 应用 Flow 设置到资源，返回需要重新导入的资源路径列表
        /// </summary>
        private static List<string> ApplyFlowToAssets(FolderSetting flow, List<Object> managedAssets, Type importerType)
        {
            var assetsToReimport = new List<string>();

            foreach (var asset in managedAssets)
            {
                var assetPath = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer?.GetType() != importerType)
                    continue;

                if (ApplyFlow(flow, importer, assetPath))
                {
                    var normalizedPath = NormalizePath(assetPath);
                    if (!ProcessingAssets.Contains(normalizedPath))
                        assetsToReimport.Add(normalizedPath);
                }
            }

            return assetsToReimport;
        }

        /// <summary>
        /// 安排资源重新导入（延迟执行）
        /// </summary>
        private static void ScheduleReimport(List<string> assetPaths)
        {
            foreach (var path in assetPaths)
            {
                ProcessingAssets.Add(path);
            }

            EditorApplication.delayCall += () =>
            {
                foreach (var assetPath in assetPaths)
                {
                    try
                    {
                        var importer = AssetImporter.GetAtPath(assetPath);
                        importer?.SaveAndReimport();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[AssetFlow] 重新导入资源失败 [{assetPath}]: {e.Message}");
                    }
                    finally
                    {
                        ProcessingAssets.Remove(assetPath);
                    }
                }
            };
        }

        /// <summary>
        /// 校验所有托管资源
        /// </summary>
        private static void ValidateManagedAssets(FolderSetting flow, List<Object> managedAssets)
        {
            foreach (var asset in managedAssets)
            {
                var assetPath = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(assetPath))
                    LogValidationErrors(flow, assetPath);
            }
        }

        /// <summary>
        /// 校验资源，返回所有校验失败的错误信息
        /// </summary>
        private static List<string> ValidateAsset(FolderSetting flow, string assetPath)
        {
            var errors = new List<string>();
            if (flow == null || string.IsNullOrEmpty(assetPath) || IsAssetExcluded(flow, assetPath))
                return errors;

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null || flow.Validators == null || flow.Validators.Count == 0)
                return errors;

            foreach (var validator in flow.Validators)
            {
                if (validator == null)
                    continue;

                try
                {
                    if (!validator.IsValid(asset, out string errorMessage))
                    {
                        errors.Add(string.IsNullOrEmpty(errorMessage)
                            ? $"资源验证失败: {assetPath}"
                            : errorMessage);
                    }
                }
                catch (System.Exception e)
                {
                    errors.Add($"校验器执行异常 [{assetPath}]: {e.Message}");
                }
            }

            return errors;
        }



        private static void CollectAffectedAssets(string folderPath, Type importerType, bool includeSubfolders,
            List<Object> results)
        {
            if (!Directory.Exists(folderPath))
                return;

            folderPath = NormalizePath(folderPath);

            // 获取当前文件夹所有文件，使用Unity判断类型
            foreach (var file in Directory.GetFiles(folderPath))
            {
                if (file.EndsWith(".meta", StringComparison.Ordinal))
                    continue;

                var assetPath = NormalizePath(file);
                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer?.GetType() == importerType)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (asset != null)
                        results.Add(asset);
                }
            }

            // 递归子文件夹（如果需要且子文件夹没有同类型模板）
            if (includeSubfolders)
            {
                foreach (var subFolder in Directory.GetDirectories(folderPath))
                {
                    var subFolderPath = NormalizePath(subFolder);
                    if (!HasAssetFlowOfType(subFolderPath, importerType))
                    {
                        CollectAffectedAssets(subFolderPath, importerType, true, results);
                    }
                }
            }
        }

        /// <summary>
        /// 检查指定文件夹是否有指定类型的 AssetFlow
        /// </summary>
        private static bool HasAssetFlowOfType(string folderPath, Type importerType)
        {
            var flow = GetAssetFlowInFolder(folderPath);
            return flow?.Importer?.GetType() == importerType;
        }

        /// <summary>
        /// 检查资源文件是否被指定的 AssetFlow 影响
        /// </summary>
        private static bool IsAssetAffectedByFlow(string assetFolder, FolderSetting flow, string flowFolder)
        {
            // 资源文件必须在 AssetFlow 所在文件夹或其子文件夹中
            if (!IsPathUnderFolder(assetFolder, flowFolder))
                return false;

            // 如果资源文件在 AssetFlow 的直接文件夹中，直接匹配
            if (assetFolder == flowFolder)
                return true;

            // 如果资源在子文件夹中，需要检查 AssetFlow 是否包含子文件夹
            if (!flow.includeSubfolders)
                return false;

            // 检查从 assetFolder 向上到 flowFolder 的路径上是否有更近的同类型 AssetFlow
            var importerType = flow.Importer.GetType();
            var currentPath = assetFolder;

            while (currentPath != flowFolder)
            {
                if (HasAssetFlowOfType(currentPath, importerType))
                    return false;

                var parentPath = NormalizePath(Path.GetDirectoryName(currentPath));
                if (string.IsNullOrEmpty(parentPath) || parentPath == currentPath)
                    break;
                currentPath = parentPath;
            }

            return true;
        }

        /// <summary>
        /// 从受影响资源列表中排除指定路径的资源
        /// </summary>
        private static void ExcludeAssetsByPath(List<Object> assets, List<string> excludePaths)
        {
            if (excludePaths == null || excludePaths.Count == 0 || assets.Count == 0)
                return;

            // 构建排除路径集合（标准化路径，使用 LINQ 简化）
            var normalizedExcludePaths = new HashSet<string>(
                excludePaths
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(NormalizePath)
            );

            if (normalizedExcludePaths.Count == 0)
                return;

            // 移除排除的资源
            assets.RemoveAll(asset =>
            {
                if (asset == null)
                    return false;
                var path = AssetDatabase.GetAssetPath(asset);
                return !string.IsNullOrEmpty(path) && normalizedExcludePaths.Contains(NormalizePath(path));
            });
        }

        /// <summary>
        /// 检查路径是否在指定文件夹下（包括子文件夹）
        /// </summary>
        private static bool IsPathUnderFolder(string path, string folder)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder))
                return false;

            var normalizedPath = NormalizePath(path);
            var normalizedFolder = NormalizePath(folder);

            // 路径必须等于文件夹或以其开头
            return normalizedPath == normalizedFolder
                   || normalizedPath.StartsWith(normalizedFolder + "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// 在指定文件夹中查找 FolderSetting.asset 文件
        /// </summary>
        private static FolderSetting GetAssetFlowInFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return null;

            var flowPath = NormalizePath(Path.Combine(folderPath, FolderSetting.FolderSettingFileName));
            return AssetDatabase.LoadAssetAtPath<FolderSetting>(flowPath);
        }

        /// <summary>
        /// 向上遍历文件夹，对每个文件夹执行操作
        /// </summary>
        private static T TraverseUpFolders<T>(string startFolder, Func<string, TraverseResult<T>> folderAction)
            where T : class
        {
            if (string.IsNullOrEmpty(startFolder) || folderAction == null)
                return null;

            var currentFolder = NormalizePath(startFolder);
            while (!string.IsNullOrEmpty(currentFolder))
            {
                var result = folderAction(currentFolder);
                if (result.StopSearch || result.Value != null)
                    return result.Value;

                var parentFolder = NormalizePath(Path.GetDirectoryName(currentFolder));
                if (string.IsNullOrEmpty(parentFolder) || parentFolder == currentFolder)
                    break;
                currentFolder = parentFolder;
            }

            return null;
        }

        private struct TraverseResult<T> where T : class
        {
            public T Value;
            public bool StopSearch;
            public static TraverseResult<T> Continue(T value = null) => new() { Value = value, StopSearch = false };
            public static TraverseResult<T> Stop(T value = null) => new() { Value = value, StopSearch = true };
        }

        /// <summary>
        /// 根据可触达资源列表清理排除列表（移除不在可触达资源中的项）
        /// </summary>
        private static void CleanupExcludeListByReachable(FolderSetting flow, List<Object> reachableAssets)
        {
            if (flow?.ExcludeAssetPaths == null || flow.ExcludeAssetPaths.Count == 0)
                return;

            // 构建可触达资源的路径集合（使用 LINQ 简化）
            var reachablePaths = new HashSet<string>(
                reachableAssets
                    .Where(asset => asset != null)
                    .Select(asset => AssetDatabase.GetAssetPath(asset))
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(NormalizePath)
            );

            // 移除不在可触达资源列表中的排除项
            int removedCount = flow.ExcludeAssetPaths.RemoveAll(path =>
                string.IsNullOrEmpty(path) || !reachablePaths.Contains(NormalizePath(path)));

            if (removedCount > 0)
            {
                EditorUtility.SetDirty(flow);
                AssetDatabase.SaveAssets();
            }
        }
    }
}