using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;

public enum Items
{
    None, Ketchup, Mostard, Shelf, Box, Fish, Mochi, Yakisoba, Buldak, Pringles, Biscuits
}

public class Shelf : MonoBehaviour
{
    [SerializedDictionary("Item", " Segment")]
    [SerializeField] SerializedDictionary<Segment, Items> shelf;

    List<Items> allItemsInShelf = new List<Items>();

    public Segment lastItemSegment;

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


    public void RegisterSegment(Items item, Segment segment)
    {
        if (shelf.ContainsKey(segment)) return;

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