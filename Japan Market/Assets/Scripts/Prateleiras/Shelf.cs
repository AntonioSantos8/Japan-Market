using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;

public enum Items
{
    None, Ketchup, Mostard, Shelf, Box
}

public class Shelf : MonoBehaviour
{
    [SerializedDictionary("Item", " Segment")]
    [SerializeField] SerializedDictionary<Segment, Items> shelf;

    List<Items> allItemsInShelf = new List<Items>();

    public Segment lastItemSegment;

    public Items TakeRandomItem()
    {
        if (shelf.Count == 0) return Items.None;

        int index = Random.Range(0, shelf.Count);

        int i = 0;
        foreach (var pair in shelf)
        {
            if (i == index)
            {
                lastItemSegment = pair.Key;
                return pair.Value;
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