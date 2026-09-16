using System.Collections.Generic;
using JapanMarket.Data;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// O app Objetivos: o que está valendo, quanto falta, e o que rende.
    ///
    /// A tela pergunta ao objetivo quantas condições ele tem e desenha uma barra
    /// para cada — ela não sabe o que nenhuma delas conta. Um tipo de objetivo
    /// novo aparece aqui inteiro, com texto e barra, sem que este arquivo mude:
    /// o texto vem de <c>ObjectiveCondition.Describe()</c> e o número de
    /// <c>ActiveObjective.ProgressOf</c>.
    /// </summary>
    public sealed class ObjectivesApp : ComputerApp
    {
        public override string Title => "Objetivos";

        private RectTransform _list;

        private IObjectiveService _objectives;

        protected override void Build()
        {
            RectTransform column = UIKit.Column("Objetivos", Content, 8f,
                                                new RectOffset(10, 10, 10, 10));
            UIKit.Stretch(column);

            Image panel = UIKit.Panel("Lista", column, new Color(0f, 0f, 0f, 0.12f));
            panel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _list = UIKit.ScrollList("Rolagem", panel.transform, out _, spacing: 8f);
        }

        protected override void Subscribe()
        {
            if (!TryGet(out _objectives)) return;

            _objectives.ProgressChanged += Refresh;
            _objectives.ObjectiveStarted += OnObjectiveChanged;
            _objectives.ObjectiveCompleted += OnObjectiveChanged;
        }

        protected override void Unsubscribe()
        {
            if (_objectives == null) return;

            _objectives.ProgressChanged -= Refresh;
            _objectives.ObjectiveStarted -= OnObjectiveChanged;
            _objectives.ObjectiveCompleted -= OnObjectiveChanged;
        }

        private void OnObjectiveChanged(ActiveObjective _) => Refresh();

        public override void Refresh()
        {
            if (_list == null) return;
            if (_objectives == null && !TryGet(out _objectives)) return;

            UIKit.Clear(_list);

            var active = new List<ActiveObjective>(_objectives.Active);

            if (active.Count == 0)
            {
                UIKit.Label("Vazio", _list,
                            "Nenhum objetivo ativo. Eles aparecem conforme a loja cresce.",
                            UIKit.BodySize, UIKit.TextDim);
                return;
            }

            for (int i = 0; i < active.Count; i++) DrawObjective(active[i]);
        }

        private void DrawObjective(ActiveObjective objective)
        {
            ObjectiveDefinition definition = objective.Definition;

            Image card = UIKit.Panel($"Objetivo {definition.name}", _list, UIKit.SurfaceAlt);
            RectTransform body = UIKit.Column("Corpo", card.transform, 4f,
                                              new RectOffset(10, 10, 8, 8));
            UIKit.Stretch(body);

            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform head = UIKit.Row("Título", body, 26f, 8f, new RectOffset(0, 0, 0, 0));

            string title = definition.Title.IsEmpty ? definition.name : definition.Title.Value;

            UIKit.Label("Nome", head, title, UIKit.HeadingSize,
                        objective.IsCompleted ? UIKit.Good : UIKit.Text).Grow();

            UIKit.Label("Prêmio", head, DescribeReward(definition.Reward), UIKit.SmallSize,
                        UIKit.TextDim, TextAlignmentOptions.Right).Width(220f);

            if (!definition.Description.IsEmpty)
                UIKit.Label("Descrição", body, definition.Description.Value, UIKit.SmallSize,
                            UIKit.TextDim);

            if (objective.IsCompleted)
            {
                UIKit.Label("Feito", body, "Concluído", UIKit.BodySize, UIKit.Good);
                return;
            }

            IReadOnlyList<ObjectiveCondition> conditions = definition.Conditions;

            for (int i = 0; i < conditions.Count; i++)
            {
                ObjectiveCondition condition = conditions[i];
                if (condition == null) continue;

                int done = objective.ProgressOf(i);
                int target = objective.TargetOf(i);

                RectTransform line = UIKit.Row($"Meta {i}", body, 22f, 8f,
                                               new RectOffset(0, 0, 0, 0));

                UIKit.Label("Texto", line, condition.Describe(), UIKit.BodySize,
                            objective.IsConditionMet(i) ? UIKit.Good : UIKit.Text).Grow();

                UIKit.Label("Contagem", line, $"{done}/{target}", UIKit.BodySize, UIKit.TextDim,
                            TextAlignmentOptions.Right).Width(90f);

                Bar(body, target > 0 ? done / (float)target : 0f);
            }
        }

        /// <summary>
        /// Uma barra de progresso feita de dois painéis. Slider seria um
        /// componente inteiro com handle, interação e navegação para desenhar um
        /// retângulo que ninguém arrasta.
        /// </summary>
        private static void Bar(Transform parent, float fraction)
        {
            Image track = UIKit.Panel("Barra", parent, new Color(0f, 0f, 0f, 0.35f));
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 6f;

            RectTransform fill = (RectTransform)UIKit.Panel("Preenchimento", track.transform,
                                                            UIKit.Accent).transform;

            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private static string DescribeReward(ObjectiveReward reward)
        {
            if (reward.IsEmpty) return string.Empty;

            var parts = new List<string>(3);

            if (reward.HasMoney) parts.Add(reward.Money.ToString());
            if (reward.HasXP) parts.Add($"{reward.XP} XP");
            if (reward.HasFlag) parts.Add("desbloqueio");

            return string.Join(" · ", parts);
        }
    }
}
