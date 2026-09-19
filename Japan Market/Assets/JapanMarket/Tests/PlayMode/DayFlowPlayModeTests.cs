using System.Collections;
using JapanMarket.Domain;
using JapanMarket.Gameplay;
using JapanMarket.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JapanMarket.Tests.PlayMode
{
    public sealed class DayFlowPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator Main_permite_continuar_ate_meia_noite_e_finalizar_o_dia_seguinte()
        {
            // A Main já emite erros conhecidos do QuickOutline ao carregar
            // alguns FBX com Read/Write desligado. Eles são anteriores a este
            // fluxo e acontecem somente no Awake da cena; ignoramos essa janela
            // curta para o teste poder fiscalizar os erros do ciclo em si.
            LogAssert.ignoreFailingMessages = true;
            AsyncOperation load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null; // Start dos controladores de UI assina ReportClosed.
            LogAssert.ignoreFailingMessages = false;

            GameContext context = Object.FindFirstObjectByType<GameContext>(
                FindObjectsInactive.Include);
            Assert.That(context, Is.Not.Null);

            GameClock clock = context.Clock as GameClock;
            Assert.That(clock, Is.Not.Null);

            DayEndDecisionView decision = Object.FindFirstObjectByType<DayEndDecisionView>(
                FindObjectsInactive.Include);
            DaySummaryView summary = Object.FindFirstObjectByType<DaySummaryView>(
                FindObjectsInactive.Include);
            Assert.That(decision, Is.Not.Null);
            Assert.That(summary, Is.Not.Null);

            GameObject decisionPanel = decision.transform.Find("Decision Panel")?.gameObject;
            GameObject summaryPanel = summary.transform.Find("Summary Panel")?.gameObject;
            Assert.That(decisionPanel, Is.Not.Null);
            Assert.That(summaryPanel, Is.Not.Null);

            // O tutorial já tem testes próprios. Aqui liberamos a trava para
            // exercitar exatamente o ciclo normal posterior ao tutorial.
            clock.SetEndOfDayLocked(false);
            Assert.That(clock.TryOpenStore(), Is.True);
            AdvanceTo(clock, clock.EndDayPromptHour);
            yield return null;

            Assert.That(decisionPanel.activeSelf, Is.True, "A decisão deve abrir às 21h.");
            Assert.That(clock.IsRunning, Is.False, "O modal deve congelar o relógio.");

            decision.ContinueOpen();
            Assert.That(decisionPanel.activeSelf, Is.False);
            Assert.That(clock.IsRunning, Is.True);
            Assert.That(clock.StoreIsOpen, Is.True);

            AdvanceTo(clock, 23f + 59f / 60f);
            yield return null;
            Assert.That(clock.StoreIsOpen, Is.True, "23:59 ainda pertence ao expediente.");
            Assert.That(decisionPanel.activeSelf, Is.False,
                "Recusar não pode repetir a pergunta no mesmo dia.");

            AdvanceTo(clock, clock.EndOfDayHour);
            yield return null;
            Assert.That(clock.StoreIsOpen, Is.False);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(summaryPanel.activeSelf, Is.True,
                "Meia-noite deve abrir o relatório automaticamente.");
            Assert.That(clock.IsRunning, Is.False);

            summary.ContinueToNextDay();
            Assert.That(summaryPanel.activeSelf, Is.False);
            Assert.That(clock.IsRunning, Is.True);

            Assert.That(clock.TryOpenStore(), Is.True);
            AdvanceTo(clock, clock.EndDayPromptHour);
            yield return null;
            Assert.That(decisionPanel.activeSelf, Is.True);

            decision.FinishDay();
            yield return null;
            Assert.That(clock.Day, Is.EqualTo(3));
            Assert.That(clock.StoreIsOpen, Is.False);
            Assert.That(summaryPanel.activeSelf, Is.True,
                "Finalizar às 21h deve abrir o mesmo relatório do fechamento automático.");
        }

        private static void AdvanceTo(GameClock clock, float targetHour)
        {
            float gameHours = Mathf.Max(0f, targetHour - clock.TimeOfDay);
            float speed = clock.Settings.GameHoursPerRealSecond;
            clock.Tick(gameHours / speed + 0.001f);
        }
    }
}
