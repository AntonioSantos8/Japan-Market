using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Tira e repõe a fotografia da partida.
    ///
    /// Duas regras que valem para o arquivo inteiro:
    ///
    ///  • **Capturar não muda nada.** Nenhum método de captura chama coisa que
    ///    publique evento ou mexa em estado. Salvar durante o jogo não pode ter
    ///    efeito colateral nenhum — nem um evento a mais na tela.
    ///
    ///  • **Um asset que sumiu é uma linha descartada, nunca uma exceção.** Um
    ///    produto, uma faixa de empréstimo ou um objetivo removido do jogo entre
    ///    duas versões vira um id que não resolve. Derrubar o carregamento por
    ///    isso faria o jogador perder a partida por uma mudança de conteúdo que
    ///    não é problema dele — então a linha some, o resto carrega, e o console
    ///    conta o que foi descartado.
    ///
    /// Ordem de aplicação importa e é explícita em <see cref="Apply"/>.
    ///
    /// O que este save AINDA NÃO guarda, e por quê: a posição dos móveis. Não
    /// existe sistema de colocação em runtime (Fase 3b) — hoje os móveis são
    /// postos na cena à mão, então não há o que restaurar. Quando a colocação
    /// existir, ela entra aqui como mais um bloco.
    /// </summary>
    public sealed class SaveService
    {
        private readonly GameContext _game;
        private int _discarded;

        public SaveService(GameContext game)
        {
            _game = game;
        }

        // ── captura ──────────────────────────────────────────────────────────

        public GameSave Capture()
        {
            var save = new GameSave
            {
                Version = GameSave.CurrentVersion,
                SavedAtUtc = System.DateTime.UtcNow.ToString("o"),
            };

            CaptureClock(save);
            CaptureLedger(save);
            CaptureProgress(save);
            CapturePricing(save);
            CaptureMarket(save);
            CaptureLoans(save);
            CaptureObjectives(save);
            CaptureTrash(save);
            CaptureTools(save);

            return save;
        }

        private void CaptureClock(GameSave save)
        {
            if (_game.Clock == null) return;

            save.Clock = new ClockSave
            {
                Day = _game.Clock.Day,
                TimeOfDay = _game.Clock.TimeOfDay,
                StoreOpen = _game.Clock.StoreIsOpen,
            };
        }

        private void CaptureLedger(GameSave save)
        {
            if (_game.Ledger == null) return;

            save.Ledger = new LedgerSave { BalanceYen = _game.Ledger.Balance.Yen };
        }

        private void CaptureProgress(GameSave save)
        {
            var flags = new List<string>();
            if (_game.Progress != null) flags.AddRange(_game.Progress.Flags);

            save.Progress = new ProgressSave
            {
                StoreLevel = _game.StoreLevel?.CurrentLevel ?? 1,
                StoreXP = _game.StoreLevel?.CurrentXP ?? 0,
                Flags = flags.ToArray(),
            };
        }

        private void CapturePricing(GameSave save)
        {
            if (_game.Pricing == null) return;

            var lines = new List<PricingSave>();

            foreach (PricingData data in _game.Pricing.Table)
            {
                if (data.Product == null) continue;

                lines.Add(new PricingSave
                {
                    ProductId = data.Product.Id.Value,
                    HasCustomPrice = data.HasCustomPrice,
                    CustomPriceYen = data.CustomPrice.Yen,
                    LastCostYen = data.LastCost.Yen,
                    CurrentCostYen = data.CurrentCost.Yen,
                    TotalSpentYen = data.TotalSpent.Yen,
                    UnitsInAverage = data.UnitsInAverage,
                    LastChangeDay = data.LastChangeDay,
                });
            }

            save.Pricing = lines.ToArray();
        }

        private void CaptureMarket(GameSave save)
        {
            if (_game.Market is not MarketOrderService market) return;

            var orders = new List<MarketOrderSave>();

            foreach (MarketOrder order in market.Pending)
            {
                var lines = new List<MarketLineSave>();

                foreach (MarketOrderLine line in order.Lines)
                {
                    if (line.Product == null) continue;

                    lines.Add(new MarketLineSave
                    {
                        ProductId = line.Product.Id.Value,
                        Boxes = line.Boxes,
                        BoxCostYen = line.BoxCost.Yen,
                        UnitsPerBox = line.UnitsPerBox,
                    });
                }

                // Pedido que perdeu TODOS os produtos não é salvo: restaurá-lo
                // criaria uma entrega vazia que nunca some da lista de pendentes.
                if (lines.Count == 0) { _discarded++; continue; }

                orders.Add(new MarketOrderSave
                {
                    Id = order.Id,
                    DayPlaced = order.DayPlaced,
                    ArrivesAtTotalHours = order.ArrivesAtTotalHours,
                    Lines = lines.ToArray(),
                });
            }

            var lots = new List<DeliveryLotSave>();
            foreach (DeliveryLot lot in market.DeliveryQueue.Lots)
            {
                if (lot.Product == null) continue;

                lots.Add(new DeliveryLotSave
                {
                    ProductId = lot.Product.Id.Value,
                    Boxes = lot.Boxes,
                });
            }

            save.Market = new MarketSave
            {
                NextOrderId = market.NextOrderId,
                Pending = orders.ToArray(),
                Queue = lots.ToArray(),
            };
        }

        private void CaptureLoans(GameSave save)
        {
            if (_game.Bank == null) return;

            var loans = new List<LoanSave>();

            foreach (ActiveLoan loan in _game.Bank.ActiveLoans)
            {
                if (loan?.Definition == null) continue;

                loans.Add(new LoanSave
                {
                    LoanKey = loan.Definition.Key,
                    PaymentsMade = loan.PaymentsMade,
                });
            }

            save.Loans = loans.ToArray();
        }

        private void CaptureObjectives(GameSave save)
        {
            if (_game.Objectives == null) return;

            var entries = new List<ObjectiveSave>();

            foreach (ObjectiveSnapshot snapshot in _game.Objectives.Snapshot())
            {
                entries.Add(new ObjectiveSave
                {
                    ObjectiveId = snapshot.Id.Value,
                    Progress = snapshot.Progress,
                    Completed = snapshot.Completed,
                });
            }

            save.Objectives = entries.ToArray();
        }

        private void CaptureTrash(GameSave save)
        {
            if (_game.Trash == null) return;

            var bags = new List<TrashBagSave>();

            foreach (TrashBag bag in _game.Trash.Pending)
            {
                if (bag == null || bag.IsEmpty) continue;

                var keys = new List<string>();
                foreach (TrashDefinition item in bag.Items)
                    if (item != null) keys.Add(item.Key);

                bags.Add(new TrashBagSave
                {
                    CategoryKey = bag.Category != null ? bag.Category.Key : string.Empty,
                    ItemKeys = keys.ToArray(),
                });
            }

            save.TrashDock = bags.ToArray();
        }

        private void CaptureTools(GameSave save)
        {
            if (_game.Tools is not ToolBelt belt) return;

            save.Tools = new ToolsSave
            {
                UsesPerSlot = belt.Snapshot(),
                SelectedIndex = belt.SelectedIndex,
            };
        }

        // ── aplicação ────────────────────────────────────────────────────────

        /// <summary>
        /// Repõe o save. A ORDEM aqui não é estética:
        ///
        ///  1. relógio — tudo que tem prazo é comparado contra o tempo de mundo;
        ///  2. nível e flags — é o que decide o que está desbloqueado;
        ///  3. o resto — objetivos e ferramentas consultam desbloqueio ao entrar.
        ///
        /// Inverter 2 e 3 faria um objetivo liberado por flag ficar de fora, e um
        /// slot de ferramenta já comprado voltar travado.
        /// </summary>
        public void Apply(GameSave save)
        {
            if (save == null) return;

            _discarded = 0;

            ApplyClock(save);
            ApplyProgress(save);

            ApplyLedger(save);
            ApplyPricing(save);
            ApplyMarket(save);
            ApplyLoans(save);
            ApplyObjectives(save);
            ApplyTrash(save);
            ApplyTools(save);

            if (_discarded > 0)
            {
                Debug.LogWarning(
                    $"[Save] {_discarded} entrada(s) do save apontavam para assets que não " +
                    "existem mais no projeto e foram descartadas. O resto carregou normal.");
            }
        }

        private void ApplyClock(GameSave save)
        {
            if (save.Clock == null || _game.Clock is not GameClock clock) return;

            clock.Restore(save.Clock.Day, save.Clock.TimeOfDay, save.Clock.StoreOpen);
        }

        private void ApplyProgress(GameSave save)
        {
            if (save.Progress == null) return;

            _game.Progress?.RestoreFlags(save.Progress.Flags);

            if (_game.StoreLevel is StoreLevelService level)
                level.Restore(save.Progress.StoreLevel, save.Progress.StoreXP);
        }

        private void ApplyLedger(GameSave save)
        {
            if (save.Ledger == null || _game.Ledger is not Ledger ledger) return;

            ledger.Restore(Money.FromYen(save.Ledger.BalanceYen));
        }

        private void ApplyPricing(GameSave save)
        {
            if (_game.Pricing is not PricingService pricing) return;

            var restored = new List<PricingData>();

            if (save.Pricing != null)
                foreach (PricingSave line in save.Pricing)
                {
                    ItemDefinition product = FindProduct(line.ProductId);
                    if (product == null) { _discarded++; continue; }

                    restored.Add(new PricingData(
                        product,
                        Money.FromYen(line.CustomPriceYen), line.HasCustomPrice,
                        Money.FromYen(line.LastCostYen), Money.FromYen(line.CurrentCostYen),
                        Money.FromYen(line.TotalSpentYen),
                        line.UnitsInAverage, line.LastChangeDay));
                }

            pricing.Restore(restored);
        }

        private void ApplyMarket(GameSave save)
        {
            if (_game.Market is not MarketOrderService market) return;

            var orders = new List<MarketOrder>();
            var lots = new List<DeliveryLot>();

            MarketSave data = save.Market ?? new MarketSave();

            if (data.Pending != null)
                foreach (MarketOrderSave entry in data.Pending)
                {
                    var lines = new List<MarketOrderLine>();
                    Money total = Money.Zero;

                    if (entry.Lines != null)
                        foreach (MarketLineSave line in entry.Lines)
                        {
                            ItemDefinition product = FindProduct(line.ProductId);
                            if (product == null) { _discarded++; continue; }

                            var restored = new MarketOrderLine(
                                product, line.Boxes, Money.FromYen(line.BoxCostYen),
                                line.UnitsPerBox);

                            lines.Add(restored);
                            total += restored.Total;
                        }

                    if (lines.Count == 0) continue;

                    orders.Add(new MarketOrder(entry.Id, lines, total, entry.DayPlaced,
                                               entry.ArrivesAtTotalHours));
                }

            if (data.Queue != null)
                foreach (DeliveryLotSave entry in data.Queue)
                {
                    ItemDefinition product = FindProduct(entry.ProductId);
                    if (product == null) { _discarded++; continue; }

                    lots.Add(new DeliveryLot(product, entry.Boxes));
                }

            market.Restore(orders, lots, data.NextOrderId);
        }

        private void ApplyLoans(GameSave save)
        {
            if (_game.Bank is not BankService bank) return;

            var loans = new List<ActiveLoan>();

            if (save.Loans != null && _game.Services.TryResolve(out ILoanCatalog catalog))
                foreach (LoanSave entry in save.Loans)
                {
                    LoanDefinition definition = catalog.Find(entry.LoanKey);
                    if (definition == null) { _discarded++; continue; }

                    loans.Add(new ActiveLoan(definition, entry.PaymentsMade));
                }
            else if (save.Loans is { Length: > 0 })
            {
                // Sem catálogo não há como reencontrar a faixa. Perdoar a dívida
                // é ruim; travar o carregamento é pior.
                _discarded += save.Loans.Length;
            }

            bank.Restore(loans);
        }

        private void ApplyObjectives(GameSave save)
        {
            if (_game.Objectives is not ObjectiveService objectives) return;

            var snapshots = new List<ObjectiveSnapshot>();

            if (save.Objectives != null)
                foreach (ObjectiveSave entry in save.Objectives)
                    snapshots.Add(new ObjectiveSnapshot(
                        ObjectiveId.FromString(entry.ObjectiveId),
                        entry.Progress, entry.Completed));

            objectives.Restore(snapshots);
        }

        private void ApplyTrash(GameSave save)
        {
            if (_game.Trash is not TrashService trash) return;

            var bags = new List<TrashBag>();
            _game.Services.TryResolve(out ITrashCatalog catalog);

            if (save.TrashDock != null && catalog != null)
                foreach (TrashBagSave entry in save.TrashDock)
                {
                    TrashCategory category = catalog.FindCategory(entry.CategoryKey);
                    var items = new List<TrashDefinition>();

                    if (entry.ItemKeys != null)
                        foreach (string key in entry.ItemKeys)
                        {
                            TrashDefinition item = catalog.FindItem(key);
                            if (item == null) { _discarded++; continue; }

                            items.Add(item);
                        }

                    if (items.Count == 0) continue;

                    // A capacidade do saco salvo é o que ele tinha dentro: um
                    // saco entregue na doca não volta a receber lixo, então o
                    // teto original não faz diferença nenhuma aqui.
                    var bag = new TrashBag(items.Count);
                    bag.Restore(category, items);

                    if (!bag.IsEmpty) bags.Add(bag);
                }

            trash.Restore(bags);
        }

        private void ApplyTools(GameSave save)
        {
            if (save.Tools == null || _game.Tools is not ToolBelt belt) return;

            belt.Restore(save.Tools.UsesPerSlot, save.Tools.SelectedIndex);
        }

        // ── resolução de assets ──────────────────────────────────────────────

        private ItemDefinition FindProduct(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            if (!_game.Services.TryResolve(out IItemCatalog catalog)) return null;

            return catalog.TryGet(ProductId.FromString(id), out ItemDefinition product)
                ? product
                : null;
        }
    }
}
