using System.Text;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

/// <summary>
/// Rastreador compacto da HUD. Durante o tutorial mostra apenas a meta global
/// pedida pelo design; depois reaproveita o serviço de objetivos da loja.
/// </summary>
[DisallowMultipleComponent]
public sealed class ObjectiveTrackerUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI headerText;
    [SerializeField] private TMPro.TextMeshProUGUI bodyText;
    [SerializeField] private string tutorialObjective = "Finish tutorial";
    [SerializeField, Min(1)] private int maxVisibleObjectives = 3;

    private ITutorialStatus _tutorial;
    private IObjectiveService _objectives;
    private bool _objectivesSubscribed;

    private void Start()
    {
        ServiceContainer.Current.TryResolve(out _tutorial);

        if (_tutorial != null)
            _tutorial.Completed += OnTutorialCompleted;

        TrySubscribeToObjectives();
        Refresh();
    }

    private void Update()
    {
        if (!_objectivesSubscribed && TrySubscribeToObjectives()) Refresh();
    }

    private bool TrySubscribeToObjectives()
    {
        if (_objectivesSubscribed) return true;
        if (!ServiceContainer.Current.TryResolve(out _objectives)) return false;

        _objectives.ProgressChanged += Refresh;
        _objectives.ObjectiveStarted += OnObjectiveChanged;
        _objectives.ObjectiveCompleted += OnObjectiveChanged;
        _objectivesSubscribed = true;
        return true;
    }

    private void OnTutorialCompleted()
    {
        Refresh();
    }

    private void OnObjectiveChanged(ActiveObjective _) => Refresh();

    private void Refresh()
    {
        bool tutorialIsRunning = _tutorial != null && !_tutorial.IsFinished;

        if (tutorialIsRunning)
        {
            Set(headerText, "OBJECTIVE");
            Set(bodyText, tutorialObjective);
            return;
        }

        Set(headerText, "OBJETIVOS");

        if (_objectives == null)
        {
            Set(bodyText, "Nenhum objetivo ativo.");
            return;
        }

        var text = new StringBuilder();
        int visible = 0;

        for (int i = 0; i < _objectives.Active.Count; i++)
        {
            ActiveObjective objective = _objectives.Active[i];
            if (objective == null || objective.IsCompleted) continue;
            if (visible >= Mathf.Max(1, maxVisibleObjectives)) break;

            if (text.Length > 0) text.Append("\n\n");

            ObjectiveDefinition definition = objective.Definition;
            string title = definition.Title.IsEmpty
                ? definition.name
                : definition.Title.Value;
            text.Append("• ").Append(title);

            for (int conditionIndex = 0;
                 conditionIndex < definition.Conditions.Count;
                 conditionIndex++)
            {
                ObjectiveCondition condition = definition.Conditions[conditionIndex];
                if (condition == null) continue;

                text.Append("\n  ")
                    .Append(condition.Describe())
                    .Append(" — ")
                    .Append(objective.ProgressOf(conditionIndex))
                    .Append('/')
                    .Append(objective.TargetOf(conditionIndex));
            }

            visible++;
        }

        Set(bodyText, visible > 0 ? text.ToString() : "Nenhum objetivo ativo.");
    }

    private static void Set(TMPro.TextMeshProUGUI target, string value)
    {
        if (target != null) target.text = value;
    }

    private void OnDestroy()
    {
        if (_tutorial != null)
            _tutorial.Completed -= OnTutorialCompleted;

        if (!_objectivesSubscribed || _objectives == null) return;

        _objectives.ProgressChanged -= Refresh;
        _objectives.ObjectiveStarted -= OnObjectiveChanged;
        _objectives.ObjectiveCompleted -= OnObjectiveChanged;
    }
}
