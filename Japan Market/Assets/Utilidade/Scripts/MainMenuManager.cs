using System;
using System.Collections.Generic;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] OptionsGroup[] allOptions;
    [SerializeField] private string discordUrl = "https://discord.gg/";

    private readonly List<UnityEngine.UI.Button> _loadSlotButtons = new();
    private readonly List<UnityEngine.UI.Button> _newSlotButtons = new();
    private UnityEngine.UI.Button _continueButton;
    private OptionsGroup _achievementsPanel;
    private OptionsGroup _emailPanel;
    private int _pendingOverwriteSlot;

    void Start()
    {
        SetOption(0);
        BindSaveMenu();
        BindIconMenu();
        RefreshSaveMenu();
    }

    private void BindIconMenu()
    {
        UnityEngine.UI.Button discordButton =
            transform.Find("Icons/Discord")?.GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Button achievementsButton =
            transform.Find("Icons/Achievements")?.GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Button emailButton =
            transform.Find("Icons/Emails")?.GetComponent<UnityEngine.UI.Button>();

        _achievementsPanel =
            transform.Find("Achievements Panel")?.GetComponent<OptionsGroup>();
        _emailPanel = transform.Find("Email Panel")?.GetComponent<OptionsGroup>();

        _achievementsPanel?.SetTarget(0f);
        _emailPanel?.SetTarget(0f);

        discordButton?.onClick.AddListener(OpenDiscord);
        achievementsButton?.onClick.AddListener(() => OpenPopup(_achievementsPanel));
        emailButton?.onClick.AddListener(() => OpenPopup(_emailPanel));

        BindCloseButton("Achievements Panel/Window/Close Button", _achievementsPanel);
        BindCloseButton("Email Panel/Window/Close Button", _emailPanel);
    }

    private void BindCloseButton(string hierarchyPath, OptionsGroup panel)
    {
        UnityEngine.UI.Button closeButton =
            transform.Find(hierarchyPath)?.GetComponent<UnityEngine.UI.Button>();
        closeButton?.onClick.AddListener(() => panel?.SetTarget(0f));
    }

    public void OpenDiscord()
    {
        if (string.IsNullOrWhiteSpace(discordUrl))
        {
            Debug.LogWarning("[Menu] O link do Discord não foi configurado.", this);
            return;
        }

        Application.OpenURL(discordUrl);
    }

    private void OpenPopup(OptionsGroup panel)
    {
        _achievementsPanel?.SetTarget(0f);
        _emailPanel?.SetTarget(0f);
        panel?.SetTarget(1f);
    }

    public void SetOption(int option)
    {
        if (allOptions == null || option < 0 || option >= allOptions.Length) return;

        foreach (OptionsGroup options in allOptions)
        {
            if (options != null) options.SetTarget(0f);
        }

        allOptions[option].SetTarget(1f);
    }

    private void BindSaveMenu()
    {
        Transform continueTransform = transform.Find("Fundo/Continue");
        if (continueTransform != null)
        {
            _continueButton = continueTransform.GetComponent<UnityEngine.UI.Button>();
            _continueButton?.onClick.AddListener(ContinueMostRecentGame);
        }

        BindSlotPanel("Load Game Panel/Fundo2", _loadSlotButtons, LoadGame);
        BindSlotPanel("New Game Panel/Fundo2", _newSlotButtons, NewGame);
    }

    private void BindSlotPanel(string hierarchyPath,
        List<UnityEngine.UI.Button> destination, Action<int> action)
    {
        Transform container = transform.Find(hierarchyPath);
        if (container == null)
        {
            Debug.LogWarning($"[Menu Save] Não encontrei '{hierarchyPath}'.", this);
            return;
        }

        destination.Clear();

        int slotCount = Mathf.Min(container.childCount, SaveSlots.Count);
        for (int index = 0; index < slotCount; index++)
        {
            int slot = index + 1;
            UnityEngine.UI.Button button =
                container.GetChild(index).GetComponentInChildren<UnityEngine.UI.Button>(true);

            if (button == null)
            {
                Debug.LogWarning(
                    $"[Menu Save] O Slot {slot} em '{hierarchyPath}' não possui Button.", this);
                continue;
            }

            destination.Add(button);
            button.onClick.AddListener(() => action(slot));
        }
    }

    private void RefreshSaveMenu()
    {
        bool hasRecent = SaveSlots.TryGetMostRecent(out _);
        if (_continueButton != null) _continueButton.gameObject.SetActive(hasRecent);

        RefreshSlotButtons(_loadSlotButtons, isNewGamePanel: false);
        RefreshSlotButtons(_newSlotButtons, isNewGamePanel: true);
    }

    private static void RefreshSlotButtons(List<UnityEngine.UI.Button> buttons,
        bool isNewGamePanel)
    {
        for (int index = 0; index < buttons.Count; index++)
        {
            int slot = index + 1;
            UnityEngine.UI.Button button = buttons[index];
            bool occupied = SaveSlots.TryGetInfo(slot, out SaveSlotInfo info);

            button.interactable = isNewGamePanel || occupied;

            TMP_Text label = button.transform.parent.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;

            if (!occupied)
            {
                label.text = $"Slot {slot}\nVazio";
                continue;
            }

            string details = $"Dia {info.Day} • ¥{info.BalanceYen:N0}";
            label.text = isNewGamePanel
                ? $"Slot {slot}\n{details} • Substituir"
                : $"Slot {slot}\n{details}";
        }
    }

    private void ContinueMostRecentGame()
    {
        if (!SaveSlots.TryGetMostRecent(out SaveSlotInfo info))
        {
            RefreshSaveMenu();
            return;
        }

        StartGame(info.Slot, loadExisting: true);
    }

    private void LoadGame(int slot)
    {
        if (!SaveSlots.TryGetInfo(slot, out _))
        {
            RefreshSaveMenu();
            return;
        }

        StartGame(slot, loadExisting: true);
    }

    private void NewGame(int slot)
    {
        if (SaveSlots.TryGetInfo(slot, out _) && _pendingOverwriteSlot != slot)
        {
            _pendingOverwriteSlot = slot;
            ShowOverwriteConfirmation(slot);
            return;
        }

        string fileName = SaveSlots.FileNameFor(slot);
        if (!SaveFile.TryDelete(fileName)) return;

        StartGame(slot, loadExisting: false);
    }

    private void ShowOverwriteConfirmation(int slot)
    {
        RefreshSlotButtons(_newSlotButtons, isNewGamePanel: true);

        int index = slot - 1;
        if (index < 0 || index >= _newSlotButtons.Count) return;

        UnityEngine.UI.Button button = _newSlotButtons[index];
        TMP_Text label = button.transform.parent.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = $"Slot {slot}\nClique novamente para substituir";
    }

    private static void StartGame(int slot, bool loadExisting)
    {
        if (loadExisting) SaveSession.BeginLoadGame(slot);
        else SaveSession.BeginNewGame(slot);

        SceneManager.LoadScene("Main", LoadSceneMode.Single);
    }
}
