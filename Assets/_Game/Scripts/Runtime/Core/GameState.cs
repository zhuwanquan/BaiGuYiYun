using System;
using System.Collections.Generic;

namespace BaiguVN
{
    [Serializable]
    public class GameState
    {
        public string currentNodeId;
        public int pageIndex;

        public HashSet<string> observed = new HashSet<string>();
        public List<HistoryEntry> history = new List<HistoryEntry>();

        public VisualSnapshot visuals = new VisualSnapshot();

        public List<string> completedChapters = new List<string>();

        public bool mainCompleted;

        public GameState Clone()
        {
            GameState copy = new GameState();

            copy.currentNodeId = currentNodeId;
            copy.pageIndex = pageIndex;

            copy.observed = new HashSet<string>(observed);

            copy.history = new List<HistoryEntry>();
            foreach (HistoryEntry entry in history)
            {
                copy.history.Add(new HistoryEntry
                {
                    nodeId = entry.nodeId,
                    speaker = entry.speaker,
                    text = entry.text
                });
            }

            copy.visuals = new VisualSnapshot
            {
                backgroundId =
                    visuals.backgroundId,

                cgId =
                    visuals.cgId,

                leftPortraitId =
                    visuals.leftPortraitId,

                centerPortraitId =
                    visuals.centerPortraitId,

                rightPortraitId =
                    visuals.rightPortraitId,
                
                focusSlot =
                    visuals.focusSlot,

                bgmId =
                    visuals.bgmId,

                ambienceId =
                    visuals.ambienceId
            };

            copy.completedChapters = new List<string>(completedChapters);

            copy.mainCompleted = mainCompleted;

            return copy;
        }

        public VNSnapshot ToSnapshot(string contentVersion)
        {
            VNSnapshot snapshot = new VNSnapshot();

            snapshot.contentVersion = contentVersion;
            snapshot.currentNodeId = currentNodeId;
            snapshot.pageIndex = pageIndex;

            snapshot.observed = new List<string>(observed);

            snapshot.history = new List<HistoryEntry>();
            foreach (HistoryEntry entry in history)
            {
                snapshot.history.Add(new HistoryEntry
                {
                    nodeId = entry.nodeId,
                    speaker = entry.speaker,
                    text = entry.text
                });
            }

            snapshot.visuals =
                new VisualSnapshot
                {
                    backgroundId =
                        visuals.backgroundId,

                    cgId =
                        visuals.cgId,

                    leftPortraitId =
                        visuals.leftPortraitId,

                    centerPortraitId =
                        visuals.centerPortraitId,

                    rightPortraitId =
                        visuals.rightPortraitId,
                    
                    focusSlot =
                        visuals.focusSlot,

                    bgmId =
                        visuals.bgmId,

                    ambienceId =
                        visuals.ambienceId
                };

            snapshot.completedChapters =
                new List<string>(completedChapters);

            snapshot.mainCompleted = mainCompleted;

            return snapshot;
        }
    }
}