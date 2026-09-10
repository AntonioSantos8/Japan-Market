using UnityEngine;
using GameJam.Utilities;

namespace GameJam.Examples
{
    /// <summary>
    /// Exemplos de uso de TimerUtility, CooldownTimer e Stopwatch.
    /// </summary>
    public class TimerUtilityExample : MonoBehaviour
    {
        // Timer simples
        private TimerUtility delayTimer;

        // Cooldown de ataque
        private CooldownTimer attackCooldown;

        // Cronômetro para medir duração
        private Stopwatch actionStopwatch;

        private void Start()
        {
            // Exemplo 1: Timer simples com callback
            delayTimer = new TimerUtility(2f, OnTimerComplete);

            // Exemplo 2: Cooldown de ataque (0.5 segundos entre ataques)
            attackCooldown = new CooldownTimer(0.5f);

            // Exemplo 3: Cronômetro começando agora
            actionStopwatch = new Stopwatch(startRunning: true);
        }

        private void Update()
        {
            // Atualizar timer
            if (delayTimer.Tick())
            {
                Debug.Log("Timer completou!");
            }

            // Atualizar cooldown
            attackCooldown.Tick();

            // Atualizar cronômetro
            actionStopwatch.Tick();

            // Exemplos de controle
            ExampleAttack();
            ExampleRestartTimer();
            ExampleCheckProgress();
        }

        private void OnTimerComplete()
        {
            Debug.Log("Callback do timer foi chamado!");
        }

        /// <summary>
        /// Exemplo de ataque com cooldown.
        /// </summary>
        private void ExampleAttack()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (attackCooldown.CanUse)
                {
                    Debug.Log("Atacando!");
                    attackCooldown.Use();
                }
                else
                {
                    Debug.Log($"Cooldown: {attackCooldown.RemainingTime:F2}s");
                }
            }
        }

        /// <summary>
        /// Exemplo de reiniciar timer.
        /// </summary>
        private void ExampleRestartTimer()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                delayTimer.Restart();
                Debug.Log("Timer reiniciado!");
            }
        }

        /// <summary>
        /// Exemplo de verificar progresso.
        /// </summary>
        private void ExampleCheckProgress()
        {
            if (delayTimer.IsRunning)
            {
                // Mostrar progresso do timer (0-1)
                float progress = delayTimer.Progress;
                // Debug.Log($"Progresso: {progress:P0}");
            }

            if (actionStopwatch.IsRunning)
            {
                // Debug.Log($"Tempo decorrido: {actionStopwatch.ElapsedTime:F2}s");
            }
        }
    }

    /// <summary>
    /// Exemplo avançado: Sistema de delay para executar código depois.
    /// </summary>
    public class DelayedAction : MonoBehaviour
    {
        private TimerUtility timer;

        public void ExecuteAfterDelay(float delay, System.Action callback)
        {
            timer = new TimerUtility(delay, callback);
        }

        private void Update()
        {
            timer.Tick();
        }
    }

    /// <summary>
    /// Exemplo: Combo system com timeouts.
    /// </summary>
    public class ComboExample : MonoBehaviour
    {
        private int comboCount = 0;
        private TimerUtility comboResetTimer;

        public void OnHitEnemy()
        {
            comboCount++;
            Debug.Log($"Combo: {comboCount}");

            // Reseta o timer de combo
            comboResetTimer.Restart(3f); // 3 segundos para resetar combo
        }

        private void Update()
        {
            if (comboResetTimer.Tick())
            {
                Debug.Log("Combo resetado!");
                comboCount = 0;
            }
        }
    }

    /// <summary>
    /// Exemplo: Ação com timer.
    /// </summary>
    public class ActionWithTimer : MonoBehaviour
    {
        private TimerUtility castTimer;
        private bool isCasting = false;

        public void StartCast(float castTime)
        {
            castTimer = new TimerUtility(castTime, () =>
            {
                Debug.Log("Cast completo!");
                isCasting = false;
            });
            isCasting = true;
        }

        private void Update()
        {
            if (isCasting)
            {
                castTimer.Tick();

                // Mostrar barra de progresso
                float progress = castTimer.Progress;
                Debug.Log($"Casting... {progress:P0}");

                // Cancelar se pressionar ESC
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    castTimer.Stop();
                    isCasting = false;
                    Debug.Log("Cast cancelado!");
                }
            }
        }
    }
}
