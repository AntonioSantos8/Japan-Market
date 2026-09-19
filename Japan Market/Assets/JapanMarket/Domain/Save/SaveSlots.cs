using System;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Catálogo fixo dos espaços de save exibidos pelo menu.
    /// O Slot 1 conserva o nome histórico para que saves antigos apareçam sem migração.
    /// </summary>
    public static class SaveSlots
    {
        public const int Count = 3;

        public static string FileNameFor(int slot)
        {
            Validate(slot);
            return slot == 1
                ? SaveFile.DefaultName
                : $"japanmarket.slot{slot}.save.json";
        }

        public static bool TryGetInfo(int slot, out SaveSlotInfo info)
        {
            info = default;
            if (!IsValid(slot)) return false;

            string fileName = FileNameFor(slot);
            if (!SaveFile.TryRead(out GameSave save, fileName)) return false;

            DateTime savedAtUtc = DateTime.MinValue;
            if (!string.IsNullOrWhiteSpace(save.SavedAtUtc))
                DateTime.TryParse(save.SavedAtUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out savedAtUtc);

            info = new SaveSlotInfo(
                slot,
                fileName,
                save.Clock?.Day ?? 1,
                save.Ledger?.BalanceYen ?? 0L,
                savedAtUtc);

            return true;
        }

        public static bool TryGetMostRecent(out SaveSlotInfo info)
        {
            info = default;
            bool found = false;

            for (int slot = 1; slot <= Count; slot++)
            {
                if (!TryGetInfo(slot, out SaveSlotInfo candidate)) continue;
                if (found && candidate.SavedAtUtc <= info.SavedAtUtc) continue;

                info = candidate;
                found = true;
            }

            return found;
        }

        public static bool IsValid(int slot) => slot >= 1 && slot <= Count;

        private static void Validate(int slot)
        {
            if (!IsValid(slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot,
                    $"O slot precisa estar entre 1 e {Count}.");
        }
    }

    public readonly struct SaveSlotInfo
    {
        public SaveSlotInfo(int slot, string fileName, int day, long balanceYen,
            DateTime savedAtUtc)
        {
            Slot = slot;
            FileName = fileName;
            Day = day;
            BalanceYen = balanceYen;
            SavedAtUtc = savedAtUtc;
        }

        public int Slot { get; }
        public string FileName { get; }
        public int Day { get; }
        public long BalanceYen { get; }
        public DateTime SavedAtUtc { get; }
    }

    /// <summary>
    /// Leva a escolha do menu até a cena Main e mantém o slot ativo para o autosave.
    /// </summary>
    public static class SaveSession
    {
        private static int _currentSlot = 1;
        private static SaveStartMode _pendingStart = SaveStartMode.None;

        public static int CurrentSlot => _currentSlot;
        public static string CurrentFileName => SaveSlots.FileNameFor(_currentSlot);

        public static void BeginNewGame(int slot) => Configure(slot, SaveStartMode.NewGame);
        public static void BeginLoadGame(int slot) => Configure(slot, SaveStartMode.LoadGame);

        public static bool TryConsumeStartRequest(out SaveStartMode mode)
        {
            mode = _pendingStart;
            _pendingStart = SaveStartMode.None;
            return mode != SaveStartMode.None;
        }

        private static void Configure(int slot, SaveStartMode mode)
        {
            if (!SaveSlots.IsValid(slot))
                throw new ArgumentOutOfRangeException(nameof(slot));

            _currentSlot = slot;
            _pendingStart = mode;
        }
    }

    public enum SaveStartMode
    {
        None,
        NewGame,
        LoadGame,
    }
}
