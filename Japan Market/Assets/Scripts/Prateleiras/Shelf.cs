using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;

public enum Items
{
    None, Ketchup, Mostard, Shelf, Freezer,
    Box, Fish, Mochi, Yakisoba,
    Buldak, Pringles, Biscuits,
    Chocopie, Cola, IceCream, FrozenMeat,
    FrozenPizza,
    KitKatWasabi = 17,
    Milk = 18,
    Water = 19,
    Yogurt = 20,
    EnergyDrink = 21,
    Yakult = 22
}

public class Shelf : MonoBehaviour
{
    [SerializedDictionary("Item", " Segment")]
    [SerializeField] SerializedDictionary<Segment, Items> shelf = new SerializedDictionary<Segment, Items>();

    List<Items> allItemsInShelf = new List<Items>();

    public Segment lastItemSegment;

    public bool HasItems
    {
        get
        {
            foreach (var pair in shelf)
                if (HasStock(pair.Key, pair.Value)) return true;
            return false;
        }
    }

    private static bool HasStock(Segment segment, Items type)
    {
        if (segment == null || type == Items.None) return false;
        foreach (var group in segment.Groups)
        {
            if (group == null || group.type != type) continue;
            foreach (var item in group.spaces)
                if (item != null) return true;
        }
        return false;
    }

    [ContextMenu("Tirar Item")]
    public Items TakeRandomItem()
    {
        if (shelf.Count == 0) { print("SEM ITEM"); return Items.None; }

        int index = Random.Range(0, shelf.Count);

        int i = 0;
        foreach (var pair in shelf)
        {
            if (i == index)
            {
                Segment segment = pair.Key;
                Items itemType = pair.Value;

                for (int g = 0; g < segment.Groups.Length; g++)
                {
                    if (segment.Groups[g].type != itemType) continue;

                    for (int s = segment.Groups[g].spaces.Count - 1; s >= 0; s--)
                    {
                        Transform item = segment.Groups[g].spaces[s];
                        if (item == null) continue;
                        segment.RemoveItem(g, s);
                        Destroy(item.gameObject);
                        lastItemSegment = segment;
                        print("TIRANDO ITEM: " + itemType);
                        if (segment.IsEmpty())
                        {
                            RemoveSegment(segment);
                        }

                        return itemType;
                    }
                }

                return Items.None;
            }
            i++;
        }

        return Items.None;
    }


    // Retorna o tipo de um item aleatório SEM remover da prateleira.
    public Items PeekRandomItemType()
    {
        return PeekRandomMatchingItemType(null);
    }

    public Items PeekRandomMatchingItemType(System.Predicate<Items> matches)
    {
        Items selected = Items.None;
        int available = 0;
        foreach (var pair in shelf)
        {
            if (!HasStock(pair.Key, pair.Value) || (matches != null && !matches(pair.Value))) continue;
            if (Random.Range(0, ++available) == 0) selected = pair.Value;
        }
        return selected;
    }

    // Remove e destrói um item de um tipo específico.
    public Items TakeItemOfType(Items targetType)
    {
        foreach (var pair in shelf)
        {
            if (pair.Value != targetType || !HasStock(pair.Key, targetType)) continue;
            Segment segment = pair.Key;
            for (int g = 0; g < segment.Groups.Length; g++)
            {
                if (segment.Groups[g].type != targetType) continue;
                for (int s = segment.Groups[g].spaces.Count - 1; s >= 0; s--)
                {
                    Transform item = segment.Groups[g].spaces[s];
                    if (item == null) continue;
                    segment.RemoveItem(g, s);
                    Destroy(item.gameObject);
                    lastItemSegment = segment;
                    if (segment.IsEmpty()) RemoveSegment(segment);
                    return targetType;
                }
            }
        }
        return Items.None;
    }

    public void RegisterSegment(Items item, Segment segment)
    {
        if (segment == null) return;
        if (shelf.TryGetValue(segment, out Items previous))
        {
            if (previous == item) return;
            shelf[segment] = item;
            allItemsInShelf.Remove(previous);
            allItemsInShelf.Add(item);
            return;
        }

        shelf.Add(segment, item);
        allItemsInShelf.Add(item);
    }

    public void RemoveSegment(Segment segment)
    {
        if (!shelf.ContainsKey(segment)) return;

        Items item = shelf[segment];

        shelf.Remove(segment);
        allItemsInShelf.Remove(item);
    }
}
