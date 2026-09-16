using DG.Tweening;
using JapanMarket.Core;
using UnityEngine;

/// <summary>
/// A placa da porta. Ela é uma VISTA do relógio, não a dona do estado.
///
/// Antes ela era a dona: o jogador clicava, a animação rodava, e no fim dela a
/// placa escrevia <c>marketManager.Open</c>. Isso tinha três defeitos que só
/// apareciam quando o relógio fechava a loja sozinho no horário:
///
///  • a placa continuava girada em "aberto" até alguém clicar nela de novo;
///  • o spawner de NPC nunca era parado, então continuava chegando cliente numa
///    loja fechada;
///  • e um fechamento automático DURANTE a animação era desfeito no OnComplete,
///    que escrevia de volta o estado de antes.
///
/// Agora quem manda é o relógio. O clique só PEDE a troca; a animação, o aviso,
/// o som e o spawner reagem ao <see cref="StoreOpenStateChanged"/> — venha ele
/// do clique ou do horário.
/// </summary>
public class StoreSign : InteractableBase
{
    [SerializeField] float rotationY = 90f;
    [SerializeField] float duration = 1f;
    [SerializeField] float moveBack = 0.32f;
    [SerializeField] Ease ease = Ease.OutBack;
    [SerializeField] Transform placaTransform;

    TutorialManager _tutorialManager;
    Warnings warnings;
    MarketManager marketManager;
    NpcManager npcManager;

    System.IDisposable _openStateSubscription;
    Sequence _sequence;

    Vector3 originalPos;
    float originalYRotation;

    /// <summary>O que a placa está MOSTRANDO agora. Pode diferir do relógio durante a animação.</summary>
    bool _visualOpen;

    bool _isRotating;

    /// <summary>Estado que ficou pendente porque chegou no meio de uma animação.</summary>
    bool _hasPending;
    bool _pendingOpen;

    void Start()
    {
        originalPos = placaTransform.localPosition;
        originalYRotation = placaTransform.eulerAngles.y;

        ResolveServices();
        Subscribe();

        // Sincroniza sem animar: ao carregar uma cena com a loja já aberta, a
        // placa tem que NASCER girada, e não girar sozinha na cara do jogador.
        _visualOpen = IsOpen;
        placaTransform.localEulerAngles = new Vector3(
            placaTransform.localEulerAngles.x,
            _visualOpen ? originalYRotation + rotationY : originalYRotation,
            placaTransform.localEulerAngles.z);
    }

    void OnDestroy()
    {
        _openStateSubscription?.Dispose();
        _openStateSubscription = null;

        _sequence?.Kill();
    }

    void ResolveServices()
    {
        if (warnings == null) warnings = ServiceLocator.Get<Warnings>();
        if (marketManager == null) marketManager = ServiceLocator.Get<MarketManager>();
        if (npcManager == null) npcManager = ServiceLocator.Get<NpcManager>();
        if (_tutorialManager == null) _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }

    /// <summary>
    /// A inscrição é tentada de novo a cada clique enquanto não pegar: o
    /// GameContext pode entrar depois numa cena aditiva, e uma placa que
    /// desistiu no Start nunca mais acompanharia o relógio.
    /// </summary>
    void Subscribe()
    {
        if (_openStateSubscription != null) return;

        var game = JapanMarket.Gameplay.GameContext.Current;
        if (game == null || game.Events == null) return;

        _openStateSubscription = game.Events.Subscribe<StoreOpenStateChanged>(OnStoreOpenChanged);
    }

    bool IsOpen => marketManager != null && marketManager.Open;

    // ── o clique ─────────────────────────────────────────────────────────────

    public override void Interact()
    {
        ResolveServices();
        Subscribe();

        if (_isRotating) return;
        if (warnings == null || marketManager == null) return;
        if (warnings.IsWarningActive) return;

        bool open = IsOpen;

        // Fora do horário a loja não reabre. Checado ANTES de pedir a troca,
        // para o jogador receber o motivo em vez de um clique que não faz nada.
        if (!open && TryGetClock(out JapanMarket.Domain.IGameClock clock) && clock.TimeOfDay >= clock.ClosingHour)
        {
            warnings.ShowWarning("O horário de abertura de hoje terminou.", false);
            return;
        }

        // Só PEDE. Quem anima, avisa e liga o spawner é o evento lá embaixo —
        // e é o mesmo caminho do fechamento automático, então os dois não podem
        // divergir.
        marketManager.Open = !open;

        // O relógio pode ter recusado a abertura (TryOpenStore devolve false
        // fora do horário). Sem evento, nada acontece — e é isso mesmo.
        if (IsOpen == open && !open)
            warnings.ShowWarning("O horário de abertura de hoje terminou.", false);
    }

    bool TryGetClock(out JapanMarket.Domain.IGameClock clock)
    {
        clock = null;

        var game = JapanMarket.Gameplay.GameContext.Current;
        return game != null && game.Services.TryResolve(out clock);
    }

    // ── a reação ─────────────────────────────────────────────────────────────

    void OnStoreOpenChanged(StoreOpenStateChanged e)
    {
        ResolveServices();

        if (warnings != null)
            warnings.ShowWarning(e.IsOpen ? "Store is Open!" : "Store is Closed!", e.IsOpen);

        if (e.IsOpen && _tutorialManager != null)
            _tutorialManager.NotifyGameEvent("StoreOpened");

        // O spawner segue o estado REAL. Antes ele só era parado pelo clique, e
        // o fechamento automático deixava cliente entrando em loja fechada.
        if (npcManager != null)
        {
            if (e.IsOpen) npcManager.StartSpawning();
            else npcManager.StopSpawning();
        }

        ServiceLocator.Get<SoundManager>()?.Play(SFX.LojaAbertaFechada);

        AnimateTo(e.IsOpen);
    }

    void AnimateTo(bool open)
    {
        if (_isRotating)
        {
            // Chegou no meio do giro. Guarda e resolve no fim, em vez de cortar
            // a animação pela metade ou perder a mudança.
            _hasPending = true;
            _pendingOpen = open;
            return;
        }

        if (open == _visualOpen) return;

        _isRotating = true;
        float target = open ? originalYRotation + rotationY : originalYRotation;

        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(placaTransform.DOLocalMoveZ(originalPos.z - moveBack, duration * 0.3f)
                                       .SetEase(Ease.OutSine));
        _sequence.Append(placaTransform.DOLocalRotate(new Vector3(0, target, 0), duration)
                                       .SetEase(ease));
        _sequence.Join(placaTransform.DOLocalMoveZ(originalPos.z, duration).SetEase(Ease.OutBack));
        _sequence.OnComplete(() =>
        {
            _isRotating = false;
            _visualOpen = open;

            if (!_hasPending) return;

            _hasPending = false;
            AnimateTo(_pendingOpen);
        });
    }
}
