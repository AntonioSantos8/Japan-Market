namespace JapanMarket.Domain
{

    public interface IStoreCleanliness
    {
        int ActiveDirtCount { get; }

        float Normalized { get; }
    }
}
