using System;

namespace ModuTree.Runtime.Core
{
    /// <summary>
    /// ノードクラスに付与するメタ情報Attribute。
    /// EditorはこのAttributeを読んでGUIを自動生成する。
    /// ノードクラスはこれ1つだけ書けばよい（Editor用クラスは不要）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BehaviourNodeMetaAttribute : Attribute
    {
        /// <summary>Editorに表示するノード名</summary>
        public string DisplayName { get; }

        /// <summary>ツールバーのカテゴリ分類</summary>
        public string Category { get; }

        /// <summary>ノードの説明（ツールチップ用）</summary>
        public string Description { get; }

        /// <summary>Editorに表示するノードの色（hex: #RRGGBB）</summary>
        public string Color { get; }

        public BehaviourNodeMetaAttribute(
            string displayName,
            string category    = "Custom",
            string description = "",
            string color       = "#3A6BC8")
        {
            DisplayName = displayName;
            Category    = category;
            Description = description;
            Color       = color;
        }
    }

    /// <summary>
    /// ノードのフィールドに付与するAttribute。
    /// Editorに表示するラベル名やツールチップを指定する。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NodeFieldAttribute : Attribute
    {
        public string Label   { get; }
        public string Tooltip { get; }

        public NodeFieldAttribute(string label, string tooltip = "")
        {
            Label   = label;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Editorで非表示にするフィールドに付与するAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class NodeFieldHideAttribute : Attribute { }
}