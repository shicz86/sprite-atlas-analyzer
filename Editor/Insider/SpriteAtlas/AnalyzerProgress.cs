using System;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class AnalyzerProgress
    {
        int m_ID;

        public void StartProgressTrack()
        {
            m_ID = Progress.Start("Atlas Analyzer");
        }

        public void UpdateProgressTrack(int currentStep, int totalStep, string message)
        {
            Progress.Report(m_ID, currentStep, totalStep, message);
        }

        public void UpdateProgressTrack(float progress, string message)
        {
            Progress.Report(m_ID, progress, message);
        }

        public void EndProgressTrack()
        {
            Progress.Remove(m_ID);
        }
    }
}
