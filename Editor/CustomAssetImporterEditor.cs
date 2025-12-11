using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetFlow
{
    /// <summary>
    /// 资源导入器的统一管理编辑器
    /// </summary>
    [InitializeOnLoad]
    public class CustomAssetImporterEditor
    {
        static CustomAssetImporterEditor()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(UnityEditor.Editor editor)
        {
            // 检查是否是单个资源对象
            if (editor.targets.Length != 1 || editor.target == null)
                return;

            // 获取资源路径
            var assetPath = AssetDatabase.GetAssetPath(editor.target);
            if (string.IsNullOrEmpty(assetPath))
                return;

            // 获取资源的 AssetImporter
            var importer = AssetImporter.GetAtPath(assetPath);
            // 只显示在存在 importer 的资源文件中
            if (importer == null)
                return;

            // 检查是否是可导入的类型（排除文件夹、脚本等不需要导入设置的资源）
            if (!AssetFlowUtility.IsImportableAsset(assetPath, importer))
                return;

            // 获取实际的资源对象（而不是 importer 对象）
            Object assetObject = editor.target;
            // 如果 editor.target 是 AssetImporter，则需要加载实际的资源对象
            if (editor.target is AssetImporter)
            {
                assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (assetObject == null)
                    return;
            }

            // 检查是否已有 AssetFlow 管理
            var existingFlow = AssetFlowUtility.GetTemplateForAsset(assetPath);

            if (existingFlow != null)
            {
                DrawManagedByTemplateUI(existingFlow, assetObject);
            }
            else
            {
                // 检查当前文件夹是否已有 AssetFlow（如果有，则不显示创建按钮）
                var folderPath = Path.GetDirectoryName(assetPath);
                if (!AssetFlowUtility.HasAssetFlowInFolder(folderPath))
                {
                    DrawCreateTemplateButton(assetPath, importer);
                }
            }

            GUILayout.Space(5);
        }

        /// <summary>
        /// 绘制"由 AssetFlow 托管"的 UI（用于头部显示）
        /// </summary>
        private static void DrawManagedByTemplateUI(FolderSetting folderSetting, Object currentAsset)
        {
            var assetPath = AssetDatabase.GetAssetPath(currentAsset);
            if (string.IsNullOrEmpty(assetPath)) return;

            // 检查资源是否被托管（不在排除列表中）
            bool isManaged = !AssetFlowUtility.IsAssetExcluded(folderSetting, assetPath);

            // 绘制托管复选框和查看按钮
            GUILayout.BeginHorizontal();

            // 托管复选框（勾选表示被托管）
            bool newManaged = EditorGUILayout.Toggle(isManaged, GUILayout.Width(15));
            GUILayout.Label("被 AssetFlow 托管", EditorStyles.label);

            GUILayout.FlexibleSpace();

            // 查看 AssetFlow 按钮
            if (GUILayout.Button("查看 AssetFlow", GUILayout.Width(100)))
            {
                Selection.activeObject = folderSetting;
                EditorGUIUtility.PingObject(folderSetting);
            }

            GUILayout.EndHorizontal();

            // 如果状态改变，更新排除列表
            if (newManaged != isManaged)
            {
                UpdateManagedState(folderSetting, currentAsset, assetPath, newManaged);
                isManaged = newManaged;
            }

            GUILayout.Space(3);

            // 当勾选上时，显示提示信息
            if (isManaged)
            {
                var hintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                    normal = { textColor = new Color(0.25f, 0.5f, 1f) } // 较浅的蓝色
                };
                GUILayout.Label("该资源已被 AssetFlow 托管，修改 importer 设置无效", hintStyle);
            }
        }

        /// <summary>
        /// 更新资源的托管状态
        /// </summary>
        private static void UpdateManagedState(FolderSetting folderSetting, Object currentAsset, string assetPath, bool isManaged)
        {
            if (folderSetting == null || string.IsNullOrEmpty(assetPath))
                return;

            // 确保 ExcludeAssetPaths 列表不为 null
            if (folderSetting.ExcludeAssetPaths == null)
                folderSetting.ExcludeAssetPaths = new List<string>();

            assetPath = AssetFlowUtility.NormalizePath(assetPath);

            if (isManaged)
            {
                // 勾选：从排除列表移除（表示被托管）
                folderSetting.ExcludeAssetPaths.RemoveAll(path =>
                    AssetFlowUtility.NormalizePath(path) == assetPath);
            }
            else
            {
                // 取消勾选：添加到排除列表（表示不被托管）
                if (!AssetFlowUtility.IsAssetExcluded(folderSetting, assetPath))
                {
                    folderSetting.ExcludeAssetPaths.Add(assetPath);
                }
            }

            EditorUtility.SetDirty(folderSetting);
            AssetDatabase.SaveAssets();

            // 延迟处理，避免在 GUI 回调中立即触发资源导入导致冲突
            ScheduleProcessFlow(folderSetting);
        }

        /// <summary>
        /// 延迟处理 AssetFlow
        /// </summary>
        private static void ScheduleProcessFlow(FolderSetting flow)
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    AssetFlowUtility.Process(flow);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AssetFlow] 处理 AssetFlow 失败 [{flow.name}]: {e.Message}");
                }
            };
        }

        /// <summary>
        /// 绘制创建模板按钮
        /// </summary>
        private static void DrawCreateTemplateButton(string assetPath, AssetImporter importer)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("托管到 AssetFlow", GUILayout.Width(150)))
            {
                CreateImporterTemplate(assetPath, importer);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 创建 AssetFlow 文件
        /// </summary>
        private static void CreateImporterTemplate(string assetPath, AssetImporter importer)
        {
            if (importer == null) return;

            // 获取资源所在文件夹
            var folderPath = Path.GetDirectoryName(assetPath);
            if (folderPath == null) return;

            // 检查当前文件夹是否已存在 AssetFlow（每个文件夹只能有一个 AssetFlow，但父文件夹可以有自己的 AssetFlow）
            if (AssetFlowUtility.HasAssetFlowInFolder(folderPath))
            {
                EditorUtility.DisplayDialog("无法创建 AssetFlow",
                    "当前文件夹中已存在 AssetFlow 文件，每个文件夹只能有一个 AssetFlow。",
                    "确定");
                return;
            }

            // 生成 FolderSetting 文件路径
            var templatePath = Path.Combine(folderPath, FolderSetting.FolderSettingFileName);
            templatePath = AssetFlowUtility.NormalizePath(templatePath);

            // 检查当前文件夹是否已存在 FolderSetting.asset（不应该发生，但双重保险）
            if (AssetDatabase.LoadAssetAtPath<FolderSetting>(templatePath) != null)
            {
                EditorUtility.DisplayDialog("无法创建 FolderSetting",
                    $"当前文件夹中已存在 {FolderSetting.FolderSettingFileName} 文件。",
                    "确定");
                return;
            }

            // 创建 AssetFlow 实例
            var assetFlow = ScriptableObject.CreateInstance<FolderSetting>();

            // 复制导入器设置
            var newImporter = Object.Instantiate(importer);
            newImporter.name = importer.GetType().Name;

            // 创建主资源
            AssetDatabase.CreateAsset(assetFlow, templatePath);

            // 将导入器作为子资源添加
            AssetDatabase.AddObjectToAsset(newImporter, assetFlow);

            // 设置导入器引用
            assetFlow.Importer = newImporter;
            EditorUtility.SetDirty(assetFlow);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 选中新创建的 AssetFlow
            Selection.activeObject = assetFlow;
            EditorGUIUtility.PingObject(assetFlow);

            Debug.Log($"已创建 AssetFlow: {templatePath}");
        }
    }
}