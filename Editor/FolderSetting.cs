using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Editor.AssetFlow
{
    public class FolderSetting : SerializedScriptableObject
    {
        /// <summary>
        /// FolderSetting 文件名
        /// </summary>
        public const string FolderSettingFileName = "__FolderSetting__.asset";

        [SerializeField, Tooltip("是否作用于子文件夹")]
        public bool includeSubfolders = false;
        public List<string> ExcludeAssetPaths = new();

        [InlineEditor, TabGroup("Importer")]
        public AssetImporter Importer = new();

        [TabGroup("Validator")]
        public List<AssetValidatorBase> Validators = new();
    }
}