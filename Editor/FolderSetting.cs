using System.Collections.Generic;
using TriInspector;
using UnityEditor;
using UnityEngine;

namespace AssetFlow
{
    public class FolderSetting : ScriptableObject
    {
        /// <summary>
        /// FolderSetting 文件名
        /// </summary>
        public const string FolderSettingFileName = "__FolderSetting__.asset";

        [SerializeField, Tooltip("是否作用于子文件夹")]
        public bool includeSubfolders = false;
        public List<string> ExcludeAssetPaths = new();

        [InlineEditor, Tab("Importer")]  
        public AssetImporter Importer = new();

        [Tab("Validator")]
        public List<AssetValidatorBase> Validators = new();
    }
}