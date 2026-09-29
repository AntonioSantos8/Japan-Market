using System;

namespace JapanMarket.Core
{
    public enum GameAudioCue
    {
        ButtonHover,
        ButtonClick,
        ButtonUnhover
    }

    public static class GameAudio
    {
        public static event Action<GameAudioCue> Requested;

        public static void Play(GameAudioCue cue) => Requested?.Invoke(cue);
    }
}
