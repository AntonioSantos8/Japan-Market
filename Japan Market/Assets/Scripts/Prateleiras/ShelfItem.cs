using UnityEngine;

public class ShelfItem : MonoBehaviour
{
   public  Segment segment;
    int groupIndex;
    int spaceIndex;

    public void Setup(Segment seg, int gIndex, int sIndex)
    {
        segment = seg;
        groupIndex = gIndex;
        spaceIndex = sIndex;
    }
    public Segment GetSegment() => segment;
    public void RemoveFromShelf()
    {
        if (segment != null)
            segment.FreeSpace(groupIndex, spaceIndex);

        Destroy(this);
    }
}