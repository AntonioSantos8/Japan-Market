using JapanMarket.Core;
using UnityEditor;
using UnityEngine;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// Desenha <see cref="Money"/> como um campo numérico com o símbolo do iene,
    /// em vez do foldout "Base Cost &gt; Yen" que a serialização produziria.
    /// </summary>
    [CustomPropertyDrawer(typeof(Money))]
    public sealed class MoneyDrawer : PropertyDrawer
    {
        private const float SymbolWidth = 16f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty yen = property.FindPropertyRelative("_yen");
            if (yen == null)
            {
                EditorGUI.LabelField(position, label.text, "campo _yen ausente");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect field = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var symbolRect = new Rect(field.x, field.y, SymbolWidth, field.height);
            var valueRect  = new Rect(field.x + SymbolWidth, field.y,
                                      field.width - SymbolWidth, field.height);

            GUI.Label(symbolRect, Money.Symbol, EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            long typed = EditorGUI.LongField(valueRect, yen.longValue);
            if (EditorGUI.EndChangeCheck()) yen.longValue = typed;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;
    }
}
